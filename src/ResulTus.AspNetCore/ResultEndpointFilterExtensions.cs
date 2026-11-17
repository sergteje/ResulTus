// Copyright (c) ResulTus. All rights reserved.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ResulTus.AspNetCore;

/// <summary>
/// Adds the ResulTus endpoint filters to a Minimal API route, so a handler can return a
/// <see cref="Result"/> or a <see cref="Result{TSuccess}"/> directly instead of converting it itself.
/// </summary>
/// <remarks>
/// <para>
/// The two methods are closed at compile time, one per result tier, because an
/// <see cref="IEndpointFilter"/> sees the handler's return value as <see cref="object"/> and
/// recognizing the type at run time would need reflection, which the package's AOT-compatible
/// assemblies do not use. Pick the overload that matches what the handler returns:
/// <c>WithResultMapping()</c> for the unit <see cref="Result"/> and
/// <c>WithResultMapping&lt;TSuccess&gt;()</c> for <see cref="Result{TSuccess}"/>.
/// </para>
/// <para>
/// The filter converts only its own tier and returns every other value untouched — an
/// <see cref="IResult"/>, a DTO, a string, <c>null</c>, or a result over a different success type —
/// so adding it cannot change a handler that returns something else.
/// </para>
/// <para>
/// A <see cref="Result{TSuccess,TError}"/> is deliberately outside this surface: the error type is
/// the policy that decides its status, and <c>ResulTus.Validation</c> bridges
/// <c>Result&lt;TSuccess, ValidationErrors&gt;</c> through its own explicit conversion. The filter
/// also always uses the default status mapping; a handler that needs a custom selector calls
/// <c>ToHttpResult(statusSelector)</c> itself.
/// </para>
/// </remarks>
public static class ResultEndpointFilterExtensions
{
    /// <summary>
    /// Adds the filter that converts a unit <see cref="Result"/> returned by the route's handler.
    /// </summary>
    /// <param name="builder">The builder returned when the route was mapped.</param>
    /// <returns><paramref name="builder"/>, so the other route conventions can chain.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <c>null</c>.</exception>
    public static RouteHandlerBuilder WithResultMapping(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter(new ResultEndpointFilter());
    }

    /// <summary>
    /// Adds the filter that converts a <see cref="Result{TSuccess}"/> returned by the route's handler.
    /// </summary>
    /// <typeparam name="TSuccess">The success type of the result the handler returns.</typeparam>
    /// <param name="builder">The builder returned when the route was mapped.</param>
    /// <returns><paramref name="builder"/>, so the other route conventions can chain.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <c>null</c>.</exception>
    public static RouteHandlerBuilder WithResultMapping<TSuccess>(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter(new ResultEndpointFilter<TSuccess>());
    }
}
