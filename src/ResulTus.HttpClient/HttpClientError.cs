// Copyright (c) ResulTus. All rights reserved.

using System.Net;

namespace ResulTus.HttpClient;

/// <summary>
/// Represents a client-side HTTP failure, carrying the failure kind and any HTTP-specific context:
/// the optional response status, parsed problem details and originating exception.
/// </summary>
/// <remarks>
/// <para>
/// This is a sealed class rather than a readonly record struct. The library's inline struct error
/// (<see cref="Error"/>) is chosen for its tiny, reference-free layout; an HTTP failure inherently
/// owns references — an optional <see cref="Exception"/> and an optional <see cref="HttpProblemDetails"/>
/// (itself holding a dictionary of extension members) — so the struct convention does not apply here.
/// </para>
/// <para>
/// An <see cref="HttpClientError"/> is a failure only: it has no "no error" default, and it is not
/// collapsed into the core <see cref="Error"/> (whose code/message/type shape cannot preserve an HTTP
/// status or a problem document).
/// </para>
/// </remarks>
public sealed class HttpClientError : IEquatable<HttpClientError>
{
    /// <summary>
    /// Initializes a new <see cref="HttpClientError"/>.
    /// </summary>
    /// <param name="kind">The <see cref="HttpClientFailureKind"/> classifying the failure (required).</param>
    /// <param name="status">The optional <see cref="HttpStatusCode"/> of the response that failed, when one was received.</param>
    /// <param name="problemDetails">The optional parsed <see cref="HttpProblemDetails"/> document, when the response carried one.</param>
    /// <param name="exception">The optional originating exception.</param>
    public HttpClientError(
        HttpClientFailureKind kind,
        HttpStatusCode? status = null,
        HttpProblemDetails? problemDetails = null,
        Exception? exception = null)
    {
        Kind = kind;
        Status = status;
        ProblemDetails = problemDetails;
        Exception = exception;
    }

    /// <summary>
    /// Gets the <see cref="HttpClientFailureKind"/> classifying the failure.
    /// </summary>
    public HttpClientFailureKind Kind { get; }

    /// <summary>
    /// Gets the optional <see cref="HttpStatusCode"/> of the response that failed.
    /// </summary>
    /// <remarks><c>null</c> when no response was received (transport or timeout failures).</remarks>
    public HttpStatusCode? Status { get; }

    /// <summary>
    /// Gets the optional parsed <see cref="HttpProblemDetails"/> document from the response.
    /// </summary>
    public HttpProblemDetails? ProblemDetails { get; }

    /// <summary>
    /// Gets the optional originating exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Creates a <see cref="HttpClientFailureKind.Transport"/> failure from the originating exception.
    /// </summary>
    /// <param name="exception">The optional transport-level exception (for example an <c>HttpRequestException</c>).</param>
    /// <returns>A transport failure carrying the exception.</returns>
    public static HttpClientError Transport(Exception? exception = null)
        => new(HttpClientFailureKind.Transport, exception: exception);

    /// <summary>
    /// Creates a <see cref="HttpClientFailureKind.Timeout"/> failure.
    /// </summary>
    /// <param name="exception">The optional underlying timeout exception.</param>
    /// <returns>A timeout failure.</returns>
    public static HttpClientError Timeout(Exception? exception = null)
        => new(HttpClientFailureKind.Timeout, exception: exception);

    /// <summary>
    /// Creates a <see cref="HttpClientFailureKind.StatusError"/> failure for a non-success response.
    /// </summary>
    /// <param name="status">The actual response status (required).</param>
    /// <param name="problemDetails">The optional parsed problem document.</param>
    /// <param name="exception">The optional originating exception.</param>
    /// <returns>A status-error failure carrying the response status.</returns>
    public static HttpClientError StatusError(
        HttpStatusCode status,
        HttpProblemDetails? problemDetails = null,
        Exception? exception = null)
        => new(HttpClientFailureKind.StatusError, status, problemDetails, exception);

    /// <summary>
    /// Creates an <see cref="HttpClientFailureKind.InvalidResponse"/> failure for a malformed or unreadable body.
    /// </summary>
    /// <param name="status">The optional response status.</param>
    /// <param name="exception">The optional parsing exception.</param>
    /// <returns>An invalid-response failure.</returns>
    public static HttpClientError InvalidResponse(HttpStatusCode? status = null, Exception? exception = null)
        => new(HttpClientFailureKind.InvalidResponse, status, exception: exception);

    /// <summary>
    /// Determines whether this error equals another by value: kind, status, problem details
    /// (structurally) and originating exception (by reference).
    /// </summary>
    /// <param name="other">The error to compare with.</param>
    /// <returns><c>true</c> when the errors are equal; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The originating exception is compared by reference: two distinct exceptions with the same
    /// message are not the same failure.
    /// </remarks>
    public bool Equals(HttpClientError? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Kind == other.Kind
            && Status == other.Status
            && ProblemDetails == other.ProblemDetails
            && ReferenceEquals(Exception, other.Exception);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as HttpClientError);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Status, ProblemDetails, Exception);

    /// <summary>
    /// Determines whether two errors are equal.
    /// </summary>
    /// <param name="left">The first error to compare.</param>
    /// <param name="right">The second error to compare.</param>
    /// <returns><c>true</c> when the errors are equal; otherwise <c>false</c>.</returns>
    public static bool operator ==(HttpClientError? left, HttpClientError? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Determines whether two errors are not equal.
    /// </summary>
    /// <param name="left">The first error to compare.</param>
    /// <param name="right">The second error to compare.</param>
    /// <returns><c>true</c> when the errors are not equal; otherwise <c>false</c>.</returns>
    public static bool operator !=(HttpClientError? left, HttpClientError? right)
        => !(left == right);
}
