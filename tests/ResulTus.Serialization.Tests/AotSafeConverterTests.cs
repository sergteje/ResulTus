// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ResulTus.Serialization.Tests;

/// <summary>
/// A small carrier payload: with a source-generated context this is the type handed to the serializer,
/// and its result member is handled by the <see cref="ResultJsonConverter{T}"/> registered on options.
/// </summary>
/// <param name="Name">The entity name.</param>
/// <param name="Age">The entity age, as a result.</param>
public sealed record EmployeePayload(string Name, Result<int> Age);

/// <summary>
/// A source-generated serializer context: the payload metadata is emitted at compile time, so the
/// <see cref="ResultJsonConverter{T}"/> path that consumes it needs no reflection.
/// </summary>
[JsonSerializable(typeof(EmployeePayload))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
internal partial class ResultConverterTestContext : JsonSerializerContext
{
}

/// <summary>
/// Tests for S7: the public <see cref="ResultJsonConverter{T}"/> and its two construction paths —
/// the ambient-options path the factory uses, and the source-generated metadata path suitable for
/// Native AOT.
/// </summary>
public class AotSafeConverterTests
{
    /// <summary>
    /// GIVEN the shipped converter type
    /// THEN it MUST be a public closed generic a consumer can instantiate and register.
    /// </summary>
    [Fact]
    public void Converter_ShouldBePublicAndClosed()
    {
        typeof(ResultJsonConverter<>).IsPublic.Should().BeTrue();
        typeof(ResultJsonConverter<int>).Should().BeAssignableTo<JsonConverter<Result<int>>>();
    }

    /// <summary>
    /// GIVEN a value converted through source-generated metadata and through the factory
    /// WHEN both serialize the same result
    /// THEN the JSON MUST be identical for both outcomes, so direct registration changes nothing on
    /// the wire.
    /// </summary>
    [Fact]
    public void ContextBoundConverter_ShouldProduceTheSameJsonAsTheFactory()
    {
        var factoryOptions = new JsonSerializerOptions().AddResulTusConverters();
        var directOptions = new JsonSerializerOptions();
        directOptions.Converters.Add(new ResultJsonConverter<int>(ResultConverterTestContext.Default.Int32));

        var success = Result<int>.Success(7);
        var failure = Result<int>.Failure(Error.Validation("bad", "not allowed"));

        JsonSerializer.Serialize(success, directOptions).Should()
            .Be(JsonSerializer.Serialize(success, factoryOptions));
        JsonSerializer.Serialize(failure, directOptions).Should()
            .Be(JsonSerializer.Serialize(failure, factoryOptions));
    }

    /// <summary>
    /// GIVEN a converter bound to a source-generated context
    /// WHEN a successful result is serialized and deserialized
    /// THEN the payload MUST round-trip through the context metadata.
    /// </summary>
    [Fact]
    public void ContextBoundConverter_ShouldRoundTripASuccess()
    {
        var options = CreateDirectOptions<int>();

        var json = JsonSerializer.Serialize(Result<int>.Success(7), options);
        var back = JsonSerializer.Deserialize<Result<int>>(json, options);

        json.Should().Be("{\"isSuccess\":true,\"value\":7}");
        back.IsSuccess.Should().BeTrue();
        back.GetValueOrDefault().Should().Be(7);
    }

    /// <summary>
    /// GIVEN a converter bound to a source-generated context
    /// WHEN a failed result is serialized and deserialized
    /// THEN the exact error MUST survive.
    /// </summary>
    [Fact]
    public void ContextBoundConverter_ShouldRoundTripAFailure()
    {
        var options = CreateDirectOptions<int>();
        var expected = Error.NotFound("user.missing", "no such user");
        var original = Result<int>.Failure(expected);

        var json = JsonSerializer.Serialize(original, options);
        var back = JsonSerializer.Deserialize<Result<int>>(json, options);

        json.Should().Be("{\"isSuccess\":false,\"error\":{\"code\":\"user.missing\",\"message\":\"no such user\",\"type\":\"NotFound\"}}");
        back.TryGetValue(out _, out var error).Should().BeFalse();
        error.Should().Be(expected);
    }

    /// <summary>
    /// GIVEN a converter bound to a source-generated context
    /// WHEN a successful result carries a <c>null</c> reference value
    /// THEN it MUST write a JSON <c>null</c> and read it back as a success carrying <c>null</c>.
    /// </summary>
    [Fact]
    public void ContextBoundConverter_ShouldRoundTripANullReferenceValue()
    {
        var options = CreateDirectOptions<string>();
        var original = Result<string>.Success(null!);

        var json = JsonSerializer.Serialize(original, options);
        var back = JsonSerializer.Deserialize<Result<string>>(json, options);

        json.Should().Be("{\"isSuccess\":true,\"value\":null}");
        back.IsSuccess.Should().BeTrue();
        back.GetValueOrDefault().Should().BeNull();
    }

    /// <summary>
    /// GIVEN a converter bound to a source-generated context
    /// WHEN the ambient options also carry a different converter for the payload type
    /// THEN the context metadata MUST win, proving the payload converter does not come from
    /// <see cref="JsonSerializerOptions.GetConverter(Type)"/>.
    /// </summary>
    [Fact]
    public void ContextBoundConverter_ShouldIgnoreTheAmbientPayloadConverter()
    {
        var options = CreateDirectOptions<int>();
        options.Converters.Add(new OffsetIntJsonConverter(100));

        var json = JsonSerializer.Serialize(Result<int>.Success(5), options);
        var back = JsonSerializer.Deserialize<Result<int>>(json, options);

        json.Should().Be("{\"isSuccess\":true,\"value\":5}");
        back.GetValueOrDefault().Should().Be(5);
    }

    /// <summary>
    /// GIVEN a converter bound to a source-generated context
    /// WHEN the type handed to the serializer is a payload that the context knows and the result is a
    /// member of it
    /// THEN the whole payload MUST round-trip and the JSON MUST not change next to the factory path.
    /// </summary>
    /// <remarks>
    /// With a source-generated context the type handed to <c>JsonSerializer.Serialize</c> must be in the
    /// context; a result nested inside it is resolved from <c>options.Converters</c>, which is exactly
    /// what a Native AOT application registers here.
    /// </remarks>
    [Fact]
    public void SourceGeneratedContext_ShouldRoundTripACarrierPayload()
    {
        var resolverOptions = CreateResolverOptions();
        var factoryOptions = new JsonSerializerOptions().AddResulTusConverters();
        var payload = new EmployeePayload("Ada", Result<int>.Success(36));

        var json = JsonSerializer.Serialize(payload, resolverOptions);
        var back = JsonSerializer.Deserialize<EmployeePayload>(json, resolverOptions);

        back.Should().NotBeNull();

        json.Should()
            .Be(JsonSerializer.Serialize(payload, factoryOptions))
            .And.Be("{\"Name\":\"Ada\",\"Age\":{\"isSuccess\":true,\"value\":36}}");
        back.Name.Should().Be("Ada");
        back.Age.GetValueOrDefault().Should().Be(36);
    }

    /// <summary>
    /// GIVEN a resolver-backed options over a source-generated context
    /// WHEN the ambient options carry a different converter for the payload type
    /// THEN the generated member metadata MUST still resolve the result through the directly
    /// registered converter and its bound context metadata, not through the ambient converter.
    /// </summary>
    [Fact]
    public void SourceGeneratedContext_ShouldIgnoreTheAmbientPayloadConverter()
    {
        var resolverOptions = CreateResolverOptions();
        resolverOptions.Converters.Add(new OffsetIntJsonConverter(100));
        var payload = new EmployeePayload("Ada", Result<int>.Success(36));

        var json = JsonSerializer.Serialize(payload, resolverOptions);
        var back = JsonSerializer.Deserialize<EmployeePayload>(json, resolverOptions);

        back.Should().NotBeNull();
        json.Should().Be("{\"Name\":\"Ada\",\"Age\":{\"isSuccess\":true,\"value\":36}}");
        back!.Age.IsSuccess.Should().BeTrue();
        back.Age.GetValueOrDefault().Should().Be(36);
    }

    /// <summary>
    /// GIVEN a <c>null</c> metadata argument
    /// WHEN the converter is constructed
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_WithNullMetadata_ShouldThrow()
    {
        var act = () => new ResultJsonConverter<int>((JsonTypeInfo<int>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN the parameterless converter the factory instantiates
    /// WHEN the ambient options carry a converter for the payload type
    /// THEN that ambient converter MUST be honored, preserving the pre-existing behavior.
    /// </summary>
    [Fact]
    public void ParameterlessConverter_ShouldResolveTheAmbientPayloadConverter()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new OffsetIntJsonConverter(100));
        options.Converters.Add(new ResultJsonConverter<int>());

        var json = JsonSerializer.Serialize(Result<int>.Success(5), options);
        var back = JsonSerializer.Deserialize<Result<int>>(json, options);

        json.Should().Be("{\"isSuccess\":true,\"value\":105}");
        back.GetValueOrDefault().Should().Be(5);
    }

    private static JsonSerializerOptions CreateDirectOptions<T>()
        => new()
        {
            Converters = { new ResultJsonConverter<T>(GetTypeInfo<T>()) },
        };

    private static JsonSerializerOptions CreateResolverOptions()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = ResultConverterTestContext.Default };
        options.Converters.Add(new ResultJsonConverter<int>(ResultConverterTestContext.Default.Int32));
        return options;
    }

    private static JsonTypeInfo<T> GetTypeInfo<T>() => typeof(T) switch
    {
        var type when type == typeof(int) => (JsonTypeInfo<T>)(object)ResultConverterTestContext.Default.Int32,
        var type when type == typeof(string) => (JsonTypeInfo<T>)(object)ResultConverterTestContext.Default.String,
        _ => throw new InvalidOperationException($"No probe metadata for {typeof(T)}."),
    };

    private sealed class OffsetIntJsonConverter(int offset) : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetInt32() - offset;

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value + offset);
    }
}
