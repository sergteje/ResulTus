// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Validation;

/// <summary>
/// A single validation failure, tied to the member that caused it.
/// </summary>
/// <param name="Field">The member name that failed, or the empty string for an object-level failure.</param>
/// <param name="Message">The human-readable failure message.</param>
/// <param name="Code">An optional machine-readable failure code.</param>
/// <remarks>
/// Named <c>ValidationIssue</c> rather than <c>ValidationFailure</c> so it never collides with
/// FluentValidation's own public type in code that imports both namespaces.
/// </remarks>
public readonly record struct ValidationIssue(string Field, string Message, string? Code = null);
