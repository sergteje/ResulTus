// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ResulTus.Extensions;

/// <summary>
/// Asynchronous pipeline extensions for <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// Every method is available over a <see cref="Task{TResult}"/> and over a
/// <see cref="ValueTask{TResult}"/> source, and takes a cancellation-aware delegate plus an optional
/// <see cref="CancellationToken"/>. A cancelled token surfaces as
/// <see cref="OperationCanceledException"/> from the awaited work — it is never converted into an
/// <see cref="Error"/>. A failure short-circuits the pipeline exactly like the synchronous API.
/// </remarks>
public static class ResultAsyncExtensions
{
    /// <summary>
    /// Projects the success value asynchronously, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <typeparam name="TOut">The type produced by <paramref name="map"/>.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">Asynchronous projection of the success value. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the projection.</param>
    /// <returns>A success holding the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result<TOut>> MapAsync<T, TOut>(
        this ValueTask<Result<T>> result,
        Func<T, CancellationToken, ValueTask<TOut>> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (!current.TryGetValue(out var value, out var error))
        {
            return Result<TOut>.Failure(error);
        }

        var mapped = await map(value!, cancellationToken).ConfigureAwait(false);
        return Result<TOut>.Success(mapped);
    }

    /// <summary>
    /// Projects the success value asynchronously, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <typeparam name="TOut">The type produced by <paramref name="map"/>.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">Asynchronous projection of the success value. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the projection.</param>
    /// <returns>A success holding the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result<TOut>> MapAsync<T, TOut>(
        this Task<Result<T>> result,
        Func<T, CancellationToken, ValueTask<TOut>> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return MapAsync(new ValueTask<Result<T>>(result), map, cancellationToken);
    }

    /// <summary>
    /// Chains another asynchronous result-producing operation on the success value.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <typeparam name="TOut">The type of the chained success value.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="bind">Asynchronous binder. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the bind.</param>
    /// <returns>The result produced by <paramref name="bind"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result<TOut>> BindAsync<T, TOut>(
        this ValueTask<Result<T>> result,
        Func<T, CancellationToken, ValueTask<Result<TOut>>> bind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bind);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (!current.TryGetValue(out var value, out var error))
        {
            return Result<TOut>.Failure(error);
        }

        return await bind(value!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Chains another asynchronous result-producing operation on the success value.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <typeparam name="TOut">The type of the chained success value.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="bind">Asynchronous binder. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the bind.</param>
    /// <returns>The result produced by <paramref name="bind"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="bind"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result<TOut>> BindAsync<T, TOut>(
        this Task<Result<T>> result,
        Func<T, CancellationToken, ValueTask<Result<TOut>>> bind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return BindAsync(new ValueTask<Result<T>>(result), bind, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous side effect with the success value and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="tap">Asynchronous side effect. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tap"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result<T>> TapAsync<T>(
        this ValueTask<Result<T>> result,
        Func<T, CancellationToken, ValueTask> tap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tap);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (current.TryGetValue(out var value, out _))
        {
            await tap(value!, cancellationToken).ConfigureAwait(false);
        }

        return current;
    }

    /// <summary>
    /// Runs an asynchronous side effect with the success value and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="tap">Asynchronous side effect. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="tap"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result<T>> TapAsync<T>(
        this Task<Result<T>> result,
        Func<T, CancellationToken, ValueTask> tap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return TapAsync(new ValueTask<Result<T>>(result), tap, cancellationToken);
    }

    /// <summary>
    /// Keeps a success only while the asynchronous predicate holds, turning it into a failure
    /// carrying <paramref name="error"/> otherwise.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Asynchronous condition. Never invoked on failure.</param>
    /// <param name="error">Error used when <paramref name="predicate"/> returns <c>false</c>.</param>
    /// <param name="cancellationToken">Token observed before and during the predicate.</param>
    /// <returns>The original success, a failure carrying <paramref name="error"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result<T>> EnsureAsync<T>(
        this ValueTask<Result<T>> result,
        Func<T, CancellationToken, ValueTask<bool>> predicate,
        Error error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (!current.TryGetValue(out var value, out _))
        {
            return current;
        }

        return await predicate(value!, cancellationToken).ConfigureAwait(false)
            ? current
            : Result<T>.Failure(error);
    }

    /// <summary>
    /// Keeps a success only while the asynchronous predicate holds, turning it into a failure
    /// carrying <paramref name="error"/> otherwise.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Asynchronous condition. Never invoked on failure.</param>
    /// <param name="error">Error used when <paramref name="predicate"/> returns <c>false</c>.</param>
    /// <param name="cancellationToken">Token observed before and during the predicate.</param>
    /// <returns>The original success, a failure carrying <paramref name="error"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="predicate"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> result,
        Func<T, CancellationToken, ValueTask<bool>> predicate,
        Error error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return EnsureAsync(new ValueTask<Result<T>>(result), predicate, error, cancellationToken);
    }

    /// <summary>
    /// Invokes exactly one asynchronous branch depending on the result state.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <typeparam name="TResult">The type produced by the invoked branch.</typeparam>
    /// <param name="result">The result to match on.</param>
    /// <param name="onSuccess">Asynchronous success branch.</param>
    /// <param name="onFailure">Asynchronous failure branch.</param>
    /// <param name="cancellationToken">Token observed before and during the branch.</param>
    /// <returns>The value produced by the invoked branch.</returns>
    /// <exception cref="ArgumentNullException">Any of the branches is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<TResult> MatchAsync<T, TResult>(
        this ValueTask<Result<T>> result,
        Func<T, CancellationToken, ValueTask<TResult>> onSuccess,
        Func<Error, CancellationToken, ValueTask<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        return current.TryGetValue(out var value, out var error)
            ? await onSuccess(value!, cancellationToken).ConfigureAwait(false)
            : await onFailure(error, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes exactly one asynchronous branch depending on the result state.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <typeparam name="TResult">The type produced by the invoked branch.</typeparam>
    /// <param name="result">The result to match on.</param>
    /// <param name="onSuccess">Asynchronous success branch.</param>
    /// <param name="onFailure">Asynchronous failure branch.</param>
    /// <param name="cancellationToken">Token observed before and during the branch.</param>
    /// <returns>The value produced by the invoked branch.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <c>null</c> or any of the branches is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> result,
        Func<T, CancellationToken, ValueTask<TResult>> onSuccess,
        Func<Error, CancellationToken, ValueTask<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return MatchAsync(new ValueTask<Result<T>>(result), onSuccess, onFailure, cancellationToken);
    }

    /// <summary>
    /// Recovers a failure with an asynchronously produced value. A success is left untouched.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Asynchronous recovery. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the recovery.</param>
    /// <returns>The original success, or a success holding the recovered value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recover"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result<T>> RecoverAsync<T>(
        this ValueTask<Result<T>> result,
        Func<Error, CancellationToken, ValueTask<T>> recover,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recover);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        if (current.TryGetValue(out var value, out var error))
        {
            return Result<T>.Success(value!);
        }

        return Result<T>.Success(await recover(error, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Recovers a failure with an asynchronously produced value. A success is left untouched.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Asynchronous recovery. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the recovery.</param>
    /// <returns>The original success, or a success holding the recovered value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="recover"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result<T>> RecoverAsync<T>(
        this Task<Result<T>> result,
        Func<Error, CancellationToken, ValueTask<T>> recover,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return RecoverAsync(new ValueTask<Result<T>>(result), recover, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous cleanup action with the outcome and returns the original result unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to finalize.</param>
    /// <param name="finallyAction">Asynchronous action invoked exactly once, on either path.</param>
    /// <param name="cancellationToken">Token observed before and during the action.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="finallyAction"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Result<T>> FinallyAsync<T>(
        this ValueTask<Result<T>> result,
        Func<T, Error, CancellationToken, ValueTask> finallyAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(finallyAction);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        var isSuccess = current.TryGetValue(out var value, out var error);
        await finallyAction(
            isSuccess ? value! : default!,
            isSuccess ? Error.None : error,
            cancellationToken).ConfigureAwait(false);

        return current;
    }

    /// <summary>
    /// Runs an asynchronous cleanup action with the outcome and returns the original result unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to finalize.</param>
    /// <param name="finallyAction">Asynchronous action invoked exactly once, on either path.</param>
    /// <param name="cancellationToken">Token observed before and during the action.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="finallyAction"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<Result<T>> FinallyAsync<T>(
        this Task<Result<T>> result,
        Func<T, Error, CancellationToken, ValueTask> finallyAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return FinallyAsync(new ValueTask<Result<T>>(result), finallyAction, cancellationToken);
    }
}
