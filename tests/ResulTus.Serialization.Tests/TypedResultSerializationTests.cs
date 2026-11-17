// Copyright (c) ResulTus. All rights reserved.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ResulTus.Serialization.Tests;

/// <summary>
/// Source-generated metadata for the typed-tier tests: the success type, a plain string error type
/// and the structured <see cref="Error"/> the real contracts use.
/// </summary>
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Error))]
internal partial class TypedResultConverterTestContext : JsonSerializerContext
{
}

/// <summary>
/// Tests for the <see cref="Result{TSuccess, TError}"/> converter: the wire format it shares with
/// the rest of the package, the strict reading rules, and the two construction paths.
/// </summary>
public class TypedResultSerializationTests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions().AddResulTusConverters();

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int, Error&gt;</c>
    /// WHEN it is serialized and deserialized through the converters registered by
    /// <c>AddResulTusConverters()</c>
    /// THEN the JSON MUST carry <c>isSuccess</c> and the value, and the value MUST survive.
    /// </summary>
    [Fact]
    public void Success_ShouldRoundTripWithTheSharedWireFormat()
    {
        var original = Result<int, Error>.Success(42);

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<Result<int, Error>>(json, Options);

        json.Should().Be("{\"isSuccess\":true,\"value\":42}");
        back.IsSuccess.Should().BeTrue();
        back.Match(onSuccess: value => value, onFailure: _ => -1).Should().Be(42);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int, Error&gt;</c>
    /// WHEN it is serialized and deserialized through the registered converters
    /// THEN the JSON MUST carry the structured error and no value, and the whole error MUST survive.
    /// </summary>
    [Fact]
    public void Failure_ShouldRoundTripAnErrorPayload()
    {
        var expected = Error.NotFound("USER_NOT_FOUND", "No such user");
        var original = Result<int, Error>.Failure(expected);

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<Result<int, Error>>(json, Options);

        json.Should().Be(
            "{\"isSuccess\":false,\"error\":{\"code\":\"USER_NOT_FOUND\",\"message\":\"No such user\",\"type\":\"NotFound\"}}");
        back.IsFailure.Should().BeTrue();
        back.Match(onSuccess: _ => Error.None, onFailure: error => error).Should().Be(expected);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int, string&gt;</c>
    /// WHEN it is serialized and deserialized
    /// THEN the error MUST be written by the converter the options registered for <c>string</c>,
    /// proving <c>TError</c> is not special-cased to <see cref="Error"/>.
    /// </summary>
    [Fact]
    public void Failure_ShouldRoundTripAPlainStringError()
    {
        var original = Result<int, string>.Failure("boom");

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<Result<int, string>>(json, Options);

        json.Should().Be("{\"isSuccess\":false,\"error\":\"boom\"}");
        back.IsFailure.Should().BeTrue();
        back.Match(onSuccess: _ => string.Empty, onFailure: error => error).Should().Be("boom");
    }

    /// <summary>
    /// GIVEN <c>default(Result&lt;int, Error&gt;)</c>
    /// WHEN it is serialized and deserialized
    /// THEN it MUST be a failure carrying the "no error" value, because the default struct is a
    /// failure with <see cref="Error.None"/>, not a success hiding a <c>null</c>.
    /// </summary>
    [Fact]
    public void DefaultValue_ShouldRoundTripAsAFailureCarryingNoError()
    {
        var json = JsonSerializer.Serialize(default(Result<int, Error>), Options);
        var back = JsonSerializer.Deserialize<Result<int, Error>>(json, Options);

        json.Should().Be("{\"isSuccess\":false,\"error\":{\"code\":\"\",\"type\":\"None\"}}");
        back.IsFailure.Should().BeTrue();
        back.Match(onSuccess: _ => Error.Unexpected("unexpected"), onFailure: error => error)
            .Should().Be(Error.None);
    }

    /// <summary>
    /// GIVEN an empty result object
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/> naming the missing <c>isSuccess</c> property.
    /// </summary>
    [Fact]
    public void EmptyObject_ShouldThrowForTheMissingIsSuccess()
    {
        var act = () => JsonSerializer.Deserialize<Result<int, Error>>("{}", Options);

        act.Should().Throw<JsonException>()
            .WithMessage("*missing the required 'isSuccess' property*");
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;string, Error&gt;</c> carrying a <c>null</c> reference value
    /// WHEN it is serialized and deserialized
    /// THEN it MUST write a JSON <c>null</c> and read back as a success carrying <c>null</c>, not as
    /// a failure.
    /// </summary>
    [Fact]
    public void NullReferenceSuccess_ShouldRoundTripAsASuccess()
    {
        var json = JsonSerializer.Serialize(Result<string, Error>.Success(null!), Options);
        var back = JsonSerializer.Deserialize<Result<string, Error>>(json, Options);

        json.Should().Be("{\"isSuccess\":true,\"value\":null}");
        back.IsSuccess.Should().BeTrue();
        back.Match(onSuccess: payload => payload, onFailure: _ => "failed").Should().BeNull();
    }

    /// <summary>
    /// GIVEN an object that reports success and also carries an <c>error</c>
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/> because the success has no <c>value</c>,
    /// using the same message as the <c>Result&lt;T&gt;</c> converter.
    /// </summary>
    [Fact]
    public void SuccessWithError_ShouldThrowForTheMissingValue()
    {
        var act = () => JsonSerializer.Deserialize<Result<int, Error>>(
            "{\"isSuccess\":true,\"error\":{\"code\":\"x\",\"type\":\"NotFound\"}}",
            Options);

        act.Should().Throw<JsonException>()
            .WithMessage("*reports success but carries no 'value'*");
    }

    /// <summary>
    /// GIVEN an object that reports failure and also carries a <c>value</c>
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/> because the failure has no <c>error</c>,
    /// using the same message as the <c>Result&lt;T&gt;</c> converter.
    /// </summary>
    [Fact]
    public void FailureWithValue_ShouldThrowForTheMissingError()
    {
        var act = () => JsonSerializer.Deserialize<Result<int, Error>>(
            "{\"isSuccess\":false,\"value\":1}",
            Options);

        act.Should().Throw<JsonException>()
            .WithMessage("*reports failure but carries no 'error'*");
    }

    /// <summary>
    /// GIVEN a JSON <c>null</c> token
    /// WHEN it is deserialized as a typed result
    /// THEN it MUST throw <see cref="JsonException"/> instead of degrading to a default result.
    /// </summary>
    [Fact]
    public void NullPayload_ShouldThrow()
    {
        var act = () => JsonSerializer.Deserialize<Result<int, Error>>("null", Options);

        act.Should().Throw<JsonException>()
            .WithMessage("*Expected a Result object, found Null*");
    }

    /// <summary>
    /// GIVEN a converter built from source-generated metadata for both generic arguments
    /// WHEN it serializes the same results the registered path serializes
    /// THEN the bytes MUST be identical, so the Native AOT route changes nothing on the wire.
    /// </summary>
    [Fact]
    public void MetadataBoundConverter_ShouldProduceTheSameJsonAsTheRegisteredPath()
    {
        var metadataOptions = new JsonSerializerOptions();
        metadataOptions.Converters.Add(new ErrorJsonConverter());
        var context = new TypedResultConverterTestContext(metadataOptions);
        var converter = new ResultJsonConverter<int, Error>(context.Int32, context.Error);

        var success = Result<int, Error>.Success(7);
        var failure = Result<int, Error>.Failure(Error.Validation("bad", "not allowed"));

        var successJson = WriteThroughConverter(converter, success);
        var failureJson = WriteThroughConverter(converter, failure);

        successJson.Should().Be(JsonSerializer.Serialize(success, Options));
        failureJson.Should().Be(JsonSerializer.Serialize(failure, Options));

        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(successJson));
        reader.Read();
        var back = converter.Read(ref reader, typeof(Result<int, Error>), metadataOptions);
        back.Match(onSuccess: value => value, onFailure: _ => -1).Should().Be(7);
    }

    /// <summary>
    /// GIVEN a <c>null</c> metadata argument
    /// WHEN the converter is constructed
    /// THEN it MUST throw <see cref="ArgumentNullException"/> for either argument.
    /// </summary>
    [Fact]
    public void Constructor_WithNullMetadata_ShouldThrow()
    {
        var context = TypedResultConverterTestContext.Default;

        var nullSuccess = () => new ResultJsonConverter<int, Error>(
            (JsonTypeInfo<int>)null!, context.Error);
        var nullError = () => new ResultJsonConverter<int, Error>(
            context.Int32, (JsonTypeInfo<Error>)null!);

        nullSuccess.Should().Throw<ArgumentNullException>();
        nullError.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN the converters registered by <c>AddResulTusConverters()</c>
    /// WHEN a single-argument <c>Result&lt;int&gt;</c> is round-tripped
    /// THEN its pre-existing wire format MUST be unchanged, proving the added typed tier did not
    /// take over the one-argument tier.
    /// </summary>
    [Fact]
    public void SingleArgumentResult_ShouldKeepItsWireFormat()
    {
        var json = JsonSerializer.Serialize(Result<int>.Success(42), Options);
        var back = JsonSerializer.Deserialize<Result<int>>(json, Options);

        json.Should().Be("{\"isSuccess\":true,\"value\":42}");
        back.GetValueOrDefault().Should().Be(42);
    }

    private static string WriteThroughConverter(
        ResultJsonConverter<int, Error> converter,
        Result<int, Error> value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            converter.Write(writer, value, new JsonSerializerOptions());
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
