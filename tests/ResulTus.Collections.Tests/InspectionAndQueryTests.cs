// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Tests for the inspection, partition, flatten and query operations.
/// </summary>
public class InspectionAndQueryTests
{
    /// <summary>
    /// GIVEN a sequence with two failures
    /// WHEN <c>FirstFailure()</c>
    /// THEN the first error MUST be returned.
    /// </summary>
    [Fact]
    public void FirstFailure_WithFailures_ShouldReturnFirstError()
    {
        var first = Error.NotFound("first");
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Failure(first),
            Result<int>.Failure(Error.Unexpected("second")),
        };

        results.FirstFailure().Should().Be(first);
    }

    /// <summary>
    /// GIVEN a sequence with no failures
    /// WHEN <c>FirstFailure()</c>
    /// THEN it MUST return <c>null</c>.
    /// </summary>
    [Fact]
    public void FirstFailure_AllSuccess_ShouldReturnNull()
    {
        new[] { Result<int>.Success(1) }.FirstFailure().Should().BeNull();
    }

    /// <summary>
    /// GIVEN a mixed sequence
    /// WHEN <c>FirstSuccess()</c>
    /// THEN the first successful result MUST be returned.
    /// </summary>
    [Fact]
    public void FirstSuccess_WithSuccess_ShouldReturnFirstSuccess()
    {
        var results = new[]
        {
            Result<int>.Failure(Error.NotFound("missing")),
            Result<int>.Success(7),
            Result<int>.Success(9),
        };

        var first = results.FirstSuccess();

        first.IsSuccess.Should().BeTrue();
        first.GetValueOrDefault().Should().Be(7);
    }

    /// <summary>
    /// GIVEN a sequence where every element fails
    /// WHEN <c>FirstSuccess()</c>
    /// THEN the first failure MUST be returned.
    /// </summary>
    [Fact]
    public void FirstSuccess_AllFailure_ShouldReturnFirstError()
    {
        var first = Error.Timeout("slow");
        var results = new[] { Result<int>.Failure(first), Result<int>.Failure(Error.Unexpected("later")) };

        var result = results.FirstSuccess();

        result.IsFailure.Should().BeTrue();
        result.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(first);
    }

    /// <summary>
    /// GIVEN an empty sequence
    /// WHEN <c>FirstSuccess()</c>
    /// THEN it MUST return a failure with code <c>"empty"</c>.
    /// </summary>
    [Fact]
    public void FirstSuccess_Empty_ShouldReturnEmptyError()
    {
        var result = Array.Empty<Result<int>>().FirstSuccess();

        result.IsFailure.Should().BeTrue();
        result.TryGetValue(out _, out var error).Should().BeFalse();
        error.Code.Should().Be("empty");
    }

    /// <summary>
    /// GIVEN a mixed sequence
    /// WHEN <c>Partition()</c>
    /// THEN successes and failures MUST be split in order.
    /// </summary>
    [Fact]
    public void Partition_ShouldSplitSuccessesAndFailures()
    {
        var error = Error.Conflict("dup");
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Failure(error),
            Result<int>.Success(3),
        };

        var (successes, failures) = results.Partition();

        successes.Should().Equal(1, 3);
        failures.Should().Equal(error);
    }

    /// <summary>
    /// GIVEN an empty sequence
    /// WHEN <c>Partition()</c>
    /// THEN both sides MUST be empty.
    /// </summary>
    [Fact]
    public void Partition_Empty_ShouldReturnEmptySides()
    {
        var (successes, failures) = Array.Empty<Result<int>>().Partition();

        successes.Should().BeEmpty();
        failures.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a mixed sequence
    /// WHEN <c>Flatten()</c> is enumerated
    /// THEN only the success values MUST be yielded, in order.
    /// </summary>
    [Fact]
    public void Flatten_ShouldYieldOnlySuccessValues()
    {
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Failure(Error.NotFound("missing")),
            Result<int>.Success(3),
        };

        results.Flatten().Should().Equal(1, 3);
    }

    /// <summary>
    /// GIVEN a mixed sequence
    /// WHEN <c>ForEach</c> runs an action
    /// THEN the action MUST run per success and the first failure MUST stop it.
    /// </summary>
    [Fact]
    public void ForEach_OnFailure_ShouldStopAtFirstError()
    {
        var error = Error.Validation("bad");
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Failure(error),
            Result<int>.Success(3),
        };
        var seen = new List<int>();

        var outcome = results.ForEach(seen.Add);

        seen.Should().Equal(1);
        outcome.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN sequences that are all-success, mixed and empty
    /// WHEN the three query methods run
    /// THEN they MUST follow LINQ's vacuous-truth conventions.
    /// </summary>
    [Fact]
    public void Queries_ShouldFollowVacuousTruth()
    {
        var allSuccess = new[] { Result<int>.Success(1), Result<int>.Success(2) };
        var mixed = new[] { Result<int>.Success(1), Result<int>.Failure(Error.NotFound("x")) };
        var empty = Array.Empty<Result<int>>();

        allSuccess.AllSucceeded().Should().BeTrue();
        allSuccess.AnySucceeded().Should().BeTrue();
        allSuccess.NoneSucceeded().Should().BeFalse();

        mixed.AllSucceeded().Should().BeFalse();
        mixed.AnySucceeded().Should().BeTrue();
        mixed.NoneSucceeded().Should().BeFalse();

        empty.AllSucceeded().Should().BeTrue();
        empty.AnySucceeded().Should().BeFalse();
        empty.NoneSucceeded().Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a mixed sequence of unit results
    /// WHEN the inspection and query methods run
    /// THEN they MUST report the same way as the generic ones.
    /// </summary>
    [Fact]
    public void UnitVariants_ShouldInspectAndQuery()
    {
        var error = Error.Conflict("dup");
        var mixed = new[] { Result.Success(), Result.Failure(error) };
        var seen = 0;

        mixed.FirstFailure().Should().Be(error);
        mixed.AllSucceeded().Should().BeFalse();
        mixed.AnySucceeded().Should().BeTrue();
        mixed.NoneSucceeded().Should().BeFalse();
        mixed.ForEach(() => seen++).TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
        seen.Should().Be(1);
    }
}
