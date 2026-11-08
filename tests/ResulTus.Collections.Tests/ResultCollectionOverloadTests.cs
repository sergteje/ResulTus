// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Tests for the array and read-only-list collection overloads.
/// </summary>
public class ResultCollectionOverloadTests
{
    private const int MeasurementIterations = 16;

    private static readonly Func<int, int, int> Accumulate = static (accumulator, value) => accumulator + value;
    private static readonly Action<int> Ignore = static _ => { };
    private static readonly Func<int, Result<int>> Select = static value => Result<int>.Success(value * 2);

    private static IReadOnlyList<int>? _materializedSink;

    /// <summary>
    /// GIVEN an array or read-only list with the failure at either end
    /// WHEN every non-materializing overload is called
    /// THEN it MUST preserve order, short-circuiting and the existing outcomes.
    /// </summary>
    /// <param name="failureIndex">The index of the failed result.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ArrayAndReadOnlyListOverloads_ShouldPreserveFirstAndLastFailures(int failureIndex)
    {
        var error = Error.Conflict($"failure-{failureIndex}");
        var array = CreateResults(failureIndex, error);
        IReadOnlyList<Result<int>> list = CreateResultsList(failureIndex, error);

        AssertFailureBehaviors(
            array,
            failureIndex,
            error,
            source => source.Sequence(),
            (source, accumulate) => source.Fold(0, accumulate),
            source => source.FirstFailure(),
            source => source.FirstSuccess(),
            source => source.Partition(),
            (source, action) => source.ForEach(action),
            source => source.AllSucceeded(),
            source => source.AnySucceeded(),
            source => source.NoneSucceeded());

        AssertFailureBehaviors(
            list,
            failureIndex,
            error,
            source => source.Sequence(),
            (source, accumulate) => source.Fold(0, accumulate),
            source => source.FirstFailure(),
            source => source.FirstSuccess(),
            source => source.Partition(),
            (source, action) => source.ForEach(action),
            source => source.AllSucceeded(),
            source => source.AnySucceeded(),
            source => source.NoneSucceeded());

        var sourceArray = new[] { 0, 1, 2, 3 };
        IReadOnlyList<int> sourceList = new List<int>(sourceArray);
        AssertTraverseFailure(sourceArray, failureIndex, error, (source, selector) => source.Traverse(selector));
        AssertTraverseFailure(sourceList, failureIndex, error, (source, selector) => source.Traverse(selector));
    }

    /// <summary>
    /// GIVEN all-failure and all-success read-only lists
    /// WHEN FirstFailure and FirstSuccess are called
    /// THEN they MUST preserve the first error and the empty-error distinction.
    /// </summary>
    [Fact]
    public void ReadOnlyListOverloads_AllFailureAndAllSuccess_ShouldPreserveInspectionResults()
    {
        var firstError = Error.Timeout("first");
        IReadOnlyList<Result<int>> allFailures = new List<Result<int>>
        {
            Result<int>.Failure(firstError),
            Result<int>.Failure(Error.Conflict("later")),
        };
        IReadOnlyList<Result<int>> allSuccesses = new List<Result<int>>
        {
            Result<int>.Success(7),
            Result<int>.Success(9),
        };

        allFailures.FirstFailure().Should().Be(firstError);
        allFailures.FirstSuccess().TryGetValue(out _, out var firstSuccessError).Should().BeFalse();
        firstSuccessError.Should().Be(firstError);
        allSuccesses.FirstFailure().Should().BeNull();
        allSuccesses.FirstSuccess().GetValueOrDefault().Should().Be(7);
    }

