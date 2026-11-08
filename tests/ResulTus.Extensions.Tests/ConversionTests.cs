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

    /// <summary>
    /// GIVEN a successful typed result
    /// WHEN <c>ToErrorResult()</c> runs with a mapper
    /// THEN the value MUST survive and the mapper MUST NOT run.
    /// </summary>
    [Fact]
    public void ToErrorResult_OnSuccess_ShouldPreserveTheValueWithoutMapping()
    {
        var result = Result<int, string>.Success(7);
        var invoked = false;

        var converted = result.ToErrorResult(error =>
        {
            invoked = true;
            return Error.NotFound(error);
        });

        converted.IsSuccess.Should().BeTrue();
        converted.GetValueOrDefault().Should().Be(7);
        invoked.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a failed typed result
    /// WHEN <c>ToErrorResult()</c> runs with a mapper
    /// THEN the failure MUST hold the mapped <c>Error</c>.
    /// </summary>
    [Fact]
    public void ToErrorResult_OnFailure_ShouldMapTheTypedError()
    {
        var result = Result<int, string>.Failure("bad");

        var converted = result.ToErrorResult(error => Error.Conflict(error));

        converted.TryGetValue(out _, out var error).Should().BeFalse();
        error.Code.Should().Be("bad");
        error.Type.Should().Be(ErrorType.Conflict);
    }

    /// <summary>
    /// GIVEN a typed result whose error is already an <c>Error</c>
    /// WHEN the direct <c>ToErrorResult()</c> overload runs
    /// THEN the success value MUST survive.
    /// </summary>
    [Fact]
    public void ToErrorResult_DirectOverloadOnSuccess_ShouldPreserveTheValue()
    {
        var result = Result<int, Error>.Success(7);

        var converted = result.ToErrorResult();

        converted.IsSuccess.Should().BeTrue();
        converted.GetValueOrDefault().Should().Be(7);
    }

    /// <summary>
    /// GIVEN a typed result whose error is already an <c>Error</c>
    /// WHEN the direct <c>ToErrorResult()</c> overload runs on a failure
    /// THEN the exact error value MUST survive.
    /// </summary>
    [Fact]
    public void ToErrorResult_DirectOverloadOnFailure_ShouldKeepTheSameError()
    {
        var error = Error.NotFound("missing");
        var result = Result<int, Error>.Failure(error);

        var converted = result.ToErrorResult();

        converted.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;T&gt;</c>
    /// WHEN <c>ToGenericResult()</c> runs with a mapper
    /// THEN the value MUST survive and the mapper MUST NOT run.
    /// </summary>
    [Fact]
    public void ToGenericResult_OnSuccess_ShouldPreserveTheValueWithoutMapping()
    {
        Result<int> result = 7;
        var invoked = false;

        var converted = result.ToGenericResult(error =>
        {
            invoked = true;
            return error.Code;
        });

        converted.IsSuccess.Should().BeTrue();
        converted.Match(static value => value, static _ => -1).Should().Be(7);
        invoked.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;T&gt;</c>
    /// WHEN <c>ToGenericResult()</c> runs with a mapper
    /// THEN the failure MUST hold the mapped typed error.
    /// </summary>
    [Fact]
    public void ToGenericResult_OnFailure_ShouldMapTheError()
    {
        var result = Result<int>.Failure(Error.NotFound("missing"));

        var converted = result.ToGenericResult(static error => error.Code);

        converted.IsFailure.Should().BeTrue();
        converted.Match(static _ => "unexpected", static error => error).Should().Be("missing");
    }

    /// <summary>
    /// GIVEN both conversions
    /// WHEN a <c>null</c> mapper is supplied
    /// THEN they MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void BridgeConversions_WithNullMapper_ShouldThrow()
    {
        var typed = Result<int, string>.Success(1);
        Result<int> plain = 1;

        var calls = new Action[]
        {
            () => typed.ToErrorResult(null!),
            () => plain.ToGenericResult<int, string>(null!),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }
}
