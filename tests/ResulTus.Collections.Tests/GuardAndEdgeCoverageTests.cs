// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Covers the null-argument guards, the unit <c>WhenAnyAsync</c> and the buffer growth on traversal.
/// </summary>
public class GuardAndEdgeCoverageTests
{
    /// <summary>
    /// GIVEN a <c>null</c> source or a <c>null</c> delegate
    /// WHEN any generic collection method is called
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void GenericMethods_WithNullArguments_ShouldThrow()
    {
        IEnumerable<Result<int>>? nullResults = null;
        IEnumerable<int>? nullSource = null;

        var calls = new Action[]
        {
            () => nullResults!.Sequence(),
            () => nullSource!.Traverse(v => Result<int>.Success(v)),
            () => new[] { 1 }.Traverse<int, int>(null!),
            () => nullResults!.Fold(0, (acc, v) => acc + v),
            () => new[] { Result<int>.Success(1) }.Fold(0, null!),
            () => nullResults!.FirstFailure(),
            () => nullResults!.FirstSuccess(),
            () => nullResults!.Partition(),
            () => nullResults!.Flatten(),
            () => nullResults!.ForEach(_ => { }),
            () => new[] { Result<int>.Success(1) }.ForEach(null!),
            () => nullResults!.AllSucceeded(),
            () => nullResults!.AnySucceeded(),
            () => nullResults!.NoneSucceeded(),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN a <c>null</c> source or a <c>null</c> delegate
    /// WHEN any unit collection method is called
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void UnitMethods_WithNullArguments_ShouldThrow()
    {
        IEnumerable<Result>? nullResults = null;

        var calls = new Action[]
        {
            () => nullResults!.Combine(),
            () => nullResults!.Fold(0, acc => acc + 1),
            () => new[] { Result.Success() }.Fold(0, (Func<int, int>)null!),
            () => nullResults!.FirstFailure(),
            () => nullResults!.ForEach(() => { }),
            () => new[] { Result.Success() }.ForEach(null!),
            () => nullResults!.AllSucceeded(),
            () => nullResults!.AnySucceeded(),
            () => nullResults!.NoneSucceeded(),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN a <c>null</c> task sequence
    /// WHEN any async collection method is called
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task AsyncMethods_WithNullArguments_ShouldThrow()
    {
        IEnumerable<Task<Result<int>>>? nullTasks = null;
        IEnumerable<Task<Result>>? nullUnitTasks = null;

        var calls = new Func<Task>[]
        {
            async () => await nullTasks!.WhenAllAsync(),
            async () => await nullUnitTasks!.WhenAllAsync(),
            async () => await nullTasks!.WhenAnyAsync(),
            async () => await nullUnitTasks!.WhenAnyAsync(),
        };

        foreach (var call in calls)
        {
            await call.Should().ThrowAsync<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN one completed unit-result task and one that never completes
    /// WHEN the unit <c>WhenAnyAsync</c> is called
    /// THEN the completed result MUST be returned.
    /// </summary>
    [Fact]
    public async Task UnitWhenAnyAsync_ShouldReturnFirstCompleted()
    {
        var never = new TaskCompletionSource<Result>();
        var tasks = new[] { Task.FromResult(Result.Success()), never.Task };

        var first = await tasks.WhenAnyAsync();

        first.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN an empty unit task sequence
    /// WHEN the unit <c>WhenAnyAsync</c> is called
    /// THEN it MUST throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public async Task UnitWhenAnyAsync_Empty_ShouldThrow()
    {
        var act = async () => await Array.Empty<Task<Result>>().WhenAnyAsync();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// GIVEN a unit result sequence with a failure
    /// WHEN <c>Fold</c> is called
    /// THEN the failure MUST surface.
    /// </summary>
    [Fact]
    public void UnitFold_OnFailure_ShouldShortCircuit()
    {
        var error = Error.Timeout("slow");
        var results = new[] { Result.Success(), Result.Failure(error) };

        var folded = results.Fold(0, acc => acc + 1);

        folded.IsFailure.Should().BeTrue();
        folded.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a large source
    /// WHEN <c>Traverse()</c> grows the pooled buffer
    /// THEN every mapped value MUST be collected.
    /// </summary>
    [Fact]
    public void Traverse_LargeInput_ShouldGrowAndCollectEveryValue()
    {
        var traversed = Enumerable.Range(0, 5_000).Traverse(v => Result<int>.Success(v * 2));

        traversed.IsSuccess.Should().BeTrue();
        traversed.GetValueOrDefault().Should().HaveCount(5_000);
        traversed.GetValueOrDefault()[4_999].Should().Be(9_998);
    }
}
