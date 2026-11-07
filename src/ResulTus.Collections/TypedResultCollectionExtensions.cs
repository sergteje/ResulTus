// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ResulTus.Collections;

/// <summary>
/// Collection-level operations over sequences, arrays and read-only lists of
/// <see cref="Result{TSuccess, TError}"/>.
/// </summary>
/// <remarks>
/// Mirrors the <see cref="Result{T}"/> collection operators without dropping the typed error. Every
/// method short-circuits on the first failure and stops enumerating there, and the failure it returns
/// is the exact <c>TError</c> of that first failed element in source order — never an
/// aggregate of several errors. The materializing operations return an exactly sized array; the
/// internal buffering used to build it is either an indexed write straight into that array or a rented
/// <see cref="ArrayPool{T}"/> buffer that is always returned.
/// </remarks>
public static class TypedResultCollectionExtensions
{
    /// <summary>
    /// Collects every success value in enumeration order, or fails with the first typed error.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success values.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="results">The results to sequence.</param>
    /// <returns>A success holding the values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<TSuccess>, TError> Sequence<TSuccess, TError>(
        this IEnumerable<Result<TSuccess, TError>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var clearOnReturn = RuntimeHelpers.IsReferenceOrContainsReferences<TSuccess>();
        var buffer = ArrayPool<TSuccess>.Shared.Rent(InitialCapacity(results));
        var count = 0;

