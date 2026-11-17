// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ResulTus.Collections;

/// <summary>
/// Asynchronous collection-level operations over sequences of result-producing tasks and over async
/// streams of results.
/// </summary>
/// <remarks>
/// <c>WhenAllAsync</c> awaits every task and reports the first failure in enumeration order;
/// <c>WhenAnyAsync</c> returns the first task to complete, whatever its outcome.
/// <c>SequenceAsync</c>, <c>TraverseAsync</c> and <c>PartitionAsync</c> are the streaming half: each
/// consumes an <see cref="IAsyncEnumerable{T}"/> and produces the same outcome its synchronous
/// counterpart in <see cref="ResultCollectionExtensions"/> produces for the same sequence, with the
/// same short-circuit on the first failure, the same order and the same exactly sized result built
/// from a rented <see cref="ArrayPool{T}"/> buffer that is always returned.
/// Cancellation is control flow: every method here rethrows
/// <see cref="OperationCanceledException"/> and never turns it into a failed result.
/// </remarks>
public static class ResultCollectionAsyncExtensions
{
    /// <summary>
    /// The floor a streamed buffer starts at. A stream reports no count up front, so unlike the
    /// indexed overloads there is nothing to size the first rental from.
    /// </summary>
    private const int DefaultCapacity = 4;

    /// <summary>
    /// Awaits every task and sequences the results, or reports the first failure in enumeration order.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="tasks">The tasks to await.</param>
    /// <param name="cancellationToken">Token observed before and during the wait.</param>
    /// <returns>A success holding the values in enumeration order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tasks"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<Result<IReadOnlyList<T>>> WhenAllAsync<T>(
        this IEnumerable<Task<Result<T>>> tasks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        cancellationToken.ThrowIfCancellationRequested();

        var completed = await Task.WhenAll(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        return completed.Sequence();
    }

    /// <summary>
    /// Awaits every unit-result task and reduces them, or reports the first failure in enumeration order.
    /// </summary>
    /// <param name="tasks">The tasks to await.</param>
    /// <param name="cancellationToken">Token observed before and during the wait.</param>
    /// <returns>A success, or the first failure in enumeration order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tasks"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<Result> WhenAllAsync(
        this IEnumerable<Task<Result>> tasks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        cancellationToken.ThrowIfCancellationRequested();

        var completed = await Task.WhenAll(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        return completed.Combine();
    }

    /// <summary>
    /// Returns the result of the first task to complete.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="tasks">The tasks to race.</param>
    /// <param name="cancellationToken">Token observed before and during the wait.</param>
    /// <returns>The result of the first task to complete, whatever its outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tasks"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tasks"/> contains no elements.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<Result<T>> WhenAnyAsync<T>(
        this IEnumerable<Task<Result<T>>> tasks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        cancellationToken.ThrowIfCancellationRequested();

        var materialized = tasks as IReadOnlyList<Task<Result<T>>> ?? tasks.ToList();
        if (materialized.Count == 0)
        {
            throw new ArgumentException("The sequence contains no tasks.", nameof(tasks));
        }

        var completed = await Task.WhenAny(materialized).WaitAsync(cancellationToken).ConfigureAwait(false);
        return await completed.ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the unit result of the first task to complete.
    /// </summary>
    /// <param name="tasks">The tasks to race.</param>
    /// <param name="cancellationToken">Token observed before and during the wait.</param>
    /// <returns>The result of the first task to complete, whatever its outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tasks"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tasks"/> contains no elements.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<Result> WhenAnyAsync(
        this IEnumerable<Task<Result>> tasks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        cancellationToken.ThrowIfCancellationRequested();

        var materialized = tasks as IReadOnlyList<Task<Result>> ?? tasks.ToList();
        if (materialized.Count == 0)
        {
            throw new ArgumentException("The sequence contains no tasks.", nameof(tasks));
        }

