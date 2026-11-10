// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ResulTus.Extensions;

/// <summary>
/// Asynchronous pipeline extensions for <see cref="Result{TSuccess, TError}"/>.
/// </summary>
/// <remarks>
/// Every method is available over a <see cref="Task{TResult}"/> and over a
/// <see cref="ValueTask{TResult}"/> source, and takes a cancellation-aware delegate plus an
/// optional <see cref="CancellationToken"/>, mirroring the shapes of the <c>Result&lt;T&gt;</c>
/// async surface. A cancelled token surfaces as <see cref="OperationCanceledException"/> from
/// the awaited work and is never converted into an error value. A failure short-circuits the
/// pipeline exactly like the synchronous API.
/// </remarks>
public static class TypedResultAsyncExtensions
{
    /// <summary>
    /// Projects the success value asynchronously, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TOut">The type produced by <paramref name="map"/>.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">Asynchronous projection of the success value. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the projection.</param>
    /// <returns>A success holding the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Result<TOut, TError>> MapAsync<TSuccess, TError, TOut>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask<TOut>> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out var value, out var error);
        if (!isSuccess)
        {
            return Result<TOut, TError>.Failure(error!);
        }

        return Result<TOut, TError>.Success(await map(value!, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Projects the success value asynchronously, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TOut">The type produced by <paramref name="map"/>.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">Asynchronous projection of the success value. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the projection.</param>
    /// <returns>A success holding the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TOut, TError>> MapAsync<TSuccess, TError, TOut>(
        this Task<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask<TOut>> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return MapAsync(new ValueTask<Result<TSuccess, TError>>(result), map, cancellationToken);
    }

    /// <summary>
    /// Chains another asynchronous typed result on the success value, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TOut">The success type of the chained result.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="bind">Asynchronous binder. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the bind.</param>
    /// <returns>The result produced by <paramref name="bind"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Result<TOut, TError>> BindAsync<TSuccess, TError, TOut>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask<Result<TOut, TError>>> bind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bind);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out var value, out var error);
        if (!isSuccess)
        {
            return Result<TOut, TError>.Failure(error!);
        }

        return await bind(value!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Chains another asynchronous typed result on the success value, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TOut">The success type of the chained result.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="bind">Asynchronous binder. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the bind.</param>
    /// <returns>The result produced by <paramref name="bind"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="bind"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TOut, TError>> BindAsync<TSuccess, TError, TOut>(
        this Task<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask<Result<TOut, TError>>> bind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return BindAsync(new ValueTask<Result<TSuccess, TError>>(result), bind, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous side effect with the success value and returns the original result.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="tap">Asynchronous side effect. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tap"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Result<TSuccess, TError>> TapAsync<TSuccess, TError>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask> tap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tap);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out var value, out _);
        if (isSuccess)
        {
            await tap(value!, cancellationToken).ConfigureAwait(false);
        }

        return current;
    }

    /// <summary>
    /// Runs an asynchronous side effect with the success value and returns the original result.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="tap">Asynchronous side effect. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="tap"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TSuccess, TError>> TapAsync<TSuccess, TError>(
        this Task<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask> tap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return TapAsync(new ValueTask<Result<TSuccess, TError>>(result), tap, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous side effect with the success value and returns the original result.
    /// Alias of <c>TapAsync</c> for the <c>ValueTask</c> source, mirroring the synchronous naming.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="onSuccess">Asynchronous side effect. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TSuccess, TError>> OnSuccessAsync<TSuccess, TError>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask> onSuccess,
        CancellationToken cancellationToken = default)
        => TapAsync(result, onSuccess, cancellationToken);

    /// <summary>
    /// Runs an asynchronous side effect with the success value and returns the original result.
    /// Alias of the <c>Task</c>-source <c>TapAsync</c>, mirroring the synchronous naming.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="onSuccess">Asynchronous side effect. Never invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="onSuccess"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TSuccess, TError>> OnSuccessAsync<TSuccess, TError>(
        this Task<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask> onSuccess,
        CancellationToken cancellationToken = default)
        => TapAsync(result, onSuccess, cancellationToken);

    /// <summary>
    /// Matches the result state through asynchronous functions and produces their outcome.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TResult">The type produced by the matched function.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onSuccess">Asynchronous projection invoked on success.</param>
    /// <param name="onFailure">Asynchronous projection invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the matching.</param>
    /// <returns>The value produced by the invoked function.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<TResult> MatchAsync<TSuccess, TError, TResult>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask<TResult>> onSuccess,
        Func<TError, CancellationToken, ValueTask<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out var value, out var error);
        return isSuccess
            ? await onSuccess(value!, cancellationToken).ConfigureAwait(false)
            : await onFailure(error!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Matches the result state through asynchronous functions and produces their outcome.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TResult">The type produced by the matched function.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onSuccess">Asynchronous projection invoked on success.</param>
    /// <param name="onFailure">Asynchronous projection invoked on failure.</param>
    /// <param name="cancellationToken">Token observed before and during the matching.</param>
    /// <returns>The value produced by the invoked function.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/>, <paramref name="onSuccess"/> or <paramref name="onFailure"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<TResult> MatchAsync<TSuccess, TError, TResult>(
        this Task<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask<TResult>> onSuccess,
        Func<TError, CancellationToken, ValueTask<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return MatchAsync(new ValueTask<Result<TSuccess, TError>>(result), onSuccess, onFailure, cancellationToken);
    }

