// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResulTus.Serialization;

/// <summary>
/// Serializes <see cref="Error"/> as <c>{"code","message","type"}</c>.
/// </summary>
/// <remarks>
/// <c>code</c> and <c>type</c> are required on read, and <c>message</c> is omitted from the output
/// when it is <c>null</c>. <see cref="Error"/> has a private constructor, so deserialization maps
/// <see cref="ErrorType"/> back to its static factory. An <see cref="ErrorType"/> with no factory
/// mapping throws <see cref="JsonException"/> instead of degrading silently.
/// </remarks>
public sealed class ErrorJsonConverter : JsonConverter<Error>
{
    /// <summary>
    /// Writes the <see cref="Error"/> as a JSON object.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">The serializer options in effect.</param>
    public override void Write(Utf8JsonWriter writer, Error value, JsonSerializerOptions options)
        => WriteValue(writer, value);

    /// <summary>
    /// Reads an <see cref="Error"/> from a JSON object.
    /// </summary>
    /// <param name="reader">The reader positioned at the object.</param>
    /// <param name="typeToConvert">The type being converted.</param>
    /// <param name="options">The serializer options in effect.</param>
    /// <returns>The deserialized <see cref="Error"/>.</returns>
    /// <exception cref="JsonException">A required property is missing or malformed.</exception>
    public override Error Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ReadValue(ref reader);

    /// <summary>
    /// Writes an <see cref="Error"/>, sharing the exact format used by the result converters.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="error">The error to write.</param>
    internal static void WriteValue(Utf8JsonWriter writer, Error error)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteString("code", error.Code);

        if (error.Message is { } message)
        {
            writer.WriteString("message", message);
        }

        writer.WritePropertyName("type");
        ErrorTypeJsonConverter.WriteValue(writer, error.Type);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Reads an <see cref="Error"/> from the object the reader is positioned at.
    /// </summary>
    /// <param name="reader">The reader positioned at the object.</param>
    /// <returns>The deserialized <see cref="Error"/>.</returns>
    /// <exception cref="JsonException">A required property is missing or malformed.</exception>
    internal static Error ReadValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected an Error object, found {reader.TokenType}.");
        }

        string? code = null;
        string? message = null;
        ErrorType? type = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (code is null)
                {
                    throw new JsonException("The Error object is missing the required 'code' property.");
                }

                if (type is not { } parsedType)
                {
                    throw new JsonException("The Error object is missing the required 'type' property.");
                }

                return Create(code, message, parsedType);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Unexpected token {reader.TokenType} while reading an Error.");
            }

            if (reader.ValueTextEquals("code"))
            {
                reader.Read();
                code = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            }
            else if (reader.ValueTextEquals("message"))
            {
                reader.Read();
                message = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            }
            else if (reader.ValueTextEquals("type"))
            {
                reader.Read();
                type = ErrorTypeJsonConverter.ReadValue(ref reader);
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON while reading an Error.");
    }

    private static Error Create(string code, string? message, ErrorType type) => type switch
    {
        ErrorType.None => code.Length == 0 && message is null
            ? Error.None
            : throw new JsonException("An error of type None cannot carry a code or a message."),
        ErrorType.Failure => Error.Failure(code, message),
        ErrorType.Validation => Error.Validation(code, message),
        ErrorType.NotFound => Error.NotFound(code, message),
        ErrorType.Conflict => Error.Conflict(code, message),
        ErrorType.Unauthorized => Error.Unauthorized(code, message),
        ErrorType.Forbidden => Error.Forbidden(code, message),
        ErrorType.Timeout => Error.Timeout(code, message),
        ErrorType.Cancelled => Error.Cancelled(code, message),
        ErrorType.Unexpected => Error.Unexpected(code, message),
        _ => throw new JsonException($"The value {(byte)type} is not a defined {nameof(ErrorType)}."),
    };
}
