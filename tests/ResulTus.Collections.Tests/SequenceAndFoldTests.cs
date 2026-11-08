// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Tests for sequencing, traversal and folding over result collections.
/// </summary>
public class SequenceAndFoldTests
{
    /// <summary>
    /// GIVEN three successful results
    /// WHEN <c>Sequence()</c>
    /// THEN the result MUST be a success holding the three values in order.
    /// </summary>
    [Fact]
    public void Sequence_AllSuccess_ShouldCollectValuesInOrder()
    {
        var results = new[] { Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3) };

        var sequenced = results.Sequence();

        sequenced.IsSuccess.Should().BeTrue();
        sequenced.GetValueOrDefault().Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// GIVEN an empty sequence
    /// WHEN <c>Sequence()</c>
    /// THEN the result MUST be a success holding an empty collection.
    /// </summary>
    [Fact]
    public void Sequence_Empty_ShouldBeEmptySuccess()
    {
        var sequenced = Array.Empty<Result<int>>().Sequence();

        sequenced.IsSuccess.Should().BeTrue();
        sequenced.GetValueOrDefault().Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a sequence whose third element fails
    /// WHEN <c>Sequence()</c>
    /// THEN the failure MUST surface and enumeration MUST stop at that element.
    /// </summary>
    [Fact]
    public void Sequence_OnFailure_ShouldShortCircuitEnumeration()
    {
        var error = Error.Conflict("dup");
        var visited = 0;

        IEnumerable<Result<int>> Source()
        {
            for (var i = 0; i < 5; i++)
            {
                visited++;
                yield return i == 2 ? Result<int>.Failure(error) : Result<int>.Success(i);
            }
        }

        var sequenced = Source().Sequence();

        sequenced.IsFailure.Should().BeTrue();
        sequenced.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
        visited.Should().Be(3);
    }

    /// <summary>
    /// GIVEN a large successful sequence
    /// WHEN <c>Sequence()</c>
    /// THEN every value MUST be collected, proving the pooled buffer grows correctly.
    /// </summary>
    [Fact]
    public void Sequence_LargeInput_ShouldCollectEveryValue()
    {
        var results = Enumerable.Range(0, 10_000).Select(Result<int>.Success);

        var sequenced = results.Sequence();

        sequenced.IsSuccess.Should().BeTrue();
        sequenced.GetValueOrDefault().Should().HaveCount(10_000);
        sequenced.GetValueOrDefault()[9_999].Should().Be(9_999);
    }

    /// <summary>
    /// GIVEN a <c>null</c> sequence
    /// WHEN <c>Sequence()</c>
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Sequence_WithNullSource_ShouldThrow()
    {
        IEnumerable<Result<int>>? source = null;

        var act = () => source!.Sequence();

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN three source values and a selector that fails on the second
    /// WHEN <c>Traverse()</c>
    /// THEN the failure MUST surface and the selector MUST NOT run for later values.
    /// </summary>
    [Fact]
    public void Traverse_OnFailure_ShouldShortCircuit()
    {
        var error = Error.Validation("bad");
        var visited = 0;

        Result<string> Select(int value)
        {
            visited++;
            return value == 2 ? Result<string>.Failure(error) : Result<string>.Success($"v{value}");
        }

        var traversed = new[] { 1, 2, 3, 4 }.Traverse(Select);

        traversed.IsFailure.Should().BeTrue();
        traversed.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
        visited.Should().Be(2);
    }

    /// <summary>
    /// GIVEN three source values and a selector that always succeeds
    /// WHEN <c>Traverse()</c>
    /// THEN the mapped values MUST be collected in order.
    /// </summary>
    [Fact]
    public void Traverse_AllSuccess_ShouldCollectMappedValues()
    {
        var traversed = new[] { 1, 2, 3 }.Traverse(v => Result<string>.Success($"v{v}"));

        traversed.IsSuccess.Should().BeTrue();
        traversed.GetValueOrDefault().Should().Equal("v1", "v2", "v3");
    }

    /// <summary>
    /// GIVEN three successful results
    /// WHEN <c>Fold(0, (acc, v) =&gt; acc + v)</c>
    /// THEN the accumulator MUST be the sum.
    /// </summary>
    [Fact]
    public void Fold_AllSuccess_ShouldAccumulate()
    {
        var results = new[] { Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3) };

        var folded = results.Fold(0, (acc, value) => acc + value);

        folded.IsSuccess.Should().BeTrue();
        folded.GetValueOrDefault().Should().Be(6);
    }

    /// <summary>
    /// GIVEN a sequence with a failure
    /// WHEN <c>Fold</c> is called
    /// THEN the failure MUST surface and the accumulator MUST NOT consume later values.
    /// </summary>
    [Fact]
    public void Fold_OnFailure_ShouldShortCircuit()
    {
        var error = Error.Timeout("slow");
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Failure(error),
            Result<int>.Success(3),
        };

        var folded = results.Fold(0, (acc, value) => acc + value);

        folded.IsFailure.Should().BeTrue();
        folded.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN three successful unit results
    /// WHEN <c>Combine()</c>
    /// THEN the result MUST be a success.
    /// </summary>
    [Fact]
    public void Combine_AllSuccess_ShouldBeSuccess()
    {
        var results = new[] { Result.Success(), Result.Success(), Result.Success() };

        results.Combine().IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a sequence of unit results with a failure
    /// WHEN <c>Combine()</c>
    /// THEN the first failure MUST surface.
    /// </summary>
    [Fact]
    public void Combine_OnFailure_ShouldReturnFirstFailure()
    {
        var error = Error.NotFound("missing");
        var results = new[] { Result.Success(), Result.Failure(error), Result.Failure(Error.Unexpected("later")) };

        var combined = results.Combine();

        combined.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN an empty sequence of unit results
    /// WHEN <c>Combine()</c>
    /// THEN the result MUST be a success.
    /// </summary>
    [Fact]
    public void Combine_Empty_ShouldBeSuccess()
    {
        Array.Empty<Result>().Combine().IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN three successful unit results
    /// WHEN <c>Fold(0, acc =&gt; acc + 1)</c>
    /// THEN the accumulator MUST count them.
    /// </summary>
    [Fact]
    public void UnitFold_AllSuccess_ShouldAccumulate()
    {
        var results = new[] { Result.Success(), Result.Success(), Result.Success() };

        var folded = results.Fold(0, acc => acc + 1);

        folded.IsSuccess.Should().BeTrue();
        folded.GetValueOrDefault().Should().Be(3);
    }
}
