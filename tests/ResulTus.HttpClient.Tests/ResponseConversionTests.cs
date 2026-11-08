// Copyright (c) ResulTus. All rights reserved.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ResulTus.HttpClient.Tests;

/// <summary>
/// Tests for <see cref="HttpResponseMessageResultExtensions"/>, the response-to-result conversions that
/// never throw for ordinary HTTP failures and never dispose the caller-owned response.
/// </summary>
public class ResponseConversionTests
{
    /// <summary>
    /// GIVEN a 2xx response with a JSON body
    /// WHEN converted with the <see cref="JsonTypeInfo{T}"/> overload
    /// THEN the result MUST be a success carrying the deserialized value.
    /// </summary>
    [Fact]
    public async Task TypedConversion_ShouldDeserializeJsonValue_OnSuccess()
    {
        using var response = JsonResponse(HttpStatusCode.OK, """{"Name":"widget","Count":42}""");

        var result = await response.ToResultAsync(TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Deconstruct(out _, out var payload, out _);
        payload.Should().NotBeNull();
        payload!.Name.Should().Be("widget");
        payload.Count.Should().Be(42);
    }

    /// <summary>
    /// GIVEN a 2xx response with a JSON body
    /// WHEN converted with the <see cref="JsonSerializerOptions"/> convenience overload
    /// THEN the result MUST be a success carrying the deserialized value.
    /// </summary>
    [Fact]
    public async Task TypedConversion_WithOptions_ShouldDeserializeJsonValue_OnSuccess()
    {
        using var response = JsonResponse(HttpStatusCode.OK, """{"Name":"widget","Count":42}""");

        var result = await response.ToResultAsync<TestPayload>(new JsonSerializerOptions(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Deconstruct(out _, out var payload, out _);
        payload.Should().NotBeNull();
        payload!.Name.Should().Be("widget");
        payload.Count.Should().Be(42);
    }

    /// <summary>
    /// GIVEN a 2xx response
    /// WHEN converted with the raw overload
    /// THEN the result MUST be a success carrying that exact response instance.
    /// </summary>
    [Fact]
    public async Task RawConversion_ShouldReturnTheResponse_OnSuccess()
    {
        using var response = JsonResponse(HttpStatusCode.OK, """{"Name":"widget","Count":42}""");

        var result = await response.ToResultAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Deconstruct(out _, out var returned, out _);
        returned.Should().BeSameAs(response);
    }

    /// <summary>
    /// GIVEN a response with a non-2xx status and a non-problem body
    /// WHEN converted
    /// THEN the failure MUST carry that exact <see cref="HttpStatusCode"/>, never a guess.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task NonSuccessConversion_ShouldCarryTheExactStatusCode(HttpStatusCode status)
    {
        using var response = JsonResponse(status, """{"message":"nope"}""", "application/json");

        var result = await response.ToResultAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Deconstruct(out _, out _, out var error);
        error.Should().NotBeNull();
        error!.Kind.Should().Be(HttpClientFailureKind.StatusError);
        error.Status.Should().Be(status);
    }

    /// <summary>
    /// GIVEN a non-2xx response with content type <c>application/problem+json</c> carrying standard
    /// members plus an unknown extension and an <c>errors</c> extension
    /// WHEN converted
    /// THEN the failure MUST carry the exact status and a parsed <see cref="HttpProblemDetails"/> whose
    /// extension members (including <c>errors</c>) are retained as faithful, detached
    /// <see cref="JsonElement"/> values.
    /// </summary>
    [Fact]
    public async Task NonSuccessConversion_ShouldParseProblemDetails_WhenProblemJsonContentType()
    {
        const string body = """
            {
              "type": "https://example.com/probs/validation",
              "title": "Validation failed",
              "status": 422,
              "detail": "One or more fields are invalid",
              "instance": "/orders/123",
              "traceId": "abc-123",
              "errors": { "password": ["too short"] }
            }
            """;

        using var response = JsonResponse(HttpStatusCode.UnprocessableEntity, body, "application/problem+json");

        var result = await response.ToResultAsync(TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Deconstruct(out _, out _, out var error);
        error!.Kind.Should().Be(HttpClientFailureKind.StatusError);
        error.Status.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = error.ProblemDetails;
        problem.Should().NotBeNull();
        problem!.Type.Should().Be("https://example.com/probs/validation");
        problem.Title.Should().Be("Validation failed");
        problem.Status.Should().Be(422);
        problem.Detail.Should().Be("One or more fields are invalid");
        problem.Instance.Should().Be("/orders/123");

        problem.Extensions.Should().NotBeNull();
        problem.Extensions!.Count.Should().Be(2);
        problem.Extensions["traceId"].GetString().Should().Be("abc-123");
        problem.Extensions["errors"].GetProperty("password")[0].GetString().Should().Be("too short");
    }

    /// <summary>
    /// GIVEN a non-2xx response whose content type is not <c>application/problem+json</c>
    /// WHEN converted
    /// THEN the failure MUST be a status-only failure with <see cref="HttpClientError.ProblemDetails"/>
    /// null and no originating exception.
    /// </summary>
    [Fact]
    public async Task NonSuccessConversion_ShouldReturnStatusOnlyFailure_WhenNotProblemJson()
    {
        using var response = JsonResponse(HttpStatusCode.NotFound, """{"message":"not found"}""", "application/json");

        var result = await response.ToResultAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Deconstruct(out _, out _, out var error);
        error!.Kind.Should().Be(HttpClientFailureKind.StatusError);
        error.Status.Should().Be(HttpStatusCode.NotFound);
        error.ProblemDetails.Should().BeNull();
        error.Exception.Should().BeNull();
    }

    /// <summary>
    /// GIVEN a non-2xx response with content type <c>application/problem+json</c> and a malformed body
    /// WHEN converted
    /// THEN the failure MUST keep the status, leave <see cref="HttpClientError.ProblemDetails"/> unset,
    /// and retain the parsing exception.
    /// </summary>
    [Fact]
    public async Task NonSuccessConversion_ShouldKeepStatusAndLeaveProblemDetailsUnset_WhenProblemBodyMalformed()
    {
        using var response = JsonResponse(HttpStatusCode.BadRequest, "{ not valid json", "application/problem+json");

        var result = await response.ToResultAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Deconstruct(out _, out _, out var error);
        error!.Kind.Should().Be(HttpClientFailureKind.StatusError);
        error.Status.Should().Be(HttpStatusCode.BadRequest);
        error.ProblemDetails.Should().BeNull();
        error.Exception.Should().BeAssignableTo<JsonException>();
    }

    /// <summary>
    /// GIVEN a 2xx response whose JSON body is malformed
    /// WHEN converted with the typed overload
    /// THEN the failure MUST be an <see cref="HttpClientFailureKind.InvalidResponse"/> error carrying the
    /// response status and the parsing exception.
    /// </summary>
    [Fact]
    public async Task TypedConversion_ShouldReturnInvalidResponse_WhenBodyMalformed()
    {
        using var response = JsonResponse(HttpStatusCode.OK, "{ not valid json", "application/json");

        var result = await response.ToResultAsync(TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Deconstruct(out _, out _, out var error);
        error!.Kind.Should().Be(HttpClientFailureKind.InvalidResponse);
        error.Status.Should().Be(HttpStatusCode.OK);
        error.Exception.Should().BeAssignableTo<JsonException>();
    }

    /// <summary>
    /// GIVEN a caller-owned response routed through every conversion path — typed success, typed failure,
    /// raw success and raw failure
    /// WHEN converted
    /// THEN the response MUST never be disposed by the conversion.
    /// </summary>
    [Fact]
    public async Task Conversion_ShouldNotDisposeTheCallerOwnedResponse()
    {
        using (var response = new TrackingResponse(HttpStatusCode.OK, JsonContent("""{"Name":"widget","Count":42}""")))
        {
            await response.ToResultAsync(TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);
            response.IsDisposed.Should().BeFalse();
        }

        using (var response = new TrackingResponse(HttpStatusCode.BadRequest, JsonContent("""{"title":"bad"}""", problemJson: true)))
        {
            await response.ToResultAsync(TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);
            response.IsDisposed.Should().BeFalse();
        }

        using (var response = new TrackingResponse(HttpStatusCode.OK, JsonContent("""{"Name":"widget","Count":42}""")))
        {
            await response.ToResultAsync(TestContext.Current.CancellationToken);
            response.IsDisposed.Should().BeFalse();
        }

        using (var response = new TrackingResponse(HttpStatusCode.InternalServerError, JsonContent("""{"title":"boom"}""", problemJson: true)))
        {
            await response.ToResultAsync(TestContext.Current.CancellationToken);
            response.IsDisposed.Should().BeFalse();
        }
    }

    /// <summary>
    /// GIVEN a null response, null <see cref="JsonTypeInfo{T}"/> or null <see cref="JsonSerializerOptions"/>
    /// WHEN converted
    /// THEN an <see cref="ArgumentNullException"/> MUST be thrown.
    /// </summary>
    [Fact]
    public async Task Conversion_ShouldThrowArgumentNullException_ForNullArguments()
    {
        HttpResponseMessage nullResponse = null!;

        Func<Task> nullResponseTyped = async () => await nullResponse.ToResultAsync(TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);
        await nullResponseTyped.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> nullResponseRaw = async () => await nullResponse.ToResultAsync(TestContext.Current.CancellationToken);
        await nullResponseRaw.Should().ThrowAsync<ArgumentNullException>();

        using var response = JsonResponse(HttpStatusCode.OK, """{"Name":"widget","Count":42}""");

        Func<Task> nullTypeInfo = async () => await response.ToResultAsync<TestPayload>((JsonTypeInfo<TestPayload>)null!, TestContext.Current.CancellationToken);
        await nullTypeInfo.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> nullOptions = async () => await response.ToResultAsync<TestPayload>((JsonSerializerOptions)null!, TestContext.Current.CancellationToken);
        await nullOptions.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN a body whose read raises <see cref="OperationCanceledException"/> — the same exception a
    /// cancelled token produces mid-read — on both a success-read and a problem-details read
    /// WHEN converted
    /// THEN cancellation MUST surface as <see cref="OperationCanceledException"/> and never be converted
    /// into an <see cref="HttpClientError"/>.
    /// </summary>
    [Fact]
    public async Task Conversion_ShouldPropagateCancellation_AsOperationCanceledException()
    {
        using var success = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new CanceledReadStream()),
        };

        Func<Task> typed = async () => await success.ToResultAsync(TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);
        await typed.Should().ThrowAsync<OperationCanceledException>();

        using var failure = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StreamContent(new CanceledReadStream())
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/problem+json") },
            },
        };

        Func<Task> raw = async () => await failure.ToResultAsync(TestContext.Current.CancellationToken);
        await raw.Should().ThrowAsync<OperationCanceledException>();
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode status,
        string body,
        string contentType = "application/json")
        => new(status) { Content = JsonContent(body, contentType == "application/problem+json") };

    private static StringContent JsonContent(string body, bool problemJson = false)
        => new(body, Encoding.UTF8, problemJson ? "application/problem+json" : "application/json");

    private sealed class TrackingResponse : HttpResponseMessage
    {
        public TrackingResponse(HttpStatusCode statusCode, HttpContent content)
            : base(statusCode)
        {
            Content = content;
        }

        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class CanceledReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new OperationCanceledException("The operation was cancelled.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException("The operation was cancelled.");

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

/// <summary>
/// The test payload deserialized from successful JSON bodies.
/// </summary>
internal sealed record TestPayload(string Name, int Count);

/// <summary>
/// Source-generated metadata for <see cref="TestPayload"/>, exercising the Native AOT
/// <see cref="JsonTypeInfo{T}"/> route in tests.
/// </summary>
[JsonSerializable(typeof(TestPayload))]
internal sealed partial class TestJsonContext : JsonSerializerContext
{
}
