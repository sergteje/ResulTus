// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Allocation and behavior coverage for <c>Partition</c> after the pooled-buffer change (S4).
/// </summary>
public class PartitionAllocationTests
{
    private const int MeasurementIterations = 16;

    private static int[]? _partitionSuccessSink;
    private static Error[]? _partitionFailureSink;

    /// <summary>
    /// GIVEN a mixed array and a mixed read-only list
    /// WHEN <c>Partition</c> is repeated
    /// THEN each overload MUST allocate only its two output arrays, matching a hand-written loop.
    /// </summary>
    [Fact]
    public void Partition_ShouldAllocateOnlyOutputArrays()
    {
        var results = CreateMixedResults(1_000);
        IReadOnlyList<Result<int>> resultList = new List<Result<int>>(results);

        Func<int> arrayPartition = () => CollectPartition(results.Partition());
        Func<int> listPartition = () => CollectPartition(resultList.Partition());
        Func<int> arrayPartitionLoop = () => PartitionWithLoop(results);
        Func<int> listPartitionLoop = () => PartitionWithLoop(resultList);

        Measure(arrayPartition, MeasurementIterations);
        Measure(listPartition, MeasurementIterations);
        Measure(arrayPartitionLoop, MeasurementIterations);
        Measure(listPartitionLoop, MeasurementIterations);

        Measure(arrayPartition, MeasurementIterations).Should().Be(Measure(arrayPartitionLoop, MeasurementIterations));
        Measure(listPartition, MeasurementIterations).Should().Be(Measure(listPartitionLoop, MeasurementIterations));
    }

    /// <summary>
    /// GIVEN array, list and enumerable sources where every result failed
    /// WHEN <c>Partition</c> is called
    /// THEN the success array MUST be empty and the failures MUST stay in source order.
    /// </summary>
    [Fact]
    public void Partition_AllFailure_ShouldProduceEmptySuccessesAndOrderedFailures()
    {
        var first = Error.NotFound("first");
        var second = Error.Conflict("second");
        var third = Error.Timeout("third");
        var errors = new[] { first, second, third };

        Result<int>[] array =
        [
            Result<int>.Failure(first),
            Result<int>.Failure(second),
            Result<int>.Failure(third),
        ];
        List<Result<int>> list =
        [
            Result<int>.Failure(first),
            Result<int>.Failure(second),
            Result<int>.Failure(third),
        ];
        IEnumerable<Result<int>> enumerable = array;

        AssertAllFailurePartition(array.Partition(), errors);
        AssertAllFailurePartition(list.Partition(), errors);
        AssertAllFailurePartition(enumerable.Partition(), errors);
    }

    /// <summary>
    /// GIVEN a statically typed <see cref="List{T}"/>
    /// WHEN <c>Partition</c> is called
    /// THEN it MUST bind to the indexed overload and preserve value order and failures.
    /// </summary>
    [Fact]
    public void Partition_StaticallyTypedList_ShouldBindToIndexedOverload()
    {
        var error = Error.Conflict("dup");
        List<Result<int>> list =
        [
            Result<int>.Success(1),
            Result<int>.Failure(error),
            Result<int>.Success(3),
        ];

        var (successes, failures) = list.Partition();

        successes.Should().Equal(1, 3);
        failures.Should().Equal(error);
    }

    private static Result<int>[] CreateMixedResults(int count)
    {
        var results = new Result<int>[count];
        for (var index = 0; index < count; index++)
        {
            results[index] = index % 2 == 0
                ? Result<int>.Success(index)
                : Result<int>.Failure(Error.Conflict($"failure-{index}"));
        }

        return results;
    }

    private static void AssertAllFailurePartition((int[] Successes, Error[] Failures) partition, Error[] expected)
    {
        partition.Successes.Should().BeEmpty();
        partition.Failures.Should().Equal(expected);
    }

    private static int CollectPartition((int[] Successes, Error[] Failures) partition)
    {
        _partitionSuccessSink = partition.Successes;
        _partitionFailureSink = partition.Failures;
        return partition.Successes.Length + partition.Failures.Length;
    }

    private static int PartitionWithLoop(IReadOnlyList<Result<int>> source)
    {
        var successCount = 0;
        for (var index = 0; index < source.Count; index++)
        {
            if (source[index].IsSuccess)
            {
                successCount++;
            }
        }

        var failureCount = source.Count - successCount;
        var successes = new int[successCount];
        var failures = new Error[failureCount];
        var successIndex = 0;
        var failureIndex = 0;
        for (var index = 0; index < source.Count; index++)
        {
            if (source[index].TryGetValue(out var value, out var error))
            {
                successes[successIndex++] = value!;
            }
            else
            {
                failures[failureIndex++] = error;
            }
        }

        _partitionSuccessSink = successes;
        _partitionFailureSink = failures;
        return successes.Length + failures.Length;
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
        GC.KeepAlive(_partitionSuccessSink);
        GC.KeepAlive(_partitionFailureSink);
        return allocated;
    }
}
