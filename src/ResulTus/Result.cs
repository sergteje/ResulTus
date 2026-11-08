// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ResulTus;

/// <summary>
/// A void-result type that represents either success or failure.
/// </summary>
/// <remarks>
/// Use <see cref="Success"/> for successful outcomes and <see cref="Failure(Error)"/>
/// for failures. Default value (<c>default(Result)</c>) is a failure with
/// <see cref="Error.None"/> as the underlying error.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct Result
{
    private readonly byte _isSuccess;
    private readonly Error _error;

    /// <summary>
    /// Initializes a new <see cref="Result"/> with explicit success state and error.
    /// </summary>
    /// <param name="isSuccess">1 for success, 0 for failure.</param>
    /// <param name="error">The associated error (used only when <paramref name="isSuccess"/> is 0).</param>
    private Result(byte isSuccess, Error error)
    {
        _isSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the result represents a successful outcome.
    /// </summary>
    public bool IsSuccess => _isSuccess == 1;

    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    public bool IsFailure => _isSuccess == 0;

    /// <summary>
    /// Creates a successful <see cref="Result"/>.
    /// </summary>
    /// <returns>A <see cref="Result"/> with <see cref="IsSuccess"/> = <c>true</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Success() => new Result(1, default);

    /// <summary>
    /// Creates a failed <see cref="Result"/> wrapping the specified error.
    /// </summary>
    /// <param name="error">The error to wrap.</param>
    /// <returns>A <see cref="Result"/> with <see cref="IsFailure"/> = <c>true</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Failure(Error error) => new Result(0, error);

    /// <summary>
    /// Implicitly converts an <see cref="Error"/> into a failed <see cref="Result"/>.
    /// </summary>
    /// <param name="error">The error to wrap.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result(Error error) => new Result(0, error);

    /// <summary>
    /// Matches the result state and invokes the appropriate function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="onSuccess">Function invoked when the result is successful.</param>
    /// <param name="onFailure">Function invoked when the result is a failure, receiving the <see cref="Error"/>.</param>
    /// <returns>The value returned by the invoked function.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure)
        => _isSuccess == 1 ? onSuccess() : onFailure(_error);

    /// <summary>
    /// Deconstructs the result into its components.
    /// </summary>
    /// <param name="isSuccess">When this method returns, <c>true</c> if the result is successful; otherwise, <c>false</c>.</param>
    /// <param name="error">When this method returns, the <see cref="Error"/> (on failure) or <see cref="Error.None"/> (on success).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out bool isSuccess, out Error error)
    {
        isSuccess = _isSuccess == 1;
        error = _error;
    }

    /// <summary>
    /// Attempts to retrieve the error from a failed result.
    /// </summary>
    /// <param name="error">When this method returns, the <see cref="Error"/> if the result is a failure; otherwise, <see cref="Error.None"/>.</param>
    /// <returns><c>true</c> if the result is a failure; <c>false</c> if the result is successful.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetError(out Error error)
    {
        error = _error;
        return _isSuccess == 0;
    }

    /// <summary>
    /// Runs a value-producing delegate and captures a synchronous exception as a failed result.
    /// </summary>
    /// <typeparam name="T">The type of the value produced by <paramref name="action"/>.</typeparam>
    /// <param name="action">The delegate to run.</param>
    /// <returns>A success holding the produced value, or a failure holding <see cref="Error.FromException"/> of the thrown exception.</returns>
    /// <remarks>
    /// <see cref="OperationCanceledException"/> and its derived types are rethrown instead of captured, so
    /// cancellation keeps its normal control flow. No stack trace is captured.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The delegate threw a cancellation exception.</exception>
    public static Result<T> Try<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            return Result<T>.Success(action());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<T>.Failure(Error.FromException(exception));
        }
    }

    /// <summary>
    /// Runs a value-producing delegate and translates a synchronous exception into a failed result.
    /// </summary>
    /// <typeparam name="T">The type of the value produced by <paramref name="action"/>.</typeparam>
    /// <param name="action">The delegate to run.</param>
    /// <param name="map">Function translating the thrown exception into the error. Never invoked on success.</param>
    /// <returns>A success holding the produced value, or a failure holding the mapped error.</returns>
    /// <remarks>
    /// <see cref="OperationCanceledException"/> and its derived types are rethrown instead of mapped, so
    /// cancellation keeps its normal control flow.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The delegate threw a cancellation exception.</exception>
    public static Result<T> Try<T>(Func<T> action, Func<Exception, Error> map)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(map);

        try
        {
            return Result<T>.Success(action());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<T>.Failure(map(exception));
        }
    }

    /// <summary>
    /// Runs a unit delegate and captures a synchronous exception as a failed result.
    /// </summary>
    /// <param name="action">The delegate to run.</param>
    /// <returns>A successful unit result, or a failure holding <see cref="Error.FromException"/> of the thrown exception.</returns>
    /// <remarks>
    /// <see cref="OperationCanceledException"/> and its derived types are rethrown instead of captured, so
    /// cancellation keeps its normal control flow. No stack trace is captured.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The delegate threw a cancellation exception.</exception>
    public static Result Try(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
            return Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failure(Error.FromException(exception));
        }
    }

    /// <summary>
    /// Runs a unit delegate and translates a synchronous exception into a failed result.
    /// </summary>
    /// <param name="action">The delegate to run.</param>
    /// <param name="map">Function translating the thrown exception into the error. Never invoked on success.</param>
    /// <returns>A successful unit result, or a failure holding the mapped error.</returns>
    /// <remarks>
    /// <see cref="OperationCanceledException"/> and its derived types are rethrown instead of mapped, so
    /// cancellation keeps its normal control flow.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="map"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The delegate threw a cancellation exception.</exception>
    public static Result Try(Action action, Func<Exception, Error> map)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(map);

        try
        {
            action();
            return Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failure(map(exception));
        }
    }
}
