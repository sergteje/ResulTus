// Copyright (c) ResulTus. All rights reserved.

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ResulTus.HttpClient;
using Xunit;

namespace ResulTus.HttpClient.Tests;

/// <summary>
/// Tests for the RFC 9457 model edges: equality across extension dictionaries, the null
/// comparisons, and the problem parser's strictness rules. Every non-covered case in the model
/// file reads as an input a real server could legally emit.
/// </summary>
public class ProblemDetailsEdgeTests
{
    private static readonly Func<string, JsonElement> JsonOf = text =>
        JsonDocument.Parse(text).RootElement.Clone();

    /// <summary>
    /// GIVEN two documents carrying the same extension members
    /// WHEN they are compared and hashed
    /// THEN they must be equal and hash alike; a different extension value must break both.
    /// </summary>
    [Fact]
    public void Documents_WithEqualExtensions_ShouldBeEqualAndHashAlike()
    {
        var left = new HttpProblemDetails(extensions: new Dictionary<string, JsonElement>
        {
            ["balance"] = JsonOf("42"),
            ["retries"] = JsonOf("3"),
        });
        var right = new HttpProblemDetails(extensions: new Dictionary<string, JsonElement>
        {
            ["balance"] = JsonOf("42"),
            ["retries"] = JsonOf("3"),
        });

        left.Should().Be(right);
        (left == right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
        (left != right).Should().BeFalse();
    }

    /// <summary>
    /// GIVEN two documents whose extension members carry different values
    /// WHEN they are compared
    /// THEN they must be unequal, and the != operator must reflect it.
    /// </summary>
    [Fact]
    public void Documents_WithDifferentExtensionValues_ShouldBeUnequal()
    {
        var left = new HttpProblemDetails(extensions: new Dictionary<string, JsonElement>
        {
            ["attempts"] = JsonOf("1"),
        });
        var right = new HttpProblemDetails(extensions: new Dictionary<string, JsonElement>
        {
            ["attempts"] = JsonOf("2"),
        });

        left.Should().NotBe(right);
        (left != right).Should().BeTrue();
        left!.GetHashCode().Should().NotBe(right!.GetHashCode());
    }

    /// <summary>
    /// GIVEN an instance document compared against null, itself, and a same-content document
    /// WHEN Equals or the == operator runs
    /// THEN they must behave per the contract: null unequal, reference equal, content equal.
    /// </summary>
    [Fact]
    public void NullAndReferenceComparisons_ShouldFollowTheContract()
    {
        var document = new HttpProblemDetails(title: "unavailable");

        document.Equals(null as HttpProblemDetails).Should().BeFalse();
        HttpProblemDetails? nullDocument = null;
        (document == nullDocument).Should().BeFalse();
        (nullDocument == document).Should().BeFalse();
        document.Equals(document).Should().BeTrue();
        document.Should().Be(new HttpProblemDetails(title: "unavailable"));
    }

    /// <summary>
    /// GIVEN a non-2xx response whose problem body is NOT a JSON object
    /// WHEN <c>ToResultAsync&lt;T&gt;</c> runs
    /// THEN the failure MUST carry the raw status error with the parser's JsonException nested
    /// and no ProblemDetails.
    /// </summary>
    [Fact]
    public async Task NonObjectProblemBody_ShouldProduceAStatusErrorWithTheParseException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/problem+json"),
        };

        var result = await response.ToResultAsync<string>(new JsonSerializerOptions(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Deconstruct(out _, out var value, out var failure);
        failure!.Kind.Should().Be(HttpClientFailureKind.StatusError);
        failure.ProblemDetails.Should().BeNull();
        failure.Exception.Should().BeOfType<JsonException>();
    }

    /// <summary>
    /// GIVEN problem bodies that violate the member-shape rules, plus the legal null members
    /// WHEN they are parsed
    /// THEN the numeric <c>type</c> and the string <c>status</c> must be rejected with a
    /// JsonException, and the null members must parse to unset.
    /// </summary>
    [Fact]
    public async Task MemberShapeViolations_ShouldBeRejectedAndLegalNullsAccepted()
    {
        async Task<(Exception? Thrown, HttpProblemDetails? Problem)> TryParse(string body)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/problem+json"),
            };

            var result = await response.ToResultAsync<string>(new JsonSerializerOptions(), TestContext.Current.CancellationToken);
            result.IsSuccess.Should().BeFalse();
            result.Deconstruct(out _, out var value, out var failure);
            return (failure!.Exception, failure!.ProblemDetails);
        }

        var numericType = await TryParse("""{"title":"t","type":123}""");
        numericType.Problem.Should().BeNull();
        numericType.Thrown.Should().BeOfType<JsonException>();

        var stringStatus = await TryParse("""{"status":"four hundred nine"}""");
        stringStatus.Problem.Should().BeNull();
        stringStatus.Thrown.Should().BeOfType<JsonException>();

        var nullStatus = await TryParse("""{"status":null,"title":"ok"}""");
        nullStatus.Problem!.Title.Should().Be("ok");
        nullStatus.Problem.Status.Should().BeNull();

        var nullType = await TryParse("""{"type":null,"title":"ok"}""");
        nullType.Problem!.Type.Should().BeNull();
    }
}