        var completed = await Task.WhenAny(materialized).WaitAsync(cancellationToken).ConfigureAwait(false);
        return await completed.ConfigureAwait(false);
    }

    /// <summary>
    /// Collects every success value in stream order, or fails with the first error.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The async stream of results to sequence.</param>
    /// <param name="cancellationToken">Token observed before enumeration and on every element.</param>
    /// <returns>A success holding the values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <remarks>
    /// Enumeration stops at the first failure, exactly as <c>Sequence</c> does. The token reaches the
    /// source through <c>WithCancellation</c>, so an async iterator observes it, and it is also
    /// checked per element so a source that ignores the token cannot turn a cancellation into a
    /// success.
    /// </remarks>
    public static async Task<Result<IReadOnlyList<T>>> SequenceAsync<T>(
        this IAsyncEnumerable<Result<T>> results,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);
        cancellationToken.ThrowIfCancellationRequested();

        var clearOnReturn = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        var buffer = ArrayPool<T>.Shared.Rent(DefaultCapacity);
        var count = 0;

        try
        {
            await foreach (var result in results.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!result.TryGetValue(out var value, out var error))
                {
                    return Result<IReadOnlyList<T>>.Failure(error);
                }

                if (count == buffer.Length)
                {
                    buffer = ResultCollectionExtensions.Grow(buffer, count, clearOnReturn);
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
    /// Maps each stream element to a result and collects every success value in order, or fails with
    /// the first error.
    /// </summary>
    /// <typeparam name="TSource">The type of the stream elements.</typeparam>
    /// <typeparam name="TOut">The type of the mapped success values.</typeparam>
    /// <param name="source">The elements to traverse.</param>
    /// <param name="selector">
    /// Asynchronous projection applied to each element with the token. Never invoked after the first
    /// failure.
    /// </param>
    /// <param name="cancellationToken">
    /// Token observed before enumeration, on every element and by <paramref name="selector"/>.
    /// </param>
    /// <returns>A success holding the mapped values in order, or the first failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="selector"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <remarks>
    /// Elements are mapped one at a time in stream order, never in parallel, so the collected values
    /// keep the source order and the selector stops being invoked after the first failure, exactly
    /// as <c>Traverse</c> behaves.
    /// </remarks>
    public static async Task<Result<IReadOnlyList<TOut>>> TraverseAsync<TSource, TOut>(
        this IAsyncEnumerable<TSource> source,
        Func<TSource, CancellationToken, Task<Result<TOut>>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        cancellationToken.ThrowIfCancellationRequested();

        var clearOnReturn = RuntimeHelpers.IsReferenceOrContainsReferences<TOut>();
        var buffer = ArrayPool<TOut>.Shared.Rent(DefaultCapacity);
        var count = 0;

        try
        {
            await foreach (var element in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await selector(element, cancellationToken).ConfigureAwait(false);
                if (!result.TryGetValue(out var value, out var error))
                {
                    return Result<IReadOnlyList<TOut>>.Failure(error);
                }

                if (count == buffer.Length)
                {
                    buffer = ResultCollectionExtensions.Grow(buffer, count, clearOnReturn);
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
    /// Splits the stream into its success values and its errors, preserving order.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The async stream of results to partition.</param>
    /// <param name="cancellationToken">Token observed before enumeration and on every element.</param>
    /// <returns>Two exactly sized arrays: the success values and the errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <remarks>
    /// Every element is consumed, successes and failures alike, exactly as <c>Partition</c> does. The
    /// returned arrays are copies, never the rented buffers: both rentals are returned on every exit
    /// path, including a throw from the enumeration or from the second rent.
    /// </remarks>
    public static async Task<(T[] Successes, Error[] Failures)> PartitionAsync<T>(
        this IAsyncEnumerable<Result<T>> results,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);
        cancellationToken.ThrowIfCancellationRequested();

        var clearOnReturn = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        var clearErrorsOnReturn = RuntimeHelpers.IsReferenceOrContainsReferences<Error>();
        var successBuffer = ArrayPool<T>.Shared.Rent(DefaultCapacity);
        Error[]? failureBuffer = null;
        var successCount = 0;
        var failureCount = 0;

        try
        {
            // Rented inside the guarded region so a failure here still returns successBuffer.
            failureBuffer = ArrayPool<Error>.Shared.Rent(DefaultCapacity);

            await foreach (var result in results.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (result.TryGetValue(out var value, out var error))
                {
                    if (successCount == successBuffer.Length)
                    {
                        successBuffer = ResultCollectionExtensions.Grow(successBuffer, successCount, clearOnReturn);
                    }

                    successBuffer[successCount++] = value!;
                }
                else
                {
                    if (failureCount == failureBuffer.Length)
                    {
                        failureBuffer = ResultCollectionExtensions.Grow(failureBuffer, failureCount, clearErrorsOnReturn);
                    }

                    failureBuffer[failureCount++] = error;
                }
            }

            var successes = new T[successCount];
            Array.Copy(successBuffer, successes, successCount);
            var failures = new Error[failureCount];
            Array.Copy(failureBuffer, failures, failureCount);
            return (successes, failures);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(successBuffer, clearOnReturn);
            if (failureBuffer is not null)
            {
                ArrayPool<Error>.Shared.Return(failureBuffer, clearErrorsOnReturn);
            }
        }
    }
}
