// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ResulTus;

/// <summary>
/// A result type that carries a value of type <typeparamref name="TSuccess"/> on success
/// or a value of type <typeparamref name="TError"/> on failure.
/// </summary>
/// <typeparam name="TSuccess">The type of the success value.</typeparam>
/// <typeparam name="TError">The type of the error value.</typeparam>
/// <remarks>
/// Use implicit conversion from either <typeparamref name="TSuccess"/> or <typeparamref name="TError"/>
/// to create instances. Use <see cref="Match{TResult}"/> or <see cref="Deconstruct"/> to access the value.
/// <para>
/// Keeps <c>[StructLayout(LayoutKind.Sequential, Pack = 1)]</c> for a measured reason: when the
/// error type holds no references the runtime honors the pack, so <c>Result&lt;int, EightByteError&gt;</c>
/// is 13 B instead of an aligned 16 B. The aligned alternative was measured against it twice per
/// layout on 2026-10-29 (two samples each, success and failure, 1 000 and 7 000 elements): the
/// aligned stride was 2.5% faster in one cell and 1.5%–5.0% slower in the other three, i.e. at or
/// below this machine's resolution, with the failure path consistently favoring the packed stride.
/// When the error type does hold references the runtime realigns anyway and the attribute is inert.
/// See docs/decisions/001-result-struct-size.md.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct Result<TSuccess, TError>
{
    private readonly TSuccess _value;
    private readonly TError _error;
    private readonly byte _isSuccess;

    /// <summary>
    /// Initializes a new <see cref="Result{TSuccess, TError}"/> as a success with the given value.
    /// </summary>
    /// <param name="value">The success value.</param>
    private Result(TSuccess value)
    {
        _value = value;
        _error = default!;
        _isSuccess = 1;
    }

    /// <summary>
    /// Initializes a new <see cref="Result{TSuccess, TError}"/> as a failure with the given error.
    /// </summary>
    /// <param name="error">The error value.</param>
    private Result(TError error)
    {
        _value = default!;
        _error = error;
        _isSuccess = 0;
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
    /// Creates a successful <see cref="Result{TSuccess, TError}"/> with the given value.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A <see cref="Result{TSuccess, TError}"/> with <see cref="IsSuccess"/> = <c>true</c>.</returns>
    /// <remarks>
    /// A success may carry a null value when <typeparamref name="TSuccess"/> is a nullable
    /// reference type: the parameter accepts any value at runtime, and the nullability
    /// annotation carries the intent. Consumers read the state through
    /// <see cref="Match{TResult}"/> or <see cref="Deconstruct"/> and never assume a success
    /// value is non-null.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TError> Success(TSuccess value) => new(value);

    /// <summary>
    /// Creates a failed <see cref="Result{TSuccess, TError}"/> with the given error value.
    /// </summary>
    /// <param name="error">The error value.</param>
    /// <returns>A <see cref="Result{TSuccess, TError}"/> with <see cref="IsFailure"/> = <c>true</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TSuccess, TError> Failure(TError error) => new(error);

    /// <summary>
    /// Implicitly converts a <typeparamref name="TSuccess"/> value into a successful result.
    /// </summary>
    /// <param name="value">The success value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TSuccess, TError>(TSuccess value) => new(value);

    /// <summary>
    /// Implicitly converts a <typeparamref name="TError"/> value into a failed result.
    /// </summary>
    /// <param name="error">The error value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TSuccess, TError>(TError error) => new(error);

    /// <summary>
    /// Matches the result state and invokes the appropriate function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="onSuccess">Function invoked with the success value when the result is successful.</param>
    /// <param name="onFailure">Function invoked with the error value when the result is a failure.</param>
    /// <returns>The value returned by the invoked function.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Match<TResult>(Func<TSuccess, TResult> onSuccess, Func<TError, TResult> onFailure)
        => _isSuccess == 1 ? onSuccess(_value) : onFailure(_error);

    /// <summary>
    /// Deconstructs the result into its components.
    /// </summary>
    /// <param name="isSuccess">When this method returns, <c>true</c> if the result is successful; otherwise, <c>false</c>.</param>
    /// <param name="value">When this method returns, the success value if successful; otherwise, <c>default</c>.</param>
    /// <param name="error">When this method returns, the error value if failed; otherwise, <c>default</c>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out bool isSuccess, out TSuccess? value, out TError? error)
    {
        isSuccess = _isSuccess == 1;
        value = _value;
        error = _error;
    }
}
