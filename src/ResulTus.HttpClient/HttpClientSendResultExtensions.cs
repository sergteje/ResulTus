// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using HttpCli = System.Net.Http.HttpClient;

namespace ResulTus.HttpClient;

/// <summary>
/// Sends an <see cref="System.Net.Http.HttpRequestMessage"/> through a caller-owned
/// <see cref="System.Net.Http.HttpClient"/> and returns a <c>Result</c>, capturing transport and timeout
/// failures without throwing for them.
/// </summary>
/// <remarks>
/// <para>
/// The send layer is distinct from the response-conversion layer
/// (<see cref="HttpResponseMessageResultExtensions"/>). <c>SendResultAsync</c> sends the request and maps
/// only transport-level failures: an <see cref="System.Net.Http.HttpRequestException"/> becomes an
/// <see cref="HttpClientFailureKind.Transport"/> failure and a client timeout becomes an
/// <see cref="HttpClientFailureKind.Timeout"/> failure. HTTP status and body handling (a non-2xx response,
/// ProblemDetails parsing, malformed JSON) remain the response conversion's job; the typed overloads
/// delegate to it after the send.
/// </para>
/// <para>
/// Ownership: the caller owns and disposes both the <see cref="System.Net.Http.HttpClient"/> and the
/// <see cref="System.Net.Http.HttpRequestMessage"/>; neither is created, disposed or replaced here. The raw
/// overload returns the response to the caller for inspection and disposal, while the typed overloads
/// dispose the response they receive after consuming it. The request is never disposed and the operation
/// is never retried.
/// </para>
/// <para>
/// Cancellation is control flow, not an HTTP failure: a cancelled caller token surfaces as
/// <see cref="System.OperationCanceledException"/> and is never converted into an
/// <see cref="HttpClientError"/>. A client timeout is distinguished from caller cancellation by the
/// <see cref="System.TimeoutException"/> the BCL nests inside the
/// <see cref="System.Threading.Tasks.TaskCanceledException"/> when <c>HttpClient.Timeout</c> elapses;
/// a caller-cancelled token produces no such inner exception and is rethrown instead.
/// </para>
/// </remarks>
public static class HttpClientSendResultExtensions
{
    /// <summary>
    /// Sends <paramref name="request"/> through <paramref name="client"/> and returns the raw
    /// <see cref="System.Net.Http.HttpResponseMessage"/> on success.
    /// </summary>
    /// <param name="client">The caller-owned <see cref="System.Net.Http.HttpClient"/> to send with. It is never disposed.</param>
    /// <param name="request">The caller-owned <see cref="System.Net.Http.HttpRequestMessage"/> to send. It is never disposed.</param>
    /// <param name="cancellationToken">A token that cancels the send.</param>
    /// <returns>
    /// A success holding the received <see cref="System.Net.Http.HttpResponseMessage"/> — regardless of its
    /// HTTP status, which the response conversion interprets — or an <see cref="HttpClientError"/> failure:
    /// <see cref="HttpClientFailureKind.Transport"/> for an <see cref="System.Net.Http.HttpRequestException"/>,
    /// or <see cref="HttpClientFailureKind.Timeout"/> when the client timed out.
    /// </returns>
    /// <remarks>
    /// The returned response is caller-owned on success: the caller inspects and disposes it. This overload
    /// disposes nothing.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public static Task<Result<HttpResponseMessage, HttpClientError>> SendResultAsync(
        this HttpCli client,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        return SendCoreAsync(client, request, cancellationToken);
    }

    /// <summary>
    /// Sends <paramref name="request"/> through <paramref name="client"/> and converts the received response
    /// into a <c>Result&lt;TSuccess, HttpClientError&gt;</c> using source-generated metadata for the value type.
    /// </summary>
    /// <typeparam name="TSuccess">The type to deserialize a successful (2xx) JSON body into.</typeparam>
    /// <param name="client">The caller-owned <see cref="System.Net.Http.HttpClient"/> to send with. It is never disposed.</param>
    /// <param name="request">The caller-owned <see cref="System.Net.Http.HttpRequestMessage"/> to send. It is never disposed.</param>
    /// <param name="jsonTypeInfo">
    /// The <see cref="JsonTypeInfo{T}"/> metadata for <typeparamref name="TSuccess"/> emitted by a
    /// source-generated <c>JsonSerializerContext</c> declaring <c>[JsonSerializable(typeof(TSuccess))]</c>.
    /// This is the Native AOT route.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the send and the body read.</param>
    /// <returns>
    /// A success holding the deserialized value for a 2xx response, or an <see cref="HttpClientError"/>
    /// failure: <see cref="HttpClientFailureKind.Transport"/> for an
    /// <see cref="System.Net.Http.HttpRequestException"/>, <see cref="HttpClientFailureKind.Timeout"/> when the
    /// client timed out, or the response conversion's error for a non-2xx status or a malformed body.
    /// </returns>
    /// <remarks>The received response is disposed exactly once, after it has been consumed.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="client"/>, <paramref name="request"/> or <paramref name="jsonTypeInfo"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public static async Task<Result<TSuccess, HttpClientError>> SendResultAsync<TSuccess>(
        this HttpCli client,
        HttpRequestMessage request,
        JsonTypeInfo<TSuccess> jsonTypeInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        var send = await SendCoreAsync(client, request, cancellationToken).ConfigureAwait(false);
        send.Deconstruct(out var isSuccess, out var response, out var error);
        if (!isSuccess)
        {
            return Result<TSuccess, HttpClientError>.Failure(error!);
        }

        using var httpResponse = response!;
        return await httpResponse.ToResultAsync(jsonTypeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <paramref name="request"/> through <paramref name="client"/> and converts the received response
    /// into a <c>Result&lt;TSuccess, HttpClientError&gt;</c> using the supplied
    /// <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <typeparam name="TSuccess">The type to deserialize a successful (2xx) JSON body into.</typeparam>
    /// <param name="client">The caller-owned <see cref="System.Net.Http.HttpClient"/> to send with. It is never disposed.</param>
    /// <param name="request">The caller-owned <see cref="System.Net.Http.HttpRequestMessage"/> to send. It is never disposed.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> used to deserialize the body.</param>
    /// <param name="cancellationToken">A token that cancels the send and the body read.</param>
    /// <returns>
    /// A success holding the deserialized value for a 2xx response, or an <see cref="HttpClientError"/>
    /// failure: <see cref="HttpClientFailureKind.Transport"/> for an
    /// <see cref="System.Net.Http.HttpRequestException"/>, <see cref="HttpClientFailureKind.Timeout"/> when the
    /// client timed out, or the response conversion's error for a non-2xx status or a malformed body.
    /// </returns>
    /// <remarks>
    /// This is the reflection-based convenience path: the converter for <typeparamref name="TSuccess"/>
    /// is resolved from <paramref name="options"/>, which requires dynamic and unreferenced code. Native
    /// AOT callers should use the <see cref="JsonTypeInfo{T}"/> overload instead. The received response is
    /// disposed exactly once, after it has been consumed.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="client"/>, <paramref name="request"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public static async Task<Result<TSuccess, HttpClientError>> SendResultAsync<TSuccess>(
        this HttpCli client,
        HttpRequestMessage request,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        var send = await SendCoreAsync(client, request, cancellationToken).ConfigureAwait(false);
        send.Deconstruct(out var isSuccess, out var response, out var error);
        if (!isSuccess)
        {
            return Result<TSuccess, HttpClientError>.Failure(error!);
        }

        using var httpResponse = response!;
        return await httpResponse.ToResultAsync<TSuccess>(options, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<HttpResponseMessage, HttpClientError>> SendCoreAsync(
        HttpCli client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return Result<HttpResponseMessage, HttpClientError>.Success(response);
        }
        catch (OperationCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return Result<HttpResponseMessage, HttpClientError>.Failure(HttpClientError.Timeout(ex));
        }
        catch (OperationCanceledException)
        {
            // Caller cancellation is control flow, not an HTTP failure: preserve it untouched.
            throw;
        }
        catch (HttpRequestException ex)
        {
            return Result<HttpResponseMessage, HttpClientError>.Failure(HttpClientError.Transport(ex));
        }
    }
}
