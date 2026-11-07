// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Collections.Generic;

namespace ResulTus.Collections;

/// <summary>
/// Collection-level operations over sequences, arrays and read-only lists of the unit
/// <see cref="Result"/>.
/// </summary>
/// <remarks>
/// Every method short-circuits on the first failure. This type deliberately does not repeat
/// <c>Sequence</c>: the unit result carries no value to collect, so
/// <see cref="Combine(IEnumerable{Result})"/> is the whole operation.
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

    /// <summary>
    /// Reduces an array of unit results to a single result: success when every element succeeded, or
    /// the first failure.
    /// </summary>
    /// <param name="results">The results to combine.</param>
    /// <returns>A success, or the first failure in array order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result Combine(this Result[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Length; index++)
        {
            if (results[index].TryGetError(out var error))
            {
                return Result.Failure(error);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Reduces a read-only list of unit results to a single result: success when every element
    /// succeeded, or the first failure.
    /// </summary>
    /// <param name="results">The results to combine.</param>
    /// <returns>A success, or the first failure in list order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Result Combine(this IReadOnlyList<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].TryGetError(out var error))
            {
                return Result.Failure(error);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Folds the successful unit results of an array into an accumulator, or fails with the first
    /// error.
    /// </summary>
    /// <typeparam name="TAcc">The type of the accumulator.</typeparam>
    /// <param name="results">The results to fold.</param>
    /// <param name="seed">The initial accumulator.</param>
    /// <param name="accumulate">Folding function. Never invoked after the first failure.</param>
    /// <returns>A success holding the final accumulator, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="accumulate"/> is <c>null</c>.</exception>
    public static Result<TAcc> Fold<TAcc>(
        this Result[] results,
        TAcc seed,
        Func<TAcc, TAcc> accumulate)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(accumulate);

        var accumulator = seed;
        for (var index = 0; index < results.Length; index++)
        {
            if (results[index].TryGetError(out var error))
            {
                return Result<TAcc>.Failure(error);
            }

            accumulator = accumulate(accumulator);
        }

        return Result<TAcc>.Success(accumulator);
    }

    /// <summary>
    /// Folds the successful unit results of a read-only list into an accumulator, or fails with the
    /// first error.
    /// </summary>
    /// <typeparam name="TAcc">The type of the accumulator.</typeparam>
    /// <param name="results">The results to fold.</param>
    /// <param name="seed">The initial accumulator.</param>
    /// <param name="accumulate">Folding function. Never invoked after the first failure.</param>
    /// <returns>A success holding the final accumulator, or the first failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="accumulate"/> is <c>null</c>.</exception>
    public static Result<TAcc> Fold<TAcc>(
        this IReadOnlyList<Result> results,
        TAcc seed,
        Func<TAcc, TAcc> accumulate)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(accumulate);

        var accumulator = seed;
        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].TryGetError(out var error))
            {
                return Result<TAcc>.Failure(error);
            }

            accumulator = accumulate(accumulator);
        }

        return Result<TAcc>.Success(accumulator);
    }

    /// <summary>
    /// Returns the error of the first failed element in an array, or <c>null</c> when every element
    /// succeeded.
    /// </summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns>The first error in array order, or <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Error? FirstFailure(this Result[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Length; index++)
        {
            if (results[index].TryGetError(out var error))
            {
                return error;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the error of the first failed element in a read-only list, or <c>null</c> when every
    /// element succeeded.
    /// </summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns>The first error in list order, or <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static Error? FirstFailure(this IReadOnlyList<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].TryGetError(out var error))
            {
                return error;
            }
        }

        return null;
    }

    /// <summary>
    /// Runs an action per successful element of an array and reduces it to a single result.
    /// </summary>
    /// <param name="results">The results to visit.</param>
    /// <param name="action">Side effect invoked per success. Never invoked after the first failure.</param>
    /// <returns>A success, or the first failure in array order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static Result ForEach(this Result[] results, Action action)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(action);

        for (var index = 0; index < results.Length; index++)
        {
            if (results[index].TryGetError(out var error))
            {
                return Result.Failure(error);
            }

            action();
        }

        return Result.Success();
    }

    /// <summary>
    /// Runs an action per successful element of a read-only list and reduces it to a single result.
    /// </summary>
    /// <param name="results">The results to visit.</param>
    /// <param name="action">Side effect invoked per success. Never invoked after the first failure.</param>
    /// <returns>A success, or the first failure in list order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static Result ForEach(this IReadOnlyList<Result> results, Action action)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(action);

        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].TryGetError(out var error))
            {
                return Result.Failure(error);
            }

            action();
        }

        return Result.Success();
    }

    /// <summary>
    /// Determines whether every element of an array succeeded. An empty array returns <c>true</c>.
    /// </summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when no element failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AllSucceeded(this Result[] results)
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
    /// Determines whether every element of a read-only list succeeded. An empty list returns
    /// <c>true</c>.
    /// </summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when no element failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AllSucceeded(this IReadOnlyList<Result> results)
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
    /// Determines whether at least one element of an array succeeded. An empty array returns
    /// <c>false</c>.
    /// </summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when at least one element succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AnySucceeded(this Result[] results)
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
    /// Determines whether at least one element of a read-only list succeeded. An empty list returns
    /// <c>false</c>.
    /// </summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when at least one element succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool AnySucceeded(this IReadOnlyList<Result> results)
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
    /// Determines whether no element of an array succeeded. An empty array returns <c>true</c>.
    /// </summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when every element failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool NoneSucceeded(this Result[] results)
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
    /// Determines whether no element of a read-only list succeeded. An empty list returns
    /// <c>true</c>.
    /// </summary>
    /// <param name="results">The results to inspect.</param>
    /// <returns><c>true</c> when every element failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <c>null</c>.</exception>
    public static bool NoneSucceeded(this IReadOnlyList<Result> results)
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
}
