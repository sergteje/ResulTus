// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResulTus.Serialization;

/// <summary>
/// Serializes <see cref="ErrorType"/> as its enum name instead of a number.
/// </summary>
/// <remarks>
/// Names are self-describing and survive the enum being reordered; a raw ordinal does not.
/// Reading rejects numbers and unknown names with <see cref="JsonException"/> rather than
/// degrading to a default value. The name is matched case-insensitively.
/// </remarks>
public sealed class ErrorTypeJsonConverter : JsonConverter<ErrorType>
{
    private const int MaximumNameLength = 12;

    private static readonly Dictionary<ErrorType, string> Names = BuildNames();
    private static readonly Dictionary<string, ErrorType> Values = BuildValues();

    /// <summary>
    /// Writes the <see cref="ErrorType"/> as a JSON string holding its canonical enum name.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">The serializer options in effect.</param>
    /// <exception cref="JsonException">The value is not a defined <see cref="ErrorType"/>.</exception>
    public override void Write(Utf8JsonWriter writer, ErrorType value, JsonSerializerOptions options)
        => WriteValue(writer, value);

    /// <summary>
    /// Reads an <see cref="ErrorType"/> from a JSON string holding its enum name.
    /// </summary>
    /// <param name="reader">The reader positioned at the value.</param>
    /// <param name="typeToConvert">The type being converted.</param>
    /// <param name="options">The serializer options in effect.</param>
    /// <returns>The parsed <see cref="ErrorType"/>.</returns>
    /// <exception cref="JsonException">The token is not a string, or the name is not a defined <see cref="ErrorType"/>.</exception>
    public override ErrorType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ReadValue(ref reader);

    /// <summary>
    /// Writes an <see cref="ErrorType"/> as its enum name, sharing the exact format used by
    /// <see cref="ErrorJsonConverter"/> so nested and standalone output always agree.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to write.</param>
    /// <exception cref="JsonException">The value is not a defined <see cref="ErrorType"/>.</exception>
    internal static void WriteValue(Utf8JsonWriter writer, ErrorType value)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (!Names.TryGetValue(value, out var name))
        {
            throw new JsonException($"The value {(byte)value} is not a defined {nameof(ErrorType)}.");
        }

        writer.WriteStringValue(name);
    }

    /// <summary>
    /// Reads an <see cref="ErrorType"/> from the token the reader is positioned at.
    /// </summary>
    /// <param name="reader">The reader positioned at the value.</param>
    /// <returns>The parsed <see cref="ErrorType"/>.</returns>
    /// <exception cref="JsonException">The token is not a string, or the name is not a defined <see cref="ErrorType"/>.</exception>
    internal static ErrorType ReadValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                $"Expected a {nameof(ErrorType)} name string, found {reader.TokenType}. Numbers are rejected on purpose.");
        }

        if (!reader.ValueIsEscaped)
        {
            if (reader.HasValueSequence)
            {
                var length = reader.ValueSequence.Length;
                if (length <= MaximumNameLength)
                {
                    Span<byte> nameBuffer = stackalloc byte[MaximumNameLength];
                    reader.ValueSequence.CopyTo(nameBuffer);
                    if (TryGetValue(nameBuffer[..(int)length], out var sequenceValue))
                    {
                        return sequenceValue;
                    }
                }
            }
            else if (TryGetValue(reader.ValueSpan, out var spanValue))
            {
                return spanValue;
            }
        }

        var name = reader.GetString();
        if (name is null || !Values.TryGetValue(name, out var value))
        {
            throw new JsonException($"'{name}' is not a defined {nameof(ErrorType)} name.");
        }

        return value;
    }

    private static bool TryGetValue(ReadOnlySpan<byte> name, out ErrorType value)
    {
        if (Ascii.EqualsIgnoreCase(name, "None"u8))
        {
            value = ErrorType.None;
        }
        else if (Ascii.EqualsIgnoreCase(name, "Failure"u8))
        {
            value = ErrorType.Failure;
        }
        else if (Ascii.EqualsIgnoreCase(name, "Validation"u8))
        {
            value = ErrorType.Validation;
        }
        else if (Ascii.EqualsIgnoreCase(name, "NotFound"u8))
        {
            value = ErrorType.NotFound;
        }
        else if (Ascii.EqualsIgnoreCase(name, "Conflict"u8))
        {
            value = ErrorType.Conflict;
        }
        else if (Ascii.EqualsIgnoreCase(name, "Unauthorized"u8))
        {
            value = ErrorType.Unauthorized;
        }
        else if (Ascii.EqualsIgnoreCase(name, "Forbidden"u8))
        {
            value = ErrorType.Forbidden;
        }
        else if (Ascii.EqualsIgnoreCase(name, "Timeout"u8))
        {
            value = ErrorType.Timeout;
        }
        else if (Ascii.EqualsIgnoreCase(name, "Cancelled"u8))
        {
            value = ErrorType.Cancelled;
        }
        else if (Ascii.EqualsIgnoreCase(name, "Unexpected"u8))
        {
            value = ErrorType.Unexpected;
        }
        else
        {
            value = default;
            return false;
        }

        return true;
    }

    private static Dictionary<ErrorType, string> BuildNames()
    {
        var values = Enum.GetValues<ErrorType>();
        var map = new Dictionary<ErrorType, string>(values.Length);
        foreach (var value in values)
        {
            map[value] = value.ToString();
        }

        return map;
    }

    private static Dictionary<string, ErrorType> BuildValues()
    {
        var values = Enum.GetValues<ErrorType>();
        var map = new Dictionary<string, ErrorType>(values.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            map[value.ToString()] = value;
        }

        return map;
    }
}
