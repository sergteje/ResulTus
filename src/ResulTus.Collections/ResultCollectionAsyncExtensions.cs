// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ResulTus.Collections;

/// <summary>
/// Asynchronous collection-level operations over sequences of result-producing tasks.
/// </summary>
/// <remarks>
/// <c>WhenAllAsync</c> awaits every task and reports the first failure in enumeration order;
/// <c>WhenAnyAsync</c> returns the first task to complete, whatever its outcome. Both surface
/// cancellation as <see cref="OperationCanceledException"/>.
/// </remarks>
public static class ResultCollectionAsyncExtensions
{
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
}