    /// <summary>
    /// GIVEN a successful read-only list and an integer read-only list
    /// WHEN every collection overload is called
    /// THEN values and query results MUST remain in source order.
    /// </summary>
    [Fact]
    public void ReadOnlyListOverloads_AllSuccess_ShouldPreserveValuesAndOrder()
    {
        IReadOnlyList<Result<int>> results = new List<Result<int>>
        {
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Success(3),
        };
        IReadOnlyList<int> values = new List<int> { 1, 2, 3 };
        var seen = new List<int>();

        results.Sequence().GetValueOrDefault().Should().Equal(1, 2, 3);
        values.Traverse(Select).GetValueOrDefault().Should().Equal(2, 4, 6);
        results.Fold(0, Accumulate).GetValueOrDefault().Should().Be(6);
        results.FirstFailure().Should().BeNull();
        results.FirstSuccess().GetValueOrDefault().Should().Be(1);
        var partitioned = results.Partition();
        partitioned.Successes.Should().Equal(1, 2, 3);
        partitioned.Failures.Should().BeEmpty();
        results.ForEach(seen.Add).IsSuccess.Should().BeTrue();
        seen.Should().Equal(1, 2, 3);
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
    public void ArrayAndReadOnlyListOverloads_EmptySources_ShouldPreserveOutcomes()
    {
        Result<int>[] emptyArray = [];
        IReadOnlyList<Result<int>> emptyList = new List<Result<int>>();
        int[] emptySourceArray = [];
        IReadOnlyList<int> emptySourceList = new List<int>();

        AssertEmptyResults(
            emptyArray,
            source => source.Sequence(),
            source => source.Fold(7, Accumulate),
            source => source.FirstFailure(),
            source => source.FirstSuccess(),
            source => source.Partition(),
            source => source.ForEach(Ignore),
            source => source.AllSucceeded(),
            source => source.AnySucceeded(),
            source => source.NoneSucceeded());
        AssertEmptyResults(
            emptyList,
            source => source.Sequence(),
            source => source.Fold(7, Accumulate),
            source => source.FirstFailure(),
            source => source.FirstSuccess(),
            source => source.Partition(),
            source => source.ForEach(Ignore),
            source => source.AllSucceeded(),
            source => source.AnySucceeded(),
            source => source.NoneSucceeded());
        AssertEmptyTraverse(emptySourceArray, (source, selector) => source.Traverse(selector));
        AssertEmptyTraverse(emptySourceList, (source, selector) => source.Traverse(selector));
    }

    /// <summary>
    /// GIVEN null arrays and null read-only lists
    /// WHEN every array/list overload is called
    /// THEN each source guard MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void ArrayAndReadOnlyListOverloads_WithNullSources_ShouldThrow()
    {
        Result<int>[]? nullArray = null;
        IReadOnlyList<Result<int>>? nullResultList = null;
        int[]? nullSourceArray = null;
        IReadOnlyList<int>? nullSourceList = null;

        var calls = new Action[]
        {
            () => nullArray!.Sequence(),
            () => nullResultList!.Sequence(),
            () => nullSourceArray!.Traverse(Select),
            () => nullSourceList!.Traverse(Select),
            () => nullArray!.Fold(0, Accumulate),
            () => nullResultList!.Fold(0, Accumulate),
            () => nullArray!.FirstFailure(),
            () => nullResultList!.FirstFailure(),
            () => nullArray!.FirstSuccess(),
            () => nullResultList!.FirstSuccess(),
            () => nullArray!.Partition(),
            () => nullResultList!.Partition(),
            () => nullArray!.ForEach(Ignore),
            () => nullResultList!.ForEach(Ignore),
            () => nullArray!.AllSucceeded(),
            () => nullResultList!.AllSucceeded(),
            () => nullArray!.AnySucceeded(),
            () => nullResultList!.AnySucceeded(),
            () => nullArray!.NoneSucceeded(),
            () => nullResultList!.NoneSucceeded(),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN successful array and read-only-list sources
    /// WHEN the non-materializing operations are repeated
    /// THEN they MUST allocate zero bytes per call.
    /// </summary>
    [Fact]
    public void NonMaterializingArrayAndReadOnlyListOverloads_ShouldAllocateZeroBytes()
    {
        var array = Enumerable.Range(1, 4).Select(Result<int>.Success).ToArray();
        IReadOnlyList<Result<int>> list = new List<Result<int>>(array);

        var arrayWarmupChecksum = RunNonMaterializing(array, MeasurementIterations);
        var listWarmupChecksum = RunNonMaterializing(list, MeasurementIterations);
        var arrayAllocated = Measure(() => RunNonMaterializing(array, MeasurementIterations), MeasurementIterations);
        var listAllocated = Measure(() => RunNonMaterializing(list, MeasurementIterations), MeasurementIterations);

        arrayWarmupChecksum.Should().Be(listWarmupChecksum);
        arrayAllocated.Should().Be(0);
        listAllocated.Should().Be(0);
    }

    /// <summary>
    /// GIVEN successful array and read-only-list sources
    /// WHEN Sequence and Traverse materialize their output
    /// THEN each overload MUST allocate only the output array, matching a hand-written indexed loop.
    /// </summary>
    [Fact]
    public void MaterializingArrayAndReadOnlyListOverloads_ShouldAllocateOnlyOutputArrays()
    {
        var results = Enumerable.Range(0, 1_000).Select(Result<int>.Success).ToArray();
        IReadOnlyList<Result<int>> resultList = new List<Result<int>>(results);
        var values = Enumerable.Range(0, 1_000).ToArray();
        IReadOnlyList<int> valueList = new List<int>(values);

        Func<int> arraySequence = () => Collect(results.Sequence());
        Func<int> listSequence = () => Collect(resultList.Sequence());
        Func<int> arrayTraverse = () => Collect(values.Traverse(Select));
        Func<int> listTraverse = () => Collect(valueList.Traverse(Select));
        Func<int> arraySequenceLoop = () => SequenceWithLoop(results);
        Func<int> listSequenceLoop = () => SequenceWithLoop(resultList);
        Func<int> arrayTraverseLoop = () => TraverseWithLoop(values);
        Func<int> listTraverseLoop = () => TraverseWithLoop(valueList);

        Measure(arraySequence, MeasurementIterations);
        Measure(listSequence, MeasurementIterations);
        Measure(arrayTraverse, MeasurementIterations);
        Measure(listTraverse, MeasurementIterations);

        Measure(arraySequenceLoop, MeasurementIterations);
        Measure(listSequenceLoop, MeasurementIterations);
        Measure(arrayTraverseLoop, MeasurementIterations);
        Measure(listTraverseLoop, MeasurementIterations);

        Measure(arraySequence, MeasurementIterations).Should().Be(Measure(arraySequenceLoop, MeasurementIterations));
        Measure(listSequence, MeasurementIterations).Should().Be(Measure(listSequenceLoop, MeasurementIterations));
        Measure(arrayTraverse, MeasurementIterations).Should().Be(Measure(arrayTraverseLoop, MeasurementIterations));
        Measure(listTraverse, MeasurementIterations).Should().Be(Measure(listTraverseLoop, MeasurementIterations));
    }

    private static Result<int>[] CreateResults(int failureIndex, Error error)
    {
        var results = new Result<int>[4];
        for (var index = 0; index < results.Length; index++)
        {
            results[index] = index == failureIndex
                ? Result<int>.Failure(error)
                : Result<int>.Success(index);
        }

        return results;
    }

    private static List<Result<int>> CreateResultsList(int failureIndex, Error error)
        => [.. CreateResults(failureIndex, error)];

    private static void AssertFailureBehaviors<TSource>(
        TSource source,
        int failureIndex,
        Error error,
        Func<TSource, Result<IReadOnlyList<int>>> sequence,
        Func<TSource, Func<int, int, int>, Result<int>> fold,
        Func<TSource, Error?> firstFailure,
        Func<TSource, Result<int>> firstSuccess,
        Func<TSource, (int[] Successes, Error[] Failures)> partition,
        Func<TSource, Action<int>, Result> forEach,
        Func<TSource, bool> allSucceeded,
        Func<TSource, bool> anySucceeded,
        Func<TSource, bool> noneSucceeded)
    {
        sequence(source).TryGetValue(out _, out var sequenceError).Should().BeFalse();
        sequenceError.Should().Be(error);

        var foldCalls = 0;
        var folded = fold(source, (accumulator, value) =>
        {
            foldCalls++;
            return accumulator + value;
        });
        folded.TryGetValue(out _, out var foldError).Should().BeFalse();
        foldError.Should().Be(error);
        foldCalls.Should().Be(failureIndex);

        firstFailure(source).Should().Be(error);

        var firstSuccessfulIndex = failureIndex == 0 ? 1 : 0;
        firstSuccess(source).GetValueOrDefault().Should().Be(firstSuccessfulIndex);

        var partitioned = partition(source);
        partitioned.Successes.Should().Equal(Enumerable.Range(0, 4).Where(index => index != failureIndex));
        partitioned.Failures.Should().Equal(error);

        var visited = 0;
        forEach(source, _ => visited++).TryGetError(out var forEachError).Should().BeTrue();
        forEachError.Should().Be(error);
        visited.Should().Be(failureIndex);

        allSucceeded(source).Should().BeFalse();
        anySucceeded(source).Should().BeTrue();
        noneSucceeded(source).Should().BeFalse();
    }

    private static void AssertTraverseFailure<TSource>(
        TSource source,
        int failureIndex,
        Error error,
        Func<TSource, Func<int, Result<int>>, Result<IReadOnlyList<int>>> traverse)
    {
        var visited = 0;
        Result<int> SelectValue(int value)
        {
            visited++;
            return value == failureIndex ? Result<int>.Failure(error) : Result<int>.Success(value);
        }

        var result = traverse(source, SelectValue);

        result.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
        visited.Should().Be(failureIndex + 1);
    }

    private static void AssertEmptyResults<TSource>(
        TSource source,
        Func<TSource, Result<IReadOnlyList<int>>> sequence,
        Func<TSource, Result<int>> fold,
        Func<TSource, Error?> firstFailure,
        Func<TSource, Result<int>> firstSuccess,
        Func<TSource, (int[] Successes, Error[] Failures)> partition,
        Func<TSource, Result> forEach,
        Func<TSource, bool> allSucceeded,
        Func<TSource, bool> anySucceeded,
        Func<TSource, bool> noneSucceeded)
    {
        var sequenced = sequence(source);
        sequenced.IsSuccess.Should().BeTrue();
        sequenced.GetValueOrDefault().Should().BeEmpty();
        fold(source).GetValueOrDefault().Should().Be(7);
        firstFailure(source).Should().BeNull();

        var firstSuccessResult = firstSuccess(source);
        firstSuccessResult.TryGetValue(out _, out var firstSuccessError).Should().BeFalse();
        firstSuccessError.Code.Should().Be("empty");

        var partitioned = partition(source);
        partitioned.Successes.Should().BeEmpty();
        partitioned.Failures.Should().BeEmpty();

        forEach(source).IsSuccess.Should().BeTrue();
        allSucceeded(source).Should().BeTrue();
        anySucceeded(source).Should().BeFalse();
        noneSucceeded(source).Should().BeTrue();
    }

    private static void AssertEmptyTraverse<TSource>(
        TSource source,
        Func<TSource, Func<int, Result<int>>, Result<IReadOnlyList<int>>> traverse)
    {
        var calls = 0;
        Result<int> Selector(int value)
        {
            calls++;
            return Result<int>.Success(value);
        }

        var result = traverse(source, Selector);

        result.IsSuccess.Should().BeTrue();
        result.GetValueOrDefault().Should().BeEmpty();
        calls.Should().Be(0);
    }

    private static int RunNonMaterializing(Result<int>[] source, int iterations)
    {
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            checksum += source.Fold(0, Accumulate).GetValueOrDefault();
            checksum += source.FirstFailure() is null ? 1 : 0;
            checksum += source.FirstSuccess().GetValueOrDefault();
            checksum += source.ForEach(Ignore).IsSuccess ? 1 : 0;
            checksum += source.AllSucceeded() ? 1 : 0;
            checksum += source.AnySucceeded() ? 1 : 0;
            checksum += source.NoneSucceeded() ? 1 : 0;
        }

        return checksum;
    }

    private static int RunNonMaterializing(IReadOnlyList<Result<int>> source, int iterations)
    {
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            checksum += source.Fold(0, Accumulate).GetValueOrDefault();
            checksum += source.FirstFailure() is null ? 1 : 0;
            checksum += source.FirstSuccess().GetValueOrDefault();
            checksum += source.ForEach(Ignore).IsSuccess ? 1 : 0;
            checksum += source.AllSucceeded() ? 1 : 0;
            checksum += source.AnySucceeded() ? 1 : 0;
            checksum += source.NoneSucceeded() ? 1 : 0;
        }

        return checksum;
    }

    private static long Measure(Func<int> operation, int iterations)
    {
        _ = operation();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            checksum += operation();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(checksum);
        GC.KeepAlive(_materializedSink);
        return allocated;
    }

    private static int Collect(Result<IReadOnlyList<int>> result)
    {
        var values = result.GetValueOrDefault()!;
        _materializedSink = values;
        return values.Count;
    }

    private static int SequenceWithLoop(IReadOnlyList<Result<int>> source)
    {
        var values = new int[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            if (!source[index].TryGetValue(out var value, out _))
            {
                throw new InvalidOperationException("The allocation probe requires successful results.");
            }

            values[index] = value!;
        }

        _materializedSink = values;
        return values.Length;
    }

    private static int TraverseWithLoop(IReadOnlyList<int> source)
    {
        var values = new int[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            values[index] = source[index] * 2;
        }

        _materializedSink = values;
        return values.Length;
    }
}
