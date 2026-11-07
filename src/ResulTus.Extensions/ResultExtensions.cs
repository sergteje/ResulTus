// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Runtime.CompilerServices;

namespace ResulTus.Extensions;

/// <summary>
/// Synchronous pipeline extensions for <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// Every method short-circuits on failure: a failed result travels through the pipeline
/// untouched until something recovers it. No method allocates on the heap on either path —
/// the only allocation is the delegate the caller supplies.
/// </remarks>
public static class ResultExtensions
{
    /// <summary>
    /// Projects the success value with <paramref name="map"/>, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <typeparam name="TOut">The type produced by <paramref name="map"/>.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">Function applied to the success value. Never invoked on failure.</param>
    /// <returns>A success holding the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TOut> Map<T, TOut>(this Result<T> result, Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return result.TryGetValue(out var value, out var error)
            ? Result<TOut>.Success(map(value!))
            : Result<TOut>.Failure(error);
    }

    /// <summary>
    /// Chains another result-producing operation on the success value, leaving a failure untouched.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <typeparam name="TOut">The type of the chained success value.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="bind">Function producing the next result. Never invoked on failure.</param>
    /// <returns>The result produced by <paramref name="bind"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TOut> Bind<T, TOut>(this Result<T> result, Func<T, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.TryGetValue(out var value, out var error)
            ? bind(value!)
            : Result<TOut>.Failure(error);
    }

    /// <summary>
    /// Combines two results into one holding both success values in argument order, or returns the
    /// first failure.
    /// </summary>
    /// <typeparam name="TFirst">The type of the first success value.</typeparam>
    /// <typeparam name="TSecond">The type of the second success value.</typeparam>
    /// <param name="first">The first result.</param>
    /// <param name="second">The second result.</param>
    /// <returns>A success holding both values, or the first error in argument order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<(TFirst First, TSecond Second)> Combine<TFirst, TSecond>(
        this Result<TFirst> first,
        Result<TSecond> second)
    {
        if (!first.TryGetValue(out var firstValue, out var firstError))
        {
            return Result<(TFirst First, TSecond Second)>.Failure(firstError);
        }

        return second.TryGetValue(out var secondValue, out var secondError)
            ? Result<(TFirst First, TSecond Second)>.Success((firstValue!, secondValue!))
            : Result<(TFirst First, TSecond Second)>.Failure(secondError);
    }

    /// <summary>
    /// Transforms the error of a failed result, leaving a success untouched.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result whose error is mapped.</param>
    /// <param name="map">Function applied to the error. Never invoked on success.</param>
    /// <returns>The original success, or a failure carrying the mapped error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> MapError<T>(this Result<T> result, Func<Error, Error> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return result.TryGetValue(out var value, out var error)
            ? Result<T>.Success(value!)
            : Result<T>.Failure(map(error));
    }

    /// <summary>
    /// Runs a side effect with the success value and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="tap">Side effect invoked with the success value. Never invoked on failure.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tap"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Tap<T>(this Result<T> result, Action<T> tap)
    {
        ArgumentNullException.ThrowIfNull(tap);

        if (result.TryGetValue(out var value, out _))
        {
            tap(value!);
        }

        return result;
    }

    /// <summary>
    /// Runs a side effect with the success value and returns the original result.
    /// Alias of <see cref="Tap{T}(Result{T}, Action{T})"/>.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="onSuccess">Side effect invoked with the success value. Never invoked on failure.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> OnSuccess<T>(this Result<T> result, Action<T> onSuccess)
        => Tap(result, onSuccess);

    /// <summary>
    /// Runs a side effect with the error of a failed result and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="onFailure">Side effect invoked with the error. Never invoked on success.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onFailure"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> OnFailure<T>(this Result<T> result, Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.TryGetValue(out _, out var error))
        {
            return result;
        }

        onFailure(error);
        return result;
    }

    /// <summary>
    /// Invokes exactly one of the two branches depending on the result state.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to switch on.</param>
    /// <param name="onSuccess">Branch invoked with the success value.</param>
    /// <param name="onFailure">Branch invoked with the error.</param>
    /// <exception cref="ArgumentNullException">Any of the branches is <c>null</c>.</exception>
    public static void Switch<T>(this Result<T> result, Action<T> onSuccess, Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.TryGetValue(out var value, out var error))
        {
            onSuccess(value!);
        }
        else
        {
            onFailure(error);
        }
    }

    /// <summary>
    /// Keeps a success only while <paramref name="predicate"/> holds, turning it into a failure
    /// carrying <paramref name="error"/> otherwise. A failure is left untouched.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">Condition the success value must satisfy. Never invoked on failure.</param>
    /// <param name="error">Error used when <paramref name="predicate"/> returns <c>false</c>.</param>
    /// <returns>The original success, a failure carrying <paramref name="error"/>, or the original failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (!result.TryGetValue(out var value, out _))
        {
            return result;
        }

        return predicate(value!) ? result : Result<T>.Failure(error);
    }

    /// <summary>
    /// Recovers a failure with a value produced from its error. A success is left untouched.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Function producing the recovery value. Never invoked on success.</param>
    /// <returns>The original success, or a success holding the recovered value.</returns>
    /// <remarks>
    /// Overloaded with <see cref="Recover{T}(Result{T}, Func{Error, Result{T}})"/>. They differ only in the
    /// delegate's return type, so a call whose lambda returns a value convertible to both is ambiguous;
    /// pass an explicitly typed delegate in that case.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="recover"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Recover<T>(this Result<T> result, Func<Error, T> recover)
    {
        ArgumentNullException.ThrowIfNull(recover);

        return result.TryGetValue(out var value, out var error)
            ? Result<T>.Success(value!)
            : Result<T>.Success(recover(error));
    }

    /// <summary>
    /// Recovers a failure with another result produced from its error. A success is left untouched.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to recover.</param>
    /// <param name="recover">Function producing the recovery result. Never invoked on success.</param>
    /// <returns>The original success, or the result produced by <paramref name="recover"/>.</returns>
    /// <remarks>
    /// Overloaded with <see cref="Recover{T}(Result{T}, Func{Error, T})"/>. They differ only in the
    /// delegate's return type, so a call whose lambda returns a value convertible to both is ambiguous;
    /// pass an explicitly typed delegate in that case.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="recover"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Recover<T>(this Result<T> result, Func<Error, Result<T>> recover)
    {
        ArgumentNullException.ThrowIfNull(recover);

        return result.TryGetValue(out var value, out var error)
            ? Result<T>.Success(value!)
            : recover(error);
    }

    /// <summary>
    /// Runs a cleanup action with the outcome — the success value and <see cref="Error.None"/>,
    /// or the default value and the error — and returns the original result unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to finalize.</param>
    /// <param name="finallyAction">Action invoked exactly once, on either path.</param>
    /// <returns>The original <paramref name="result"/>, unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="finallyAction"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Finally<T>(this Result<T> result, Action<T, Error> finallyAction)
    {
        ArgumentNullException.ThrowIfNull(finallyAction);

        var isSuccess = result.TryGetValue(out var value, out var error);
        finallyAction(isSuccess ? value! : default!, isSuccess ? Error.None : error);
        return result;
    }
}
