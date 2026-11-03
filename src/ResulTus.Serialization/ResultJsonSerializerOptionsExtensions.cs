// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Text.Json;

namespace ResulTus.Serialization;

/// <summary>
/// Registration helpers for the ResulTus System.Text.Json converters.
/// </summary>
public static class ResultJsonSerializerOptionsExtensions
{
    /// <summary>
    /// Registers every ResulTus converter on <paramref name="options"/> and returns the same instance.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The same <paramref name="options"/> instance, for chaining.</returns>
    /// <remarks>
    /// Call this once, before the options instance is first used: a <see cref="JsonSerializerOptions"/>
    /// becomes read-only after its first serialization, and mutating it afterwards throws
    /// <see cref="InvalidOperationException"/>.
    /// <para>
    /// The converters emit a fixed camelCase shape regardless of
    /// <see cref="JsonSerializerOptions.PropertyNamingPolicy"/>. A wire format is a contract, and letting
    /// ambient options change it would make the same result serialize differently depending on who
    /// serializes it.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    public static JsonSerializerOptions AddResulTusConverters(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Converters.Add(new ErrorTypeJsonConverter());
        options.Converters.Add(new ErrorJsonConverter());
        options.Converters.Add(new UnitResultJsonConverter());
        options.Converters.Add(new ResultJsonConverterFactory());

        return options;
    }
}
