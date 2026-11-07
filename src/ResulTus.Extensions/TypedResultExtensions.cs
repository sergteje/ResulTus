// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Runtime.CompilerServices;

namespace ResulTus.Extensions;

/// <summary>
/// Synchronous pipeline extensions for <see cref="Result{TSuccess, TError}"/>.
/// </summary>
/// <remarks>
/// Mirrors the established <see cref="Result{T}"/> pipeline without dropping the typed error: every
/// failure path carries the receiver's <c>TError</c> unchanged, and a callback that
/// does not apply to the outcome is never invoked. The operators return structs and read the receiver
/// through <see cref="Result{TSuccess, TError}.Deconstruct"/>, so a call whose delegates do not capture
/// state allocates nothing.
/// </remarks>
public static class TypedResultExtensions
{
    /// <summary>
    /// Transforms the success value, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TOut">The type of the transformed success value.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">Function applied to the success value. Never invoked on failure.</param>
    /// <returns>A success holding the mapped value and the same error type, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TOut, TError> Map<TSuccess, TError, TOut>(
        this Result<TSuccess, TError> result,
        Func<TSuccess, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        result.Deconstruct(out var isSuccess, out var value, out var error);
        return isSuccess ? Result<TOut, TError>.Success(map(value!)) : Result<TOut, TError>.Failure(error!);
    }

    /// <summary>
    /// Chains another typed result on the success value, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TOut">The type of the success value produced by <paramref name="bind"/>.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="bind">Function producing the next result. Never invoked on failure.</param>
    /// <returns>The result produced by <paramref name="bind"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TOut, TError> Bind<TSuccess, TError, TOut>(
        this Result<TSuccess, TError> result,
        Func<TSuccess, Result<TOut, TError>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        result.Deconstruct(out var isSuccess, out var value, out var error);
        return isSuccess ? bind(value!) : Result<TOut, TError>.Failure(error!);
    }

    /// <summary>
    /// Transforms the error of a failed result, leaving a success untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <typeparam name="TNewError">The type of the mapped error value.</typeparam>
    /// <param name="result">The result whose error is mapped.</param>
    /// <param name="map">Function applied to the error. Never invoked on success.</param>
    /// <returns>The original success with the new error type, or a failure holding the mapped error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TNewError> MapError<TSuccess, TError, TNewError>(
        this Result<TSuccess, TError> result,
        Func<TError, TNewError> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        result.Deconstruct(out var isSuccess, out var value, out var error);
        return isSuccess ? Result<TSuccess, TNewError>.Success(value!) : Result<TSuccess, TNewError>.Failure(map(error!));
    }

    /// <summary>
    /// Runs a side effect on the success value and returns the receiver unchanged.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to tap.</param>
    /// <param name="tap">Action invoked with the success value. Never invoked on failure.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tap"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TError> Tap<TSuccess, TError>(
        this Result<TSuccess, TError> result,
        Action<TSuccess> tap)
    {
        ArgumentNullException.ThrowIfNull(tap);

        result.Deconstruct(out var isSuccess, out var value, out _);
        if (isSuccess)
        {
            tap(value!);
        }

        return result;
    }

    /// <summary>
    /// Observes a success without changing the result.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="onSuccess">Action invoked with the success value. Never invoked on failure.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TError> OnSuccess<TSuccess, TError>(
        this Result<TSuccess, TError> result,
        Action<TSuccess> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        result.Deconstruct(out var isSuccess, out var value, out _);
        if (isSuccess)
        {
            onSuccess(value!);
        }

        return result;
    }

    /// <summary>
    /// Observes a failure without changing the result.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="onFailure">Action invoked with the typed error. Never invoked on success.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onFailure"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TError> OnFailure<TSuccess, TError>(
        this Result<TSuccess, TError> result,
        Action<TError> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onFailure);

        result.Deconstruct(out var isSuccess, out _, out var error);
        if (!isSuccess)
        {
            onFailure(error!);
        }

        return result;
    }

    /// <summary>
    /// Invokes exactly one of the two branches depending on the result state.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to switch on.</param>
    /// <param name="onSuccess">Branch invoked with the success value.</param>
    /// <param name="onFailure">Branch invoked with the typed error.</param>
    /// <exception cref="ArgumentNullException">Any of the branches is <c>null</c>.</exception>
    public static void Switch<TSuccess, TError>(
        this Result<TSuccess, TError> result,
        Action<TSuccess> onSuccess,
        Action<TError> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        result.Deconstruct(out var isSuccess, out var value, out var error);
        if (isSuccess)
        {
            onSuccess(value!);
        }
        else
        {
            onFailure(error!);
        }
    }

    /// <summary>
    /// Keeps a success only while <paramref name="predicate"/> holds, turning it into a failure
    /// carrying <paramref name="error"/> otherwise. A failure is left untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Condition the success value must satisfy. Never invoked on failure.</param>
    /// <param name="error">Typed error used when <paramref name="predicate"/> returns <c>false</c>.</param>
    /// <returns>The original success, a failure carrying <paramref name="error"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TError> Ensure<TSuccess, TError>(
        this Result<TSuccess, TError> result,
        Func<TSuccess, bool> predicate,
        TError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        result.Deconstruct(out var isSuccess, out var value, out _);
        if (!isSuccess)
        {
            return result;
        }

        return predicate(value!) ? result : Result<TSuccess, TError>.Failure(error);
    }

    /// <summary>
    /// Recovers a failure with a value produced from its error. A success is left untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Function producing the recovery value. Never invoked on success.</param>
    /// <returns>The original success, or a success holding the recovered value.</returns>
    /// <remarks>
    /// Overloaded with <see cref="Recover{TSuccess, TError}(Result{TSuccess, TError}, Func{TError, Result{TSuccess, TError}})"/>.
    /// The two never collide: a lambda that returns a <typeparamref name="TSuccess"/> binds here because the
    /// identity conversion beats the user-defined conversion into <see cref="Result{TSuccess, TError}"/>,
    /// and a lambda that returns the result struct binds the other overload.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="recover"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TError> Recover<TSuccess, TError>(
        this Result<TSuccess, TError> result,
        Func<TError, TSuccess> recover)
    {
        ArgumentNullException.ThrowIfNull(recover);

        result.Deconstruct(out var isSuccess, out _, out var error);
        return isSuccess ? result : Result<TSuccess, TError>.Success(recover(error!));
    }

    /// <summary>
    /// Recovers a failure with another typed result produced from its error. A success is left untouched.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Function producing the recovery result. Never invoked on success.</param>
    /// <returns>The original success, or the result produced by <paramref name="recover"/>.</returns>
    /// <remarks>
    /// Overloaded with <see cref="Recover{TSuccess, TError}(Result{TSuccess, TError}, Func{TError, TSuccess})"/>.
    /// The two never collide: a lambda that returns the result struct binds here, and one that returns a
    /// <typeparamref name="TSuccess"/> binds the other overload by conversion betterness.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="recover"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TError> Recover<TSuccess, TError>(
        this Result<TSuccess, TError> result,
        Func<TError, Result<TSuccess, TError>> recover)
    {
        ArgumentNullException.ThrowIfNull(recover);

        result.Deconstruct(out var isSuccess, out _, out var error);
        return isSuccess ? result : recover(error!);
    }

    /// <summary>
    /// Runs a cleanup action with the outcome and returns the original result unchanged.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="result">The result to finalize.</param>
    /// <param name="finallyAction">Action invoked exactly once, on either path.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <remarks>
    /// The typed tier has no error sentinel such as <see cref="Error.None"/>, so on a success the second
    /// argument is <c>default</c> and on a failure the first argument is <c>default</c>; use
    /// <paramref name="result"/> itself or the receiver's state to tell the two paths apart.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="finallyAction"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TError> Finally<TSuccess, TError>(
        this Result<TSuccess, TError> result,
        Action<TSuccess, TError> finallyAction)
    {
        ArgumentNullException.ThrowIfNull(finallyAction);

        result.Deconstruct(out var isSuccess, out var value, out var error);
        finallyAction(isSuccess ? value! : default!, isSuccess ? default! : error!);
        return result;
    }
}
