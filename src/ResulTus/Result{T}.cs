// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Runtime.CompilerServices;

namespace ResulTus;

/// <summary>
/// A result type that carries a value of type <typeparamref name="T"/> on success
/// or an <see cref="Error"/> on failure.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
/// <remarks>
/// Use implicit conversion from <typeparamref name="T"/> for success or from
/// <see cref="Error"/> for failure. Do NOT access a direct <c>Value</c> property —
/// use <see cref="TryGetValue(out T?, out Error)"/>, <see cref="GetValueOrDefault(T)"/>,
/// <see cref="GetValueOr(System.Func{Error, T})"/>, or <see cref="Match"/> instead.
/// <para>
/// Deliberately **not** marked <c>[StructLayout(LayoutKind.Sequential, Pack = 1)]</c>: the
/// inline <see cref="Error"/> holds object references, the GC forces pointer alignment, and the
/// attribute is a measured no-op here — <c>Result&lt;int&gt;</c> measures 32 B with and without
/// it. It is kept on <c>Result&lt;TSuccess, TError&gt;</c> only, where it is honored and saves
/// three bytes when the error type holds no references. Both sizes are pinned by
/// <c>LayoutTests</c>; see docs/decisions/001-result-struct-size.md.
/// </para>
/// </remarks>
public readonly record struct Result<T>
{
    private readonly T _value;
    private readonly Error _error;
    private readonly byte _isSuccess;

    /// <summary>
    /// Initializes a new <see cref="Result{T}"/> as a success with the given value.
    /// </summary>
    /// <param name="value">The success value.</param>
    private Result(T value)
    {
        _value = value;
        _error = default;
        _isSuccess = 1;
    }

    /// <summary>
    /// Initializes a new <see cref="Result{T}"/> as a failure wrapping the given error.
    /// </summary>
    /// <param name="error">The error to wrap.</param>
    private Result(Error error)
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
    /// Creates a successful <see cref="Result{T}"/> with the given value.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A <see cref="Result{T}"/> with <see cref="IsSuccess"/> = <c>true</c>.</returns>
    /// <remarks>
    /// A success may carry a null value: <typeparamref name="T"/> is unconstrained, so
    /// <c>Success(null)</c> is representable for every <typeparamref name="T"/>, and a
    /// nullable <typeparamref name="T"/> makes null a sanctioned success state (pinned by
    /// <c>DefaultAndNullStateAuditTests</c>). The nullability annotation carries the
    /// intent; consumers read the state through <see cref="TryGetValue(out T?, out Error)"/>
    /// or <see cref="Match{TResult}"/> and never assume a success value is non-null.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Success(T value) => new Result<T>(value);

    /// <summary>
    /// Creates a failed <see cref="Result{T}"/> wrapping the specified error.
    /// </summary>
    /// <param name="error">The error to wrap.</param>
    /// <returns>A <see cref="Result{T}"/> with <see cref="IsFailure"/> = <c>true</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Failure(Error error) => new Result<T>(error);

    /// <summary>
    /// Implicitly converts a value of <typeparamref name="T"/> into a successful <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="value">The success value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<T>(T value) => new Result<T>(value);

    /// <summary>
    /// Implicitly converts an <see cref="Error"/> into a failed <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="error">The error to wrap.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<T>(Error error) => new Result<T>(error);

    /// <summary>
    /// Matches the result state and invokes the appropriate function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="onSuccess">Function invoked with the success value when the result is successful.</param>
    /// <param name="onFailure">Function invoked with the <see cref="Error"/> when the result is a failure.</param>
    /// <returns>The value returned by the invoked function.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure)
        => _isSuccess == 1 ? onSuccess(_value) : onFailure(_error);

    /// <summary>
    /// Deconstructs the result into its components.
    /// </summary>
    /// <param name="isSuccess">When this method returns, <c>true</c> if the result is successful; otherwise, <c>false</c>.</param>
    /// <param name="value">When this method returns, the success value if successful; otherwise, <c>default</c>.</param>
    /// <param name="error">When this method returns, the <see cref="Error"/> if failed; otherwise, <see cref="Error.None"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out bool isSuccess, out T? value, out Error error)
    {
        isSuccess = _isSuccess == 1;
        value = _value;
        error = _error;
    }

    /// <summary>
    /// Attempts to retrieve the value and error from the result.
    /// </summary>
    /// <param name="value">When this method returns, the success value if successful; otherwise, <c>default</c>.</param>
    /// <param name="error">When this method returns, the <see cref="Error"/> if failed; otherwise, <see cref="Error.None"/>.</param>
    /// <returns><c>true</c> if the result is successful; <c>false</c> if it is a failure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(out T? value, out Error error)
    {
        value = _value;
        error = _error;
        return _isSuccess == 1;
    }

    /// <summary>
    /// Attempts to retrieve the value from the result.
    /// </summary>
    /// <param name="value">When this method returns, the success value if successful; otherwise, <c>default</c>.</param>
    /// <returns><c>true</c> if the result is successful; <c>false</c> if it is a failure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(out T? value)
    {
        value = _value;
        return _isSuccess == 1;
    }

    /// <summary>
    /// Gets the value if successful; otherwise returns the specified default value.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the result is a failure (default: <c>default(T)</c>).</param>
    /// <returns>The success value or <paramref name="defaultValue"/> on failure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValueOrDefault(T defaultValue = default!)
        => _isSuccess == 1 ? _value : defaultValue;

    /// <summary>
    /// Gets the value if successful; otherwise invokes <paramref name="fallback"/> with the error to produce a value.
    /// </summary>
    /// <param name="fallback">Function that receives the <see cref="Error"/> and returns a recovery value.</param>
    /// <returns>The success value or the result of <paramref name="fallback"/> on failure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValueOr(Func<Error, T> fallback)
        => _isSuccess == 1 ? _value : fallback(_error);
}
