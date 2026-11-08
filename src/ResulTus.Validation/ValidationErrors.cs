// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Collections.Generic;

namespace ResulTus.Validation;

/// <summary>
/// The structured failure value carried by a failed <see cref="Result{TSuccess,TError}"/> produced
/// by validation.
/// </summary>
/// <remarks>
/// <see cref="Error"/> deliberately keeps no untyped payload, so structured validation detail
/// belongs to a typed <c>TError</c>. This is that type: it preserves every
/// <see cref="ValidationIssue"/>, its field, its message and its optional code, instead of
/// collapsing N failures into one string.
/// </remarks>
public sealed class ValidationErrors
{
    /// <summary>
    /// Initializes a new <see cref="ValidationErrors"/> holding the given issues.
    /// </summary>
    /// <param name="issues">The issues to preserve, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="issues"/> is <c>null</c>.</exception>
    public ValidationErrors(IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        Issues = issues;
    }

    /// <summary>
    /// Gets the issues in the order they were reported.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }

    /// <summary>
    /// Gets a value indicating whether any issue was reported.
    /// </summary>
    public bool HasErrors => Issues.Count > 0;

    /// <summary>
    /// Groups the issue messages by field, preserving both field order and per-field message order.
    /// </summary>
    /// <returns>A map from field name to the messages reported for that field.</returns>
    /// <remarks>
    /// An object-level failure has an empty field name, which is the convention ASP.NET Core's
    /// <c>ModelState</c> uses.
    /// </remarks>
    public IReadOnlyDictionary<string, string[]> ToDictionary()
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var issue in Issues)
        {
            if (!grouped.TryGetValue(issue.Field, out var messages))
            {
                messages = [];
                grouped[issue.Field] = messages;
            }

            messages.Add(issue.Message);
        }

        var result = new Dictionary<string, string[]>(grouped.Count, StringComparer.Ordinal);
        foreach (var pair in grouped)
        {
            result[pair.Key] = [.. pair.Value];
        }

        return result;
    }

    /// <summary>
    /// Collapses the failures into a single <see cref="Error"/> for callers that need one.
    /// </summary>
    /// <param name="code">The error code to carry. Defaults to <c>Validation.Failed</c>.</param>
    /// <returns>A <see cref="ErrorType.Validation"/> error whose message states the failure count.</returns>
    /// <remarks>
    /// This is lossy by design: it exists for the paths that cannot carry structured detail. Use
    /// <see cref="Issues"/> when the detail matters.
    /// </remarks>
    public Error ToError(string code = "Validation.Failed")
        => Error.Validation(code, $"{Issues.Count} validation failure(s).");
}
