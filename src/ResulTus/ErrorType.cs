// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus;

/// <summary>
/// Defines the category of an <see cref="Error"/>.
/// </summary>
/// <remarks>
/// Values are ordered by severity. <see cref="None"/> is the default and indicates
/// absence of error. Each value maps to a conceptual HTTP status category
/// (see spec for mapping).
/// </remarks>
public enum ErrorType : byte
{
    /// <summary>
    /// No error (default value).
    /// </summary>
    None = 0,

    /// <summary>
    /// General failure (maps to HTTP 500).
    /// </summary>
    Failure,

    /// <summary>
    /// Validation error (maps to HTTP 400).
    /// </summary>
    Validation,

    /// <summary>
    /// Resource not found (maps to HTTP 404).
    /// </summary>
    NotFound,

    /// <summary>
    /// Resource conflict (maps to HTTP 409).
    /// </summary>
    Conflict,

    /// <summary>
    /// Authentication required (maps to HTTP 401).
    /// </summary>
    Unauthorized,

    /// <summary>
    /// Access denied (maps to HTTP 403).
    /// </summary>
    Forbidden,

    /// <summary>
    /// Operation timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Operation was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Unexpected error (maps to HTTP 500).
    /// </summary>
    Unexpected,
}
