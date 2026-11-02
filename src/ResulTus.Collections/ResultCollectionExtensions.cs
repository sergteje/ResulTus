// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ResulTus.Collections;

/// <summary>
/// Collection-level operations over <see cref="IEnumerable{T}"/> of <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// Every method short-circuits on the first failure and stops enumerating there. Sequencing
/// accumulates into a rented <see cref="ArrayPool{T}"/> buffer that is always returned, and the
/// returned collection is an exactly sized array — never the rented buffer.
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