        try
        {
            foreach (var result in results)
            {
                result.Deconstruct(out var isSuccess, out var value, out var error);
                if (!isSuccess)
                {
                    return Result<IReadOnlyList<TSuccess>, TError>.Failure(error!);
                }

                if (count == buffer.Length)
                {
                    buffer = Grow(buffer, count, clearOnReturn);
                }

                buffer[count++] = value!;
            }

            var values = new TSuccess[count];
            Array.Copy(buffer, values, count);
            return Result<IReadOnlyList<TSuccess>, TError>.Success(values);
        }
        finally
        {
            ArrayPool<TSuccess>.Shared.Return(buffer, clearOnReturn);
        }
    }

    /// <summary>
    /// Maps each source element to a typed result and collects every success value in order,
    /// or fails with the first typed error.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TSuccess">The type of the mapped success values.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="source">The elements to traverse.</param>
    /// <param name="selector">Projection applied to each element. Never invoked after the first failure.</param>
    /// <returns>A success holding the mapped values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="selector"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<TSuccess>, TError> Traverse<TSource, TSuccess, TError>(
        this IEnumerable<TSource> source,
        Func<TSource, Result<TSuccess, TError>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var clearOnReturn = RuntimeHelpers.IsReferenceOrContainsReferences<TSuccess>();
        var buffer = ArrayPool<TSuccess>.Shared.Rent(InitialCapacity(source));
        var count = 0;

        try
        {
            foreach (var element in source)
            {
                selector(element).Deconstruct(out var isSuccess, out var value, out var error);
                if (!isSuccess)
                {
                    return Result<IReadOnlyList<TSuccess>, TError>.Failure(error!);
                }

                if (count == buffer.Length)
                {
                    buffer = Grow(buffer, count, clearOnReturn);
                }

                buffer[count++] = value!;
            }

            var values = new TSuccess[count];
            Array.Copy(buffer, values, count);
            return Result<IReadOnlyList<TSuccess>, TError>.Success(values);
        }
        finally
        {
            ArrayPool<TSuccess>.Shared.Return(buffer, clearOnReturn);
        }
    }

    /// <summary>
    /// Folds the success values into an accumulator, or fails with the first typed error.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success values.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TAcc">The type of the accumulator.</typeparam>
    /// <param name="results">The results to fold.</param>
    /// <param name="seed">The initial accumulator.</param>
    /// <param name="accumulate">Folding function. Never invoked after the first failure.</param>
    /// <returns>A success holding the final accumulator, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="accumulate"/> is <c>null</c>.</exception>
    public static Result<TAcc, TError> Fold<TSuccess, TError, TAcc>(
        this IEnumerable<Result<TSuccess, TError>> results,
        TAcc seed,
        Func<TAcc, TSuccess, TAcc> accumulate)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(accumulate);

        var accumulator = seed;
        foreach (var result in results)
        {
            result.Deconstruct(out var isSuccess, out var value, out var error);
            if (!isSuccess)
            {
                return Result<TAcc, TError>.Failure(error!);
            }

            accumulator = accumulate(accumulator, value!);
        }

        return Result<TAcc, TError>.Success(accumulator);
    }

    /// <summary>
    /// Sequences successful typed results from an array into a materialized read-only list.
    /// </summary>
    /// <typeparam name="TSuccess">The type of each success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="results">The results to sequence.</param>
    /// <returns>A success holding the values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<TSuccess>, TError> Sequence<TSuccess, TError>(
        this Result<TSuccess, TError>[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var values = new TSuccess[results.Length];
        for (var index = 0; index < results.Length; index++)
        {
            results[index].Deconstruct(out var isSuccess, out var value, out var error);
            if (!isSuccess)
            {
                return Result<IReadOnlyList<TSuccess>, TError>.Failure(error!);
            }

            values[index] = value!;
        }

        return Result<IReadOnlyList<TSuccess>, TError>.Success(values);
    }

    /// <summary>
    /// Sequences successful typed results from a read-only list into a materialized read-only list.
    /// </summary>
    /// <typeparam name="TSuccess">The type of each success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="results">The results to sequence.</param>
    /// <returns>A success holding the values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<TSuccess>, TError> Sequence<TSuccess, TError>(
        this IReadOnlyList<Result<TSuccess, TError>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var length = results.Count;
        var values = new TSuccess[length];
        for (var index = 0; index < length; index++)
        {
            results[index].Deconstruct(out var isSuccess, out var value, out var error);
            if (!isSuccess)
            {
                return Result<IReadOnlyList<TSuccess>, TError>.Failure(error!);
            }

            values[index] = value!;
        }

        return Result<IReadOnlyList<TSuccess>, TError>.Success(values);
    }

    /// <summary>
    /// Maps every element in an array to a typed result and sequences the successful values.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TSuccess">The type of each mapped success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="source">The source values to traverse.</param>
    /// <param name="selector">The operation applied to each source value. It stops at the first failure.</param>
    /// <returns>A success holding the mapped values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="selector"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<TSuccess>, TError> Traverse<TSource, TSuccess, TError>(
        this TSource[] source,
        Func<TSource, Result<TSuccess, TError>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var values = new TSuccess[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            selector(source[index]).Deconstruct(out var isSuccess, out var value, out var error);
            if (!isSuccess)
            {
                return Result<IReadOnlyList<TSuccess>, TError>.Failure(error!);
            }

            values[index] = value!;
        }

        return Result<IReadOnlyList<TSuccess>, TError>.Success(values);
    }

    /// <summary>
    /// Maps every element in a read-only list to a typed result and sequences the successful values.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TSuccess">The type of each mapped success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="source">The source values to traverse.</param>
    /// <param name="selector">The operation applied to each source value. It stops at the first failure.</param>
    /// <returns>A success holding the mapped values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="selector"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<TSuccess>, TError> Traverse<TSource, TSuccess, TError>(
        this IReadOnlyList<TSource> source,
        Func<TSource, Result<TSuccess, TError>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var length = source.Count;
        var values = new TSuccess[length];
        for (var index = 0; index < length; index++)
        {
            selector(source[index]).Deconstruct(out var isSuccess, out var value, out var error);
            if (!isSuccess)
            {
                return Result<IReadOnlyList<TSuccess>, TError>.Failure(error!);
            }

            values[index] = value!;
        }

        return Result<IReadOnlyList<TSuccess>, TError>.Success(values);
    }

    /// <summary>
    /// Folds successful typed results from an array into an accumulator, or returns the first failure.
    /// </summary>
    /// <typeparam name="TSuccess">The type of each success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TAcc">The type of the accumulator.</typeparam>
    /// <param name="results">The results to fold.</param>
    /// <param name="seed">The initial accumulator.</param>
    /// <param name="accumulate">The operation applied to each success value. It stops at the first failure.</param>
    /// <returns>The final accumulator, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="accumulate"/> is <c>null</c>.</exception>
    public static Result<TAcc, TError> Fold<TSuccess, TError, TAcc>(
        this Result<TSuccess, TError>[] results,
        TAcc seed,
        Func<TAcc, TSuccess, TAcc> accumulate)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(accumulate);

        var accumulator = seed;
        for (var index = 0; index < results.Length; index++)
        {
            results[index].Deconstruct(out var isSuccess, out var value, out var error);
            if (!isSuccess)
            {
                return Result<TAcc, TError>.Failure(error!);
            }

            accumulator = accumulate(accumulator, value!);
        }

        return Result<TAcc, TError>.Success(accumulator);
    }

    /// <summary>
    /// Folds successful typed results from a read-only list into an accumulator, or returns the first failure.
    /// </summary>
    /// <typeparam name="TSuccess">The type of each success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TAcc">The type of the accumulator.</typeparam>
    /// <param name="results">The results to fold.</param>
    /// <param name="seed">The initial accumulator.</param>
    /// <param name="accumulate">The operation applied to each success value. It stops at the first failure.</param>
    /// <returns>The final accumulator, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="accumulate"/> is <c>null</c>.</exception>
    public static Result<TAcc, TError> Fold<TSuccess, TError, TAcc>(
        this IReadOnlyList<Result<TSuccess, TError>> results,
        TAcc seed,
        Func<TAcc, TSuccess, TAcc> accumulate)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(accumulate);

        var accumulator = seed;
        for (var index = 0; index < results.Count; index++)
        {
            results[index].Deconstruct(out var isSuccess, out var value, out var error);
            if (!isSuccess)
            {
                return Result<TAcc, TError>.Failure(error!);
            }

            accumulator = accumulate(accumulator, value!);
        }

        return Result<TAcc, TError>.Success(accumulator);
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
