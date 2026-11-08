// Copyright (c) ResulTus. All rights reserved.

using System;

namespace ResulTus;

/// <summary>
/// Marks a partial class or struct whose <see cref="ErrorFactoryAttribute"/> methods are
/// implemented by <c>ResulTus.SourceGenerators</c>.
/// </summary>
/// <remarks>
/// <para>
/// The attribute is the opt-in: a type that does not carry it is never scanned, even when it has
/// methods annotated with <see cref="ErrorFactoryAttribute"/>. It carries no configuration of its
/// own, because every factory states its code, its category and its message on the method.
/// </para>
/// <para>
/// The annotated type must be <c>partial</c>, and so must every type that encloses it, because the
/// generator adds a second part in a separate file.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ResultErrors]
/// public static partial class UserErrors
/// {
///     [ErrorFactory("USER_NOT_FOUND", ErrorType.NotFound, "User {id} not found")]
///     public static partial Error NotFound(Guid id);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class ResultErrorsAttribute : Attribute
{
}
