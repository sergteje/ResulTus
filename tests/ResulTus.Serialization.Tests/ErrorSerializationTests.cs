// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;

namespace ResulTus.Serialization.Tests;

/// <summary>
/// Tests for the <see cref="ErrorType"/> and <see cref="Error"/> converters.
/// </summary>
public class ErrorSerializationTests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions().AddResulTusConverters();

    /// <summary>
    /// GIVEN <c>ErrorType.NotFound</c>
    /// WHEN it is serialized
    /// THEN the JSON MUST be the quoted enum name <c>"NotFound"</c>, not a number.
    /// </summary>
    [Fact]
    public void ErrorType_ShouldSerializeAsName()
    {
        var json = JsonSerializer.Serialize(ErrorType.NotFound, Options);

        json.Should().Be("\"NotFound\"");
    }

    /// <summary>
    /// GIVEN the JSON <c>"Conflict"</c>
    /// WHEN it is deserialized as an <see cref="ErrorType"/>
    /// THEN it MUST yield <c>ErrorType.Conflict</c>.
    /// </summary>
    [Fact]
    public void ErrorType_ShouldDeserializeFromName()
    {
        var type = JsonSerializer.Deserialize<ErrorType>("\"Conflict\"", Options);

        type.Should().Be(ErrorType.Conflict);
    }

    /// <summary>
    /// GIVEN the JSON <c>"notfound"</c> in lower case
    /// WHEN it is deserialized as an <see cref="ErrorType"/>
    /// THEN it MUST still yield <c>ErrorType.NotFound</c>.
    /// </summary>
    [Fact]
    public void ErrorType_ShouldDeserializeCaseInsensitively()
    {
        var type = JsonSerializer.Deserialize<ErrorType>("\"notfound\"", Options);

        type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// GIVEN the JSON number <c>3</c>
    /// WHEN it is deserialized as an <see cref="ErrorType"/>
    /// THEN it MUST throw <see cref="JsonException"/>, because a raw ordinal breaks when the enum is reordered.
    /// </summary>
    [Fact]
    public void ErrorType_ShouldRejectNumericValue()
    {
        var act = () => JsonSerializer.Deserialize<ErrorType>("3", Options);

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// GIVEN the JSON string <c>"NotARealType"</c>
    /// WHEN it is deserialized as an <see cref="ErrorType"/>
    /// THEN it MUST throw <see cref="JsonException"/> instead of defaulting silently.
    /// </summary>
    [Fact]
    public void ErrorType_ShouldRejectUnknownName()
    {
        var act = () => JsonSerializer.Deserialize<ErrorType>("\"NotARealType\"", Options);

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// GIVEN <c>Error.NotFound("missing", "not there")</c>
    /// WHEN it is serialized and deserialized again
    /// THEN the code, message and type MUST survive unchanged.
    /// </summary>
    [Fact]
    public void Error_ShouldRoundTrip()
    {
        var error = Error.NotFound("missing", "not there");

        var json = JsonSerializer.Serialize(error, Options);
        var back = JsonSerializer.Deserialize<Error>(json, Options);

        back.Should().Be(error);
        back.Code.Should().Be("missing");
        back.Message.Should().Be("not there");
        back.Type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// GIVEN <c>Error.Timeout("slow")</c>, which has no message
    /// WHEN it is serialized
    /// THEN the JSON MUST omit the <c>message</c> property entirely.
    /// </summary>
    [Fact]
    public void Error_ShouldOmitNullMessage()
    {
        var json = JsonSerializer.Serialize(Error.Timeout("slow"), Options);

        json.Should().Be("{\"code\":\"slow\",\"type\":\"Timeout\"}");
    }

    /// <summary>
    /// GIVEN <c>Error.None</c>, whose code is the empty string
    /// WHEN it is serialized and deserialized again
    /// THEN it MUST equal <c>Error.None</c>.
    /// </summary>
    [Fact]
    public void Error_None_ShouldRoundTrip()
    {
        var json = JsonSerializer.Serialize(Error.None, Options);
        var back = JsonSerializer.Deserialize<Error>(json, Options);

        back.Should().Be(Error.None);
    }

    /// <summary>
    /// GIVEN an error object missing its <c>code</c> and <c>type</c>
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/> rather than manufacturing <c>Error.None</c>.
    /// </summary>
    [Fact]
    public void Error_ShouldRequireCodeAndType()
    {
        var missingCode = () => JsonSerializer.Deserialize<Error>("{\"type\":\"NotFound\"}", Options);
        var missingType = () => JsonSerializer.Deserialize<Error>("{\"code\":\"missing\"}", Options);

        missingCode.Should().Throw<JsonException>();
        missingType.Should().Throw<JsonException>();
    }

    /// <summary>
    /// GIVEN an undefined <see cref="ErrorType"/> value such as <c>(ErrorType)99</c>
    /// WHEN it is serialized
    /// THEN it MUST throw <see cref="JsonException"/> instead of writing a name that cannot be read back.
    /// </summary>
    [Fact]
    public void ErrorType_ShouldRejectUndefinedValueOnWrite()
    {
        var act = () => JsonSerializer.Serialize((ErrorType)99, Options);

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// GIVEN an error object carrying an extra property the wire format does not define
    /// WHEN it is deserialized
    /// THEN the known properties MUST be honoured and the unknown one skipped, so older
    /// readers tolerate newer payloads.
    /// </summary>
    [Fact]
    public void Error_ShouldSkipUnknownProperties()
    {
        var json = "{\"code\":\"missing\",\"extra\":{\"nested\":true},\"type\":\"NotFound\"}";

        var back = JsonSerializer.Deserialize<Error>(json, Options);

        back.Should().Be(Error.NotFound("missing"));
    }

    /// <summary>
    /// GIVEN a non-object JSON token
    /// WHEN it is deserialized as an <see cref="Error"/>
    /// THEN it MUST throw <see cref="JsonException"/>.
    /// </summary>
    [Fact]
    public void Error_ShouldRejectNonObjectToken()
    {
        var act = () => JsonSerializer.Deserialize<Error>("42", Options);

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// Provides one instance per defined <see cref="ErrorType"/> so the factory mapping is proven total.
    /// </summary>
    /// <returns>One row per enum value paired with its expected error.</returns>
    public static IEnumerable<object[]> AllErrors()
    {
        yield return new object[] { Error.None };
        yield return new object[] { Error.Failure("failure", "general failure") };
        yield return new object[] { Error.Validation("validation", "invalid input") };
        yield return new object[] { Error.NotFound("missing", "not there") };
        yield return new object[] { Error.Conflict("duplicate", "already exists") };
        yield return new object[] { Error.Unauthorized("auth", "not allowed") };
        yield return new object[] { Error.Forbidden("forbidden", "denied") };
        yield return new object[] { Error.Timeout("slow", "took too long") };
        yield return new object[] { Error.Cancelled("cancelled", "aborted") };
        yield return new object[] { Error.Unexpected("unexpected", "surprise") };
    }

    /// <summary>
    /// GIVEN an <see cref="Error"/> of every defined <see cref="ErrorType"/>
    /// WHEN it is serialized and deserialized again
    /// THEN it MUST survive with its code, message and type intact, proving the
    /// <see cref="ErrorType"/> to factory mapping is total.
    /// </summary>
    /// <param name="error">The error under test.</param>
    [Theory]
    [MemberData(nameof(AllErrors))]
    public void EveryErrorType_ShouldMapToItsFactory(Error error)
    {
        var json = JsonSerializer.Serialize(error, Options);
        var back = JsonSerializer.Deserialize<Error>(json, Options);

        back.Should().Be(error);
        back.Type.Should().Be(error.Type);
    }
}
