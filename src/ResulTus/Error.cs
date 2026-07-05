// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ResulTus;

/// <summary>
/// Represents a structured error with a code, optional message, error type, and optional metadata.
/// </summary>
/// <remarks>
/// This is a <c>readonly record struct</c> with sequential layout. The default value
/// (<c>default(Error)</c>) behaves as <see cref="None"/> with an empty code and no metadata.
/// Use the static factory methods (<see cref="Failure"/>, <see cref="NotFound"/>, etc.)
/// to create typed errors.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct Error
{
    private readonly string _code;
    private readonly string? _message;
    private readonly ErrorType _type;
    private readonly IReadOnlyDictionary<string, object?>? _metadata;

    /// <summary>
    /// Initializes a new <see cref="Error"/> with the specified properties.
    /// </summary>
    /// <param name="code">The error code (coalesced to <see cref="string.Empty"/> if <c>null</c>).</param>
    /// <param name="message">An optional human-readable message.</param>
    /// <param name="type">The <see cref="ErrorType"/> category.</param>
    /// <param name="metadata">Optional structured metadata dictionary.</param>
    private Error(string code, string? message, ErrorType type, IReadOnlyDictionary<string, object?>? metadata)
    {
        _code = code ?? string.Empty;
        _message = message;
        _type = type;
        _metadata = metadata;
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
    /// Gets the optional metadata dictionary. <c>null</c> when no metadata is set.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Metadata => _metadata;

    /// <summary>
    /// The no-error singleton. Its <see cref="Code"/> is <see cref="string.Empty"/>,
    /// <see cref="Type"/> is <see cref="ErrorType.None"/>, and <see cref="Metadata"/> is <c>null</c>.
    /// </summary>
    public static Error None => new Error(string.Empty, null, ErrorType.None, null);

    /// <summary>
    /// Creates a <see cref="ErrorType.Failure"/> error.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">Optional human-readable message.</param>
    /// <param name="metadata">Optional structured metadata.</param>
    /// <returns>A new <see cref="Error"/> with <see cref="Type"/> = <see cref="ErrorType.Failure"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Failure(string code, string? message = null, IReadOnlyDictionary<string, object?>? metadata = null)
        => new Error(code, message, ErrorType.Failure, metadata);

    /// <summary>
    /// Creates a <see cref="ErrorType.Validation"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Validation(string code, string? message = null, IReadOnlyDictionary<string, object?>? metadata = null)
        => new Error(code, message, ErrorType.Validation, metadata);

    /// <summary>
    /// Creates a <see cref="ErrorType.NotFound"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error NotFound(string code, string? message = null, IReadOnlyDictionary<string, object?>? metadata = null)
        => new Error(code, message, ErrorType.NotFound, metadata);

    /// <summary>
    /// Creates a <see cref="ErrorType.Conflict"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Conflict(string code, string? message = null, IReadOnlyDictionary<string, object?>? metadata = null)
        => new Error(code, message, ErrorType.Conflict, metadata);

    /// <summary>
    /// Creates a <see cref="ErrorType.Unauthorized"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Unauthorized(string code, string? message = null, IReadOnlyDictionary<string, object?>? metadata = null)
        => new Error(code, message, ErrorType.Unauthorized, metadata);

    /// <summary>
    /// Creates a <see cref="ErrorType.Forbidden"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Forbidden(string code, string? message = null, IReadOnlyDictionary<string, object?>? metadata = null)
        => new Error(code, message, ErrorType.Forbidden, metadata);

    /// <summary>
    /// Creates a <see cref="ErrorType.Timeout"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Timeout(string code, string? message = null, IReadOnlyDictionary<string, object?>? metadata = null)
        => new Error(code, message, ErrorType.Timeout, metadata);

    /// <summary>
    /// Creates a <see cref="ErrorType.Cancelled"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Cancelled(string code, string? message = null, IReadOnlyDictionary<string, object?>? metadata = null)
        => new Error(code, message, ErrorType.Cancelled, metadata);

    /// <summary>
    /// Creates a <see cref="ErrorType.Unexpected"/> error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Unexpected(string code, string? message = null, IReadOnlyDictionary<string, object?>? metadata = null)
        => new Error(code, message, ErrorType.Unexpected, metadata);
}
