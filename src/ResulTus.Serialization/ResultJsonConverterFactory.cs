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
/// converter per value type.
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
    /// Native AOT applications should declare the <see cref="Result{T}"/> instantiations they use
    /// through a <c>JsonSerializerContext</c> so the generic instantiation is preserved.
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

/// <summary>
/// Serializes a closed <see cref="Result{T}"/> as <c>{"isSuccess":true,"value":...}</c> or
/// <c>{"isSuccess":false,"error":{...}}</c>.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
internal sealed class ResultJsonConverter<T> : JsonConverter<Result<T>>
{
    private ConverterCache? _cachedConverter;

    /// <inheritdoc />
    /// <remarks>
    /// Resolving the converter for <typeparamref name="T"/> goes through
    /// <see cref="JsonSerializerOptions.GetConverter(Type)"/>, which System.Text.Json annotates as
    /// requiring dynamic code and unreferenced code. It is nonetheless the only public way to reach
    /// the converter for an arbitrary <typeparamref name="T"/> from inside a
    /// <see cref="JsonConverter{T}"/>. Normal and trimmed applications are unaffected; Native AOT
    /// callers must ensure the value type is reachable through a <c>JsonSerializerContext</c>.
    /// </remarks>
    public override void Write(Utf8JsonWriter writer, Result<T> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);

        writer.WriteStartObject();
        writer.WriteBoolean("isSuccess", value.IsSuccess);

        if (value.TryGetValue(out var inner, out var error))
        {
            writer.WritePropertyName("value");
            if (inner is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                var valueConverter = GetValueConverter(options);
                valueConverter.Write(writer, inner, options);
            }
        }
        else
        {
            writer.WritePropertyName("error");
            ErrorJsonConverter.WriteValue(writer, error);
        }

        writer.WriteEndObject();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Same AOT limitation as <see cref="Write"/>: resolving the converter for
    /// <typeparamref name="T"/> uses <see cref="JsonSerializerOptions.GetConverter(Type)"/>.
    /// </remarks>
    public override Result<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected a Result object, found {reader.TokenType}.");
        }

        bool? isSuccess = null;
        var hasValue = false;
        T? value = default;
        Error? error = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return isSuccess switch
                {
                    true when hasValue => Result<T>.Success(value!),
                    true => throw new JsonException("The Result object reports success but carries no 'value'."),
                    false => Result<T>.Failure(
                        error ?? throw new JsonException("The Result object reports failure but carries no 'error'.")),
                    null => throw new JsonException("The Result object is missing the required 'isSuccess' property."),
                };
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Unexpected token {reader.TokenType} while reading a Result.");
            }

            if (reader.ValueTextEquals("isSuccess"))
            {
                reader.Read();
                isSuccess = UnitResultJsonConverter.ReadBoolean(ref reader, "isSuccess");
            }
            else if (reader.ValueTextEquals("value"))
            {
                reader.Read();
                var valueConverter = GetValueConverter(options);
                value = valueConverter.Read(ref reader, typeof(T), options);
                hasValue = true;
            }
            else if (reader.ValueTextEquals("error"))
            {
                reader.Read();
                error = ErrorJsonConverter.ReadValue(ref reader);
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON while reading a Result.");
    }

    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:RequiresDynamicCode",
        Justification = "The factory-based converter resolves an arbitrary T through JsonSerializerOptions.GetConverter; the AOT-specific direct-registration path is tracked separately.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "The factory-based converter resolves an arbitrary T through JsonSerializerOptions.GetConverter; the AOT-specific direct-registration path is tracked separately.")]
    private JsonConverter<T> GetValueConverter(JsonSerializerOptions options)
    {
        var cached = _cachedConverter;
        if (cached is not null && ReferenceEquals(cached.Options, options))
        {
            return cached.Converter;
        }

        var converter = (JsonConverter<T>)options.GetConverter(typeof(T));
        _cachedConverter = new ConverterCache(options, converter);
        return converter;
    }

    private sealed class ConverterCache(JsonSerializerOptions options, JsonConverter<T> converter)
    {
        public JsonSerializerOptions Options { get; } = options;

        public JsonConverter<T> Converter { get; } = converter;
    }
}
