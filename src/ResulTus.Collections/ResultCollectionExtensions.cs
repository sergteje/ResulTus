// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ResulTus.Collections;

/// <summary>
/// Collection-level operations over sequences, arrays and read-only lists of
/// <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// Every method short-circuits on the first failure and stops enumerating there. The
/// materializing operations return an exactly sized array; the internal buffering used to build
/// it is either an indexed write straight into that array or a rented
/// <see cref="ArrayPool{T}"/> buffer that is always returned.
/// </remarks>
public static class ResultCollectionExtensions
{
    /// <summary>
    /// Collects every success value in enumeration order, or fails with the first error.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The results to sequence.</param>
    /// <returns>A success holding the values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<T>> Sequence<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var clearOnReturn = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        var buffer = ArrayPool<T>.Shared.Rent(InitialCapacity(results));
        var count = 0;

        try
        {
            foreach (var result in results)
            {
                if (!result.TryGetValue(out var value, out var error))
                {
                    return Result<IReadOnlyList<T>>.Failure(error);
                }

                if (count == buffer.Length)
                {
                    buffer = Grow(buffer, count, clearOnReturn);
                }

                buffer[count++] = value!;
            }

            var values = new T[count];
            Array.Copy(buffer, values, count);
            return Result<IReadOnlyList<T>>.Success(values);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(buffer, clearOnReturn);
        }
    }

    /// <summary>
    /// Maps each source element to a result and collects every success value in order,
    /// or fails with the first error.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TOut">The type of the mapped success values.</typeparam>
    /// <param name="source">The elements to traverse.</param>
    /// <param name="selector">Projection applied to each element. Never invoked after the first failure.</param>
    /// <returns>A success holding the mapped values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="selector"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<TOut>> Traverse<TSource, TOut>(
        this IEnumerable<TSource> source,
        Func<TSource, Result<TOut>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var clearOnReturn = RuntimeHelpers.IsReferenceOrContainsReferences<TOut>();
        var buffer = ArrayPool<TOut>.Shared.Rent(InitialCapacity(source));
        var count = 0;

        try
        {
            foreach (var element in source)
            {
                if (!selector(element).TryGetValue(out var value, out var error))
                {
                    return Result<IReadOnlyList<TOut>>.Failure(error);
                }

                if (count == buffer.Length)
                {
                    buffer = Grow(buffer, count, clearOnReturn);
                }

                buffer[count++] = value!;
            }

            var values = new TOut[count];
            Array.Copy(buffer, values, count);
            return Result<IReadOnlyList<TOut>>.Success(values);
        }
        finally
        {
            ArrayPool<TOut>.Shared.Return(buffer, clearOnReturn);
        }
    }

    /// <summary>
    /// Folds the success values into an accumulator, or fails with the first error.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <typeparam name="TAcc">The type of the accumulator.</typeparam>
    /// <param name="results">The results to fold.</param>
    /// <param name="seed">The initial accumulator.</param>
    /// <param name="accumulate">Folding function. Never invoked after the first failure.</param>
    /// <returns>A success holding the final accumulator, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="accumulate"/> is <c>null</c>.</exception>
    public static Result<TAcc> Fold<T, TAcc>(
        this IEnumerable<Result<T>> results,
        TAcc seed,
        Func<TAcc, T, TAcc> accumulate)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(accumulate);

        var accumulator = seed;
        foreach (var result in results)
        {
            if (!result.TryGetValue(out var value, out var error))
            {
                return Result<TAcc>.Failure(error);
            }

            accumulator = accumulate(accumulator, value!);
        }

        return Result<TAcc>.Success(accumulator);
    }

    /// <summary>
    /// Returns the error of the first failed element, or <c>null</c> when every element succeeded.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns>The first error in enumeration order, or <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Error? FirstFailure<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        foreach (var result in results)
        {
            if (!result.TryGetValue(out _, out var error))
            {
                return error;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the first successful element, the first failure when every element failed, or a
    /// failure with code <c>"empty"</c> for an empty sequence.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns>The first success, the first failure, or <c>Error.Unexpected("empty")</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result<T> FirstSuccess<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        Error? firstFailure = null;
        foreach (var result in results)
        {
            if (result.TryGetValue(out _, out var error))
            {
                return result;
            }

            firstFailure ??= error;
        }

        return firstFailure is { } failure
            ? Result<T>.Failure(failure)
            : Result<T>.Failure(Error.Unexpected("empty", "The sequence contains no results."));
    }

    /// <summary>
    /// Splits the sequence into its success values and its errors, preserving order.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The results to partition.</param>
    /// <returns>Two exactly sized arrays: the success values and the errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static (T[] Successes, Error[] Failures) Partition<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var successes = new List<T>();
        var failures = new List<Error>();

        foreach (var result in results)
        {
            if (result.TryGetValue(out var value, out var error))
            {
                successes.Add(value!);
            }
            else
            {
                failures.Add(error);
            }
        }

        return (successes.ToArray(), failures.ToArray());
    }

    /// <summary>
    /// Lazily yields the success values, skipping every failure.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The results to flatten.</param>
    /// <returns>A lazy sequence of the success values, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static IEnumerable<T> Flatten<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return FlattenCore(results);
    }

    /// <summary>
    /// Runs an action per success value and reduces the sequence to a single result.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The results to visit.</param>
    /// <param name="action">Side effect invoked per success. Never invoked after the first failure.</param>
    /// <returns>A success, or the first failure in enumeration order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static Result ForEach<T>(this IEnumerable<Result<T>> results, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(action);

        foreach (var result in results)
        {
            if (!result.TryGetValue(out var value, out var error))
            {
                return Result.Failure(error);
            }

            action(value!);
        }

        return Result.Success();
    }

    /// <summary>
    /// Determines whether every element succeeded. An empty sequence returns <c>true</c>.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when no element failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AllSucceeded<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        foreach (var result in results)
        {
            if (!result.IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether at least one element succeeded. An empty sequence returns <c>false</c>.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when at least one element succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AnySucceeded<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether no element succeeded. An empty sequence returns <c>true</c>.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when every element failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool NoneSucceeded<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Sequences successful results from an array into a materialized read-only list.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to sequence.</param>
    /// <returns>A success holding the values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<T>> Sequence<T>(this Result<T>[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var values = new T[results.Length];
        for (var index = 0; index < results.Length; index++)
        {
            if (!results[index].TryGetValue(out var value, out var error))
            {
                return Result<IReadOnlyList<T>>.Failure(error);
            }

            values[index] = value!;
        }

        return Result<IReadOnlyList<T>>.Success(values);
    }

    /// <summary>
    /// Sequences successful results from a read-only list into a materialized read-only list.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to sequence.</param>
    /// <returns>A success holding the values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<T>> Sequence<T>(this IReadOnlyList<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var length = results.Count;
        var values = new T[length];
        for (var index = 0; index < length; index++)
        {
            if (!results[index].TryGetValue(out var value, out var error))
            {
                return Result<IReadOnlyList<T>>.Failure(error);
            }

            values[index] = value!;
        }

        return Result<IReadOnlyList<T>>.Success(values);
    }

    /// <summary>
    /// Maps every element in an array and sequences the successful values.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TOut">The type of each mapped success value.</typeparam>
    /// <param name="source">The source values to traverse.</param>
    /// <param name="selector">The operation applied to each source value. It stops at the first failure.</param>
    /// <returns>A success holding the mapped values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="selector"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<TOut>> Traverse<TSource, TOut>(
        this TSource[] source,
        Func<TSource, Result<TOut>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var values = new TOut[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            if (!selector(source[index]).TryGetValue(out var value, out var error))
            {
                return Result<IReadOnlyList<TOut>>.Failure(error);
            }

            values[index] = value!;
        }

        return Result<IReadOnlyList<TOut>>.Success(values);
    }

    /// <summary>
    /// Maps every element in a read-only list and sequences the successful values.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TOut">The type of each mapped success value.</typeparam>
    /// <param name="source">The source values to traverse.</param>
    /// <param name="selector">The operation applied to each source value. It stops at the first failure.</param>
    /// <returns>A success holding the mapped values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="selector"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<TOut>> Traverse<TSource, TOut>(
        this IReadOnlyList<TSource> source,
        Func<TSource, Result<TOut>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var length = source.Count;
        var values = new TOut[length];
        for (var index = 0; index < length; index++)
        {
            if (!selector(source[index]).TryGetValue(out var value, out var error))
            {
                return Result<IReadOnlyList<TOut>>.Failure(error);
            }

            values[index] = value!;
        }

        return Result<IReadOnlyList<TOut>>.Success(values);
    }

    /// <summary>
    /// Folds successful results from an array into an accumulator, or returns the first failure.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <typeparam name="TAcc">The type of the accumulator.</typeparam>
    /// <param name="results">The results to fold.</param>
    /// <param name="seed">The initial accumulator.</param>
    /// <param name="accumulate">The operation applied to each success value. It stops at the first failure.</param>
    /// <returns>The final accumulator, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="accumulate"/> is <c>null</c>.</exception>
    public static Result<TAcc> Fold<T, TAcc>(
        this Result<T>[] results,
        TAcc seed,
        Func<TAcc, T, TAcc> accumulate)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(accumulate);

        var accumulator = seed;
        for (var index = 0; index < results.Length; index++)
        {
            if (!results[index].TryGetValue(out var value, out var error))
            {
                return Result<TAcc>.Failure(error);
            }

            accumulator = accumulate(accumulator, value!);
        }

        return Result<TAcc>.Success(accumulator);
    }

    /// <summary>
    /// Folds successful results from a read-only list into an accumulator, or returns the first failure.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <typeparam name="TAcc">The type of the accumulator.</typeparam>
    /// <param name="results">The results to fold.</param>
    /// <param name="seed">The initial accumulator.</param>
    /// <param name="accumulate">The operation applied to each success value. It stops at the first failure.</param>
    /// <returns>The final accumulator, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="accumulate"/> is <c>null</c>.</exception>
    public static Result<TAcc> Fold<T, TAcc>(
        this IReadOnlyList<Result<T>> results,
        TAcc seed,
        Func<TAcc, T, TAcc> accumulate)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(accumulate);

        var accumulator = seed;
        for (var index = 0; index < results.Count; index++)
        {
            if (!results[index].TryGetValue(out var value, out var error))
            {
                return Result<TAcc>.Failure(error);
            }

            accumulator = accumulate(accumulator, value!);
        }

        return Result<TAcc>.Success(accumulator);
    }

    /// <summary>
    /// Returns the first failure in an array, or <c>null</c> when every result succeeded.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns>The first error in array order, or <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Error? FirstFailure<T>(this Result<T>[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Length; index++)
        {
            if (!results[index].TryGetValue(out _, out var error))
            {
                return error;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the first failure in a read-only list, or <c>null</c> when every result succeeded.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns>The first error in list order, or <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Error? FirstFailure<T>(this IReadOnlyList<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Count; index++)
        {
            if (!results[index].TryGetValue(out _, out var error))
            {
                return error;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the first successful result in an array, or the first failure when none succeeds.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns>The first success, the first failure, or an empty-sequence failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result<T> FirstSuccess<T>(this Result<T>[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        Error? firstFailure = null;
        for (var index = 0; index < results.Length; index++)
        {
            var result = results[index];
            if (result.TryGetValue(out _, out var error))
            {
                return result;
            }

            firstFailure ??= error;
        }

        return firstFailure is { } failure
            ? Result<T>.Failure(failure)
            : Result<T>.Failure(Error.Unexpected("empty", "The sequence contains no results."));
    }

    /// <summary>
    /// Returns the first successful result in a read-only list, or the first failure when none succeeds.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns>The first success, the first failure, or an empty-sequence failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result<T> FirstSuccess<T>(this IReadOnlyList<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        Error? firstFailure = null;
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            if (result.TryGetValue(out _, out var error))
            {
                return result;
            }

            firstFailure ??= error;
        }

        return firstFailure is { } failure
            ? Result<T>.Failure(failure)
            : Result<T>.Failure(Error.Unexpected("empty", "The sequence contains no results."));
    }

    /// <summary>
    /// Splits an array of results into success values and errors, preserving order.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to partition.</param>
    /// <returns>Two arrays containing all successes and failures in source order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static (T[] Successes, Error[] Failures) Partition<T>(this Result<T>[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var successes = new List<T>();
        var failures = new List<Error>();
        for (var index = 0; index < results.Length; index++)
        {
            if (results[index].TryGetValue(out var value, out var error))
            {
                successes.Add(value!);
            }
            else
            {
                failures.Add(error);
            }
        }

        return (successes.ToArray(), failures.ToArray());
    }

    /// <summary>
    /// Splits a read-only list of results into success values and errors, preserving order.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to partition.</param>
    /// <returns>Two arrays containing all successes and failures in source order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static (T[] Successes, Error[] Failures) Partition<T>(this IReadOnlyList<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var successes = new List<T>();
        var failures = new List<Error>();
        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].TryGetValue(out var value, out var error))
            {
                successes.Add(value!);
            }
            else
            {
                failures.Add(error);
            }
        }

        return (successes.ToArray(), failures.ToArray());
    }

    /// <summary>
    /// Runs an action for each successful result in an array and returns the first failure, if any.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to visit.</param>
    /// <param name="action">The action invoked for each success. It stops at the first failure.</param>
    /// <returns>A success, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static Result ForEach<T>(this Result<T>[] results, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(action);

        for (var index = 0; index < results.Length; index++)
        {
            if (!results[index].TryGetValue(out var value, out var error))
            {
                return Result.Failure(error);
            }

            action(value!);
        }

        return Result.Success();
    }

    /// <summary>
    /// Runs an action for each successful result in a read-only list and returns the first failure, if any.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to visit.</param>
    /// <param name="action">The action invoked for each success. It stops at the first failure.</param>
    /// <returns>A success, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static Result ForEach<T>(this IReadOnlyList<Result<T>> results, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(action);

        for (var index = 0; index < results.Count; index++)
        {
            if (!results[index].TryGetValue(out var value, out var error))
            {
                return Result.Failure(error);
            }

            action(value!);
        }

        return Result.Success();
    }

    /// <summary>
    /// Determines whether every result in an array succeeded. An empty array returns <c>true</c>.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when no result failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AllSucceeded<T>(this Result<T>[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Length; index++)
        {
            if (!results[index].IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether every result in a read-only list succeeded. An empty list returns <c>true</c>.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when no result failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AllSucceeded<T>(this IReadOnlyList<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Count; index++)
        {
            if (!results[index].IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether at least one result in an array succeeded. An empty array returns <c>false</c>.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when at least one result succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AnySucceeded<T>(this Result<T>[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Length; index++)
        {
            if (results[index].IsSuccess)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether at least one result in a read-only list succeeded. An empty list returns <c>false</c>.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when at least one result succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AnySucceeded<T>(this IReadOnlyList<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].IsSuccess)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether no result in an array succeeded. An empty array returns <c>true</c>.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when every result failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool NoneSucceeded<T>(this Result<T>[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Length; index++)
        {
            if (results[index].IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether no result in a read-only list succeeded. An empty list returns <c>true</c>.
    /// </summary>
    /// <typeparam name="T">The type of each success value.</typeparam>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when every result failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool NoneSucceeded<T>(this IReadOnlyList<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<T> FlattenCore<T>(IEnumerable<Result<T>> results)
    {
        foreach (var result in results)
        {
            if (result.TryGetValue(out var value, out _))
            {
                yield return value!;
            }
        }
    }

    private static int InitialCapacity<T>(IEnumerable<T> source)
        => source is ICollection<T> collection && collection.Count > 0 ? collection.Count : 4;
    private static T[] Grow<T>(T[] buffer, int count, bool clearOnReturn)
    {
        var grown = ArrayPool<T>.Shared.Rent(buffer.Length * 2);
        Array.Copy(buffer, grown, count);
        ArrayPool<T>.Shared.Return(buffer, clearOnReturn);
        return grown;
    }
}
