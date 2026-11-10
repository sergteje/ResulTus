// Copyright (c) ResulTus. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ResulTus.HttpClient;

/// <summary>
/// Converts an <see cref="System.Net.Http.HttpResponseMessage"/> into a
/// <c>Result&lt;TSuccess, HttpClientError&gt;</c> (or a <c>Result&lt;HttpResponseMessage, HttpClientError&gt;</c>)
/// without throwing for ordinary HTTP failures.
/// </summary>
/// <remarks>
/// <para>
/// A 2xx response is a success: the typed conversions deserialize its JSON body, while the raw
/// conversion returns the response itself. A non-2xx response is a
/// <see cref="HttpClientFailureKind.StatusError"/> failure carrying the actual status and, when the
/// content type is <c>application/problem+json</c>, a parsed <see cref="HttpProblemDetails"/>. A
/// malformed JSON body on a 2xx response is an <see cref="HttpClientFailureKind.InvalidResponse"/>
/// failure. None of these conversions disposes the caller-owned response.
/// </para>
/// <para>
/// There are two ways to deserialize the typed value, and the difference is exactly the difference
/// between what a reflection-based serializer can resolve and what Native AOT can:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Source-generated metadata — AOT-suitable.</b> The typed overload taking a
/// <see cref="JsonTypeInfo{T}"/> stores the metadata the source generator emitted and never consults
/// ambient options for the value type.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Ambient options — not AOT-safe.</b> The options-based convenience overload resolves the
/// converter for the value type from the supplied <see cref="JsonSerializerOptions"/>, which is the
/// reflection path. Native AOT callers should use the <see cref="JsonTypeInfo{T}"/> overload instead.
/// </description>
/// </item>
/// </list>
/// <para>
/// Cancellation is control flow, not an HTTP failure: a cancelled token surfaces as
/// <see cref="OperationCanceledException"/> from the awaited read and is never converted into an
/// <see cref="HttpClientError"/>.
/// </para>
/// </remarks>
public static class HttpResponseMessageResultExtensions
{
    /// <summary>
    /// Converts <paramref name="response"/> into a <c>Result&lt;TSuccess, HttpClientError&gt;</c> using
    /// source-generated metadata for the value type.
    /// </summary>
    /// <typeparam name="TSuccess">The type to deserialize a successful (2xx) JSON body into.</typeparam>
    /// <param name="response">The caller-owned response to convert. It is never disposed.</param>
    /// <param name="jsonTypeInfo">
    /// The <see cref="JsonTypeInfo{T}"/> metadata for <typeparamref name="TSuccess"/> emitted by a
    /// source-generated <c>JsonSerializerContext</c> declaring <c>[JsonSerializable(typeof(TSuccess))]</c>.
    /// This is the Native AOT route.
    /// </param>
    /// <param name="cancellationToken">A token that cancels reading and deserializing the body.</param>
    /// <returns>
    /// A success holding the deserialized value for a 2xx response, or an <see cref="HttpClientError"/>
    /// failure: <see cref="HttpClientFailureKind.StatusError"/> for a non-2xx response, or
    /// <see cref="HttpClientFailureKind.InvalidResponse"/> when a 2xx body is malformed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> or <paramref name="jsonTypeInfo"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public static async Task<Result<TSuccess, HttpClientError>> ToResultAsync<TSuccess>(
        this HttpResponseMessage response,
        JsonTypeInfo<TSuccess> jsonTypeInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        if (response.IsSuccessStatusCode)
        {
            return await ReadValueAsync(response, jsonTypeInfo, cancellationToken).ConfigureAwait(false);
        }

