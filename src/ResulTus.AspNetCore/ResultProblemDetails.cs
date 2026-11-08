// Copyright (c) ResulTus. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ResulTus.AspNetCore;

/// <summary>
/// Builds the RFC 9457 <see cref="ProblemDetails"/> document that carries an <see cref="Error"/>
/// to the client.
/// </summary>
internal static class ResultProblemDetails
{
    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> describing <paramref name="error"/> at the given status.
    /// </summary>
    /// <param name="error">The error to describe.</param>
    /// <param name="statusCode">The HTTP status code being returned.</param>
    /// <returns>The problem document.</returns>
    /// <remarks>
    /// <c>type</c> is deliberately left unset: RFC 9457 defaults it to <c>about:blank</c>, and
    /// inventing a URI scheme would impose a contract nobody asked for. <c>detail</c> is omitted
    /// when the error carries no message, rather than being replaced with an empty string.
    /// </remarks>
    internal static ProblemDetails Create(Error error, int statusCode)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
        };

        var reason = ReasonPhrases.GetReasonPhrase(statusCode);
        if (!string.IsNullOrEmpty(reason))
        {
            problem.Title = reason;
        }

        if (error.Message is { } message)
        {
            problem.Detail = message;
        }

        problem.Extensions["code"] = error.Code;

        return problem;
    }
}
