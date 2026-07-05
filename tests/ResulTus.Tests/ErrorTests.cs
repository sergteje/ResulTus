// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Tests;

/// <summary>
/// Tests for <see cref="Error"/> readonly record struct.
/// </summary>
public class ErrorTests
{
    /// <summary>
    /// GIVEN <c>Error.NotFound("user.missing")</c>
    /// THEN <c>Type</c> MUST be <c>ErrorType.NotFound</c> and <c>Code</c> MUST be <c>"user.missing"</c>.
    /// </summary>
    [Fact]
    public void Factory_ShouldSetCorrectErrorTypeAndCode()
    {
        var error = Error.NotFound("user.missing");

        error.Type.Should().Be(ErrorType.NotFound);
        error.Code.Should().Be("user.missing");
    }

    /// <summary>
    /// GIVEN <c>Error.None</c>
    /// THEN <c>Code</c> MUST be <c>string.Empty</c> and <c>Metadata</c> MUST be <c>null</c>.
    /// </summary>
    [Fact]
    public void None_ShouldHaveEmptyCodeAndNullMetadata()
    {
        var none = Error.None;

        none.Code.Should().Be(string.Empty);
        none.Metadata.Should().BeNull();
    }

    /// <summary>
    /// GIVEN two errors with identical <c>Code</c>, <c>Message</c>, <c>Type</c>, <c>Metadata</c>
    /// WHEN comparing with <c>==</c>
    /// THEN they MUST be equal.
    /// </summary>
    [Fact]
    public void StructuralEquality_SameFields_ShouldBeEqual()
    {
        var e1 = Error.Failure("err");
        var e2 = Error.Failure("err");

        e1.Should().Be(e2);
        (e1 == e2).Should().BeTrue();
        e1.GetHashCode().Should().Be(e2.GetHashCode());
    }

    /// <summary>
    /// GIVEN <c>Error.Failure("A")</c> and <c>Error.Failure("B")</c>
    /// WHEN comparing
    /// THEN they MUST NOT be equal.
    /// </summary>
    [Fact]
    public void DifferentErrors_ShouldNotBeEqual()
    {
        var e1 = Error.Failure("A");
        var e2 = Error.Failure("B");

        e1.Should().NotBe(e2);
        (e1 != e2).Should().BeTrue();
    }

    /// <summary>
    /// GIVEN <c>Error.Validation("bad", metadata: new Dictionary&lt;string, object?&gt; { ["field"] = "email" })</c>
    /// THEN <c>Metadata</c> MUST contain the key <c>"field"</c> with value <c>"email"</c>.
    /// </summary>
    [Fact]
    public void Factory_WithMetadata_ShouldContainKey()
    {
        var metadata = new Dictionary<string, object?> { ["field"] = "email" };
        var error = Error.Validation("bad", metadata: metadata);

        error.Metadata.Should().ContainKey("field").WhoseValue.Should().Be("email");
    }

    /// <summary>
    /// GIVEN <c>Error.NotFound("x", message: "not found")</c>
    /// THEN <c>Message</c> MUST be <c>"not found"</c>.
    /// </summary>
    [Fact]
    public void Message_ShouldBeSet()
    {
        var error = Error.NotFound("x", message: "not found");

        error.Message.Should().Be("not found");
    }

    /// <summary>
    /// GIVEN <c>Error.Unauthorized("no.access")</c>
    /// THEN <c>Type</c> MUST be <c>ErrorType.Unauthorized</c> and <c>Code</c> MUST be <c>"no.access"</c>.
    /// </summary>
    [Fact]
    public void Factory_Unauthorized_ShouldSetCorrectType()
    {
        var error = Error.Unauthorized("no.access");

        error.Type.Should().Be(ErrorType.Unauthorized);
        error.Code.Should().Be("no.access");
    }

    /// <summary>
    /// GIVEN <c>Error.Forbidden("no.perm")</c>
    /// THEN <c>Type</c> MUST be <c>ErrorType.Forbidden</c> and <c>Code</c> MUST be <c>"no.perm"</c>.
    /// </summary>
    [Fact]
    public void Factory_Forbidden_ShouldSetCorrectType()
    {
        var error = Error.Forbidden("no.perm");

        error.Type.Should().Be(ErrorType.Forbidden);
        error.Code.Should().Be("no.perm");
    }

    /// <summary>
    /// GIVEN <c>Error.Cancelled("op.cancelled")</c>
    /// THEN <c>Type</c> MUST be <c>ErrorType.Cancelled</c> and <c>Code</c> MUST be <c>"op.cancelled"</c>.
    /// </summary>
    [Fact]
    public void Factory_Cancelled_ShouldSetCorrectType()
    {
        var error = Error.Cancelled("op.cancelled");

        error.Type.Should().Be(ErrorType.Cancelled);
        error.Code.Should().Be("op.cancelled");
    }

    /// <summary>
    /// GIVEN <c>Error.Failure(null!, message: "no code")</c>
    /// THEN <c>Code</c> MUST be <c>string.Empty</c> (null coalesced in constructor).
    /// </summary>
    [Fact]
    public void Factory_NullCode_ShouldCoalesceToEmpty()
    {
        var error = Error.Failure(null!, message: "no code");

        error.Code.Should().Be(string.Empty);
        error.Message.Should().Be("no code");
    }

    /// <summary>
    /// GIVEN <c>default(Error)</c>
    /// THEN <c>Code</c> MUST be <c>string.Empty</c> (null coalesced in getter, since the constructor is not called).
    /// </summary>
    [Fact]
    public void DefaultStruct_Code_ShouldFallbackToEmpty()
    {
        default(Error).Code.Should().Be(string.Empty);
    }
}
