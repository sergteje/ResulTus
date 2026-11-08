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
    /// THEN <c>Code</c> MUST be <c>string.Empty</c> and <c>Type</c> MUST be <c>ErrorType.None</c>.
    /// </summary>
    [Fact]
    public void None_ShouldHaveEmptyCodeAndNoneType()
    {
        var none = Error.None;

        none.Code.Should().Be(string.Empty);
        none.Type.Should().Be(ErrorType.None);
    }

    /// <summary>
    /// GIVEN the <see cref="Error"/> type
    /// THEN it MUST expose only <c>Code</c>, <c>Message</c> and <c>Type</c> — no untyped payload
    /// property such as a metadata dictionary (ER-2, ER-6).
    /// </summary>
    [Fact]
    public void Error_ShouldNotExposeUntypedPayload()
    {
        typeof(Error)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(property => property.Name)
            .Should().BeEquivalentTo("Code", "Message", "Type");
    }

    /// <summary>
    /// GIVEN two errors with identical <c>Code</c>, <c>Message</c> and <c>Type</c>
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

    /// <summary>
    /// GIVEN <c>default(Error)</c> and <c>Error.None</c>
    /// WHEN comparing with <c>==</c>, <c>Equals</c> and <c>GetHashCode</c>
    /// THEN they MUST be indistinguishable, because both represent "no error".
    /// </summary>
    /// <remarks>
    /// <c>default(Error)</c> leaves <c>_code</c> as <c>null</c> while <c>Error.None</c> stores
    /// <see cref="string.Empty"/>. Equality is defined over the <c>Code</c> contract
    /// (null-coalesced, ordinal), not over the raw field, so the two agree.
    /// </remarks>
    [Fact]
    public void DefaultStruct_ShouldEqualNone()
    {
        default(Error).Should().Be(Error.None);
        default(Error).Equals(Error.None).Should().BeTrue();
        (default(Error) == Error.None).Should().BeTrue();
        (default(Error) != Error.None).Should().BeFalse();
        default(Error).GetHashCode().Should().Be(Error.None.GetHashCode());
    }

    /// <summary>
    /// GIVEN <c>Error.None</c> and a failure whose code is empty
    /// WHEN comparing
    /// THEN they MUST NOT be equal: the <see cref="ErrorType"/> tag distinguishes them.
    /// </summary>
    [Fact]
    public void None_ShouldNotEqualFailureWithEmptyCode()
    {
        (Error.None == Error.Failure(string.Empty)).Should().BeFalse();
        Error.None.Equals(Error.Failure(string.Empty)).Should().BeFalse();
    }

    /// <summary>
    /// GIVEN two errors that differ only in <c>Message</c> (<c>null</c> versus <c>string.Empty</c>)
    /// WHEN comparing
    /// THEN they MUST NOT be equal: only <c>Code</c> carries the null-coalescing contract.
    /// </summary>
    [Fact]
    public void Message_EmptyAndNull_ShouldNotBeEqual()
    {
        var withoutMessage = Error.NotFound("x");
        var withEmptyMessage = Error.NotFound("x", string.Empty);

        (withoutMessage == withEmptyMessage).Should().BeFalse();
    }
}
