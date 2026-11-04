// Copyright (c) ResulTus. All rights reserved.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ResulTus.AspNetCore;

/// <summary>
/// Renders a <see cref="Result{TSuccess,TError}"/> carrying <see cref="Validation.ValidationErrors"/>
/// as an RFC 9457 response whose <c>errors</c> extension maps each field to its messages.
/// </summary>
/// <remarks>
/// <c>errors</c> is the member RFC 9457 defines for validation failures, so a client can place each
/// message next to the input it belongs to instead of parsing one collapsed string. Object-level
/// failures use the empty field name, matching ASP.NET Core's <c>ModelState</c>.
/// </remarks>
public static class ValidationResultHttpExtensions
{
    /// <summary>
    /// Converts a validation result into an <see cref="IResult"/> for Minimal APIs.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns>
    /// <c>200 OK</c> carrying the value on success, or <c>400 Bad Request</c> with a ProblemDetails
    /// body carrying the <c>errors</c> map on failure.
    /// </returns>
    public static IResult ToHttpResult<T>(this Result<T, Validation.ValidationErrors> result)
    {
        var (isSuccess, value, errors) = result;

        if (isSuccess)
        {
            return Results.Ok(value);
        }

        return Results.Problem(ValidationProblemDetails.Create(errors!));
    }

    /// <summary>
    /// Converts a validation result into an MVC action result.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> carrying the value on success, or an
    /// <see cref="ObjectResult"/> with status <c>400</c> and the ProblemDetails document on failure.
    /// </returns>
    public static ActionResult<T> ToActionResult<T>(this Result<T, Validation.ValidationErrors> result)
    {
        var (isSuccess, value, errors) = result;

        if (isSuccess)
        {
            return new ActionResult<T>(value!);
        }

        return new ActionResult<T>(new ObjectResult(ValidationProblemDetails.Create(errors!))
        {
            StatusCode = StatusCodes.Status400BadRequest,
        });
    }
}

/// <summary>
/// Builds the RFC 9457 <see cref="ProblemDetails"/> document that carries validation failures.
/// </summary>
internal static class ValidationProblemDetails
{
    /// <summary>
    /// Creates a <c>400 Bad Request</c> problem document describing <paramref name="errors"/>.
    /// </summary>
    /// <param name="errors">The validation failures to render.</param>
    /// <returns>The problem document.</returns>
    /// <remarks>
    /// No <c>detail</c> is written: there is no single message that honestly summarizes several
    /// field failures. <c>type</c> is left unset so ASP.NET Core supplies the RFC 9110 section URI.
    /// </remarks>
    internal static ProblemDetails Create(Validation.ValidationErrors errors)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
        };

        problem.Extensions["errors"] = errors.ToDictionary();

        return problem;
    }
}
