// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Tests for the array and read-only-list unit-result collection overloads.
/// </summary>
public class UnitResultCollectionOverloadTests
{
    private const int MeasurementIterations = 16;

    private static readonly Func<int, int> Increment = static accumulator => accumulator + 1;
    private static readonly Action Ignore = static () => { };

    /// <summary>
    /// GIVEN an array or read-only list of unit results with the failure at either end
    /// WHEN every non-materializing overload is called
    /// THEN it MUST preserve order, short-circuiting and the existing outcomes.
    /// </summary>
    /// <param name="failureIndex">The index of the failed result.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void UnitArrayAndReadOnlyListOverloads_ShouldPreserveFirstAndLastFailures(int failureIndex)
    {
        var error = Error.Conflict($"failure-{failureIndex}");
        var array = CreateResults(failureIndex, error);
        IReadOnlyList<Result> list = CreateResultsList(failureIndex, error);

        AssertFailureBehaviors(
            array,
            failureIndex,
            error,
            source => source.Combine(),
            (source, accumulate) => source.Fold(0, accumulate),
            source => source.FirstFailure(),
            (source, action) => source.ForEach(action),
            source => source.AllSucceeded(),
            source => source.AnySucceeded(),
            source => source.NoneSucceeded());

        AssertFailureBehaviors(
            list,
            failureIndex,
            error,
            source => source.Combine(),
            (source, accumulate) => source.Fold(0, accumulate),
            source => source.FirstFailure(),
            (source, action) => source.ForEach(action),
            source => source.AllSucceeded(),
            source => source.AnySucceeded(),
            source => source.NoneSucceeded());
    }

