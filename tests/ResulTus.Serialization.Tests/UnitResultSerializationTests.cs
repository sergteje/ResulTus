// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;

namespace ResulTus.Serialization.Tests;

/// <summary>
/// Tests for the unit <see cref="Result"/> converter.
/// </summary>
public class UnitResultSerializationTests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions().AddResulTusConverters();

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN it is serialized
    /// THEN the JSON MUST be exactly <c>{"isSuccess":true}</c>, with no redundant <c>isFailure</c>.
    /// </summary>
    [Fact]
    public void Success_ShouldSerializeAsIsSuccessOnly()
    {
        var json = JsonSerializer.Serialize(Result.Success(), Options);

        json.Should().Be("{\"isSuccess\":true}");
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN it is serialized
    /// THEN the JSON MUST carry <c>isSuccess:false</c> and the full error object.
    /// </summary>
    [Fact]
    public void Failure_ShouldSerializeWithError()
    {
        var json = JsonSerializer.Serialize(Result.Failure(Error.Conflict("dup", "already there")), Options);

        json.Should().Be(
            "{\"isSuccess\":false,\"error\":{\"code\":\"dup\",\"message\":\"already there\",\"type\":\"Conflict\"}}");
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN it is serialized and deserialized again
    /// THEN it MUST still be a success.
    /// </summary>
    [Fact]
    public void Success_ShouldRoundTrip()
    {
        var json = JsonSerializer.Serialize(Result.Success(), Options);
        var back = JsonSerializer.Deserialize<Result>(json, Options);

        back.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN it is serialized and deserialized again
    /// THEN the failure and its whole error MUST survive.
    /// </summary>
    [Fact]
    public void Failure_ShouldRoundTrip()
    {
        var error = Error.NotFound("missing", "not there");

        var json = JsonSerializer.Serialize(Result.Failure(error), Options);
        var back = JsonSerializer.Deserialize<Result>(json, Options);

        back.IsFailure.Should().BeTrue();
        back.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a result object with no <c>isSuccess</c> property
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/>.
    /// </summary>
    [Fact]
    public void MissingIsSuccess_ShouldThrow()
    {
        var act = () => JsonSerializer.Deserialize<Result>("{}", Options);

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// GIVEN a result object that reports failure but carries no <c>error</c>
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/> instead of manufacturing <c>Error.None</c>.
    /// </summary>
    [Fact]
    public void FailureWithoutError_ShouldThrow()
    {
        var act = () => JsonSerializer.Deserialize<Result>("{\"isSuccess\":false}", Options);

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// GIVEN a result object carrying an extra property the wire format does not define
    /// WHEN it is deserialized
    /// THEN the unknown property MUST be skipped.
    /// </summary>
    [Fact]
    public void UnknownProperty_ShouldBeSkipped()
    {
        var back = JsonSerializer.Deserialize<Result>("{\"isSuccess\":true,\"extra\":{\"nested\":true}}", Options);

        back.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN an <c>isSuccess</c> property that is not a JSON boolean
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/> rather than coercing the value.
    /// </summary>
    [Fact]
    public void NonBooleanIsSuccess_ShouldThrow()
    {
        var act = () => JsonSerializer.Deserialize<Result>("{\"isSuccess\":\"yes\"}", Options);

        act.Should().Throw<JsonException>();
    }
}
