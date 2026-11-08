// Copyright (c) ResulTus. All rights reserved.

using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace ResulTus.HttpClient.Tests;

/// <summary>
/// Tests for <see cref="HttpClientFailureKind"/>, the closed classification of client-side HTTP failures.
/// </summary>
public class HttpClientFailureKindTests
{
    /// <summary>
    /// GIVEN the <see cref="HttpClientFailureKind"/> enum
    /// THEN it MUST be a closed set of exactly the four kinds S1 names:
    /// transport, timeout, status error and invalid response — no more, no less.
    /// </summary>
    [Fact]
    public void Kind_ShouldBeAClosedSetOfFourValues()
    {
        Enum.GetValues<HttpClientFailureKind>()
            .Should().BeEquivalentTo(new[]
            {
                HttpClientFailureKind.Transport,
                HttpClientFailureKind.Timeout,
                HttpClientFailureKind.StatusError,
                HttpClientFailureKind.InvalidResponse,
            });
    }
}

/// <summary>
/// Tests for <see cref="HttpClientError"/> value construction, field retention and value semantics.
/// </summary>
public class HttpClientErrorTests
{
    /// <summary>
    /// GIVEN <c>new HttpClientError(HttpClientFailureKind.Transport)</c>
    /// THEN <see cref="HttpClientError.Kind"/> MUST be set and the three optional fields MUST be null.
    /// </summary>
    [Fact]
    public void Constructor_WithOnlyKind_ShouldLeaveOptionalFieldsUnset()
    {
        var error = new HttpClientError(HttpClientFailureKind.Transport);

        error.Kind.Should().Be(HttpClientFailureKind.Transport);
        error.Status.Should().BeNull();
        error.ProblemDetails.Should().BeNull();
        error.Exception.Should().BeNull();
    }

    /// <summary>
    /// GIVEN a constructor call carrying every field
    /// THEN <see cref="HttpClientError.Kind"/>, <see cref="HttpClientError.Status"/>,
    /// <see cref="HttpClientError.ProblemDetails"/> and <see cref="HttpClientError.Exception"/>
    /// MUST each retain the supplied value.
    /// </summary>
    [Fact]
    public void Constructor_WithEveryField_ShouldRetainEach()
    {
        var problem = new HttpProblemDetails(title: "Not Found", status: 404);
        var exception = new InvalidOperationException("boom");

        var error = new HttpClientError(
            HttpClientFailureKind.StatusError,
            HttpStatusCode.NotFound,
            problem,
            exception);

        error.Kind.Should().Be(HttpClientFailureKind.StatusError);
        error.Status.Should().Be(HttpStatusCode.NotFound);
        error.ProblemDetails.Should().Be(problem);
        error.Exception.Should().BeSameAs(exception);
    }

    /// <summary>
    /// GIVEN a constructor call passing explicit null for every optional field
    /// THEN the error MUST accept them without throwing and expose null for each.
    /// </summary>
    [Fact]
    public void Constructor_WithNullOptionalArguments_ShouldStoreNull()
    {
        var error = new HttpClientError(
            HttpClientFailureKind.InvalidResponse,
            null,
            null,
            null);

        error.Kind.Should().Be(HttpClientFailureKind.InvalidResponse);
        error.Status.Should().BeNull();
        error.ProblemDetails.Should().BeNull();
        error.Exception.Should().BeNull();
    }

    /// <summary>
    /// GIVEN <c>HttpClientError.Transport(exception)</c>
    /// THEN the error MUST be a <see cref="HttpClientFailureKind.Transport"/> failure carrying that exception
    /// and no status or problem details.
    /// </summary>
    [Fact]
    public void TransportFactory_ShouldSetKindAndException()
    {
        var exception = new HttpRequestException("no connection");

        var error = HttpClientError.Transport(exception);

        error.Kind.Should().Be(HttpClientFailureKind.Transport);
        error.Exception.Should().BeSameAs(exception);
        error.Status.Should().BeNull();
        error.ProblemDetails.Should().BeNull();
    }

