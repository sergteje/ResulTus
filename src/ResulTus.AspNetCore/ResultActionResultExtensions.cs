// Copyright (c) ResulTus. All rights reserved.

using System;
using Microsoft.AspNetCore.Mvc;

namespace ResulTus.AspNetCore;

/// <summary>
/// Converts a <see cref="Result"/> or <see cref="Result{T}"/> into an MVC action result.
/// </summary>
/// <remarks>
/// A successful result becomes <c>200 OK</c> carrying the value, if any. A failure becomes an
/// <see cref="ObjectResult"/> whose status is the mapped status code and whose value is the
/// RFC 9457 ProblemDetails document describing the <see cref="Error"/>.
/// </remarks>
public static class ResultActionResultExtensions
{
    /// <summary>
    /// Converts a <see cref="Result"/> into an <see cref="IActionResult"/> using the default status mapping.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns><see cref="OkResult"/> on success, or an <see cref="ObjectResult"/> carrying ProblemDetails on failure.</returns>
    public static IActionResult ToActionResult(this Result result) => result.ToActionResult(ResultHttpStatus.GetStatusCode);

    /// <summary>
    /// Converts a <see cref="Result"/> into an <see cref="IActionResult"/> using a custom status selector.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <param name="statusSelector">Selects the HTTP status code for the error. Overrides the default mapping.</param>
    /// <returns><see cref="OkResult"/> on success, or an <see cref="ObjectResult"/> carrying ProblemDetails on failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="statusSelector"/> is <c>null</c>.</exception>
    public static IActionResult ToActionResult(this Result result, Func<Error, int> statusSelector)
    {
        ArgumentNullException.ThrowIfNull(statusSelector);

        if (result.TryGetError(out var error))
        {
            var statusCode = statusSelector(error);
            return new ObjectResult(ResultProblemDetails.Create(error, statusCode)) { StatusCode = statusCode };
        }

        return new OkResult();
    }

    /// <summary>
    /// Converts a <see cref="Result{T}"/> into an <see cref="ActionResult{T}"/> using the default status mapping.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns>An <see cref="ActionResult{T}"/> carrying the value on success, or ProblemDetails on failure.</returns>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
        => result.ToActionResult(ResultHttpStatus.GetStatusCode);

    /// <summary>
    /// Converts a <see cref="Result{T}"/> into an <see cref="ActionResult{T}"/> using a custom status selector.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <param name="statusSelector">Selects the HTTP status code for the error. Overrides the default mapping.</param>
    /// <returns>An <see cref="ActionResult{T}"/> carrying the value on success, or ProblemDetails on failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="statusSelector"/> is <c>null</c>.</exception>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result, Func<Error, int> statusSelector)
    {
        ArgumentNullException.ThrowIfNull(statusSelector);

        if (result.TryGetValue(out var value, out var error))
        {
            return new ActionResult<T>(value!);
        }

        var statusCode = statusSelector(error);
        return new ActionResult<T>(
            new ObjectResult(ResultProblemDetails.Create(error, statusCode)) { StatusCode = statusCode });
    }
}
