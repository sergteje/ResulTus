// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ResulTus.AspNetCore;

/// <summary>
/// Converts a unit <see cref="Result"/> returned by a Minimal API handler into the same
/// <see cref="IResult"/> the package's <c>ToHttpResult()</c> conversion produces.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="IEndpointFilter"/> receives the handler's return value as <see cref="object"/>, so
/// recognizing a result type at run time would need reflection, and this assembly is AOT-compatible
/// and trimming-safe. The package therefore ships two filters closed at compile time instead: this
/// one for the unit tier and <see cref="ResultEndpointFilter{TSuccess}"/> for
/// <see cref="Result{TSuccess}"/>. Neither infers the other's type.
/// </para>
/// <para>
/// Every other value — an <see cref="IResult"/>, a DTO, a string, <c>null</c> — is returned
/// untouched, so adding the filter never changes a handler that returns something else. The failure
/// path uses the default <c>ErrorType</c> to status mapping, the same one the parameterless
/// <c>ToHttpResult()</c> uses; a handler that needs a custom selector keeps calling
/// <c>ToHttpResult(statusSelector)</c> itself.
/// </para>
/// <para>
/// A <see cref="Result{TSuccess,TError}"/> is deliberately not converted here: the error type
/// carries the policy that maps it to a status, and <c>ResulTus.Validation</c> bridges
/// <c>Result&lt;TSuccess, ValidationErrors&gt;</c> through its own explicit conversion. Attach this
/// filter with <c>WithResultMapping()</c> on the route handler; the caller never names this type.
/// </para>
/// </remarks>
public sealed class ResultEndpointFilter : IEndpointFilter
{
    /// <summary>
    /// Converts a unit <see cref="Result"/> returned by the next filter or the handler, and returns
    /// every other value untouched.
    /// </summary>
    /// <param name="context">The invocation context of the current request.</param>
    /// <param name="next">The next filter in the pipeline, or the handler itself when this is the last one.</param>
    /// <returns>
    /// The <see cref="IResult"/> the unit result converts to, or the value <paramref name="next"/>
    /// produced when it is not a unit <see cref="Result"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="next"/> is <c>null</c>.</exception>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var value = await next(context).ConfigureAwait(false);

        return value is Result result ? result.ToHttpResult() : value;
    }
}

/// <summary>
/// Converts a <see cref="Result{TSuccess}"/> returned by a Minimal API handler into the same
/// <see cref="IResult"/> the package's <c>ToHttpResult()</c> conversion produces.
/// </summary>
/// <typeparam name="TSuccess">The success type of the result this filter converts.</typeparam>
/// <remarks>
/// <para>
/// The success type is closed at registration time, so no reflection is needed to recognize the
/// result and the assembly stays AOT-compatible. Attach it with <c>WithResultMapping&lt;TSuccess&gt;()</c>
/// on the route handler; the caller never names this type.
/// </para>
/// <para>
/// Only <see cref="Result{TSuccess}"/> is converted: a result over another success type, an
/// <see cref="IResult"/>, a DTO, a string or <c>null</c> is returned untouched, so a handler that
/// returns something else behaves exactly as it did without the filter. The failure path uses the
/// default <c>ErrorType</c> to status mapping, the same one the parameterless <c>ToHttpResult()</c>
/// uses.
/// </para>
/// <para>
/// A <see cref="Result{TSuccess,TError}"/> is deliberately out of scope: the error type is the policy
/// that maps it to a status, and <c>ResulTus.Validation</c> bridges
/// <c>Result&lt;TSuccess, ValidationErrors&gt;</c> through its own explicit conversion.
/// </para>
/// </remarks>
public sealed class ResultEndpointFilter<TSuccess> : IEndpointFilter
{
    /// <summary>
    /// Converts a <see cref="Result{TSuccess}"/> returned by the next filter or the handler, and
    /// returns every other value untouched.
    /// </summary>
    /// <param name="context">The invocation context of the current request.</param>
    /// <param name="next">The next filter in the pipeline, or the handler itself when this is the last one.</param>
    /// <returns>
    /// The <see cref="IResult"/> the result converts to, or the value <paramref name="next"/>
    /// produced when it is not a <see cref="Result{TSuccess}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="next"/> is <c>null</c>.</exception>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var value = await next(context).ConfigureAwait(false);

        return value is Result<TSuccess> result ? result.ToHttpResult() : value;
    }
}
