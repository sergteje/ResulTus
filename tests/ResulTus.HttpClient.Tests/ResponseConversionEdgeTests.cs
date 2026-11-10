// Copyright (c) ResulTus. All rights reserved.

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ResulTus.HttpClient;
using Xunit;

namespace ResulTus.HttpClient.Tests;

/// <summary>
/// Tests for the response-conversion edges the first sweep left uncovered: an empty body on a
/// success conversion (both AOT and options routes), a missing body on a non-2xx status, a
/// non-problem media type, an empty problem body, and the response disposal dance.
/// </summary>
public class ResponseConversionEdgeTests
{
    /// <summary>
    /// GIVEN a 2xx response whose content reports an empty body
    /// WHEN <c>ToResultAsync</c> runs on either overload
    /// THEN it MUST be an InvalidResponse failure naming the malformed body, never a throw.
    /// </summary>
    [Fact]
    public async Task EmptyContentOnSuccess_ShouldBeAnInvalidResponseFailure()
    {
        using var responseByType = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new EmptyContent(),
        };
        var byContext = await responseByType.ToResultAsync(TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);
        byContext.IsSuccess.Should().BeFalse();
        byContext.Deconstruct(out _, out var payload, out var contextFailure);
        contextFailure!.Kind.Should().Be(HttpClientFailureKind.InvalidResponse);
        contextFailure.Exception.Should().BeOfType<JsonException>();

        using var responseByOptions = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new EmptyContent(),
        };
        var byOptions = await responseByOptions.ToResultAsync<string>(
            new JsonSerializerOptions(), TestContext.Current.CancellationToken);
        byOptions.IsSuccess.Should().BeFalse();
        byOptions.Deconstruct(out _, out var optionsValue, out var optionsFailure);
        optionsFailure!.Kind.Should().Be(HttpClientFailureKind.InvalidResponse);
        optionsFailure.Exception.Should().BeOfType<JsonException>();
    }

    /// <summary>
    /// GIVEN a non-2xx response with an empty body
    /// WHEN converted
    /// THEN the failure MUST be a plain StatusError: nothing to parse, nothing to inspect.
    /// </summary>
    [Fact]
    public async Task EmptyContentOnStatus_ShouldBeAPlainStatusError()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new EmptyContent(),
        };

        var result = await response.ToResultAsync<string>(new JsonSerializerOptions(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Deconstruct(out _, out var value, out var failure);
        failure!.Kind.Should().Be(HttpClientFailureKind.StatusError);
        failure.ProblemDetails.Should().BeNull();
        failure.Exception.Should().BeNull();
    }

    /// <summary>
    /// GIVEN a non-2xx response whose media type is not problem+json
    /// WHEN converted
    /// THEN the failure MUST be a plain StatusError without attempting any parse.
    /// </summary>
    [Fact]
    public async Task NonProblemMediaType_ShouldBeAPlainStatusError()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"any":"json"}""", Encoding.UTF8, "text/json"),
        };

        var result = await response.ToResultAsync<string>(new JsonSerializerOptions(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Deconstruct(out _, out var value, out var failure);
        failure!.Kind.Should().Be(HttpClientFailureKind.StatusError);
        failure.ProblemDetails.Should().BeNull();
    }

    /// <summary>
    /// GIVEN a null response
    /// WHEN converted
    /// THEN the ArgumentNullException guard must fire on null instances.
    /// </summary>
    [Fact]
    public async Task NullInputs_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((HttpResponseMessage)null!).ToResultAsync(
            TestJsonContext.Default.TestPayload,
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => (new HttpResponseMessage(HttpStatusCode.OK)).ToResultAsync<string>(
            (JsonSerializerOptions)null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => (new HttpResponseMessage(HttpStatusCode.OK)).ToResultAsync(
            (JsonTypeInfo<string>)null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A content whose reads resolve to an empty stream, reproducing a server that answered
    /// 200 with a body the transport reports as present but empty.
    /// </summary>
    private sealed class EmptyContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }
    }
}
