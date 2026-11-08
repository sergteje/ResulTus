// Copyright (c) ResulTus. All rights reserved.

using System.Collections.ObjectModel;
using System.Text.Json;

namespace ResulTus.HttpClient;

/// <summary>
/// Represents an RFC 9457 problem details document, independent of ASP.NET Core.
/// </summary>
/// <remarks>
/// <para>
/// Carries the five RFC 9457 standard members (<c>type</c>, <c>title</c>, <c>status</c>,
/// <c>detail</c>, <c>instance</c>) plus any unknown extension members, including the validation
/// <c>errors</c> member, without referencing ASP.NET Core's <c>ProblemDetails</c> type.
/// </para>
/// <para>
/// This is a sealed class rather than a readonly record struct. The library's inline struct error
/// (<see cref="Error"/>) is chosen for its tiny, reference-free layout; this type instead owns a
/// dictionary of arbitrary JSON values, which is exactly the reference-holding payload that
/// convention avoids. Extension values are stored as <see cref="JsonElement"/>, the built-in faithful
/// representation of an arbitrary JSON value.
/// </para>
/// </remarks>
public sealed class HttpProblemDetails : IEquatable<HttpProblemDetails>
{
    private readonly IReadOnlyDictionary<string, JsonElement>? _extensions;

    /// <summary>
    /// Initializes a new <see cref="HttpProblemDetails"/>.
    /// </summary>
    /// <param name="type">The optional <c>type</c> member — a URI reference identifying the problem type.</param>
    /// <param name="title">The optional <c>title</c> member — a short, human-readable summary of the problem type.</param>
    /// <param name="status">The optional <c>status</c> member — the HTTP status code as a number.</param>
    /// <param name="detail">The optional <c>detail</c> member — a human-readable explanation specific to this occurrence.</param>
    /// <param name="instance">The optional <c>instance</c> member — a URI reference identifying the specific occurrence.</param>
    /// <param name="extensions">
    /// The optional extension members beyond the standard set, including <c>errors</c>. The dictionary
    /// is defensively copied; a <c>null</c> or empty dictionary is normalized to no extensions.
    /// </param>
    public HttpProblemDetails(
        string? type = null,
        string? title = null,
        int? status = null,
        string? detail = null,
        string? instance = null,
        IReadOnlyDictionary<string, JsonElement>? extensions = null)
    {
        Type = type;
        Title = title;
        Status = status;
        Detail = detail;
        Instance = instance;
        _extensions = extensions is null or { Count: 0 }
            ? null
            : new ReadOnlyDictionary<string, JsonElement>(new Dictionary<string, JsonElement>(extensions));
    }

    /// <summary>
    /// Gets the <c>type</c> member — a URI reference identifying the problem type (RFC 9457 §3.1.1).
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// Gets the <c>title</c> member — a short, human-readable summary of the problem type.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets the <c>status</c> member — the HTTP status code as a number (RFC 9457 §3.1.2).
    /// </summary>
    public int? Status { get; }

    /// <summary>
    /// Gets the <c>detail</c> member — a human-readable explanation specific to this occurrence.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    /// Gets the <c>instance</c> member — a URI reference identifying the specific occurrence.
    /// </summary>
    public string? Instance { get; }

    /// <summary>
    /// Gets the extension members beyond the standard set, including <c>errors</c>, as
    /// <see cref="JsonElement"/> values. <c>null</c> when the document has no extension members.
    /// </summary>
    /// <remarks>
    /// The returned dictionary is read-only and defensively copied from the constructor argument. The
    /// validation <c>errors</c> member (RFC 9457 §3.2.2) is retained here as an ordinary extension
    /// member, exactly as the ASP.NET Core adapter renders it. Extension <see cref="JsonElement"/>
    /// values reference the caller's source document, so a converter must keep that document alive.
    /// </remarks>
    public IReadOnlyDictionary<string, JsonElement>? Extensions => _extensions;

    /// <summary>
    /// Determines whether this document equals another by value: the five standard members plus the
    /// extension members, compared structurally.
    /// </summary>
    /// <param name="other">The document to compare with.</param>
    /// <returns><c>true</c> when the documents are equal; otherwise <c>false</c>.</returns>
    public bool Equals(HttpProblemDetails? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Type, other.Type, StringComparison.Ordinal)
            && string.Equals(Title, other.Title, StringComparison.Ordinal)
            && Status == other.Status
            && string.Equals(Detail, other.Detail, StringComparison.Ordinal)
            && string.Equals(Instance, other.Instance, StringComparison.Ordinal)
            && ExtensionsEqual(_extensions, other._extensions);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as HttpProblemDetails);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(Title);
        hash.Add(Status);
        hash.Add(Detail);
        hash.Add(Instance);
        hash.Add(ExtensionsHash(_extensions));
        return hash.ToHashCode();
    }

    /// <summary>
    /// Determines whether two documents are equal.
    /// </summary>
    /// <param name="left">The first document to compare.</param>
    /// <param name="right">The second document to compare.</param>
    /// <returns><c>true</c> when the documents are equal; otherwise <c>false</c>.</returns>
    public static bool operator ==(HttpProblemDetails? left, HttpProblemDetails? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Determines whether two documents are not equal.
    /// </summary>
    /// <param name="left">The first document to compare.</param>
    /// <param name="right">The second document to compare.</param>
    /// <returns><c>true</c> when the documents are not equal; otherwise <c>false</c>.</returns>
    public static bool operator !=(HttpProblemDetails? left, HttpProblemDetails? right)
        => !(left == right);

    /// <summary>
    /// Compares two extension dictionaries structurally: the same keys and structurally equal
    /// <see cref="JsonElement"/> values.
    /// </summary>
    /// <param name="left">The first dictionary to compare, or <c>null</c>.</param>
    /// <param name="right">The second dictionary to compare, or <c>null</c>.</param>
    /// <returns><c>true</c> when both are <c>null</c> or structurally equal; otherwise <c>false</c>.</returns>
    private static bool ExtensionsEqual(
        IReadOnlyDictionary<string, JsonElement>? left,
        IReadOnlyDictionary<string, JsonElement>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || !pair.Value.Equals(value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Computes a content-based hash over an extension dictionary, order-independent so that two
    /// structurally equal dictionaries hash alike regardless of insertion order.
    /// </summary>
    /// <param name="extensions">The dictionary to hash, or <c>null</c>.</param>
    /// <returns>The combined hash code, or <c>0</c> when <paramref name="extensions"/> is <c>null</c>.</returns>
    private static int ExtensionsHash(IReadOnlyDictionary<string, JsonElement>? extensions)
    {
        if (extensions is null)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach (var pair in extensions.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }
}
