// Copyright (c) ResulTus. All rights reserved.

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using HttpCli = System.Net.Http.HttpClient;

namespace ResulTus.HttpClient.Tests;

/// <summary>
/// Tests for <see cref="HttpClientSendResultExtensions"/>, the send-and-convert extensions that capture
/// transport and timeout failures, never retry, and never dispose the caller's client or request.
/// </summary>
public class SendResultTests
{
    /// <summary>
    /// GIVEN a transport-level success from a fake handler
    /// WHEN sent with the raw overload
    /// THEN the result MUST be a success carrying that exact response for caller inspection/disposal.
    /// </summary>
    [Fact]
    public async Task RawSend_ShouldReturnTheResponse_OnSuccess()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{"Name":"widget","Count":42}""") };
        var handler = new StubHandler((_, _) => Task.FromResult<HttpResponseMessage>(response));
        using var client = new HttpCli(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        var result = await client.SendResultAsync(request, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Deconstruct(out _, out var returned, out _);
        returned.Should().BeSameAs(response);
    }

    /// <summary>
    /// GIVEN a 2xx JSON response
    /// WHEN sent with the <see cref="JsonTypeInfo{T}"/> typed overload
    /// THEN the result MUST be a success carrying the deserialized value.
    /// </summary>
    [Fact]
    public async Task TypedSend_WithJsonTypeInfo_ShouldDeserialize_OnSuccess()
    {
        var handler = new StubHandler((_, _) => Task.FromResult<HttpResponseMessage>(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{"Name":"widget","Count":42}""") }));
        using var client = new HttpCli(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        var result = await client.SendResultAsync(request, TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Deconstruct(out _, out var payload, out _);
        payload.Should().NotBeNull();
        payload!.Name.Should().Be("widget");
        payload.Count.Should().Be(42);
    }

    /// <summary>
    /// GIVEN a 2xx JSON response
    /// WHEN sent with the <see cref="JsonSerializerOptions"/> typed overload
    /// THEN the result MUST be a success carrying the deserialized value.
    /// </summary>
    [Fact]
    public async Task TypedSend_WithOptions_ShouldDeserialize_OnSuccess()
    {
        var handler = new StubHandler((_, _) => Task.FromResult<HttpResponseMessage>(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{"Name":"widget","Count":42}""") }));
        using var client = new HttpCli(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        var result = await client.SendResultAsync<TestPayload>(request, new JsonSerializerOptions(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Deconstruct(out _, out var payload, out _);
        payload.Should().NotBeNull();
        payload!.Name.Should().Be("widget");
        payload.Count.Should().Be(42);
    }

    /// <summary>
    /// GIVEN a handler that throws an <see cref="HttpRequestException"/>
    /// WHEN sent (both raw and typed forms)
    /// THEN the failure MUST be an <see cref="HttpClientFailureKind.Transport"/> error carrying that exception.
    /// </summary>
    [Fact]
    public async Task Send_ShouldMapHttpRequestException_ToTransport()
    {
        var exception = new HttpRequestException("connection refused");
        var handler = new StubHandler((_, _) => throw exception);
        using var client = new HttpCli(handler);

        using (var rawRequest = new HttpRequestMessage(HttpMethod.Get, "http://example.com/"))
        {
            var raw = await client.SendResultAsync(rawRequest, TestContext.Current.CancellationToken);
            raw.IsSuccess.Should().BeFalse();
            raw.Deconstruct(out _, out _, out var rawError);
            rawError!.Kind.Should().Be(HttpClientFailureKind.Transport);
            rawError.Status.Should().BeNull();
            rawError.Exception.Should().BeSameAs(exception);
        }

        using (var typedRequest = new HttpRequestMessage(HttpMethod.Get, "http://example.com/"))
        {
            var typed = await client.SendResultAsync(typedRequest, TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);
            typed.IsSuccess.Should().BeFalse();
            typed.Deconstruct(out _, out _, out var typedError);
            typedError!.Kind.Should().Be(HttpClientFailureKind.Transport);
            typedError.Status.Should().BeNull();
            typedError.Exception.Should().BeSameAs(exception);
        }
    }

    /// <summary>
    /// GIVEN a handler that never completes and a client whose <see cref="HttpCli.Timeout"/> elapses
    /// WHEN sent
    /// THEN the failure MUST be an <see cref="HttpClientFailureKind.Timeout"/> error whose originating
    /// exception is the BCL's timeout <see cref="OperationCanceledException"/> wrapping a
    /// <see cref="TimeoutException"/> — never a caller cancellation.
    /// </summary>
    [Fact]
    public async Task Send_ShouldMapClientTimeout_ToTimeout()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1), ct).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpCli(handler) { Timeout = TimeSpan.FromMilliseconds(50) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        var result = await client.SendResultAsync(request, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Deconstruct(out _, out _, out var error);
        error!.Kind.Should().Be(HttpClientFailureKind.Timeout);
        error.Status.Should().BeNull();
        error.Exception.Should().BeAssignableTo<OperationCanceledException>();
        error.Exception!.InnerException.Should().BeAssignableTo<TimeoutException>();
    }

    /// <summary>
    /// GIVEN a pre-cancelled caller token
    /// WHEN sent (both raw and typed forms)
    /// THEN cancellation MUST rethrow as <see cref="OperationCanceledException"/>, never be captured as an
    /// <see cref="HttpClientError"/>.
    /// </summary>
    [Fact]
    public async Task Send_ShouldRethrowOperationCanceledException_WhenCallerTokenCancelled()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1), ct).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpCli(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using (var rawRequest = new HttpRequestMessage(HttpMethod.Get, "http://example.com/"))
        {
            Func<Task> raw = async () => await client.SendResultAsync(rawRequest, cts.Token);
            await raw.Should().ThrowAsync<OperationCanceledException>();
        }

        using (var typedRequest = new HttpRequestMessage(HttpMethod.Get, "http://example.com/"))
        {
            Func<Task> typed = async () => await client.SendResultAsync(typedRequest, TestJsonContext.Default.TestPayload, cts.Token);
            await typed.Should().ThrowAsync<OperationCanceledException>();
        }
    }

    /// <summary>
    /// GIVEN a successful handler
    /// WHEN sent once
    /// THEN the handler MUST observe exactly one send — the module never retries.
    /// </summary>
    [Fact]
    public async Task Send_ShouldNotRetry_ShouldSendExactlyOnce()
    {
        var handler = new StubHandler((_, _) => Task.FromResult<HttpResponseMessage>(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{"Name":"widget","Count":42}""") }));
        using var client = new HttpCli(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        var result = await client.SendResultAsync(request, TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        handler.SendCount.Should().Be(1);
    }

    /// <summary>
    /// GIVEN a handler that throws a non-HTTP exception
    /// WHEN sent
    /// THEN the exception MUST surface unchanged (the same instance), never converted into an
    /// <see cref="HttpClientError"/> or wrapped in an <see cref="HttpRequestException"/>.
    /// </summary>
    [Fact]
    public async Task Send_ShouldSurfaceNonHttpExceptions_Unchanged()
    {
        var boom = new InvalidOperationException("boom");
        var handler = new StubHandler((_, _) => throw boom);
        using var client = new HttpCli(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        Exception? caught = null;
        try
        {
            await client.SendResultAsync(request, TestContext.Current.CancellationToken);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        caught.Should().BeSameAs(boom);
    }

    /// <summary>
    /// GIVEN a typed send over tracking response/request/handler instances
    /// WHEN sent
    /// THEN the response it received MUST be disposed exactly once, while the caller's request and client
    /// (handler) MUST be left untouched.
    /// </summary>
    [Fact]
    public async Task TypedSend_ShouldDisposeResponseOnce_AndLeaveRequestAndClientUntouched()
    {
        var response = new TrackingResponse(HttpStatusCode.OK, JsonContent("""{"Name":"widget","Count":42}"""));
        var request = new TrackingRequest(HttpMethod.Get, "http://example.com/");
        var handler = new StubHandler((_, _) => Task.FromResult<HttpResponseMessage>(response));
        using var client = new HttpCli(handler);

        var result = await client.SendResultAsync(request, TestJsonContext.Default.TestPayload, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        response.DisposeCount.Should().Be(1);
        request.DisposeCount.Should().Be(0);
        handler.DisposeCount.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a raw send over tracking response/request/handler instances
    /// WHEN sent
    /// THEN nothing MUST be disposed: the response, request and client are all left for the caller.
    /// </summary>
    [Fact]
    public async Task RawSend_ShouldDisposeNothing()
    {
        var response = new TrackingResponse(HttpStatusCode.OK, JsonContent("""{"Name":"widget","Count":42}"""));
        var request = new TrackingRequest(HttpMethod.Get, "http://example.com/");
        var handler = new StubHandler((_, _) => Task.FromResult<HttpResponseMessage>(response));
        using var client = new HttpCli(handler);

        var result = await client.SendResultAsync(request, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        response.DisposeCount.Should().Be(0);
        request.DisposeCount.Should().Be(0);
        handler.DisposeCount.Should().Be(0);

        // The caller owns the returned response and disposes it.
        response.Dispose();
        response.DisposeCount.Should().Be(1);
    }

    /// <summary>
    /// GIVEN a null client, null request, null <see cref="JsonTypeInfo{T}"/> or null
    /// <see cref="JsonSerializerOptions"/>
    /// WHEN sent
    /// THEN an <see cref="ArgumentNullException"/> MUST be thrown.
    /// </summary>
    [Fact]
    public async Task Send_ShouldThrowArgumentNullException_ForNullArguments()
    {
        HttpCli nullClient = null!;
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");
        var jsonTypeInfo = TestJsonContext.Default.TestPayload;

        Func<Task> nullClientRaw = async () => await nullClient.SendResultAsync(request, TestContext.Current.CancellationToken);
        await nullClientRaw.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> nullClientTyped = async () => await nullClient.SendResultAsync(request, jsonTypeInfo, TestContext.Current.CancellationToken);
        await nullClientTyped.Should().ThrowAsync<ArgumentNullException>();

        using var client = new HttpCli(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        HttpRequestMessage nullRequest = null!;

        Func<Task> nullRequestRaw = async () => await client.SendResultAsync(nullRequest, TestContext.Current.CancellationToken);
        await nullRequestRaw.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> nullRequestTyped = async () => await client.SendResultAsync(nullRequest, jsonTypeInfo, TestContext.Current.CancellationToken);
        await nullRequestTyped.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> nullTypeInfo = async () => await client.SendResultAsync<TestPayload>(request, (JsonTypeInfo<TestPayload>)null!, TestContext.Current.CancellationToken);
        await nullTypeInfo.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> nullOptions = async () => await client.SendResultAsync<TestPayload>(request, (JsonSerializerOptions)null!, TestContext.Current.CancellationToken);
        await nullOptions.Should().ThrowAsync<ArgumentNullException>();
    }

    private static StringContent JsonContent(string body)
        => new(body, Encoding.UTF8, "application/json");

    /// <summary>
    /// A fake handler driven by a delegate, counting sends and disposes.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            => _send = send;

        public int SendCount { get; private set; }

        public int DisposeCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            return await _send(request, cancellationToken).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A response that records how many times it is disposed.
    /// </summary>
    private sealed class TrackingResponse : HttpResponseMessage
    {
        public TrackingResponse(HttpStatusCode statusCode, HttpContent content)
            : base(statusCode)
        {
            Content = content;
        }

        public int DisposeCount { get; private set; }

        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A request that records how many times it is disposed.
    /// </summary>
    private sealed class TrackingRequest : HttpRequestMessage
    {
        public TrackingRequest(HttpMethod method, string requestUri)
            : base(method, requestUri)
        {
        }

        public int DisposeCount { get; private set; }

        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }
}
