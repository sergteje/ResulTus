// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Tests;

/// <summary>
/// Tests for <see cref="Result{T}"/> generic readonly record struct.
/// </summary>
public class ResultTTests
{
    /// <summary>
    /// GIVEN <c>int value = 42</c>
    /// WHEN <c>Result&lt;int&gt; r = value</c>
    /// THEN <c>r.IsSuccess</c> MUST be <c>true</c> and <c>TryGetValue(out var v)</c> MUST yield <c>v == 42</c>.
    /// </summary>
    [Fact]
    public void ImplicitConversion_FromValue_ShouldBeSuccess()
    {
        Result<int> r = 42;

        r.IsSuccess.Should().BeTrue();
        r.TryGetValue(out var v).Should().BeTrue();
        v.Should().Be(42);
    }

    /// <summary>
    /// GIVEN <c>Error err = Error.Conflict("dup")</c>
    /// WHEN <c>Result&lt;int&gt; r = err</c>
    /// THEN <c>r.IsFailure</c> MUST be <c>true</c>.
    /// </summary>
    [Fact]
    public void ImplicitConversion_FromError_ShouldBeFailure()
    {
        Error err = Error.Conflict("dup");
        Result<int> r = err;

        r.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN <c>Result&lt;int&gt;.Failure(Error.Unexpected(""))</c>
    /// WHEN calling <c>TryGetValue(out var v, out var e)</c>
    /// THEN return MUST be <c>false</c>, <c>v</c> MUST be <c>default</c>, and <c>e</c> MUST be the error.
    /// </summary>
    [Fact]
    public void TryGetValue_OnFailure_ShouldReturnFalse()
    {
        var err = Error.Unexpected("");
        var r = Result<int>.Failure(err);

        r.TryGetValue(out var v, out var e).Should().BeFalse();
        v.Should().Be(default);
        e.Should().Be(err);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN calling <c>GetValueOrDefault(99)</c>
    /// THEN result MUST be <c>99</c>.
    /// </summary>
    [Fact]
    public void GetValueOrDefault_OnFailure_ShouldReturnFallback()
    {
        var r = Result<int>.Failure(Error.NotFound("x"));

        r.GetValueOrDefault(99).Should().Be(99);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN calling <c>GetValueOr(_ => -1)</c>
    /// THEN result MUST be <c>-1</c>.
    /// </summary>
    [Fact]
    public void GetValueOr_ShouldRecoverFromError()
    {
        var r = Result<int>.Failure(Error.Validation("bad"));

        r.GetValueOr(_ => -1).Should().Be(-1);
    }

    /// <summary>
    /// GIVEN <c>Result&lt;int&gt;.Success(7)</c>
    /// WHEN <c>var (ok, val, err) = r</c>
    /// THEN <c>ok</c> MUST be <c>true</c>, <c>val</c> MUST be <c>7</c>.
    /// </summary>
    [Fact]
    public void Deconstruct_OnSuccess_ShouldRevealValue()
    {
        var r = Result<int>.Success(7);
        var (ok, val, err) = r;

        ok.Should().BeTrue();
        val.Should().Be(7);
    }

    /// <summary>
    /// GIVEN <c>default(Result&lt;int&gt;)</c>
    /// THEN <c>IsSuccess</c> MUST be <c>false</c> and <c>TryGetValue(out var v)</c> MUST yield <c>v == 0</c>.
    /// </summary>
    [Fact]
    public void DefaultStruct_ShouldBeFailureWithDefaultValue()
    {
        var r = default(Result<int>);

        r.IsSuccess.Should().BeFalse();
        r.TryGetValue(out var v).Should().BeFalse();
        v.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN calling <c>GetValueOrDefault</c> with no arguments
    /// THEN it returns the success value.
    /// </summary>
    [Fact]
    public void GetValueOrDefault_OnSuccess_ShouldReturnValue()
    {
        Result<int> r = 42;

        r.GetValueOrDefault().Should().Be(42);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN calling <c>GetValueOr</c>
    /// THEN it returns the success value.
    /// </summary>
    [Fact]
    public void GetValueOr_OnSuccess_ShouldReturnValue()
    {
        Result<int> r = 42;

        r.GetValueOr(_ => -1).Should().Be(42);
    }

    /// <summary>
    /// GIVEN <c>Result&lt;int&gt;.Success(42)</c>
    /// WHEN <c>Match(v => v, _ => -1)</c>
    /// THEN the success branch MUST be invoked and return <c>42</c>.
    /// </summary>
    [Fact]
    public void Match_OnSuccess_ShouldInvokeSuccessBranch()
    {
        Result<int> r = 42;

        r.Match(v => v, _ => -1).Should().Be(42);
    }

    /// <summary>
    /// GIVEN <c>Result&lt;int&gt;.Failure(Error.NotFound("x"))</c>
    /// WHEN <c>Match(v => v, _ => -1)</c>
    /// THEN the failure branch MUST be invoked and return <c>-1</c>.
    /// </summary>
    [Fact]
    public void Match_OnFailure_ShouldInvokeFailureBranch()
    {
        var r = Result<int>.Failure(Error.NotFound("x"));

        r.Match(v => v, _ => -1).Should().Be(-1);
    }
}
