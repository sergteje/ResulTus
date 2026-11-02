// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Runtime.CompilerServices;

namespace ResulTus.Extensions;

/// <summary>
/// Conversions between <see cref="Result{T}"/>, plain values and exceptions.
/// </summary>
/// <remarks>
/// The phase-2 proposal also lists <c>ToOption</c> and <c>ToMaybe</c>. They are intentionally absent:
/// this library ships no <c>Option</c> or <c>Maybe</c> type, and inventing one here would add a public
/// type nobody asked for. Use <see cref="ToNullable{T}"/> for structs and
/// <see cref="Result{T}.GetValueOrDefault"/> for reference types.
/// </remarks>
public static class ResultConversions
{
    /// <summary>
    /// Wraps a value in a successful <see cref="Result{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A success holding <paramref name="value"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToResult<T>(this T value) => Result<T>.Success(value);

    /// <summary>
    /// Converts a <see cref="Result{T}"/> over a value type into a nullable value.
    /// </summary>
    /// <typeparam name="T">The struct type of the success value.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns>The success value, or <c>null</c> on failure.</returns>
    /// <remarks>
    /// Constrained to value types on purpose: two overloads differing only in constraints do not
    /// compile, and for reference types <see cref="Result{T}.GetValueOrDefault"/> already returns
    /// <c>null</c> on failure.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? ToNullable<T>(this Result<T> result)
        where T : struct
        => result.TryGetValue(out var value, out _) ? value : null;

    /// <summary>
    /// Converts an <see cref="Error"/> into an <see cref="Exception"/>.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    /// <returns>
    /// An exception whose <see cref="Exception.Message"/> is the error message, or the error code when
    /// no message is set, and whose <c>Data["Code"]</c> holds the error code.
    /// </returns>
    public static Exception ToException(this Error error)
    {
        var exception = new Exception(error.Message ?? error.Code);
        exception.Data["Code"] = error.Code;
        return exception;
    }
}
