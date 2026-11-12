// Copyright (c) ResulTus. All rights reserved.

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BenchmarkDotNet.Attributes;
using ResulTus.HttpClient;

namespace ResulTus.Benchmarks;

/// <summary>
/// Macro, end-to-end benchmark of the <see cref="ResulTus.HttpClient"/> result-producing pipeline
/// against the idiomatic raw <see cref="System.Net.Http.HttpClient"/> baseline.
/// </summary>
/// <remarks>
/// Each row drives a real <see cref="System.Net.Http.HttpClient"/> over a hand-written fake
/// <see cref="HttpMessageHandler"/> that returns a fresh, deterministic response — zero network. The
/// ResulTus rows send through <c>SendResultAsync&lt;T&gt;</c> and consume a
/// <c>Result&lt;Widget, HttpClientError&gt;</c>; the raw rows perform the equivalent manual status
/// check and <see cref="JsonSerializer"/> read, or <c>EnsureSuccessStatusCode()</c> on failure. The
/// value type is deserialized through the AOT-safe source-generated <see cref="JsonTypeInfo{T}"/> route
/// in both camps, so the comparison isolates the result-producing wrapper rather than the JSON engine.
/// Every method builds a fresh <see cref="HttpRequestMessage"/> per invocation because an
/// <see cref="HttpRequestMessage"/> can only be sent once; the shared request creation is paid
/// identically by both camps and is therefore excluded from the comparison. Each method returns
/// its produced outcome — BenchmarkDotNet consumes returned values — so no keep-alive
/// accumulator exists.
/// </remarks>
[MemoryDiagnoser]
public class HttpClientMacroBenchmark
{
    private System.Net.Http.HttpClient _successClient = null!;
    private System.Net.Http.HttpClient _failureClient = null!;
    private Uri _uri = null!;
    private JsonTypeInfo<Widget> _widgetTypeInfo = null!;

    /// <summary>
    /// Builds the two fake-handler clients and the source-generated metadata.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _successClient = new System.Net.Http.HttpClient(
            new FixedResponseHandler(HttpStatusCode.OK, """{"Name":"widget","Count":42}"""));
        _failureClient = new System.Net.Http.HttpClient(
            new FixedResponseHandler(HttpStatusCode.InternalServerError));
        _uri = new Uri("http://example.test/widget");
        _widgetTypeInfo = WidgetJsonContext.Default.Widget;
    }

    /// <summary>
    /// Disposes the two fake-handler clients. Each benchmark method returns its produced
    /// outcome — BenchmarkDotNet consumes returned values, so no extra keep-alive
    /// accumulator is needed.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _successClient.Dispose();
        _failureClient.Dispose();
    }

    /// <summary>
    /// Success through ResulTus: send and convert a 200 JSON body into a result, returning
    /// the produced value so the engine consumes it.
    /// </summary>
    [Benchmark]
    public async Task<Widget?> ResulTus_Success()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _uri);
        var result = await _successClient.SendResultAsync(request, _widgetTypeInfo);
        result.Deconstruct(out var isSuccess, out var value, out _);
        return isSuccess ? value : default;
    }

    /// <summary>
    /// Success through raw HttpClient: manual status check and JSON read, returning the
    /// deserialized value so the engine consumes it.
    /// </summary>
    [Benchmark]
    public async Task<Widget?> Raw_Success()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _uri);
        using var response = await _successClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync(stream, _widgetTypeInfo);
    }

    /// <summary>
    /// Failure through ResulTus: a non-success status becomes a failure carrying an
    /// <see cref="HttpClientError"/>, returned so the engine consumes it.
    /// </summary>
    [Benchmark]
    public async Task<HttpClientError?> ResulTus_Failure()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _uri);
        var result = await _failureClient.SendResultAsync(request, _widgetTypeInfo);
        result.Deconstruct(out var isSuccess, out _, out var error);
        return isSuccess ? default : error;
    }

    /// <summary>
    /// Failure through raw HttpClient: <c>EnsureSuccessStatusCode()</c> throws, and the
    /// caught exception is returned so the engine consumes it.
    /// </summary>
    [Benchmark]
    public async Task<Exception?> Raw_Failure()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _uri);
        using var response = await _failureClient.SendAsync(request);
        try
        {
            response.EnsureSuccessStatusCode();
            return default;
        }
        catch (HttpRequestException exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// A fake handler returning a fresh response with the configured status and optional JSON body.
    /// </summary>
    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _json;

        public FixedResponseHandler(HttpStatusCode statusCode, string? json = null)
        {
            _statusCode = statusCode;
            _json = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            if (_json is not null)
            {
                response.Content = new StringContent(_json, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }
}

/// <summary>
/// The payload deserialized from successful JSON bodies.
/// </summary>
public sealed record Widget(string Name, int Count);

/// <summary>
/// Source-generated metadata for <see cref="Widget"/>, exercising the Native AOT
/// <see cref="JsonTypeInfo{T}"/> route in the benchmark.
/// </summary>
[JsonSerializable(typeof(Widget))]
internal sealed partial class WidgetJsonContext : JsonSerializerContext
{
}