    /// <summary>
    /// GIVEN <c>HttpClientError.StatusError(status, problemDetails)</c>
    /// THEN the error MUST be a <see cref="HttpClientFailureKind.StatusError"/> failure carrying both the
    /// response status and the parsed problem document.
    /// </summary>
    [Fact]
    public void StatusErrorFactory_ShouldRequireStatusAndCarryProblemDetails()
    {
        var problem = new HttpProblemDetails(type: "about:blank", title: "Bad Request", status: 400);

        var error = HttpClientError.StatusError(HttpStatusCode.BadRequest, problem);

        error.Kind.Should().Be(HttpClientFailureKind.StatusError);
        error.Status.Should().Be(HttpStatusCode.BadRequest);
        error.ProblemDetails.Should().Be(problem);
    }

    /// <summary>
    /// GIVEN <c>HttpClientError.Timeout()</c>
    /// THEN the error MUST be a <see cref="HttpClientFailureKind.Timeout"/> failure with no status,
    /// problem details or exception.
    /// </summary>
    [Fact]
    public void TimeoutFactory_ShouldSetKind()
    {
        var error = HttpClientError.Timeout();

        error.Kind.Should().Be(HttpClientFailureKind.Timeout);
        error.Status.Should().BeNull();
        error.ProblemDetails.Should().BeNull();
        error.Exception.Should().BeNull();
    }

    /// <summary>
    /// GIVEN <c>HttpClientError.InvalidResponse(status, exception)</c>
    /// THEN the error MUST be an <see cref="HttpClientFailureKind.InvalidResponse"/> failure carrying the
    /// response status and parsing exception.
    /// </summary>
    [Fact]
    public void InvalidResponseFactory_ShouldSetKindAndCarryStatusAndException()
    {
        var exception = new JsonException("malformed body");

        var error = HttpClientError.InvalidResponse(HttpStatusCode.OK, exception);

        error.Kind.Should().Be(HttpClientFailureKind.InvalidResponse);
        error.Status.Should().Be(HttpStatusCode.OK);
        error.Exception.Should().BeSameAs(exception);
    }