        return Result<TSuccess, HttpClientError>.Failure(
            await ReadStatusErrorAsync(response, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Converts <paramref name="response"/> into a <c>Result&lt;TSuccess, HttpClientError&gt;</c> using
    /// the supplied <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <typeparam name="TSuccess">The type to deserialize a successful (2xx) JSON body into.</typeparam>
    /// <param name="response">The caller-owned response to convert. It is never disposed.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> used to deserialize the body.</param>
    /// <param name="cancellationToken">A token that cancels reading and deserializing the body.</param>
    /// <returns>
    /// A success holding the deserialized value for a 2xx response, or an <see cref="HttpClientError"/>
    /// failure: <see cref="HttpClientFailureKind.StatusError"/> for a non-2xx response, or
    /// <see cref="HttpClientFailureKind.InvalidResponse"/> when a 2xx body is malformed.
    /// </returns>
    /// <remarks>
    /// This is the reflection-based convenience path: the converter for <typeparamref name="TSuccess"/>
    /// is resolved from <paramref name="options"/>, which requires dynamic and unreferenced code. Native
    /// AOT callers should use the <see cref="JsonTypeInfo{T}"/> overload instead.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public static async Task<Result<TSuccess, HttpClientError>> ToResultAsync<TSuccess>(
        this HttpResponseMessage response,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(options);

        if (response.IsSuccessStatusCode)
        {
            return await ReadValueAsync<TSuccess>(response, options, cancellationToken).ConfigureAwait(false);
        }

        return Result<TSuccess, HttpClientError>.Failure(
            await ReadStatusErrorAsync(response, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Converts <paramref name="response"/> into a <c>Result&lt;HttpResponseMessage, HttpClientError&gt;</c>
    /// carrying the raw response on success.
    /// </summary>
    /// <param name="response">The caller-owned response to convert. It is never disposed.</param>
    /// <param name="cancellationToken">A token that cancels reading a problem details body, when one is read.</param>
    /// <returns>
    /// A success holding <paramref name="response"/> for a 2xx response, or an <see cref="HttpClientError"/>
    /// failure: <see cref="HttpClientFailureKind.StatusError"/> for a non-2xx response.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled while reading a problem details body.</exception>
    public static async Task<Result<HttpResponseMessage, HttpClientError>> ToResultAsync(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.IsSuccessStatusCode)
        {
            return Result<HttpResponseMessage, HttpClientError>.Success(response);
        }

        return Result<HttpResponseMessage, HttpClientError>.Failure(
            await ReadStatusErrorAsync(response, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<Result<TSuccess, HttpClientError>> ReadValueAsync<TSuccess>(
        HttpResponseMessage response,
        JsonTypeInfo<TSuccess> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return Result<TSuccess, HttpClientError>.Failure(
                HttpClientError.InvalidResponse(response.StatusCode, new JsonException("The response had no content to deserialize.")));
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var value = await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken).ConfigureAwait(false);
            return Result<TSuccess, HttpClientError>.Success(value!);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is control flow, not an HTTP failure: preserve it untouched.
            throw;
        }
        catch (JsonException ex)
        {
            return Result<TSuccess, HttpClientError>.Failure(
                HttpClientError.InvalidResponse(response.StatusCode, ex));
        }
    }

    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:RequiresDynamicCode",
        Justification = "Only the JsonSerializerOptions overload reaches this path; it resolves an arbitrary TSuccess through ambient options. Native AOT callers use the JsonTypeInfo<TSuccess> overload instead.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Only the JsonSerializerOptions overload reaches this path; it resolves an arbitrary TSuccess through ambient options. Native AOT callers use the JsonTypeInfo<TSuccess> overload instead.")]
    private static async Task<Result<TSuccess, HttpClientError>> ReadValueAsync<TSuccess>(
        HttpResponseMessage response,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return Result<TSuccess, HttpClientError>.Failure(
                HttpClientError.InvalidResponse(response.StatusCode, new JsonException("The response had no content to deserialize.")));
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var value = await JsonSerializer.DeserializeAsync<TSuccess>(stream, options, cancellationToken).ConfigureAwait(false);
            return Result<TSuccess, HttpClientError>.Success(value!);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is control flow, not an HTTP failure: preserve it untouched.
            throw;
        }
        catch (JsonException ex)
        {
            return Result<TSuccess, HttpClientError>.Failure(
                HttpClientError.InvalidResponse(response.StatusCode, ex));
        }
    }

    private static async Task<HttpClientError> ReadStatusErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = response.StatusCode;

        if (response.Content is null)
        {
            return HttpClientError.StatusError(status);
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase))
        {
            return HttpClientError.StatusError(status);
        }

        try
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                // Absent body: keep the status, leave ProblemDetails unset, no parsing exception.
                return HttpClientError.StatusError(status);
            }

            return HttpClientError.StatusError(status, ParseProblemDetails(bytes));
        }
        catch (OperationCanceledException)
        {
            // Cancellation is control flow, not an HTTP failure: preserve it untouched.
            throw;
        }
        catch (JsonException ex)
        {
            // Malformed problem document: keep the status, leave ProblemDetails unset, retain the parsing exception.
            return HttpClientError.StatusError(status, exception: ex);
        }
    }

    private static HttpProblemDetails ParseProblemDetails(byte[] utf8Json)
    {
        using var document = JsonDocument.Parse(utf8Json);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The problem details document must be a JSON object.");
        }

        string? type = null;
        string? title = null;
        int? status = null;
        string? detail = null;
        string? instance = null;
        Dictionary<string, JsonElement>? extensions = null;

        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name)
            {
                case "type":
                    type = ReadProblemString(property, "type");
                    break;
                case "title":
                    title = ReadProblemString(property, "title");
                    break;
                case "status":
                    status = ReadProblemStatus(property);
                    break;
                case "detail":
                    detail = ReadProblemString(property, "detail");
                    break;
                case "instance":
                    instance = ReadProblemString(property, "instance");
                    break;
                default:
                    // Clone detaches the JsonElement from the source document, which is disposed below.
                    (extensions ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal))
                        .Add(property.Name, property.Value.Clone());
                    break;
            }
        }

        return new HttpProblemDetails(type, title, status, detail, instance, extensions);
    }

    private static string? ReadProblemString(JsonProperty property, string name)
    {
        return property.Value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => property.Value.GetString(),
            _ => throw new JsonException($"The '{name}' member of a problem details document must be a string or null."),
        };
    }

    private static int? ReadProblemStatus(JsonProperty property)
    {
        if (property.Value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.Value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException("The 'status' member of a problem details document must be an integer or null.");
        }

        if (property.Value.TryGetInt32(out var status))
        {
            return status;
        }

        throw new JsonException("The 'status' member of a problem details document must be an integer or null.");
    }
}
