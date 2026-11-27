// Copyright (c) ResulTus. All rights reserved.

using System;

namespace ResulTus;

/// <summary>
/// A non-generic view of a result outcome, so infrastructure can inspect a result whose success
/// value type is not known at compile time (for example a generic pipeline behavior or a logger).
/// </summary>
/// <remarks>
/// <see cref="Result"/> and <see cref="Result{T}"/> implement this interface. The typed
/// <see cref="Result{TSuccess, TError}"/> tier is deliberately excluded because its error type is
/// not necessarily <see cref="Error"/>.
/// </remarks>
public interface IResultOutcome
{
    /// <summary>
    /// Gets a value indicating whether the result represents a successful outcome.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    /// Gets the success type's name, or <c>"Result"</c> for the unit tier.
    /// </summary>
    string ResultType { get; }

    /// <summary>
    /// Attempts to read the error of a failed result.
    /// </summary>
    /// <param name="error">When this method returns, the error if failed; otherwise <see cref="Error.None"/>.</param>
    /// <returns><c>true</c> when the result is a failure; otherwise, <c>false</c>.</returns>
    bool TryGetError(out Error error);
}
