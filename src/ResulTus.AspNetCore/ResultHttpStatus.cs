// Copyright (c) ResulTus. All rights reserved.

using Microsoft.AspNetCore.Http;

namespace ResulTus.AspNetCore;

/// <summary>
/// The default mapping from <see cref="ErrorType"/> to an HTTP status code.
/// </summary>
/// <remarks>
/// The mapping is total: every defined <see cref="ErrorType"/> has a status, and an undefined
/// value falls back to <c>500</c> rather than throwing inside a request. Every entry is
/// overridable per call by passing a <see cref="Func{T,TResult}"/> of <see cref="Error"/> to
/// <c>ToHttpResult</c> or <c>ToActionResult</c>.
/// </remarks>
public static class ResultHttpStatus
{
    /// <summary>
    /// Gets the default HTTP status code for an <see cref="Error"/>.
    /// </summary>
    /// <param name="error">The error to map.</param>
    /// <returns>The HTTP status code for the error's <see cref="Error.Type"/>.</returns>
    public static int GetStatusCode(Error error) => GetStatusCode(error.Type);

    /// <summary>
    /// Gets the default HTTP status code for an <see cref="ErrorType"/>.
    /// </summary>
    /// <param name="type">The error type to map.</param>
    /// <returns>The HTTP status code for <paramref name="type"/>.</returns>
    /// <remarks>
    /// <see cref="ErrorType.Validation"/> maps to 400, <see cref="ErrorType.Unauthorized"/> to 401,
    /// <see cref="ErrorType.Forbidden"/> to 403, <see cref="ErrorType.NotFound"/> to 404,
    /// <see cref="ErrorType.Conflict"/> to 409, <see cref="ErrorType.Timeout"/> to 504 and
    /// <see cref="ErrorType.Cancelled"/> to 499. <see cref="ErrorType.Failure"/>,
    /// <see cref="ErrorType.Unexpected"/> and <see cref="ErrorType.None"/> all map to 500:
    /// a failure carrying no error is a contradiction, not a client mistake.
    /// </remarks>
    public static int GetStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Cancelled => StatusCodes.Status499ClientClosedRequest,
        ErrorType.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status500InternalServerError,
    };
}
