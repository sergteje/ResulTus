// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Tests for the conversion extensions.
/// </summary>
public class ConversionTests
{
    /// <summary>
    /// GIVEN the value 5
    /// WHEN <c>ToResult()</c>
    /// THEN the result MUST be a success holding 5.
    /// </summary>
    [Fact]
    public void ToResult_FromValue_ShouldBeSuccess()
    {
        var result = 5.ToResult();

        result.IsSuccess.Should().BeTrue();
        result.GetValueOrDefault().Should().Be(5);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c> holding 5
    /// WHEN <c>ToNullable()</c>
    /// THEN it MUST return 5.
    /// </summary>
    [Fact]
    public void ToNullable_OnSuccess_ShouldReturnValue()
    {
        Result<int> result = 5;

        result.ToNullable().Should().Be(5);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>ToNullable()</c>
    /// THEN it MUST return <c>null</c>.
    /// </summary>
    [Fact]
    public void ToNullable_OnFailure_ShouldReturnNull()
    {
        var result = Result<int>.Failure(Error.NotFound("missing"));

        result.ToNullable().Should().BeNull();
    }

    /// <summary>
    /// GIVEN <c>Error.NotFound("missing", "not there")</c>
    /// WHEN <c>ToException()</c>
    /// THEN the exception message MUST be the error message and the code MUST survive in <c>Data["Code"]</c>.
    /// </summary>
    [Fact]
    public void ToException_WithMessage_ShouldCarryMessageAndCode()
    {
        var error = Error.NotFound("missing", "not there");

        var exception = error.ToException();

        exception.Message.Should().Be("not there");
        exception.Data["Code"].Should().Be("missing");
    }

    /// <summary>
    /// GIVEN <c>Error.Timeout("slow")</c> with no message
    /// WHEN <c>ToException()</c>
    /// THEN the exception message MUST fall back to the code.
    /// </summary>
    [Fact]
    public void ToException_WithoutMessage_ShouldFallBackToCode()
    {
        var error = Error.Timeout("slow");

        var exception = error.ToException();

        exception.Message.Should().Be("slow");
        exception.Data["Code"].Should().Be("slow");
    }
}
