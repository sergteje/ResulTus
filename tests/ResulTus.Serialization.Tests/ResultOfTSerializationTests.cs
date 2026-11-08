// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;

namespace ResulTus.Serialization.Tests;

/// <summary>
/// Tests for the <see cref="Result{T}"/> converter, including the nested and nullable cases.
/// </summary>
public class ResultOfTSerializationTests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions().AddResulTusConverters();

    /// <summary>
    /// GIVEN <c>Result&lt;int&gt;.Success(42)</c>
    /// WHEN it is serialized
    /// THEN the JSON MUST carry both <c>isSuccess</c> and the value.
    /// </summary>
    [Fact]
    public void Success_ShouldSerializeWithValue()
    {
        var json = JsonSerializer.Serialize(Result<int>.Success(42), Options);

        json.Should().Be("{\"isSuccess\":true,\"value\":42}");
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN it is serialized
    /// THEN the JSON MUST carry the error and MUST NOT carry a value.
    /// </summary>
    [Fact]
    public void Failure_ShouldSerializeWithErrorAndNoValue()
    {
        var json = JsonSerializer.Serialize(Result<int>.Failure(Error.NotFound("missing")), Options);

        json.Should().Be("{\"isSuccess\":false,\"error\":{\"code\":\"missing\",\"type\":\"NotFound\"}}");
    }

    /// <summary>
    /// GIVEN <c>Result&lt;int&gt;.Success(42)</c>
    /// WHEN it is serialized and deserialized again
    /// THEN the value MUST survive — the data-loss regression this phase exists to prevent.
    /// </summary>
    [Fact]
    public void Success_ShouldRoundTripTheValue()
    {
        var json = JsonSerializer.Serialize(Result<int>.Success(42), Options);
        var back = JsonSerializer.Deserialize<Result<int>>(json, Options);

        back.IsSuccess.Should().BeTrue();
        back.GetValueOrDefault().Should().Be(42);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN it is serialized and deserialized again
    /// THEN the whole error MUST survive.
    /// </summary>
    [Fact]
    public void Failure_ShouldRoundTripTheError()
    {
        var error = Error.Validation("bad", "value too small");

        var json = JsonSerializer.Serialize(Result<int>.Failure(error), Options);
        var back = JsonSerializer.Deserialize<Result<int>>(json, Options);

        back.IsFailure.Should().BeTrue();
        back.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN <c>Result&lt;string&gt;.Success("ok")</c>
    /// WHEN it is round-tripped
    /// THEN the reference-typed value MUST survive.
    /// </summary>
    [Fact]
    public void ReferenceType_ShouldRoundTrip()
    {
        var json = JsonSerializer.Serialize(Result<string>.Success("ok"), Options);
        var back = JsonSerializer.Deserialize<Result<string>>(json, Options);

        back.GetValueOrDefault().Should().Be("ok");
    }

    /// <summary>
    /// GIVEN <c>Result&lt;int?&gt;.Success(null)</c>
    /// WHEN it is round-tripped
    /// THEN it MUST stay a success holding <c>null</c>, not become a failure.
    /// </summary>
    [Fact]
    public void NullableValueType_ShouldRoundTripNull()
    {
        var json = JsonSerializer.Serialize(Result<int?>.Success(null), Options);
        var back = JsonSerializer.Deserialize<Result<int?>>(json, Options);

        json.Should().Be("{\"isSuccess\":true,\"value\":null}");
        back.IsSuccess.Should().BeTrue();
        back.GetValueOrDefault().Should().BeNull();
    }

    /// <summary>
    /// GIVEN a nested <c>Result&lt;Result&lt;int&gt;&gt;</c>
    /// WHEN it is round-tripped
    /// THEN both layers MUST survive, proving the converter resolves itself recursively.
    /// </summary>
    [Fact]
    public void NestedResult_ShouldRoundTrip()
    {
        var json = JsonSerializer.Serialize(Result<Result<int>>.Success(Result<int>.Success(7)), Options);
        var back = JsonSerializer.Deserialize<Result<Result<int>>>(json, Options);

        back.IsSuccess.Should().BeTrue();
        back.GetValueOrDefault().IsSuccess.Should().BeTrue();
        back.GetValueOrDefault().GetValueOrDefault().Should().Be(7);
    }

    /// <summary>
    /// GIVEN a success object with no <c>value</c> property
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/>.
    /// </summary>
    [Fact]
    public void SuccessWithoutValue_ShouldThrow()
    {
        var act = () => JsonSerializer.Deserialize<Result<int>>("{\"isSuccess\":true}", Options);

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// GIVEN a result object whose properties arrive out of order and with an unknown extra
    /// WHEN it is deserialized
    /// THEN the known properties MUST be honoured and the unknown one skipped.
    /// </summary>
    [Fact]
    public void OutOfOrderAndUnknownProperties_ShouldBeHandled()
    {
        var json = "{\"value\":5,\"extra\":{\"nested\":true},\"isSuccess\":true}";

        var back = JsonSerializer.Deserialize<Result<int>>(json, Options);

        back.IsSuccess.Should().BeTrue();
        back.GetValueOrDefault().Should().Be(5);
    }

    /// <summary>
    /// GIVEN a result object with no <c>isSuccess</c> property
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/>.
    /// </summary>
    [Fact]
    public void MissingIsSuccess_ShouldThrow()
    {
        var act = () => JsonSerializer.Deserialize<Result<int>>("{\"value\":5}", Options);

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// GIVEN an <c>isSuccess</c> property that is not a JSON boolean
    /// WHEN it is deserialized
    /// THEN it MUST throw <see cref="JsonException"/> rather than coercing the value.
    /// </summary>
    [Fact]
    public void NonBooleanIsSuccess_ShouldThrow()
    {
        var act = () => JsonSerializer.Deserialize<Result<int>>("{\"isSuccess\":1,\"value\":5}", Options);

        act.Should().Throw<JsonException>();
    }
}
