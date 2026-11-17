// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ResulTus.Serialization;

/// <summary>
/// Serializes a closed <see cref="Result{TSuccess, TError}"/> as
/// <c>{"isSuccess":true,"value":...}</c> or <c>{"isSuccess":false,"error":...}</c>.
/// </summary>
/// <typeparam name="TSuccess">The type of the success value.</typeparam>
/// <typeparam name="TError">The type of the error value.</typeparam>
/// <remarks>
/// <para>
/// The wire shape is the one the rest of the package already speaks: <c>isSuccess</c> plus
/// <c>value</c> on success or <c>error</c> on failure. Each payload is written and read by the
/// converter registered for <typeparamref name="TSuccess"/> and <typeparamref name="TError"/> on the
/// <see cref="JsonSerializerOptions"/> in effect, so <c>Result&lt;int, Error&gt;</c> carries the same
/// error object as <see cref="Result{T}"/>, and <c>Result&lt;int, string&gt;</c> carries a JSON string.
/// The three property names are fixed and do not follow <see cref="JsonSerializerOptions.PropertyNamingPolicy"/>.
/// </para>
/// <para>
/// There are two ways to build this converter, and the difference is exactly the difference between
/// what a reflection-based serializer can resolve and what Native AOT can.
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Ambient options — not AOT-safe.</b> The parameterless constructor resolves the converters for
/// <typeparamref name="TSuccess"/> and <typeparamref name="TError"/> from the
/// <see cref="JsonSerializerOptions"/> in effect, which is what
/// <see cref="ResultJsonConverterFactory"/> and <c>AddResulTusConverters()</c> use. It works in
/// ordinary and trimmed applications, and the wire shape is unchanged, but the lookup is annotated as
/// requiring dynamic code and unreferenced code.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Source-generated metadata — AOT-suitable.</b> The
/// <see cref="ResultJsonConverter{TSuccess, TError}(JsonTypeInfo{TSuccess}, JsonTypeInfo{TError})"/>
/// constructor stores the metadata the source generator emitted for both generic arguments and never
/// consults the ambient options for them, so a Native AOT consumer never reflects over
/// <typeparamref name="TError"/>. Register the converter directly:
/// <c>options.Converters.Add(new ResultJsonConverter&lt;MyDto, MyError&gt;(MyContext.Default.MyDto, MyContext.Default.MyError));</c>
/// where <c>MyContext</c> is a <see cref="JsonSerializerContext"/> declaring
/// <c>[JsonSerializable(typeof(MyDto))]</c> and <c>[JsonSerializable(typeof(MyError))]</c>. Keep the
/// closed <see cref="Result{TSuccess, TError}"/> reachable from a carrier payload in that context,
/// exactly as for <see cref="ResultJsonConverter{T}"/>.
/// </description>
/// </item>
/// </list>
/// <para>
/// Both paths emit the same JSON. Reading is as strict as the rest of the package: an object with no
/// <c>isSuccess</c>, a success with no <c>value</c>, or a failure with no <c>error</c> throws
/// <see cref="JsonException"/>. Unknown properties are skipped.
/// </para>
/// </remarks>
public sealed class ResultJsonConverter<TSuccess, TError> : JsonConverter<Result<TSuccess, TError>>
{
    private readonly JsonTypeInfo<TSuccess>? _successTypeInfo;
    private readonly JsonTypeInfo<TError>? _errorTypeInfo;
    private ConverterCache? _cachedConverters;

    /// <summary>
    /// Initializes a converter that resolves the payload converters from the
    /// <see cref="JsonSerializerOptions"/> in effect at each call.
    /// </summary>
    /// <remarks>
    /// This is the constructor <see cref="ResultJsonConverterFactory"/> instantiates. Prefer
    /// <see cref="ResultJsonConverter{TSuccess, TError}(JsonTypeInfo{TSuccess}, JsonTypeInfo{TError})"/>
    /// for Native AOT.
    /// </remarks>
    public ResultJsonConverter()
    {
    }

    /// <summary>
    /// Initializes a converter bound to source-generated metadata for both generic arguments.
    /// </summary>
    /// <param name="successTypeInfo">
    /// The metadata for <typeparamref name="TSuccess"/> from a <see cref="JsonSerializerContext"/>
    /// declaring <c>[JsonSerializable(typeof(TSuccess))]</c>.
    /// </param>
    /// <param name="errorTypeInfo">
    /// The metadata for <typeparamref name="TError"/> from a <see cref="JsonSerializerContext"/>
    /// declaring <c>[JsonSerializable(typeof(TError))]</c>.
    /// </param>
    /// <remarks>
    /// This is the Native AOT path: the stored metadata is used for both reading and writing, so
    /// neither payload type is resolved through reflection and no dynamic code is required. When a
    /// payload type has its own converter registered on the context's options — <see cref="Error"/>,
    /// for example — that converter is the one the metadata resolves.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="successTypeInfo"/> or <paramref name="errorTypeInfo"/> is <c>null</c>.
    /// </exception>
    public ResultJsonConverter(
        JsonTypeInfo<TSuccess> successTypeInfo,
        JsonTypeInfo<TError> errorTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(successTypeInfo);
        ArgumentNullException.ThrowIfNull(errorTypeInfo);

        _successTypeInfo = successTypeInfo;
        _errorTypeInfo = errorTypeInfo;
    }

