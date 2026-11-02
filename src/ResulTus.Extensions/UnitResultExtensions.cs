// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Runtime.CompilerServices;

namespace ResulTus.Extensions;

/// <summary>
/// Synchronous pipeline extensions for the unit <see cref="Result"/>.
/// </summary>
/// <remarks>
/// Same contract as <see cref="ResultExtensions"/>: a failure short-circuits the pipeline and
/// nothing allocates on the heap on either path.
/// </remarks>
public static class UnitResultExtensions
{
    /// <summary>
    /// Projects the successful unit result into a value, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="T">The type produced by <paramref name="map"/>.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">Function producing the success value. Never invoked on failure.</param>
    /// <returns>A success holding the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Map<T>(this Result result, Func<T> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return result.TryGetError(out var error)
            ? Result<T>.Failure(error)
            : Result<T>.Success(map());
    }

    /// <summary>
    /// Chains a value-producing result on a successful unit result, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="T">The type of the chained success value.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="bind">Function producing the next result. Never invoked on failure.</param>
    /// <returns>The result produced by <paramref name="bind"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Bind<T>(this Result result, Func<Result<T>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.TryGetError(out var error) ? Result<T>.Failure(error) : bind();
    }

    /// <summary>
    /// Chains another unit result on a successful unit result, leaving a failure untouched.
    /// </summary>
    /// <param name="result">The result to bind.</param>
    /// <param name="bind">Function producing the next unit result. Never invoked on failure.</param>
    /// <returns>The result produced by <paramref name="bind"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Bind(this Result result, Func<Result> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.TryGetError(out var error) ? Result.Failure(error) : bind();
    }

    /// <summary>
    /// Transforms the error of a failed unit result, leaving a success untouched.
    /// </summary>
    /// <param name="result">The result whose error is mapped.</param>
    /// <param name="map">Function applied to the error. Never invoked on success.</param>
    /// <returns>The original success, or a failure carrying the mapped error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result MapError(this Result result, Func<Error, Error> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return result.TryGetError(out var error) ? Result.Failure(map(error)) : result;
    }

    /// <summary>
    /// Runs a side effect on a successful unit result and returns the original result.
    /// </summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="tap">Side effect invoked on success. Never invoked on failure.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tap"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Tap(this Result result, Action tap)
    {
        ArgumentNullException.ThrowIfNull(tap);

        if (!result.TryGetError(out _))
        {
            tap();
        }

        return result;
    }

    /// <summary>
    /// Runs a side effect on a successful unit result and returns the original result.
    /// Alias of <see cref="Tap(Result, Action)"/>.
    /// </summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="onSuccess">Side effect invoked on success. Never invoked on failure.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result OnSuccess(this Result result, Action onSuccess)
        => Tap(result, onSuccess);

    /// <summary>
    /// Runs a side effect with the error of a failed unit result and returns the original result.
    /// </summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="onFailure">Side effect invoked with the error. Never invoked on success.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onFailure"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result OnFailure(this Result result, Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.TryGetError(out var error))
        {
            onFailure(error);
        }

        return result;
    }

    /// <summary>
    /// Invokes exactly one of the two branches depending on the unit result state.
    /// </summary>
    /// <param name="result">The result to switch on.</param>
    /// <param name="onSuccess">Branch invoked on success.</param>
    /// <param name="onFailure">Branch invoked with the error.</param>
    /// <exception cref="ArgumentNullException">Any of the branches is <c>null</c>.</exception>
    public static void Switch(this Result result, Action onSuccess, Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.TryGetError(out var error))
        {
            onFailure(error);
        }
        else
        {
            onSuccess();
        }
    }

    /// <summary>
    /// Keeps a successful unit result only while <paramref name="predicate"/> holds, turning it
    /// into a failure carrying <paramref name="error"/> otherwise.
    /// </summary>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Condition the success must satisfy. Never invoked on failure.</param>
    /// <param name="error">Error used when <paramref name="predicate"/> returns <c>false</c>.</param>
    /// <returns>The original success, a failure carrying <paramref name="error"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Ensure(this Result result, Func<bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (result.TryGetError(out _))
        {
            return result;
        }

        return predicate() ? result : Result.Failure(error);
    }

    /// <summary>
    /// Recovers a failed unit result with another result produced from its error.
    /// </summary>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Function producing the recovery result. Never invoked on success.</param>
    /// <returns>The original success, or the result produced by <paramref name="recover"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recover"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Recover(this Result result, Func<Error, Result> recover)
    {
        ArgumentNullException.ThrowIfNull(recover);

        return result.TryGetError(out var error) ? recover(error) : result;
    }

    /// <summary>
    /// Runs a cleanup action with the outcome — <see cref="Error.None"/> on success, the error on
    /// failure — and returns the original unit result unchanged.
    /// </summary>
    /// <param name="result">The result to finalize.</param>
    /// <param name="finallyAction">Action invoked exactly once, on either path.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="finallyAction"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Finally(this Result result, Action<Error> finallyAction)
    {
        ArgumentNullException.ThrowIfNull(finallyAction);

        finallyAction(result.TryGetError(out var error) ? error : Error.None);
        return result;
    }
}
