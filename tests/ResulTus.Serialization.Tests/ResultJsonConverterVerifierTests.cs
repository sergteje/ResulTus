// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ResulTus.Serialization.Tests;

/// <summary>Carrier payload for the S7/S8 probe pins: its members are what make the closed
/// <see cref="Result{T}"/> types reachable in the generated context.</summary>
/// <param name="Name">The entity name.</param>
/// <param name="Age">The age result.</param>
/// <param name="Nickname">The nickname result over a nullable reference type.</param>
public sealed record VerifierCarrier(string Name, Result<int> Age, Result<string?> Nickname);

/// <summary>A value payload for the metadata-options probe.</summary>
/// <param name="Amount">The amount.</param>
public readonly record struct VerifierMoney(int Amount);

/// <summary>Context declaring the carrier and its payload scalars.</summary>
[JsonSerializable(typeof(VerifierCarrier))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
internal partial class VerifierContext : JsonSerializerContext
{
}

/// <summary>A context whose own naming policy differs from the ambient default.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(VerifierMoney))]
internal partial class VerifierCamelContext : JsonSerializerContext
{
}

/// <summary>A context with no result member anywhere in its reachable graph.</summary>
[JsonSerializable(typeof(int))]
internal partial class ScalarOnlyContext : JsonSerializerContext
{
}

/// <summary>
/// Independent probe pins for S7 (both converter paths, and the context reachability rule) and S8
/// (unchanged wire shape and read errors), promoted from the delegated verification of
/// `93a7667`.
/// </summary>
public class ResultJsonConverterVerifierTests
{
    private static readonly JsonSerializerOptions Factory = new JsonSerializerOptions().AddResulTusConverters();

    /// <summary>S7: the type is a public, sealed <c>JsonConverter&lt;Result&lt;T&gt;&gt;</c>.</summary>
    [Fact]
    public void Converter_ShouldBeAPublicClosedJsonConverter()
    {
        typeof(ResultJsonConverter<>).IsPublic.Should().BeTrue();
        typeof(ResultJsonConverter<int>).BaseType.Should().Be(typeof(JsonConverter<Result<int>>));
        typeof(ResultJsonConverter<int>).IsSealed.Should().BeTrue();
    }

    /// <summary>S7/S8: the parameterless path resolves and caches the ambient converter per options instance.</summary>
    [Fact]
    public void ParameterlessPath_ShouldCacheTheAmbientConverterPerOptionsInstance()
    {
        var shared = new ResultJsonConverter<int>();
        var first = new JsonSerializerOptions();
        first.Converters.Add(new OffsetIntJsonConverter(100));
        first.Converters.Add(shared);
        var second = new JsonSerializerOptions();
        second.Converters.Add(new OffsetIntJsonConverter(200));
        second.Converters.Add(shared);

        JsonSerializer.Serialize(Result<int>.Success(5), first).Should().Be("{\"isSuccess\":true,\"value\":105}");
        JsonSerializer.Serialize(Result<int>.Success(5), second).Should().Be("{\"isSuccess\":true,\"value\":205}");
        JsonSerializer.Serialize(Result<int>.Success(5), first).Should().Be("{\"isSuccess\":true,\"value\":105}");
        JsonSerializer.Deserialize<Result<int>>("{\"isSuccess\":true,\"value\":105}", first)
            .GetValueOrDefault().Should().Be(5);
    }

    /// <summary>S7: the metadata path never consults the ambient payload converter, on read or write.</summary>
    [Fact]
    public void MetadataPath_ShouldIgnoreAPoisonedAmbientPayloadConverter()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PoisonIntJsonConverter());
        options.Converters.Add(new ResultJsonConverter<int>(VerifierContext.Default.Int32));

        var json = JsonSerializer.Serialize(Result<int>.Success(5), options);
        var back = JsonSerializer.Deserialize<Result<int>>(json, options);

        json.Should().Be("{\"isSuccess\":true,\"value\":5}");
        back.GetValueOrDefault().Should().Be(5);
    }

    /// <summary>S7: null reference values and default payloads round-trip.</summary>
    [Fact]
    public void MetadataPath_ShouldHandleNullAndDefaultPayloads()
    {
        var stringOptions = new JsonSerializerOptions();
        stringOptions.Converters.Add(new ResultJsonConverter<string>(VerifierContext.Default.String));
        var stringJson = JsonSerializer.Serialize(Result<string>.Success(null!), stringOptions);
        var stringBack = JsonSerializer.Deserialize<Result<string>>(stringJson, stringOptions)!;

        var intOptions = new JsonSerializerOptions();
        intOptions.Converters.Add(new ResultJsonConverter<int>(VerifierContext.Default.Int32));
        var intJson = JsonSerializer.Serialize(Result<int>.Success(default), intOptions);

        stringJson.Should().Be("{\"isSuccess\":true,\"value\":null}");
        stringBack.IsSuccess.Should().BeTrue();
        stringBack.GetValueOrDefault().Should().BeNull();
        intJson.Should().Be("{\"isSuccess\":true,\"value\":0}");
    }

    /// <summary>S7: a null metadata argument is rejected.</summary>
    [Fact]
    public void Constructor_WithNullMetadata_ShouldThrowArgumentNullException()
    {
        var act = () => new ResultJsonConverter<int>((JsonTypeInfo<int>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("valueTypeInfo");
    }

    /// <summary>S7: Error.None and default(Error) stay failures on the metadata path.</summary>
    [Fact]
    public void MetadataPath_ShouldPreserveSentinelErrorsAsFailures()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ResultJsonConverter<int>(VerifierContext.Default.Int32));

        foreach (var error in new[] { Error.None, default(Error) })
        {
            var json = JsonSerializer.Serialize(Result<int>.Failure(error), options);
            var back = JsonSerializer.Deserialize<Result<int>>(json, options)!;

            back.IsFailure.Should().BeTrue();
            back.TryGetValue(out _, out var read).Should().BeFalse();
            read.Should().Be(error);
        }
    }

    /// <summary>S7/S8: both paths emit identical JSON for both outcomes.</summary>
    [Fact]
    public void BothPaths_ShouldEmitIdenticalJson()
    {
        var direct = new JsonSerializerOptions();
        direct.Converters.Add(new ResultJsonConverter<int>(VerifierContext.Default.Int32));

        foreach (var value in new[] { Result<int>.Success(42), Result<int>.Failure(Error.Validation("bad", "not allowed")) })
        {
            JsonSerializer.Serialize(value, direct).Should().Be(JsonSerializer.Serialize(value, Factory));
        }
    }

    /// <summary>S7: a source-generated carrier round-trips and matches the factory byte for byte.</summary>
    [Fact]
    public void SourceGeneratedCarrier_ShouldRoundTripAndMatchTheFactory()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = VerifierContext.Default };
        options.Converters.Add(new ResultJsonConverter<int>(VerifierContext.Default.Int32));
        options.Converters.Add(new ResultJsonConverter<string>(VerifierContext.Default.String));
        var payload = new VerifierCarrier("Ada", Result<int>.Success(36), Result<string?>.Success(null));

        var json = JsonSerializer.Serialize(payload, options);
        var back = JsonSerializer.Deserialize<VerifierCarrier>(json, options)!;

        json.Should().Be(JsonSerializer.Serialize(payload, Factory));
        json.Should().Be("{\"Name\":\"Ada\",\"Age\":{\"isSuccess\":true,\"value\":36},\"Nickname\":{\"isSuccess\":true,\"value\":null}}");
        back.Age.GetValueOrDefault().Should().Be(36);
        back.Nickname.IsSuccess.Should().BeTrue();
        back.Nickname.GetValueOrDefault().Should().BeNull();
    }

    /// <summary>S7: the JsonTypeInfo instance handed to the constructor is the metadata actually used.</summary>
    [Fact]
    public void MetadataPath_ShouldHonorTheGivenTypeInfoOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ResultJsonConverter<VerifierMoney>(VerifierCamelContext.Default.VerifierMoney));

        JsonSerializer.Serialize(Result<VerifierMoney>.Success(new VerifierMoney(5)), options)
            .Should().Be("{\"isSuccess\":true,\"value\":{\"amount\":5}}");
        JsonSerializer.Serialize(new VerifierMoney(5)).Should().Be("{\"Amount\":5}");
    }

    /// <summary>
    /// S7: a root <c>Result&lt;T&gt;</c> resolves through a source-generated resolver exactly when the
    /// closed <c>Result&lt;T&gt;</c> is in the context's reachable graph, independent of options reuse.
    /// </summary>
    /// <remarks>
    /// <see cref="VerifierContext"/> declares a carrier whose members are <c>Result&lt;int&gt;</c> and
    /// <c>Result&lt;string?&gt;</c>, so those closed types are resolvable at the root — proven on a
    /// fresh <see cref="JsonSerializerOptions"/> that never serialized the carrier first.
    /// </remarks>
    [Fact]
    public void Reachability_InTheContextGraph_ShouldResolveARootResult()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = VerifierContext.Default };
        options.Converters.Add(new ResultJsonConverter<int>(VerifierContext.Default.Int32));

        var json = JsonSerializer.Serialize(Result<int>.Success(5), options);
        var back = JsonSerializer.Deserialize<Result<int>>(json, options)!;

        json.Should().Be("{\"isSuccess\":true,\"value\":5}");
        back.GetValueOrDefault().Should().Be(5);
    }

    /// <summary>
    /// S7: a root closed <c>Result&lt;T&gt;</c> outside the context's reachable graph still throws,
    /// even though the value type itself is annotated.
    /// </summary>
    /// <remarks>
    /// The exception text is pinned because it names the exact closed type that lacks metadata, so this
    /// pin documents the constraint the remarks and the task log describe.
    /// </remarks>
    [Fact]
    public void Reachability_OutsideTheContextGraph_ShouldThrowNotSupportedException()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = ScalarOnlyContext.Default };
        options.Converters.Add(new ResultJsonConverter<int>());

        var act = () => JsonSerializer.Serialize(Result<int>.Success(5), options);

        act.Should().Throw<NotSupportedException>().Which.Message.Should().Contain("Result`1[System.Int32]");
    }

    /// <summary>S8: the shape written and the error text read are unchanged from the pre-change converter.</summary>
    [Fact]
    public void ReadErrors_ShouldKeepTheirExactText()
    {
        var cases = new (string Json, string Message)[]
        {
            ("{}", "The Result object is missing the required 'isSuccess' property."),
            ("{\"isSuccess\":true}", "The Result object reports success but carries no 'value'."),
            ("{\"isSuccess\":false}", "The Result object reports failure but carries no 'error'."),
            ("[]", "Expected a Result object, found StartArray."),
        };

        foreach (var (json, message) in cases)
        {
            var thrown = Assert.Throws<JsonException>(
                () => JsonSerializer.Deserialize<Result<int>>(json, Factory));

            thrown.Message.Should().Be(message);
        }
    }

    private sealed class OffsetIntJsonConverter(int offset) : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetInt32() - offset;

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value + offset);
    }

    private sealed class PoisonIntJsonConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new InvalidOperationException("The ambient int converter must not be consulted on read.");

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            => throw new InvalidOperationException("The ambient int converter must not be consulted on write.");
    }
}
