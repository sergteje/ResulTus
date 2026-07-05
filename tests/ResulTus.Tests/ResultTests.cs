// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Tests;

/// <summary>
/// Tests for void <see cref="Result"/> readonly record struct.
/// </summary>
public class ResultTests
{
    /// <summary>
    /// GIVEN <c>Result.Success()</c>
    /// THEN <c>IsSuccess</c> MUST be <c>true</c> and <c>IsFailure</c> MUST be <c>false</c>.
    /// </summary>
    [Fact]
    public void Success_ShouldHaveIsSuccessTrue()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN <c>var err = Error.NotFound("x")</c> and <c>Result.Failure(err)</c>
    /// THEN <c>IsSuccess</c> MUST be <c>false</c> and <c>TryGetError(out var e)</c> MUST yield <c>e == err</c>.
    /// </summary>
    [Fact]
    public void Failure_ShouldWrapError()
    {
        var err = Error.NotFound("x");
        var result = Result.Failure(err);

        result.IsSuccess.Should().BeFalse();
        result.TryGetError(out var e).Should().BeTrue();
        e.Should().Be(err);
    }

    /// <summary>
    /// GIVEN <c>Error err = Error.Timeout("t")</c>
    /// WHEN <c>Result result = err</c>
    /// THEN <c>result.IsFailure</c> MUST be <c>true</c>.
    /// </summary>
    [Fact]
    public void ImplicitConversion_FromError_ShouldBeFailure()
    {
        Error err = Error.Timeout("t");
        Result result = err;

        result.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN <c>Result.Success()</c>
    /// WHEN <c>Match(() => "ok", _ => "fail")</c>
    /// THEN the result MUST be <c>"ok"</c>.
    /// BUT given <c>Result.Failure(Error.None)</c>
    /// WHEN <c>Match(() => "ok", _ => "fail")</c>
    /// THEN the result MUST be <c>"fail"</c>.
    /// </summary>
    [Fact]
    public void Match_ShouldInvokeCorrectBranch()
    {
        var ok = Result.Success();
        ok.Match(() => "ok", _ => "fail").Should().Be("ok");

        var fail = Result.Failure(Error.None);
        fail.Match(() => "ok", _ => "fail").Should().Be("fail");
    }

    /// <summary>
    /// GIVEN <c>Result.Success()</c>
    /// WHEN <c>var (success, error) = result</c>
    /// THEN <c>success</c> MUST be <c>true</c> and <c>error</c> MUST be <c>default(Error)</c>.
    /// </summary>
    [Fact]
    public void Deconstruct_OnSuccess_ShouldReturnTrue()
    {
        var result = Result.Success();
        var (success, error) = result;

        success.Should().BeTrue();
        error.Should().Be(default(Error));
    }

    /// <summary>
    /// GIVEN <c>default(Result)</c>
    /// THEN <c>IsSuccess</c> MUST be <c>false</c>.
    /// </summary>
    [Fact]
    public void DefaultStruct_ShouldBeFailure()
    {
        default(Result).IsSuccess.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a success result
    /// WHEN calling <c>TryGetError</c>
    /// THEN it returns <c>false</c>.
    /// </summary>
    [Fact]
    public void TryGetError_OnSuccess_ShouldReturnFalse()
    {
        var result = Result.Success();
        result.TryGetError(out _).Should().BeFalse();
    }
}
