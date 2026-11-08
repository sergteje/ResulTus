// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Runtime.CompilerServices;

namespace ResulTus.Testing;

/// <summary>
/// Framework-agnostic assertion helpers for the three result shapes: unit <see cref="Result"/>,
/// <see cref="Result{T}"/> and <see cref="Result{TSuccess, TError}"/>.
/// </summary>
/// <remarks>
/// Each helper returns the payload the caller usually wants next — the success value, the error, or the
/// unit receiver — so a test can assert on it without a cast or a second call. An unexpected state throws
/// <see cref="ResultAssertionException"/> with a message that names the actual error, which every test
/// framework reports as a failure. No test framework or assertion library is referenced.
/// </remarks>
public static class ResultAssertions
{
    /// <summary>
    /// Asserts that a unit result is successful.
    /// </summary>
    /// <param name="result">The result under test.</param>
    /// <returns>The same <paramref name="result"/>, so further assertions can chain.</returns>
    /// <exception cref="ResultAssertionException">The result is a failure.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result ShouldSucceed(this Result result)
    {
        if (result.TryGetError(out var error))
        {
            throw new ResultAssertionException(
                $"Expected a successful result but found a failure: {Describe(error)}.");
        }

        return result;
    }

    /// <summary>
    /// Asserts that a unit result is a failure.
    /// </summary>
    /// <param name="result">The result under test.</param>
    /// <returns>The <see cref="Error"/> the result carries.</returns>
    /// <exception cref="ResultAssertionException">The result is successful.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error ShouldFail(this Result result)
        => result.TryGetError(out var error)
            ? error
            : throw new ResultAssertionException("Expected a failed result but the result was successful.");

    /// <summary>
    /// Asserts that a <see cref="Result{T}"/> is successful.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result under test.</param>
    /// <returns>The success value.</returns>
    /// <exception cref="ResultAssertionException">The result is a failure.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ShouldSucceed<T>(this Result<T> result)
        => result.TryGetValue(out var value, out var error)
            ? value!
            : throw new ResultAssertionException(
                $"Expected a successful result but found a failure: {Describe(error)}.");

    /// <summary>
    /// Asserts that a <see cref="Result{T}"/> is a failure.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result under test.</param>
    /// <returns>The <see cref="Error"/> the result carries.</returns>
    /// <remarks>
    /// The message never includes the success value: a value that failed an assertion is still data the
    /// test did not choose to print, so it stays out of the report.
    /// </remarks>
    /// <exception cref="ResultAssertionException">The result is successful.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error ShouldFail<T>(this Result<T> result)
        => result.TryGetValue(out _, out var error)
            ? throw new ResultAssertionException("Expected a failed result but the result was successful.")
            : error;

    /// <summary>
    /// Asserts that a <see cref="Result{TSuccess, TError}"/> is successful.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result under test.</param>
    /// <returns>The success value.</returns>
    /// <exception cref="ResultAssertionException">The result is a failure.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSuccess ShouldSucceed<TSuccess, TError>(this Result<TSuccess, TError> result)
    {
        result.Deconstruct(out var isSuccess, out var value, out var error);

        return isSuccess
            ? value!
            : throw new ResultAssertionException(
                $"Expected a successful typed result but found a failure: {Describe(error)}.");
    }

    /// <summary>
    /// Asserts that a <see cref="Result{TSuccess, TError}"/> is a failure.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result under test.</param>
    /// <returns>The typed error value the result carries.</returns>
    /// <exception cref="ResultAssertionException">The result is successful.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TError ShouldFail<TSuccess, TError>(this Result<TSuccess, TError> result)
    {
        result.Deconstruct(out var isSuccess, out _, out var error);

        return isSuccess
            ? throw new ResultAssertionException("Expected a failed typed result but the result was successful.")
            : error!;
    }

    private static string Describe(Error error)
        => error.Message is null
            ? $"{error.Type} '{error.Code}'"
            : $"{error.Type} '{error.Code}': {error.Message}";

    private static string Describe<TError>(TError error)
        => error is null ? "null" : error.ToString() ?? string.Empty;
}
