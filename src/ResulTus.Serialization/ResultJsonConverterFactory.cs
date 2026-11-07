// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResulTus.Serialization;

/// <summary>
/// Creates a <see cref="ResultJsonConverter{T}"/> for any closed <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// <see cref="Result{T}"/> is an open generic, so System.Text.Json needs a factory to close the
/// converter per value type. This convenience registration is the ambient-options path; the alternative
/// for Native AOT is constructing <see cref="ResultJsonConverter{T}"/> directly.
/// </remarks>
public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    /// <summary>
    /// Determines whether <paramref name="typeToConvert"/> is a closed <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="typeToConvert">The type to test.</param>
    /// <returns><c>true</c> when the type is a closed <see cref="Result{T}"/>.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return typeToConvert.IsGenericType
            && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>);
    }

    /// <summary>
    /// Creates the converter for a closed <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="typeToConvert">The closed result type.</param>
    /// <param name="options">The serializer options in effect.</param>
    /// <returns>The converter for <paramref name="typeToConvert"/>.</returns>
    /// <remarks>
    /// The closed converter is instantiated at runtime, which Native AOT cannot discover statically.
    /// Native AOT applications must not rely on this factory: use
    /// <see cref="ResultJsonConverter{T}"/> constructed with the <c>JsonTypeInfo&lt;T&gt;</c> a
    /// source-generated <see cref="JsonSerializerContext"/> emits for the value type, as described on
    /// <see cref="ResultJsonConverter{T}"/>.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:RequiresDynamicCode",
        Justification = "Closing ResultJsonConverter<T> over the value type is the only way to serialize an open generic; Native AOT callers must declare the instantiation through a JsonSerializerContext, as documented on this method.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "The closed converter type is built from Result<> and the value type only, and has a public parameterless constructor; no application members are reflected over.")]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ResultJsonConverter<>).MakeGenericType(valueType);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
