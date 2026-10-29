// Copyright (c) ResulTus. All rights reserved.

using System.Runtime.CompilerServices;

namespace ResulTus;

/// <summary>
/// Represents a structured error with a code, an optional message, and an error type.
/// </summary>
/// <remarks>
/// This is a <c>readonly record struct</c> with sequential layout that keeps no untyped
/// payload, so it is small enough to live inline in every result type and costs no heap
/// allocation on either the success or the failure path. The default value
/// (<c>default(Error)</c>) behaves as <see cref="None"/> with an empty code, and compares
/// equal to it under <c>==</c>.
/// Use the static factory methods (<see cref="Failure"/>, <see cref="NotFound"/>, etc.)
/// to create typed errors. Structured extras belong to a typed <c>TError</c> in
/// <c>Result&lt;TSuccess, TError&gt;</c>.
/// </remarks>
public readonly record struct Error
{
    private readonly string _code;
    private readonly string? _message;
    private readonly ErrorType _type;

    /// <summary>
    /// Initializes a new <see cref="Error"/> with the specified properties.
    /// </summary>
    /// <param name="code">The error code (coalesced to <see cref="string.Empty"/> if <c>null</c>).</param>
    /// <param name="message">An optional human-readable message.</param>
    /// <param name="type">The <see cref="ErrorType"/> category.</param>
    private Error(string code, string? message, ErrorType type)
    {
        _code = code ?? string.Empty;
        _message = message;
        _type = type;
    }

    /// <summary>
    /// Gets the error code (never <c>null</c>).
    /// </summary>
    public string Code => _code ?? string.Empty;

    /// <summary>
    /// Gets the optional human-readable message.
    /// </summary>
    public string? Message => _message;

    /// <summary>
    /// Gets the <see cref="ErrorType"/> category.
    /// </summary>
    public ErrorType Type => _type;

    /// <summary>
    /// The no-error singleton. Its <see cref="Code"/> is <see cref="string.Empty"/>
    /// and its <see cref="Type"/> is <see cref="ErrorType.None"/>.
    /// </summary>
    /// <remarks>
    /// Equal to <c>default(Error)</c> under <c>==</c>. The default struct never runs the
    /// constructor, so its code field stays <c>null</c>; <see cref="Equals(Error)"/> compares
    /// the null-coalesced <see cref="Code"/> contract instead of the raw field, which makes
    /// "no error" and "default" the same value.
    /// </remarks>
    public static Error None => new Error(string.Empty, null, ErrorType.None);

    /// <summary>
    /// Determines whether this error has the same <see cref="Code"/>, <see cref="Message"/>
    /// and <see cref="Type"/> as another one.
    /// </summary>
    /// <param name="other">The error to compare with.</param>
    /// <returns><c>true</c> when the three members are equal; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Declared explicitly (instead of relying on the synthesized member-by-member equality)
    /// so <c>default(Error)</c> compares equal to <see cref="None"/>: the synthesized version
    /// compares the raw <c>_code</c> field, where <c>default</c> holds <c>null</c> and
    /// <see cref="None"/> holds <see cref="string.Empty"/>. <see cref="Code"/> is the public
    /// contract and already null-coalesces, so equality follows it. Only <see cref="Code"/>
    /// carries that contract: a <c>null</c> <see cref="Message"/> stays distinct from an
    /// empty one.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Error other)
        => string.Equals(Code, other.Code, StringComparison.Ordinal)
        && string.Equals(Message, other.Message, StringComparison.Ordinal)
        && _type == other._type;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Code, Message, _type);

    /// <summary>
    /// Creates a <see cref="ErrorType.Failure"/> error.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">Optional human-readable message.</param>
    /// <returns>A new <see cref="Error"/> with <see cref="Type"/> = <see cref="ErrorType.Failure"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Failure(string code, string? message = null)
        => new Error(code, message, ErrorType.Failure);

    /// <summary>
    /// Creates a <see cref="ErrorType.Validation"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Validation(string code, string? message = null)
        => new Error(code, message, ErrorType.Validation);

    /// <summary>
    /// Creates a <see cref="ErrorType.NotFound"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error NotFound(string code, string? message = null)
        => new Error(code, message, ErrorType.NotFound);

    /// <summary>
    /// Creates a <see cref="ErrorType.Conflict"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Conflict(string code, string? message = null)
        => new Error(code, message, ErrorType.Conflict);

    /// <summary>
    /// Creates a <see cref="ErrorType.Unauthorized"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Unauthorized(string code, string? message = null)
        => new Error(code, message, ErrorType.Unauthorized);

    /// <summary>
    /// Creates a <see cref="ErrorType.Forbidden"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Forbidden(string code, string? message = null)
        => new Error(code, message, ErrorType.Forbidden);

    /// <summary>
    /// Creates a <see cref="ErrorType.Timeout"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Timeout(string code, string? message = null)
        => new Error(code, message, ErrorType.Timeout);

    /// <summary>
    /// Creates a <see cref="ErrorType.Cancelled"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Cancelled(string code, string? message = null)
        => new Error(code, message, ErrorType.Cancelled);

    /// <summary>
    /// Creates a <see cref="ErrorType.Unexpected"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Unexpected(string code, string? message = null)
        => new Error(code, message, ErrorType.Unexpected);
}