    /// <summary>
    /// Writes the <see cref="Result{TSuccess, TError}"/> as a JSON object.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">
    /// The serializer options in effect. It supplies the payload converters when this instance was
    /// built with the parameterless constructor, and is otherwise unused.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="writer"/> or <paramref name="options"/> is <c>null</c>.
    /// </exception>
    public override void Write(
        Utf8JsonWriter writer,
        Result<TSuccess, TError> value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);

        value.Deconstruct(out var isSuccess, out var successValue, out var error);

        writer.WriteStartObject();
        writer.WriteBoolean("isSuccess", isSuccess);

        if (isSuccess)
        {
            writer.WritePropertyName("value");

            if (successValue is null)
            {
                writer.WriteNullValue();
            }
            else if (_successTypeInfo is { } successTypeInfo)
            {
                JsonSerializer.Serialize(writer, successValue, successTypeInfo);
            }
            else
            {
                GetConverters(options).SuccessConverter.Write(writer, successValue, options);
            }
        }
        else
        {
            writer.WritePropertyName("error");

            if (error is null)
            {
                writer.WriteNullValue();
            }
            else if (_errorTypeInfo is { } errorTypeInfo)
            {
                JsonSerializer.Serialize(writer, error, errorTypeInfo);
            }
            else
            {
                GetConverters(options).ErrorConverter.Write(writer, error, options);
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Reads the <see cref="Result{TSuccess, TError}"/> from a JSON object.
    /// </summary>
    /// <param name="reader">The reader positioned at the object.</param>
    /// <param name="typeToConvert">The type being converted.</param>
    /// <param name="options">
    /// The serializer options in effect. It supplies the payload converters when this instance was
    /// built with the parameterless constructor, and is otherwise unused.
    /// </param>
    /// <returns>The deserialized <see cref="Result{TSuccess, TError}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="JsonException">
    /// The JSON is not an object, the required <c>isSuccess</c> property is missing, a success carries
    /// no <c>value</c>, or a failure carries no <c>error</c>.
    /// </exception>
    public override Result<TSuccess, TError> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected a Result object, found {reader.TokenType}.");
        }

        bool? isSuccess = null;
        var hasValue = false;
        TSuccess? value = default;
        var hasError = false;
        TError? error = default;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (isSuccess is null)
                {
                    throw new JsonException("The Result object is missing the required 'isSuccess' property.");
                }

                if (isSuccess.Value)
                {
                    return hasValue
                        ? Result<TSuccess, TError>.Success(value!)
                        : throw new JsonException("The Result object reports success but carries no 'value'.");
                }

                return hasError
                    ? Result<TSuccess, TError>.Failure(error!)
                    : throw new JsonException("The Result object reports failure but carries no 'error'.");
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
                value = _successTypeInfo is { } successTypeInfo
                    ? JsonSerializer.Deserialize(ref reader, successTypeInfo)
                    : GetConverters(options).SuccessConverter.Read(ref reader, typeof(TSuccess), options);
                hasValue = true;
            }
            else if (reader.ValueTextEquals("error"))
            {
                reader.Read();
                error = _errorTypeInfo is { } errorTypeInfo
                    ? JsonSerializer.Deserialize(ref reader, errorTypeInfo)
                    : GetConverters(options).ErrorConverter.Read(ref reader, typeof(TError), options);
                hasError = true;
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
        Justification = "Only the parameterless constructor reaches this path; it resolves arbitrary generic arguments through JsonSerializerOptions.GetConverter. Native AOT callers use the JsonTypeInfo<TSuccess>, JsonTypeInfo<TError> constructor instead.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Only the parameterless constructor reaches this path; it resolves arbitrary generic arguments through JsonSerializerOptions.GetConverter. Native AOT callers use the JsonTypeInfo<TSuccess>, JsonTypeInfo<TError> constructor instead.")]
    private ConverterCache GetConverters(JsonSerializerOptions options)
    {
        var cached = _cachedConverters;
        if (cached is not null && ReferenceEquals(cached.Options, options))
        {
            return cached;
        }

        var created = new ConverterCache(
            options,
            (JsonConverter<TSuccess>)options.GetConverter(typeof(TSuccess)),
            (JsonConverter<TError>)options.GetConverter(typeof(TError)));
        _cachedConverters = created;
        return created;
    }

    private sealed class ConverterCache(
        JsonSerializerOptions options,
        JsonConverter<TSuccess> successConverter,
        JsonConverter<TError> errorConverter)
    {
        public JsonSerializerOptions Options { get; } = options;

        public JsonConverter<TSuccess> SuccessConverter { get; } = successConverter;

        public JsonConverter<TError> ErrorConverter { get; } = errorConverter;
    }
}