    /// <summary>
    /// GIVEN all-failure and all-success read-only lists
    /// WHEN FirstFailure is called
    /// THEN it MUST preserve the first error and the empty-error distinction.
    /// </summary>
    [Fact]
    public void UnitReadOnlyListOverloads_AllFailureAndAllSuccess_ShouldPreserveInspectionResults()
    {
        var firstError = Error.Timeout("first");
        IReadOnlyList<Result> allFailures = new List<Result>
        {
            Result.Failure(firstError),
            Result.Failure(Error.Conflict("later")),
        };
        IReadOnlyList<Result> allSuccesses = new List<Result>
        {
            Result.Success(),
            Result.Success(),
        };

        allFailures.FirstFailure().Should().Be(firstError);
        allFailures.Combine().TryGetError(out var combinedError).Should().BeTrue();
        combinedError.Should().Be(firstError);
        allSuccesses.FirstFailure().Should().BeNull();
        allSuccesses.Combine().IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a successful read-only list
    /// WHEN every collection overload is called
    /// THEN values and query results MUST remain in source order.
    /// </summary>
    [Fact]
    public void UnitReadOnlyListOverloads_AllSuccess_ShouldPreserveOutcomes()
    {
        IReadOnlyList<Result> results = new List<Result>
        {
            Result.Success(),
            Result.Success(),
            Result.Success(),
        };
        var visited = 0;

        results.Combine().IsSuccess.Should().BeTrue();
        results.Fold(0, Increment).GetValueOrDefault().Should().Be(3);
        results.FirstFailure().Should().BeNull();
        results.ForEach(() => visited++).IsSuccess.Should().BeTrue();
        visited.Should().Be(3);
        results.AllSucceeded().Should().BeTrue();
        results.AnySucceeded().Should().BeTrue();
        results.NoneSucceeded().Should().BeFalse();
    }

    /// <summary>
    /// GIVEN empty array and read-only-list sources
    /// WHEN every collection overload is called
    /// THEN empty-result behavior and vacuous truth MUST remain unchanged.
    /// </summary>
    [Fact]
    public void UnitArrayAndReadOnlyListOverloads_EmptySources_ShouldPreserveOutcomes()
    {
        Result[] emptyArray = [];
        IReadOnlyList<Result> emptyList = new List<Result>();

        AssertEmptyResults(
            emptyArray,
            source => source.Combine(),
            source => source.Fold(7, Increment),
            source => source.FirstFailure(),
            source => source.ForEach(Ignore),
            source => source.AllSucceeded(),
            source => source.AnySucceeded(),
            source => source.NoneSucceeded());
        AssertEmptyResults(
            emptyList,
            source => source.Combine(),
            source => source.Fold(7, Increment),
            source => source.FirstFailure(),
            source => source.ForEach(Ignore),
            source => source.AllSucceeded(),
            source => source.AnySucceeded(),
            source => source.NoneSucceeded());
    }

    /// <summary>
    /// GIVEN null arrays and null read-only lists
    /// WHEN every array/list overload is called
    /// THEN each source guard MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void UnitArrayAndReadOnlyListOverloads_WithNullSources_ShouldThrow()
    {
        Result[]? nullArray = null;
        IReadOnlyList<Result>? nullList = null;

        var calls = new Action[]
        {
            () => nullArray!.Combine(),
            () => nullList!.Combine(),
            () => nullArray!.Fold(0, Increment),
            () => nullList!.Fold(0, Increment),
            () => nullArray!.FirstFailure(),
            () => nullList!.FirstFailure(),
            () => nullArray!.ForEach(Ignore),
            () => nullList!.ForEach(Ignore),
            () => nullArray!.AllSucceeded(),
            () => nullList!.AllSucceeded(),
            () => nullArray!.AnySucceeded(),
            () => nullList!.AnySucceeded(),
            () => nullArray!.NoneSucceeded(),
            () => nullList!.NoneSucceeded(),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN successful array and read-only-list sources
    /// WHEN the operations are repeated
    /// THEN they MUST allocate zero bytes per call.
    /// </summary>
    [Fact]
    public void UnitArrayAndReadOnlyListOverloads_ShouldAllocateZeroBytes()
    {
        var array = Enumerable.Range(1, 4).Select(_ => Result.Success()).ToArray();
        IReadOnlyList<Result> list = new List<Result>(array);

        var arrayWarmupChecksum = RunNonMaterializing(array, MeasurementIterations);
        var listWarmupChecksum = RunNonMaterializing(list, MeasurementIterations);
        var arrayAllocated = Measure(() => RunNonMaterializing(array, MeasurementIterations), MeasurementIterations);
        var listAllocated = Measure(() => RunNonMaterializing(list, MeasurementIterations), MeasurementIterations);

        arrayWarmupChecksum.Should().Be(listWarmupChecksum);
        arrayAllocated.Should().Be(0);
        listAllocated.Should().Be(0);
    }

    private static Result[] CreateResults(int failureIndex, Error error)
    {
        var results = new Result[4];
        for (var index = 0; index < results.Length; index++)
        {
            results[index] = index == failureIndex
                ? Result.Failure(error)
                : Result.Success();
        }

        return results;
    }

    private static List<Result> CreateResultsList(int failureIndex, Error error)
        => [.. CreateResults(failureIndex, error)];

    private static void AssertFailureBehaviors<TSource>(
        TSource source,
        int failureIndex,
        Error error,
        Func<TSource, Result> combine,
        Func<TSource, Func<int, int>, Result<int>> fold,
        Func<TSource, Error?> firstFailure,
        Func<TSource, Action, Result> forEach,
        Func<TSource, bool> allSucceeded,
        Func<TSource, bool> anySucceeded,
        Func<TSource, bool> noneSucceeded)
    {
        combine(source).TryGetError(out var combinedError).Should().BeTrue();
        combinedError.Should().Be(error);

        var foldCalls = 0;
        var folded = fold(source, accumulator =>
        {
            foldCalls++;
            return accumulator + 1;
        });
        folded.TryGetValue(out _, out var foldError).Should().BeFalse();
        foldError.Should().Be(error);
        foldCalls.Should().Be(failureIndex);

        firstFailure(source).Should().Be(error);

        var visited = 0;
        forEach(source, () => visited++).TryGetError(out var forEachError).Should().BeTrue();
        forEachError.Should().Be(error);
        visited.Should().Be(failureIndex);

        allSucceeded(source).Should().BeFalse();
        anySucceeded(source).Should().BeTrue();
        noneSucceeded(source).Should().BeFalse();
    }

    private static void AssertEmptyResults<TSource>(
        TSource source,
        Func<TSource, Result> combine,
        Func<TSource, Result<int>> fold,
        Func<TSource, Error?> firstFailure,
        Func<TSource, Result> forEach,
        Func<TSource, bool> allSucceeded,
        Func<TSource, bool> anySucceeded,
        Func<TSource, bool> noneSucceeded)
    {
        combine(source).IsSuccess.Should().BeTrue();
        fold(source).GetValueOrDefault().Should().Be(7);
        firstFailure(source).Should().BeNull();
        forEach(source).IsSuccess.Should().BeTrue();
        allSucceeded(source).Should().BeTrue();
        anySucceeded(source).Should().BeFalse();
        noneSucceeded(source).Should().BeTrue();
    }

    private static int RunNonMaterializing(Result[] source, int iterations)
    {
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            checksum += source.Combine().IsSuccess ? 1 : 0;
            checksum += source.Fold(0, Increment).GetValueOrDefault();
            checksum += source.FirstFailure() is null ? 1 : 0;
            checksum += source.ForEach(Ignore).IsSuccess ? 1 : 0;
            checksum += source.AllSucceeded() ? 1 : 0;
            checksum += source.AnySucceeded() ? 1 : 0;
            checksum += source.NoneSucceeded() ? 1 : 0;
        }

        return checksum;
    }

    private static int RunNonMaterializing(IReadOnlyList<Result> source, int iterations)
    {
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            checksum += source.Combine().IsSuccess ? 1 : 0;
            checksum += source.Fold(0, Increment).GetValueOrDefault();
            checksum += source.FirstFailure() is null ? 1 : 0;
            checksum += source.ForEach(Ignore).IsSuccess ? 1 : 0;
            checksum += source.AllSucceeded() ? 1 : 0;
            checksum += source.AnySucceeded() ? 1 : 0;
            checksum += source.NoneSucceeded() ? 1 : 0;
        }

        return checksum;
    }

    private static long Measure(Func<int> operation, int iterations)
        => AllocationProbe.Measure(operation, warmups: MeasurementIterations, iterations: iterations);
}
