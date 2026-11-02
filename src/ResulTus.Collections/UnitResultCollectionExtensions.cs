// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Collections.Generic;

namespace ResulTus.Collections;

/// <summary>
/// Collection-level operations over <see cref="IEnumerable{T}"/> of the unit <see cref="Result"/>.
/// </summary>
/// <remarks>
/// Every method short-circuits on the first failure. This type deliberately does not repeat
/// <c>Sequence</c>: the unit result carries no value to collect, so <see cref="Combine"/> is the
/// whole operation.
/// </remarks>
public static class UnitResultCollectionExtensions
{
    /// <summary>
    /// Reduces a sequence of unit results to a single result: success when every element
    /// succeeded, or the first failure.
    /// </summary>
    /// <param name="results">The results to combine.</param>
    /// <returns>A success, or the first failure in enumeration order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result Combine(this IEnumerable<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        foreach (var result in results)
        {
            if (result.TryGetError(out var error))
            {
                return Result.Failure(error);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Folds the successful unit results into an accumulator, or fails with the first error.
    /// </summary>
    /// <typeparam name="TAcc">The type of the accumulator.</typeparam>
    /// <param name="results">The results to fold.</param>
    /// <param name="seed">The initial accumulator.</param>
    /// <param name="accumulate">Folding function. Never invoked after the first failure.</param>
    /// <returns>A success holding the final accumulator, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="accumulate"/> is <c>null</c>.</exception>
    public static Result<TAcc> Fold<TAcc>(
        this IEnumerable<Result> results,
        TAcc seed,
        Func<TAcc, TAcc> accumulate)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(accumulate);

        var accumulator = seed;
        foreach (var result in results)
        {
            if (result.TryGetError(out var error))
            {
                return Result<TAcc>.Failure(error);
            }

            accumulator = accumulate(accumulator);
        }

        return Result<TAcc>.Success(accumulator);
    }

    /// <summary>
    /// Returns the error of the first failed element, or <c>null</c> when every element succeeded.
    /// </summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns>The first error in enumeration order, or <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Error? FirstFailure(this IEnumerable<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        foreach (var result in results)
        {
            if (result.TryGetError(out var error))
            {
                return error;
            }
        }

        return null;
    }

    /// <summary>
    /// Runs an action per successful element and reduces the sequence to a single result.
    /// </summary>
    /// <param name="results">The results to visit.</param>
    /// <param name="action">Side effect invoked per success. Never invoked after the first failure.</param>
    /// <returns>A success, or the first failure in enumeration order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static Result ForEach(this IEnumerable<Result> results, Action action)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(action);

        foreach (var result in results)
        {
            if (result.TryGetError(out var error))
            {
                return Result.Failure(error);
            }

            action();
        }

        return Result.Success();
    }

    /// <summary>
    /// Determines whether every element succeeded. An empty sequence returns <c>true</c>.
    /// </summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when no element failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AllSucceeded(this IEnumerable<Result> results)
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
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when at least one element succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AnySucceeded(this IEnumerable<Result> results)
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
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when every element failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool NoneSucceeded(this IEnumerable<Result> results)
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
}
