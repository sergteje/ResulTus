// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResulTus.Serialization;

/// <summary>
/// Creates a <see cref="ResultJsonConverter{T}"/> for any closed <see cref="Result{T}"/>, and a
/// <see cref="ResultJsonConverter{TSuccess, TError}"/> for any closed
/// <see cref="Result{TSuccess, TError}"/>.
/// </summary>
/// <remarks>
/// Both result tiers are open generics, so System.Text.Json needs a factory to close the converter per
/// type argument. This convenience registration is the ambient-options path; the alternative for
/// Native AOT is constructing <see cref="ResultJsonConverter{T}"/> or
/// <see cref="ResultJsonConverter{TSuccess, TError}"/> directly from source-generated metadata.
/// </remarks>
public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    /// <summary>
    /// Determines whether <paramref name="typeToConvert"/> is a closed <see cref="Result{T}"/> or
    /// <see cref="Result{TSuccess, TError}"/>.
    /// </summary>
    /// <param name="typeToConvert">The type to test.</param>
    /// <returns>
    /// <c>true</c> when the type is a closed <see cref="Result{T}"/> or
    /// <see cref="Result{TSuccess, TError}"/>.
    /// </returns>
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        if (!typeToConvert.IsGenericType)
        {
            return false;
        }

        var definition = typeToConvert.GetGenericTypeDefinition();
        return definition == typeof(Result<>) || definition == typeof(Result<,>);
    }

    /// <summary>
    /// Creates the converter for a closed <see cref="Result{T}"/> or
    /// <see cref="Result{TSuccess, TError}"/>.
    /// </summary>
    /// <param name="typeToConvert">The closed result type.</param>
    /// <param name="options">The serializer options in effect.</param>
    /// <returns>The converter for <paramref name="typeToConvert"/>.</returns>
    /// <remarks>
    /// The closed converter is instantiated at runtime, which Native AOT cannot discover statically.
    /// Native AOT applications must not rely on this factory: use
    /// <see cref="ResultJsonConverter{T}"/> or <see cref="ResultJsonConverter{TSuccess, TError}"/>
    /// constructed with the <c>JsonTypeInfo&lt;T&gt;</c> a source-generated
    /// <see cref="JsonSerializerContext"/> emits for each type argument, as described on those types.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:RequiresDynamicCode",
        Justification = "Closing the converter over the type arguments is the only way to serialize an open generic; Native AOT callers must declare the instantiation through a JsonSerializerContext, as documented on this method.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "The closed converter type is built from the result type and its type arguments only, and has a public parameterless constructor; no application members are reflected over.")]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        var typeArguments = typeToConvert.GetGenericArguments();
        var converterType = typeArguments.Length == 2
            ? typeof(ResultJsonConverter<,>).MakeGenericType(typeArguments[0], typeArguments[1])
            : typeof(ResultJsonConverter<>).MakeGenericType(typeArguments[0]);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
