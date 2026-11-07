// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Tests for the binary <c>Combine</c> over two value-bearing results.
/// </summary>
public class CombineTests
{
    /// <summary>
    /// GIVEN two successful results
    /// WHEN they are combined
    /// THEN the tuple holds both values in argument order.
    /// </summary>
    [Fact]
    public void BothSuccess_ShouldReturnTheTupleInArgumentOrder()
    {
        var combined = Result<int>.Success(7).Combine(Result<string>.Success("seven"));

        combined.IsSuccess.Should().BeTrue();
        combined.GetValueOrDefault().Should().Be((7, "seven"));
    }

    /// <summary>
    /// GIVEN a failed first result and a successful second
    /// WHEN they are combined
    /// THEN the first error is returned.
    /// </summary>
    [Fact]
    public void FirstFailure_ShouldWinOverASuccessfulSecond()
    {
        var error = Error.NotFound("first");

        var combined = Result<int>.Failure(error).Combine(Result<string>.Success("seven"));

        combined.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a successful first result and a failed second
    /// WHEN they are combined
    /// THEN the second error is returned.
    /// </summary>
    [Fact]
    public void SecondFailure_ShouldPropagateWhenTheFirstSucceeded()
    {
        var error = Error.Timeout("second");

        var combined = Result<int>.Success(7).Combine(Result<string>.Failure(error));

        combined.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN two failed results
    /// WHEN they are combined
    /// THEN the first error in argument order wins.
    /// </summary>
    [Fact]
    public void BothFailures_ShouldKeepTheFirstInArgumentOrder()
    {
        var firstError = Error.Conflict("first");
        var secondError = Error.NotFound("second");

        var combined = Result<int>.Failure(firstError).Combine(Result<string>.Failure(secondError));

        combined.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(firstError);
    }
}
