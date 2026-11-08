// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ResulTus.Serialization;

/// <summary>
/// Serializes a closed <see cref="Result{T}"/> as <c>{"isSuccess":true,"value":...}</c> or
/// <c>{"isSuccess":false,"error":{...}}</c>.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
/// <remarks>
/// <para>
/// There are two ways to build this converter, and the difference is exactly the difference between
/// what a reflection-based serializer can resolve and what Native AOT can.
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Ambient options — not AOT-safe.</b> The parameterless constructor resolves the converter for
/// <typeparamref name="T"/> from the <see cref="JsonSerializerOptions"/> in effect at each call, which
/// is what <see cref="ResultJsonConverterFactory"/> and
/// <c>AddResulTusConverters()</c> use. It works in ordinary and trimmed applications, and the wire
/// shape is unchanged, but the lookup is annotated as requiring dynamic code and unreferenced code.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Source-generated metadata — AOT-suitable.</b> The
/// <see cref="ResultJsonConverter{T}(JsonTypeInfo{T})"/> constructor stores the
/// <see cref="JsonTypeInfo{T}"/> the source generator emitted for <typeparamref name="T"/> and never
/// consults the ambient options for it. Register the converter directly:
/// <c>options.Converters.Add(new ResultJsonConverter&lt;MyDto&gt;(MyContext.Default.MyDto));</c> where
/// <c>MyContext</c> is a <see cref="JsonSerializerContext"/> declaring
/// <c>[JsonSerializable(typeof(MyDto))]</c>. This path needs no factory, no generic instantiation at
/// runtime, and no reflection over the payload type. Note that System.Text.Json resolves the type
/// handed to <c>JsonSerializer.Serialize</c> through the context, so keep the closed
/// <see cref="Result{T}"/> in the context's reachable graph: annotate a carrier payload carrying the
/// result, exactly as the serializer does for any nested converter.
/// </description>
/// </item>
/// </list>
/// <para>
/// Both paths emit the same JSON. The <c>isSuccess</c>, <c>value</c> and <c>error</c> names are fixed
/// and do not follow <see cref="JsonSerializerOptions.PropertyNamingPolicy"/>.
/// </para>
/// </remarks>
public sealed class ResultJsonConverter<T> : JsonConverter<Result<T>>
{
    private readonly JsonTypeInfo<T>? _valueTypeInfo;
    private ConverterCache? _cachedConverter;

    /// <summary>
    /// Initializes a converter that resolves the payload converter from the
    /// <see cref="JsonSerializerOptions"/> in effect at each call.
    /// </summary>
    /// <remarks>
    /// This is the constructor <see cref="ResultJsonConverterFactory"/> instantiates. Prefer
    /// <see cref="ResultJsonConverter{T}(JsonTypeInfo{T})"/> for Native AOT.
    /// </remarks>
    public ResultJsonConverter()
    {
    }

    /// <summary>
    /// Initializes a converter bound to source-generated metadata for the payload type.
    /// </summary>
    /// <param name="valueTypeInfo">
    /// The metadata for <typeparamref name="T"/> from a <see cref="JsonSerializerContext"/> declaring
    /// <c>[JsonSerializable(typeof(T))]</c>.
    /// </param>
    /// <remarks>
    /// This is the Native AOT path: the stored metadata is used for both reading and writing, so the
    /// payload type is never resolved through reflection and needs no dynamic code.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="valueTypeInfo"/> is <c>null</c>.</exception>
    public ResultJsonConverter(JsonTypeInfo<T> valueTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(valueTypeInfo);

        _valueTypeInfo = valueTypeInfo;
    }

    /// <inheritdoc />
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
            else if (_valueTypeInfo is { } valueTypeInfo)
            {
                JsonSerializer.Serialize(writer, inner, valueTypeInfo);
            }
            else
            {
                GetValueConverter(options).Write(writer, inner, options);
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
                value = _valueTypeInfo is { } valueTypeInfo
                    ? JsonSerializer.Deserialize(ref reader, valueTypeInfo)
                    : GetValueConverter(options).Read(ref reader, typeof(T), options);
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
        Justification = "Only the parameterless constructor reaches this path; it resolves an arbitrary T through JsonSerializerOptions.GetConverter. Native AOT callers use the JsonTypeInfo<T> constructor instead.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Only the parameterless constructor reaches this path; it resolves an arbitrary T through JsonSerializerOptions.GetConverter. Native AOT callers use the JsonTypeInfo<T> constructor instead.")]
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
