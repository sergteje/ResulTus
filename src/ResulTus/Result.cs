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
}
