// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ResulTus.Extensions;

/// <summary>
/// Asynchronous pipeline extensions for the unit <see cref="Result"/>.
/// </summary>
/// <remarks>
/// Same contract as <see cref="ResultAsyncExtensions"/>: available over <see cref="Task{TResult}"/>
/// and <see cref="ValueTask{TResult}"/> sources, cancellation-aware, and a failure short-circuits
/// the pipeline without invoking the delegate.
/// </remarks>
public static class UnitResultAsyncExtensions
{
    /// <summary>
    /// Projects a successful unit result into an asynchronously produced value.
    /// </summary>
    /// <typeparam name="T">The type produced by <paramref name="map"/>.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">Asynchronous producer of the success value. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the projection.</param>
    /// <returns>A success holding the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result<T>> MapAsync<T>(
        this ValueTask<Result> result,
        Func<CancellationToken, ValueTask<T>> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (current.TryGetError(out var error))
        {
            return Result<T>.Failure(error);
        }

        return Result<T>.Success(await map(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Projects a successful unit result into an asynchronously produced value.
    /// </summary>
    /// <typeparam name="T">The type produced by <paramref name="map"/>.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">Asynchronous producer of the success value. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the projection.</param>
    /// <returns>A success holding the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result<T>> MapAsync<T>(
        this Task<Result> result,
        Func<CancellationToken, ValueTask<T>> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return MapAsync(new ValueTask<Result>(result), map, cancellationToken);
    }

    /// <summary>
    /// Chains an asynchronously produced value result on a successful unit result.
    /// </summary>
    /// <typeparam name="T">The type of the chained success value.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="bind">Asynchronous binder. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the bind.</param>
    /// <returns>The result produced by <paramref name="bind"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result<T>> BindAsync<T>(
        this ValueTask<Result> result,
        Func<CancellationToken, ValueTask<Result<T>>> bind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bind);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        return current.TryGetError(out var error)
            ? Result<T>.Failure(error)
            : await bind(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Chains an asynchronously produced value result on a successful unit result.
    /// </summary>
    /// <typeparam name="T">The type of the chained success value.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="bind">Asynchronous binder. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the bind.</param>
    /// <returns>The result produced by <paramref name="bind"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="bind"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result<T>> BindAsync<T>(
        this Task<Result> result,
        Func<CancellationToken, ValueTask<Result<T>>> bind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return BindAsync(new ValueTask<Result>(result), bind, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous side effect on a successful unit result and returns the original result.
    /// </summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="tap">Asynchronous side effect. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tap"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result> TapAsync(
        this ValueTask<Result> result,
        Func<CancellationToken, ValueTask> tap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tap);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (!current.TryGetError(out _))
        {
            await tap(cancellationToken).ConfigureAwait(false);
        }

        return current;
    }

