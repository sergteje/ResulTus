// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.HttpClient;

/// <summary>
/// Classifies how a client-side HTTP operation failed, independently of the server-side error model.
/// </summary>
/// <remarks>
/// <para>
/// The set is closed and minimal: each value names a distinct stage at which an HTTP operation can
/// fail. There is deliberately no "no error" member, because an <see cref="HttpClientError"/> always
/// represents a failure and has no success-side default value to describe.
/// </para>
/// <para>
/// Caller cancellation is intentionally absent. Cancellation is control flow, not an HTTP error: a
/// cancelled operation rethrows <see cref="OperationCanceledException"/> rather than being captured here.
/// </para>
/// <para>
/// These values are NOT a reverse mapping of <see cref="ErrorType"/> from the ASP.NET Core adapter.
/// That mapping is server-side (<c>ErrorType</c> to status) and renders errors as responses; this
/// classification describes client-side failure stages, keyed to the actual
/// <see cref="System.Net.HttpStatusCode"/> of a response when one was received.
/// </para>
/// </remarks>
public enum HttpClientFailureKind : byte
{
    /// <summary>
    /// The request failed at the transport level (DNS, connection, protocol) before any response was
    /// received. No <see cref="System.Net.HttpStatusCode"/> is available.
    /// </summary>
    Transport = 0,

    /// <summary>
    /// The client timed out waiting for a response. No <see cref="System.Net.HttpStatusCode"/> is
    /// available.
    /// </summary>
    Timeout,

    /// <summary>
    /// A response was received, but its status code was not in the successful (2xx) range. The error
    /// carries the actual response status.
    /// </summary>
    StatusError,

    /// <summary>
    /// A successful (2xx) response was received, but its body could not be read or deserialized into
    /// the expected shape.
    /// </summary>
    InvalidResponse,
}
