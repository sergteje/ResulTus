// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResulTus.Serialization.Tests;

/// <summary>
/// A DTO carrying results and an error, used to prove serialization composes inside a real payload.
/// </summary>
/// <param name="Name">The entity name.</param>
/// <param name="Age">The entity age, as a result.</param>
/// <param name="Nickname">The entity nickname, as a result over a reference type.</param>
/// <param name="Failure">A raw error carried alongside.</param>
public sealed record PersonPayload(string Name, Result<int> Age, Result<string> Nickname, Error Failure);

/// <summary>
/// Tests for converter registration and for the end-to-end no-data-loss guarantee.
/// </summary>
public class RegistrationAndRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions().AddResulTusConverters();

    /// <summary>
    /// Provides every defined <see cref="ErrorType"/> value.
    /// </summary>
    /// <returns>One row per enum value.</returns>
    public static IEnumerable<object[]> AllErrorTypes()
    {
        foreach (var value in Enum.GetValues<ErrorType>())
        {
            yield return new object[] { value };
        }
    }

    /// <summary>
    /// GIVEN a fresh <see cref="JsonSerializerOptions"/>
    /// WHEN <c>AddResulTusConverters()</c> is called
    /// THEN it MUST return the same instance so calls can be chained.
    /// </summary>
    [Fact]
    public void AddResulTusConverters_ShouldReturnSameInstance()
    {
        var options = new JsonSerializerOptions();

        var returned = options.AddResulTusConverters();

        returned.Should().BeSameAs(options);
    }

    /// <summary>
    /// GIVEN a <c>null</c> options reference
    /// WHEN <c>AddResulTusConverters()</c> is called
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void AddResulTusConverters_WithNull_ShouldThrow()
    {
        JsonSerializerOptions? options = null;

        var act = () => options!.AddResulTusConverters();

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN every defined <see cref="ErrorType"/> value
    /// WHEN it is serialized and deserialized again
    /// THEN it MUST survive, proving the name mapping is total.
    /// </summary>
    /// <param name="value">The enum value under test.</param>
    [Theory]
    [MemberData(nameof(AllErrorTypes))]
    public void EveryErrorType_ShouldRoundTrip(ErrorType value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        var back = JsonSerializer.Deserialize<ErrorType>(json, Options);

        back.Should().Be(value);
    }

    /// <summary>
    /// GIVEN a payload carrying two results and an error
    /// WHEN it is serialized and deserialized again
    /// THEN every success value and the whole error MUST survive — the data-loss regression
    /// this phase exists to prevent.
    /// </summary>
    [Fact]
    public void CompositePayload_ShouldRoundTripWithoutDataLoss()
    {
        var payload = new PersonPayload(
            "Ada",
            Result<int>.Success(36),
            Result<string>.Failure(Error.Validation("nickname", "not allowed")),
            Error.NotFound("missing", "not there"));

        var json = JsonSerializer.Serialize(payload, Options);
        var back = JsonSerializer.Deserialize<PersonPayload>(json, Options);

        back.Should().NotBeNull();
        back!.Name.Should().Be("Ada");
        back.Age.IsSuccess.Should().BeTrue();
        back.Age.GetValueOrDefault().Should().Be(36);
        back.Nickname.IsFailure.Should().BeTrue();
        back.Nickname.TryGetValue(out _, out var nicknameError).Should().BeFalse();
        nicknameError.Should().Be(Error.Validation("nickname", "not allowed"));
        back.Failure.Should().Be(Error.NotFound("missing", "not there"));
    }

    /// <summary>
    /// GIVEN a list of results
    /// WHEN it is serialized and deserialized again
    /// THEN each element MUST keep its own outcome and value.
    /// </summary>
    [Fact]
    public void ResultList_ShouldRoundTrip()
    {
        var list = new List<Result<int>>
        {
            Result<int>.Success(1),
            Result<int>.Failure(Error.Timeout("slow")),
            Result<int>.Success(3),
        };

        var json = JsonSerializer.Serialize(list, Options);
        var back = JsonSerializer.Deserialize<List<Result<int>>>(json, Options);

        back.Should().NotBeNull();
        back!.Should().HaveCount(3);
        back[0].GetValueOrDefault().Should().Be(1);
        back[1].IsFailure.Should().BeTrue();
        back[1].TryGetValue(out _, out var error).Should().BeFalse();
        error.Should().Be(Error.Timeout("slow"));
        back[2].GetValueOrDefault().Should().Be(3);
    }

    /// <summary>
    /// GIVEN a shared <see cref="JsonSerializerOptions"/> instance and an array of results
    /// WHEN the same array is serialized and deserialized repeatedly
    /// THEN the JSON and both result outcomes MUST remain unchanged.
    /// </summary>
    [Fact]
    public void ResultArray_WithSharedOptions_ShouldKeepStableOutput()
    {
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Failure(Error.NotFound("missing")),
            Result<int>.Success(3),
        };

        var firstJson = JsonSerializer.Serialize(results, Options);
        var secondJson = JsonSerializer.Serialize(results, Options);
        var roundTripped = JsonSerializer.Deserialize<Result<int>[]>(secondJson, Options);

        secondJson.Should().Be(firstJson);
        roundTripped.Should().NotBeNull();
        roundTripped.Should().BeEquivalentTo(results);
    }

    /// <summary>
    /// GIVEN one result converter used with two options instances that have different value converters
    /// WHEN each options instance writes and reads a result
    /// THEN the result converter MUST resolve the value converter for the active options instance.
    /// </summary>
    [Fact]
    public void ResultConverter_ShouldResolveValueConverterPerOptionsInstance()
    {
        var resultConverter = (JsonConverter<Result<int>>)new ResultJsonConverterFactory()
            .CreateConverter(typeof(Result<int>), new JsonSerializerOptions());
        var firstOptions = new JsonSerializerOptions();
        firstOptions.Converters.Add(new OffsetIntJsonConverter(10));
        firstOptions.Converters.Add(resultConverter);
        var secondOptions = new JsonSerializerOptions();
        secondOptions.Converters.Add(new OffsetIntJsonConverter(20));
        secondOptions.Converters.Add(resultConverter);

        var firstJson = JsonSerializer.Serialize(Result<int>.Success(5), firstOptions);
        var secondJson = JsonSerializer.Serialize(Result<int>.Success(5), secondOptions);
        var firstResult = JsonSerializer.Deserialize<Result<int>>(firstJson, firstOptions);
        var secondResult = JsonSerializer.Deserialize<Result<int>>(secondJson, secondOptions);

        firstJson.Should().Be("{\"isSuccess\":true,\"value\":15}");
        secondJson.Should().Be("{\"isSuccess\":true,\"value\":25}");
        firstResult.GetValueOrDefault().Should().Be(5);
        secondResult.GetValueOrDefault().Should().Be(5);
    }

    /// <summary>
    /// GIVEN an error object whose type is <c>None</c> but which carries a code
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/> instead of silently dropping the code.
    /// </summary>
    [Fact]
    public void ErrorWithNoneTypeAndCode_ShouldThrow()
    {
        var act = () => JsonSerializer.Deserialize<Error>("{\"code\":\"x\",\"type\":\"None\"}", Options);

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// GIVEN a non-object JSON token
    /// WHEN it is deserialized as a result
    /// THEN it MUST throw <see cref="JsonException"/>.
    /// </summary>
    [Fact]
    public void NonObjectToken_ShouldThrow()
    {
        var unit = () => JsonSerializer.Deserialize<Result>("42", Options);
        var generic = () => JsonSerializer.Deserialize<Result<int>>("\"nope\"", Options);

        unit.Should().Throw<JsonException>();
        generic.Should().Throw<JsonException>();
    }

    /// <summary>
    /// GIVEN a <c>null</c> type
    /// WHEN the factory tests it
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Factory_WithNullType_ShouldThrow()
    {
        var factory = new ResultJsonConverterFactory();

        var act = () => factory.CanConvert(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN a non-result type
    /// WHEN the factory tests it
    /// THEN it MUST decline, leaving the type to the built-in converters.
    /// </summary>
    [Fact]
    public void Factory_ShouldDeclineNonResultTypes()
    {
        var factory = new ResultJsonConverterFactory();

        factory.CanConvert(typeof(int)).Should().BeFalse();
        factory.CanConvert(typeof(Result)).Should().BeFalse();
        factory.CanConvert(typeof(Result<int>)).Should().BeTrue();
    }

    private sealed class OffsetIntJsonConverter(int offset) : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetInt32() - offset;

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value + offset);
    }
}