    /// <summary>
    /// Runs an asynchronous side effect on a successful unit result and returns the original result.
    /// </summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="tap">Asynchronous side effect. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="tap"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result> TapAsync(
        this Task<Result> result,
        Func<CancellationToken, ValueTask> tap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return TapAsync(new ValueTask<Result>(result), tap, cancellationToken);
    }

    /// <summary>
    /// Keeps a successful unit result only while the asynchronous predicate holds.
    /// </summary>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Asynchronous condition. Never invoked on failure.</param>
    /// <param name="error">Error used when <paramref name="predicate"/> returns <c>false</c>.</param>
    /// <param name="cancellationToken">Token observed before and during the predicate.</param>
    /// <returns>The original success, a failure carrying <paramref name="error"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result> EnsureAsync(
        this ValueTask<Result> result,
        Func<CancellationToken, ValueTask<bool>> predicate,
        Error error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (current.TryGetError(out _))
        {
            return current;
        }

        return await predicate(cancellationToken).ConfigureAwait(false)
            ? current
            : Result.Failure(error);
    }

    /// <summary>
    /// Keeps a successful unit result only while the asynchronous predicate holds.
    /// </summary>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Asynchronous condition. Never invoked on failure.</param>
    /// <param name="error">Error used when <paramref name="predicate"/> returns <c>false</c>.</param>
    /// <param name="cancellationToken">Token observed before and during the predicate.</param>
    /// <returns>The original success, a failure carrying <paramref name="error"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="predicate"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result> EnsureAsync(
        this Task<Result> result,
        Func<CancellationToken, ValueTask<bool>> predicate,
        Error error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return EnsureAsync(new ValueTask<Result>(result), predicate, error, cancellationToken);
    }

    /// <summary>
    /// Invokes exactly one asynchronous branch depending on the unit result state.
    /// </summary>
    /// <typeparam name="TResult">The type produced by the invoked branch.</typeparam>
    /// <param name="result">The result to match on.</param>
    /// <param name="onSuccess">Asynchronous success branch.</param>
    /// <param name="onFailure">Asynchronous failure branch.</param>
    /// <param name="cancellationToken">Token observed before and during the branch.</param>
    /// <returns>The value produced by the invoked branch.</returns>
    /// <exception cref="ArgumentNullException">Any of the branches is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<TResult> MatchAsync<TResult>(
        this ValueTask<Result> result,
        Func<CancellationToken, ValueTask<TResult>> onSuccess,
        Func<Error, CancellationToken, ValueTask<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        return current.TryGetError(out var error)
            ? await onFailure(error, cancellationToken).ConfigureAwait(false)
            : await onSuccess(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes exactly one asynchronous branch depending on the unit result state.
    /// </summary>
    /// <typeparam name="TResult">The type produced by the invoked branch.</typeparam>
    /// <param name="result">The result to match on.</param>
    /// <param name="onSuccess">Asynchronous success branch.</param>
    /// <param name="onFailure">Asynchronous failure branch.</param>
    /// <param name="cancellationToken">Token observed before and during the branch.</param>
    /// <returns>The value produced by the invoked branch.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <c>null</c> or any of the branches is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<TResult> MatchAsync<TResult>(
        this Task<Result> result,
        Func<CancellationToken, ValueTask<TResult>> onSuccess,
        Func<Error, CancellationToken, ValueTask<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return MatchAsync(new ValueTask<Result>(result), onSuccess, onFailure, cancellationToken);
    }

    /// <summary>
    /// Recovers a failed unit result with an asynchronously produced result.
    /// </summary>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Asynchronous recovery. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the recovery.</param>
    /// <returns>The original success, or the result produced by <paramref name="recover"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recover"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result> RecoverAsync(
        this ValueTask<Result> result,
        Func<Error, CancellationToken, ValueTask<Result>> recover,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recover);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        return current.TryGetError(out var error)
            ? await recover(error, cancellationToken).ConfigureAwait(false)
            : current;
    }

    /// <summary>
    /// Recovers a failed unit result with an asynchronously produced result.
    /// </summary>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Asynchronous recovery. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the recovery.</param>
    /// <returns>The original success, or the result produced by <paramref name="recover"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="recover"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result> RecoverAsync(
        this Task<Result> result,
        Func<Error, CancellationToken, ValueTask<Result>> recover,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return RecoverAsync(new ValueTask<Result>(result), recover, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous cleanup action with the outcome and returns the original unit result.
    /// </summary>
    /// <param name="result">The result to finalize.</param>
    /// <param name="finallyAction">Asynchronous action invoked exactly once, on either path.</param>
    /// <param name="cancellationToken">Token observed before and during the action.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="finallyAction"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result> FinallyAsync(
        this ValueTask<Result> result,
        Func<Error, CancellationToken, ValueTask> finallyAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(finallyAction);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        await finallyAction(
            current.TryGetError(out var error) ? error : Error.None,
            cancellationToken).ConfigureAwait(false);

        return current;
    }

    /// <summary>
    /// Runs an asynchronous cleanup action with the outcome and returns the original unit result.
    /// </summary>
    /// <param name="result">The result to finalize.</param>
    /// <param name="finallyAction">Asynchronous action invoked exactly once, on either path.</param>
    /// <param name="cancellationToken">Token observed before and during the action.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="finallyAction"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result> FinallyAsync(
        this Task<Result> result,
        Func<Error, CancellationToken, ValueTask> finallyAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return FinallyAsync(new ValueTask<Result>(result), finallyAction, cancellationToken);
    }

    /// <summary>
    /// Transforms the error of a failed unit result asynchronously, leaving a success untouched.
    /// </summary>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">Asynchronous error transformation. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the transformation.</param>
    /// <returns>The original success, or a failure carrying the mapped error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result> MapErrorAsync(
        this ValueTask<Result> result,
        Func<Error, CancellationToken, ValueTask<Error>> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (current.TryGetError(out var error))
        {
            return Result.Failure(await map(error, cancellationToken).ConfigureAwait(false));
        }

        return current;
    }

    /// <summary>
    /// Transforms the error of a failed unit result asynchronously, leaving a success untouched.
    /// </summary>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">Asynchronous error transformation. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the transformation.</param>
    /// <returns>The original success, or a failure carrying the mapped error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result> MapErrorAsync(
        this Task<Result> result,
        Func<Error, CancellationToken, ValueTask<Error>> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return MapErrorAsync(new ValueTask<Result>(result), map, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous side effect with the error of a failed unit result and returns the
    /// original result unchanged.
    /// </summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="onFailure">Asynchronous side effect. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onFailure"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result> OnFailureAsync(
        this ValueTask<Result> result,
        Func<Error, CancellationToken, ValueTask> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onFailure);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (current.TryGetError(out var error))
        {
            await onFailure(error, cancellationToken).ConfigureAwait(false);
        }

        return current;
    }

    /// <summary>
    /// Runs an asynchronous side effect with the error of a failed unit result and returns the
    /// original result unchanged.
    /// </summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="onFailure">Asynchronous side effect. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="onFailure"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result> OnFailureAsync(
        this Task<Result> result,
        Func<Error, CancellationToken, ValueTask> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return OnFailureAsync(new ValueTask<Result>(result), onFailure, cancellationToken);
    }

    /// <summary>
    /// Matches the result state through asynchronous side-effect functions, invoking exactly one.
    /// </summary>
    /// <param name="result">The result to switch on.</param>
    /// <param name="onSuccess">Asynchronous side effect invoked on success.</param>
    /// <param name="onFailure">Asynchronous side effect invoked with the failure error.</param>
    /// <param name="cancellationToken">Token observed before and during the switch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask SwitchAsync(
        this ValueTask<Result> result,
        Func<CancellationToken, ValueTask> onSuccess,
        Func<Error, CancellationToken, ValueTask> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (current.TryGetError(out var error))
        {
            await onFailure(error, cancellationToken).ConfigureAwait(false);
            return;
        }

        await onSuccess(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Matches the result state through asynchronous side-effect functions, invoking exactly one.
    /// </summary>
    /// <param name="result">The result to switch on.</param>
    /// <param name="onSuccess">Asynchronous side effect invoked on success.</param>
    /// <param name="onFailure">Asynchronous side effect invoked with the failure error.</param>
    /// <param name="cancellationToken">Token observed before and during the switch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/>, <paramref name="onSuccess"/> or <paramref name="onFailure"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask SwitchAsync(
        this Task<Result> result,
        Func<CancellationToken, ValueTask> onSuccess,
        Func<Error, CancellationToken, ValueTask> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return SwitchAsync(new ValueTask<Result>(result), onSuccess, onFailure, cancellationToken);
    }

    /// <summary>
    /// Keeps a success only while the asynchronous predicate holds, turning it into a failure
    /// carrying the asynchronously produced error otherwise.
    /// </summary>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Asynchronous condition. Never invoked on failure.</param>
    /// <param name="errorFactory">Asynchronous error producer. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during both delegates.</param>
    /// <returns>The original success, a failure carrying the produced error, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> or <paramref name="errorFactory"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result> EnsureAsync(
        this ValueTask<Result> result,
        Func<CancellationToken, ValueTask<bool>> predicate,
        Func<CancellationToken, ValueTask<Error>> errorFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (current.TryGetError(out _))
        {
            return current;
        }

        if (!await predicate(cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(await errorFactory(cancellationToken).ConfigureAwait(false));
        }

        return current;
    }

    /// <summary>
    /// Keeps a success only while the asynchronous predicate holds, turning it into a failure
    /// carrying the asynchronously produced error otherwise.
    /// </summary>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Asynchronous condition. Never invoked on failure.</param>
    /// <param name="errorFactory">Asynchronous error producer. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during both delegates.</param>
    /// <returns>The original success, a failure carrying the produced error, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/>, <paramref name="predicate"/> or <paramref name="errorFactory"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result> EnsureAsync(
        this Task<Result> result,
        Func<CancellationToken, ValueTask<bool>> predicate,
        Func<CancellationToken, ValueTask<Error>> errorFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return EnsureAsync(new ValueTask<Result>(result), predicate, errorFactory, cancellationToken);
    }
}
