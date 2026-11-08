// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResulTus.Serialization;

/// <summary>
/// Serializes the unit <see cref="Result"/> as <c>{"isSuccess":true}</c> or
/// <c>{"isSuccess":false,"error":{...}}</c>.
/// </summary>
/// <remarks>
/// <c>isFailure</c> is never written: it is derivable from <c>isSuccess</c>, and two fields that can
/// disagree are a liability. A failure without an <c>error</c> is rejected on read rather than being
/// turned into <c>Error.None</c>.
/// </remarks>
public sealed class UnitResultJsonConverter : JsonConverter<Result>
{
    /// <summary>
    /// Writes the unit <see cref="Result"/> as a JSON object.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">The serializer options in effect.</param>
    public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteBoolean("isSuccess", value.IsSuccess);

        if (value.TryGetError(out var error))
        {
            writer.WritePropertyName("error");
            ErrorJsonConverter.WriteValue(writer, error);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Reads the unit <see cref="Result"/> from a JSON object.
    /// </summary>
    /// <param name="reader">The reader positioned at the object.</param>
    /// <param name="typeToConvert">The type being converted.</param>
    /// <param name="options">The serializer options in effect.</param>
    /// <returns>The deserialized <see cref="Result"/>.</returns>
    /// <exception cref="JsonException">A required property is missing or malformed.</exception>
    public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected a Result object, found {reader.TokenType}.");
        }

        bool? isSuccess = null;
        Error? error = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return isSuccess switch
                {
                    true => Result.Success(),
                    false => Result.Failure(
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
                isSuccess = ReadBoolean(ref reader, "isSuccess");
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

    /// <summary>
    /// Reads a boolean property, rejecting anything else with a <see cref="JsonException"/>.
    /// </summary>
    /// <param name="reader">The reader positioned at the value.</param>
    /// <param name="propertyName">The property name, used in the error message.</param>
    /// <returns>The boolean value.</returns>
    /// <exception cref="JsonException">The token is not <c>true</c> or <c>false</c>.</exception>
    internal static bool ReadBoolean(ref Utf8JsonReader reader, string propertyName) => reader.TokenType switch
    {
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        _ => throw new JsonException(
            $"Expected 'true' or 'false' for '{propertyName}', found {reader.TokenType}."),
    };
}
