// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Tests;

/// <summary>
/// Tests for <see cref="Result{TSuccess, TError}"/> dual-generic readonly record struct.
/// </summary>
public class ResultTErrorTests
{
    /// <summary>
    /// GIVEN <c>Result&lt;int, string&gt;.Success(10)</c>
    /// THEN <c>IsSuccess</c> MUST be <c>true</c> and <c>Match(v => v, e => -1)</c> MUST be <c>10</c>.
    /// </summary>
    [Fact]
    public void ImplicitConversion_FromSuccessValue_ShouldBeSuccess()
    {
        var r = Result<int, string>.Success(10);

        r.IsSuccess.Should().BeTrue();
        r.Match(v => v, e => -1).Should().Be(10);
    }

    /// <summary>
    /// GIVEN <c>Result&lt;int, string&gt; r = "fail"</c>
    /// THEN <c>IsFailure</c> MUST be <c>true</c>.
    /// </summary>
    [Fact]
    public void ImplicitConversion_FromErrorValue_ShouldBeFailure()
    {
        Result<int, string> r = "fail";

        r.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int, string&gt;</c>
    /// WHEN <c>var (ok, val, err) = r</c>
    /// THEN <c>ok</c> MUST be <c>false</c> and <c>err</c> MUST NOT be <c>null</c>.
    /// </summary>
    [Fact]
    public void Deconstruct_OnFailure_ShouldRevealError()
    {
        Result<int, string> r = "error message";
        var (ok, val, err) = r;

        ok.Should().BeFalse();
        err.Should().Be("error message");
    }

    /// <summary>
    /// GIVEN <c>default(Result&lt;int, string&gt;)</c>
    /// THEN <c>IsSuccess</c> MUST be <c>false</c>.
    /// </summary>
    [Fact]
    public void DefaultStruct_ShouldBeFailure()
    {
        default(Result<int, string>).IsSuccess.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN <c>Result&lt;int, string&gt;.Failure("err")</c>
    /// WHEN <c>Match</c>
    /// THEN the failure branch is invoked.
    /// </summary>
    [Fact]
    public void Match_OnFailure_ShouldInvokeFailureBranch()
    {
        var r = Result<int, string>.Failure("not found");

        r.Match(v => v, e => -1).Should().Be(-1);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int, string&gt;</c>
    /// THEN deconstruct reveals success.
    /// </summary>
    [Fact]
    public void Deconstruct_OnSuccess_ShouldRevealValue()
    {
        var r = Result<int, string>.Success(99);
        var (ok, val, err) = r;

        ok.Should().BeTrue();
        val.Should().Be(99);
    }

    /// <summary>
    /// GIVEN <c>int value = 42</c>
    /// WHEN <c>Result&lt;int, string&gt; r = 42</c>
    /// THEN the implicit conversion from TSuccess MUST produce a success.
    /// </summary>
    [Fact]
    public void ImplicitConversion_FromTSuccess_ShouldBeSuccess()
    {
        Result<int, string> r = 42;

        r.IsSuccess.Should().BeTrue();
    }
}
