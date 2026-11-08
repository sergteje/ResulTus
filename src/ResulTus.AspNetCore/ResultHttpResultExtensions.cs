// Copyright (c) ResulTus. All rights reserved.

using System;
using Microsoft.AspNetCore.Http;

namespace ResulTus.AspNetCore;

/// <summary>
/// Converts a <see cref="Result"/> or <see cref="Result{T}"/> into an ASP.NET Core
/// <see cref="IResult"/> for Minimal APIs.
/// </summary>
/// <remarks>
/// A successful result becomes <c>200 OK</c> carrying the value, if any. A failure becomes the
/// mapped status code with an RFC 9457 ProblemDetails body describing the <see cref="Error"/>.
/// </remarks>
public static class ResultHttpResultExtensions
{
    /// <summary>
    /// Converts a <see cref="Result"/> into an <see cref="IResult"/> using the default status mapping.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns><c>200 OK</c> on success, or the mapped status with a ProblemDetails body on failure.</returns>
    public static IResult ToHttpResult(this Result result) => result.ToHttpResult(ResultHttpStatus.GetStatusCode);

    /// <summary>
    /// Converts a <see cref="Result"/> into an <see cref="IResult"/> using a custom status selector.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <param name="statusSelector">Selects the HTTP status code for the error. Overrides the default mapping.</param>
    /// <returns><c>200 OK</c> on success, or the selected status with a ProblemDetails body on failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="statusSelector"/> is <c>null</c>.</exception>
    public static IResult ToHttpResult(this Result result, Func<Error, int> statusSelector)
    {
        ArgumentNullException.ThrowIfNull(statusSelector);

        if (result.TryGetError(out var error))
        {
            return Results.Problem(ResultProblemDetails.Create(error, statusSelector(error)));
        }

        return Results.Ok();
    }

    /// <summary>
    /// Converts a <see cref="Result{T}"/> into an <see cref="IResult"/> using the default status mapping.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns><c>200 OK</c> carrying the value on success, or the mapped status with a ProblemDetails body on failure.</returns>
    public static IResult ToHttpResult<T>(this Result<T> result) => result.ToHttpResult(ResultHttpStatus.GetStatusCode);

    /// <summary>
    /// Converts a <see cref="Result{T}"/> into an <see cref="IResult"/> using a custom status selector.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <param name="statusSelector">Selects the HTTP status code for the error. Overrides the default mapping.</param>
    /// <returns><c>200 OK</c> carrying the value on success, or the selected status with a ProblemDetails body on failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="statusSelector"/> is <c>null</c>.</exception>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<Error, int> statusSelector)
    {
        ArgumentNullException.ThrowIfNull(statusSelector);

        if (result.TryGetValue(out var value, out var error))
        {
            return Results.Ok(value);
        }

        return Results.Problem(ResultProblemDetails.Create(error, statusSelector(error)));
    }
}
