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
    /// Drops the success value, projecting a <see cref="Result{T}"/> onto the unit <see cref="Result"/>.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to project.</param>
    /// <returns>A successful unit result, or the original failure with the same error.</returns>
    /// <remarks>
    /// The inverse of <see cref="UnitResultExtensions.Map{T}(Result, Func{T})"/>: use it when a pipeline
    /// ends at an outcome and the value is no longer of interest. Both types are structs, so the
    /// projection is a copy.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result ToUnitResult<T>(this Result<T> result)
        => result.TryGetValue(out _, out var error) ? Result.Success() : Result.Failure(error);

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

    /// <summary>
    /// Converts a typed result into a <see cref="Result{T}"/> by mapping its error.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the typed error value.</typeparam>
    /// <param name="result">The typed result to convert.</param>
    /// <param name="map">Function applied to the typed error. Never invoked on success.</param>
    /// <returns>The success value preserved in a <see cref="Result{T}"/>, or a failure holding the mapped <see cref="Error"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess> ToErrorResult<TSuccess, TError>(
        this Result<TSuccess, TError> result,
        Func<TError, Error> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        result.Deconstruct(out var isSuccess, out var value, out var error);
        return isSuccess ? Result<TSuccess>.Success(value!) : Result<TSuccess>.Failure(map(error!));
    }

    /// <summary>
    /// Converts a typed result whose error is already an <see cref="Error"/> into a <see cref="Result{T}"/>.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <param name="result">The typed result to convert.</param>
    /// <returns>The success value preserved in a <see cref="Result{T}"/>, or the same error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess> ToErrorResult<TSuccess>(this Result<TSuccess, Error> result)
    {
        result.Deconstruct(out var isSuccess, out var value, out var error);
        return isSuccess ? Result<TSuccess>.Success(value!) : Result<TSuccess>.Failure(error!);
    }

    /// <summary>
    /// Lifts a <see cref="Result{T}"/> into the typed tier by mapping its <see cref="Error"/>.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the typed error value.</typeparam>
    /// <param name="result">The result to lift.</param>
    /// <param name="map">Function applied to the error. Never invoked on success.</param>
    /// <returns>The success value preserved in a typed result, or a failure holding the mapped error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TError> ToGenericResult<TSuccess, TError>(
        this Result<TSuccess> result,
        Func<Error, TError> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return result.TryGetValue(out var value, out var error)
            ? Result<TSuccess, TError>.Success(value!)
            : Result<TSuccess, TError>.Failure(map(error));
    }
}
