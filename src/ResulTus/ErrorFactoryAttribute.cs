// Copyright (c) ResulTus. All rights reserved.

using System;

namespace ResulTus;

/// <summary>
/// Supplies the code, the category and the message template of one generated error factory.
/// </summary>
/// <remarks>
/// <para>
/// Apply it to a <c>public static partial Error</c> method declared on a type marked with
/// <see cref="ResultErrorsAttribute"/>. The generator supplies the body, which calls the matching
/// <see cref="Error"/> factory with <see cref="Code"/> and the rendered <see cref="Message"/>.
/// </para>
/// <para>
/// The message template is emitted as the content of an interpolated string, so a placeholder is
/// the name of one of the method's parameters — <c>"User {id} not found"</c> becomes
/// <c>$"User {id} not found"</c> in the generated body. A literal brace is written
/// <c>{{</c> or <c>}}</c>, exactly as in an interpolated string. A <c>null</c> template generates
/// an error with no message, which <see cref="Error.Message"/> keeps distinct from an empty one.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ErrorFactory("USER_NOT_FOUND", ErrorType.NotFound, "User {id} not found")]
/// public static partial Error NotFound(Guid id);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ErrorFactoryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="ErrorFactoryAttribute"/>.
    /// </summary>
    /// <param name="code">The error code the generated factory passes to <see cref="Error"/>.</param>
    /// <param name="type">The category of the generated error.</param>
    /// <param name="message">
    /// The message template, or <c>null</c> for an error with no message.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <c>null</c>.</exception>
    public ErrorFactoryAttribute(string code, ErrorType type, string? message)
    {
        ArgumentNullException.ThrowIfNull(code);

        Code = code;
        Type = type;
        Message = message;
    }

    /// <summary>
    /// Gets the error code the generated factory passes to <see cref="Error"/>.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the category of the generated error.
    /// </summary>
    public ErrorType Type { get; }

    /// <summary>
    /// Gets the message template, or <c>null</c> for an error with no message.
    /// </summary>
    public string? Message { get; }
}
