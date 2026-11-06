// Copyright (c) ResulTus. All rights reserved.

using System.Globalization;

namespace ResulTus.SourceGenerators;

/// <summary>
/// Produces a short hash that is stable across processes, machines and runs.
/// </summary>
/// <remarks>
/// The hint name of a generated file must not change between two builds of the same source, or the
/// build stops being deterministic. <see cref="string.GetHashCode()"/> cannot be used for that: it
/// is randomized per process. This is FNV-1a, which is small, allocation-free and specified.
/// </remarks>
internal static class StableHash
{
    private const uint OffsetBasis = 2166136261;

    private const uint Prime = 16777619;

    /// <summary>
    /// Computes the hash of <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text to hash.</param>
    /// <returns>Eight lowercase hexadecimal digits.</returns>
    internal static string Of(string text)
    {
        var hash = OffsetBasis;

        foreach (var character in text)
        {
            hash ^= character;
            hash *= Prime;
        }

        return hash.ToString("x8", CultureInfo.InvariantCulture);
    }
}