    /// <summary>
    /// Recovers a failure with an asynchronously produced success value, keeping the error type.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Asynchronous recovery. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the recovery.</param>
    /// <returns>The original success, or a success holding the recovery value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recover"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Result<TSuccess, TError>> RecoverAsync<TSuccess, TError>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TError, CancellationToken, ValueTask<TSuccess>> recover,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recover);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out var value, out var error);
        if (isSuccess)
        {
            return Result<TSuccess, TError>.Success(value!);
        }

        return Result<TSuccess, TError>.Success(await recover(error!, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Recovers a failure with an asynchronously produced success value, keeping the error type.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Asynchronous recovery. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the recovery.</param>
    /// <returns>The original success, or a success holding the recovery value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="recover"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TSuccess, TError>> RecoverAsync<TSuccess, TError>(
        this Task<Result<TSuccess, TError>> result,
        Func<TError, CancellationToken, ValueTask<TSuccess>> recover,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return RecoverAsync(new ValueTask<Result<TSuccess, TError>>(result), recover, cancellationToken);
    }

    /// <summary>
    /// Recovers a failure with another asynchronously produced typed result. A success is left
    /// untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Asynchronous recovery. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the recovery.</param>
    /// <returns>The original success, or the result produced by <paramref name="recover"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recover"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Result<TSuccess, TError>> RecoverAsync<TSuccess, TError>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TError, CancellationToken, ValueTask<Result<TSuccess, TError>>> recover,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recover);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out _, out var error);
        if (isSuccess)
        {
            return current;
        }

        return await recover(error!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Recovers a failure with another asynchronously produced typed result. A success is left
    /// untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Asynchronous recovery. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the recovery.</param>
    /// <returns>The original success, or the result produced by <paramref name="recover"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="recover"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TSuccess, TError>> RecoverAsync<TSuccess, TError>(
        this Task<Result<TSuccess, TError>> result,
        Func<TError, CancellationToken, ValueTask<Result<TSuccess, TError>>> recover,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return RecoverAsync(new ValueTask<Result<TSuccess, TError>>(result), recover, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous cleanup action with the outcome and returns the original result
    /// unchanged.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to finalize.</param>
    /// <param name="finallyAction">Asynchronous action invoked exactly once, on either path.</param>
    /// <param name="cancellationToken">Token observed before and during the cleanup.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <remarks>
    /// The typed tier has no error sentinel such as <see cref="Error.None"/>, so on a success the
    /// second argument is <c>default</c> and on a failure the first argument is <c>default</c>;
    /// use <paramref name="result"/> itself or the receiver's state to tell the two paths apart.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="finallyAction"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Result<TSuccess, TError>> FinallyAsync<TSuccess, TError>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TSuccess, TError, CancellationToken, ValueTask> finallyAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(finallyAction);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out var value, out var error);
        await finallyAction(
            isSuccess ? value! : default!,
            isSuccess ? default! : error!,
            cancellationToken).ConfigureAwait(false);

        return current;
    }

    /// <summary>
    /// Runs an asynchronous cleanup action with the outcome and returns the original result
    /// unchanged.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to finalize.</param>
    /// <param name="finallyAction">Asynchronous action invoked exactly once, on either path.</param>
    /// <param name="cancellationToken">Token observed before and during the cleanup.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="finallyAction"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TSuccess, TError>> FinallyAsync<TSuccess, TError>(
        this Task<Result<TSuccess, TError>> result,
        Func<TSuccess, TError, CancellationToken, ValueTask> finallyAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return FinallyAsync(new ValueTask<Result<TSuccess, TError>>(result), finallyAction, cancellationToken);
    }

    /// <summary>
    /// Transforms the error of a failed typed result asynchronously, leaving a success untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TNewError">The type produced by <paramref name="map"/>.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">Asynchronous error transformation. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the transformation.</param>
    /// <returns>The original success, or a failure carrying the mapped error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Result<TSuccess, TNewError>> MapErrorAsync<TSuccess, TError, TNewError>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TError, CancellationToken, ValueTask<TNewError>> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out var value, out var error);
        return isSuccess
            ? Result<TSuccess, TNewError>.Success(value!)
            : Result<TSuccess, TNewError>.Failure(await map(error!, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Transforms the error of a failed typed result asynchronously, leaving a success untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TNewError">The type produced by <paramref name="map"/>.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">Asynchronous error transformation. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the transformation.</param>
    /// <returns>The original success, or a failure carrying the mapped error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TSuccess, TNewError>> MapErrorAsync<TSuccess, TError, TNewError>(
        this Task<Result<TSuccess, TError>> result,
        Func<TError, CancellationToken, ValueTask<TNewError>> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return MapErrorAsync(new ValueTask<Result<TSuccess, TError>>(result), map, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous side effect with the error of a failed typed result and returns the
    /// original result unchanged.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="onFailure">Asynchronous side effect. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onFailure"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Result<TSuccess, TError>> OnFailureAsync<TSuccess, TError>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TError, CancellationToken, ValueTask> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onFailure);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out _, out var error);
        if (!isSuccess)
        {
            await onFailure(error!, cancellationToken).ConfigureAwait(false);
        }

        return current;
    }

    /// <summary>
    /// Runs an asynchronous side effect with the error of a failed typed result and returns the
    /// original result unchanged.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="onFailure">Asynchronous side effect. Never invoked on success.</param>
    /// <param name="cancellationToken">Token observed before and during the side effect.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="onFailure"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TSuccess, TError>> OnFailureAsync<TSuccess, TError>(
        this Task<Result<TSuccess, TError>> result,
        Func<TError, CancellationToken, ValueTask> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return OnFailureAsync(new ValueTask<Result<TSuccess, TError>>(result), onFailure, cancellationToken);
    }

    /// <summary>
    /// Matches the result state through asynchronous side-effect functions, invoking exactly one.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to switch on.</param>
    /// <param name="onSuccess">Asynchronous side effect invoked with the success value.</param>
    /// <param name="onFailure">Asynchronous side effect invoked with the error value.</param>
    /// <param name="cancellationToken">Token observed before and during the switch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask SwitchAsync<TSuccess, TError>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask> onSuccess,
        Func<TError, CancellationToken, ValueTask> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out var value, out var error);
        if (isSuccess)
        {
            await onSuccess(value!, cancellationToken).ConfigureAwait(false);
            return;
        }

        await onFailure(error!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Matches the result state through asynchronous side-effect functions, invoking exactly one.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to switch on.</param>
    /// <param name="onSuccess">Asynchronous side effect invoked with the success value.</param>
    /// <param name="onFailure">Asynchronous side effect invoked with the error value.</param>
    /// <param name="cancellationToken">Token observed before and during the switch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/>, <paramref name="onSuccess"/> or <paramref name="onFailure"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask SwitchAsync<TSuccess, TError>(
        this Task<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask> onSuccess,
        Func<TError, CancellationToken, ValueTask> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return SwitchAsync(new ValueTask<Result<TSuccess, TError>>(result), onSuccess, onFailure, cancellationToken);
    }

    /// <summary>
    /// Keeps a success only while the asynchronous predicate holds, turning it into a failure
    /// carrying <paramref name="error"/> otherwise.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Asynchronous condition. Never invoked on failure.</param>
    /// <param name="error">Error used when <paramref name="predicate"/> returns <c>false</c>.</param>
    /// <param name="cancellationToken">Token observed before and during the predicate.</param>
    /// <returns>The original success, a failure carrying <paramref name="error"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <remarks>
    /// The typed tier keeps a single <c>Ensure</c> shape (an already-held <typeparamref name="TError"/>)
    /// rather than factory shapes: a typed error is an arbitrary value the caller already has, so a
    /// synthesizing factory is not requested until a real use is named.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Result<TSuccess, TError>> EnsureAsync<TSuccess, TError>(
        this ValueTask<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask<bool>> predicate,
        TError error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var current = await result.ConfigureAwait(false);
        current.Deconstruct(out var isSuccess, out var value, out _);
        if (!isSuccess)
        {
            return current;
        }

        return await predicate(value!, cancellationToken).ConfigureAwait(false)
            ? current
            : Result<TSuccess, TError>.Failure(error);
    }

    /// <summary>
    /// Keeps a success only while the asynchronous predicate holds, turning it into a failure
    /// carrying <paramref name="error"/> otherwise.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Asynchronous condition. Never invoked on failure.</param>
    /// <param name="error">Error used when <paramref name="predicate"/> returns <c>false</c>.</param>
    /// <param name="cancellationToken">Token observed before and during the predicate.</param>
    /// <returns>The original success, a failure carrying <paramref name="error"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="predicate"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <remarks>
    /// The typed tier keeps a single <c>Ensure</c> shape; see the synchronous twin's rationale.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TSuccess, TError>> EnsureAsync<TSuccess, TError>(
        this Task<Result<TSuccess, TError>> result,
        Func<TSuccess, CancellationToken, ValueTask<bool>> predicate,
        TError error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return EnsureAsync(new ValueTask<Result<TSuccess, TError>>(result), predicate, error, cancellationToken);
    }
}