    /// <summary>
    /// GIVEN two errors built with the same kind and a distinct-but-equal field set
    /// WHEN compared with <c>Equals</c>, <c>==</c> and <c>GetHashCode</c>
    /// THEN they MUST be equal.
    /// </summary>
    [Fact]
    public void Equality_WithSameKindAndUnsetFields_ShouldBeEqual()
    {
        var left = new HttpClientError(HttpClientFailureKind.Transport);
        var right = new HttpClientError(HttpClientFailureKind.Transport);

        left.Should().Be(right);
        (left == right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    /// <summary>
    /// GIVEN two errors differing only in kind
    /// WHEN compared
    /// THEN they MUST NOT be equal.
    /// </summary>
    [Fact]
    public void Equality_WithDifferentKind_ShouldNotBeEqual()
    {
        var left = new HttpClientError(HttpClientFailureKind.Transport);
        var right = new HttpClientError(HttpClientFailureKind.Timeout);

        left.Should().NotBe(right);
        (left != right).Should().BeTrue();
    }
}

/// <summary>
/// Tests for <see cref="HttpProblemDetails"/> data shape, extension retention and value semantics.
/// </summary>
public class HttpProblemDetailsTests
{
    /// <summary>
    /// GIVEN a constructor call carrying all five RFC 9457 standard members
    /// THEN each of <see cref="HttpProblemDetails.Type"/>, <see cref="HttpProblemDetails.Title"/>,
    /// <see cref="HttpProblemDetails.Status"/>, <see cref="HttpProblemDetails.Detail"/> and
    /// <see cref="HttpProblemDetails.Instance"/> MUST retain its value, and extensions MUST be null.
    /// </summary>
    [Fact]
    public void Constructor_ShouldRetainStandardFields()
    {
        var problem = new HttpProblemDetails(
            type: "https://example.com/probs/out-of-credit",
            title: "You do not have enough credit.",
            status: 403,
            detail: "Your current balance is 30, but that costs 50.",
            instance: "/account/12345/msgs/abc");

        problem.Type.Should().Be("https://example.com/probs/out-of-credit");
        problem.Title.Should().Be("You do not have enough credit.");
        problem.Status.Should().Be(403);
        problem.Detail.Should().Be("Your current balance is 30, but that costs 50.");
        problem.Instance.Should().Be("/account/12345/msgs/abc");
        problem.Extensions.Should().BeNull();
    }

    /// <summary>
    /// GIVEN the parameterless constructor
    /// THEN every standard member MUST be null and extensions MUST be null.
    /// </summary>
    [Fact]
    public void Constructor_WithNoArguments_ShouldStoreNullStandardFields()
    {
        var problem = new HttpProblemDetails();

        problem.Type.Should().BeNull();
        problem.Title.Should().BeNull();
        problem.Status.Should().BeNull();
        problem.Detail.Should().BeNull();
        problem.Instance.Should().BeNull();
        problem.Extensions.Should().BeNull();
    }

    /// <summary>
    /// GIVEN an extensions dictionary carrying the <c>errors</c> member and other unknown members
    /// THEN <see cref="HttpProblemDetails.Extensions"/> MUST retain every member, including <c>errors</c>,
    /// as a faithful <see cref="JsonElement"/>.
    /// </summary>
    [Fact]
    public void Constructor_ShouldRetainExtensionMembersIncludingErrors()
    {
        using var document = JsonDocument.Parse(
            """{ "errors": { "password": ["too short"] }, "balance": 30, "account": 12345 }""");
        var extensions = new Dictionary<string, JsonElement>
        {
            ["errors"] = document.RootElement.GetProperty("errors"),
            ["balance"] = document.RootElement.GetProperty("balance"),
            ["account"] = document.RootElement.GetProperty("account"),
        };

        var problem = new HttpProblemDetails(extensions: extensions);

        problem.Extensions.Should().NotBeNull();
        problem.Extensions!.Count.Should().Be(3);
        problem.Extensions["errors"].GetProperty("password")[0].GetString().Should().Be("too short");
        problem.Extensions["balance"].GetInt32().Should().Be(30);
        problem.Extensions["account"].GetInt64().Should().Be(12345);
    }

    /// <summary>
    /// GIVEN an extensions dictionary supplied to the constructor and mutated afterwards
    /// THEN the document MUST be unaffected, proving the dictionary is defensively copied.
    /// </summary>
    [Fact]
    public void Constructor_ShouldDefensivelyCopyExtensions()
    {
        using var document = JsonDocument.Parse("""{ "errors": { "password": ["too short"] } }""");
        var source = new Dictionary<string, JsonElement>
        {
            ["errors"] = document.RootElement.GetProperty("errors"),
        };

        var problem = new HttpProblemDetails(extensions: source);

        source.Clear();

        problem.Extensions.Should().NotBeNull();
        problem.Extensions!.Should().ContainKey("errors");
    }

    /// <summary>
    /// GIVEN an empty extensions dictionary
    /// THEN <see cref="HttpProblemDetails.Extensions"/> MUST be normalized to null, since there are
    /// no extension members.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyExtensions_ShouldNormalizeToNull()
    {
        var problem = new HttpProblemDetails(extensions: new Dictionary<string, JsonElement>());

        problem.Extensions.Should().BeNull();
    }

    /// <summary>
    /// GIVEN two documents with identical standard members and no extensions
    /// WHEN compared with <c>Equals</c> and <c>==</c>
    /// THEN they MUST be equal.
    /// </summary>
    [Fact]
    public void Equality_WithSameStandardFieldsAndNoExtensions_ShouldBeEqual()
    {
        var left = new HttpProblemDetails(type: "about:blank", title: "Bad Request", status: 400);
        var right = new HttpProblemDetails(type: "about:blank", title: "Bad Request", status: 400);

        left.Should().Be(right);
        (left == right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }
}
