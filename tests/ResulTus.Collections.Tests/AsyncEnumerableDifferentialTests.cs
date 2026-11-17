// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Differential tests: the same input runs through the synchronous operation and through its
/// asynchronous mirror, and the two outcomes are compared with each other instead of with a second
/// hand-written expectation. The number of elements each side consumed is compared the same way, so
/// the first-failure rule has to match, not merely the returned value.
/// </summary>
public class AsyncEnumerableDifferentialTests
{
    /// <summary>
    /// GIVEN the same sequence of results
    /// WHEN <c>Sequence()</c> and <c>SequenceAsync()</c> each consume it
    /// THEN both MUST return the same outcome and MUST stop after the same number of elements.
    /// </summary>
    [Fact]
    public async Task SequenceAsync_ShouldMatchSequence()
    {
        Result<int>[][] shapes =
        [
            [],
            [Result<int>.Success(7)],
            [Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3)],
            [
                Result<int>.Success(1),
                Result<int>.Failure(Error.Conflict("first")),
                Result<int>.Failure(Error.Unexpected("later")),
            ],
            [Result<int>.Success(0), Result<int>.Failure(Error.Validation("bad"))],
        ];

        foreach (var shape in shapes)
        {
            var syncConsumed = 0;

            IEnumerable<Result<int>> Source()
            {
                for (var index = 0; index < shape.Length; index++)
                {
                    syncConsumed++;
                    yield return shape[index];
                }
            }

            var stream = new TestAsyncStream<Result<int>>(shape);

            var synchronous = Source().Sequence();
            var asynchronous = await stream.SequenceAsync();

            AssertSameOutcome(synchronous, asynchronous);
            stream.Delivered.Should().Be(syncConsumed);
        }
    }

    /// <summary>
    /// GIVEN the same source values and a mapper with the same rule on both sides
    /// WHEN <c>Traverse()</c> and <c>TraverseAsync()</c> each consume them
    /// THEN both MUST return the same outcome and MUST invoke the mapper the same number of times.
    /// </summary>
    [Fact]
    public async Task TraverseAsync_ShouldMatchTraverse()
    {
        int[][] shapes =
        [
            [],
            [1],
            [1, 2, 3],
            [1, 2, 0, 4],
        ];

        foreach (var shape in shapes)
        {
            var syncCalls = 0;
            var asyncCalls = 0;
            var syncConsumed = 0;

            IEnumerable<int> Source()
            {
                for (var index = 0; index < shape.Length; index++)
                {
                    syncConsumed++;
                    yield return shape[index];
                }
            }

            var stream = new TestAsyncStream<int>(shape);

            var synchronous = Source().Traverse(value => Map(value, ref syncCalls));
            var asynchronous = await stream.TraverseAsync(
                (value, _) => Task.FromResult(Map(value, ref asyncCalls)));

            AssertSameOutcome(synchronous, asynchronous);
            asyncCalls.Should().Be(syncCalls);
            stream.Delivered.Should().Be(syncConsumed);
        }
    }

    /// <summary>
    /// GIVEN the same sequence of results
    /// WHEN <c>Partition()</c> and <c>PartitionAsync()</c> each consume it
    /// THEN both sides MUST hold the same values in the same order and be exactly sized.
    /// </summary>
    [Fact]
    public async Task PartitionAsync_ShouldMatchPartition()
    {
        Result<int>[][] shapes =
        [
            [],
            [Result<int>.Success(7)],
            [Result<int>.Failure(Error.NotFound("nf"))],
            [
                Result<int>.Failure(Error.Conflict("first")),
                Result<int>.Success(1),
                Result<int>.Success(2),
                Result<int>.Failure(Error.Conflict("first")),
            ],
            [Result<int>.Success(0), Result<int>.Success(1), Result<int>.Failure(Error.Conflict("last"))],
            Generated(300),
        ];

        foreach (var shape in shapes)
        {
            var stream = new TestAsyncStream<Result<int>>(shape);

            var (syncSuccesses, syncFailures) = shape.AsEnumerable().Partition();
            var (asyncSuccesses, asyncFailures) = await stream.PartitionAsync();

            asyncSuccesses.Should().Equal(syncSuccesses);
            asyncFailures.Should().Equal(syncFailures);
            asyncSuccesses.Should().HaveCount(syncSuccesses.Length);
            asyncFailures.Should().HaveCount(syncFailures.Length);
            stream.Delivered.Should().Be(shape.Length);
        }
    }

    /// <summary>
    /// Compares two sequenced outcomes: the same success values in the same order, or the same
    /// error.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="synchronous">The outcome of the synchronous operation.</param>
    /// <param name="asynchronous">The outcome of the asynchronous mirror.</param>
    private static void AssertSameOutcome<T>(
        Result<IReadOnlyList<T>> synchronous,
        Result<IReadOnlyList<T>> asynchronous)
    {
        synchronous.IsSuccess.Should().Be(asynchronous.IsSuccess);

        if (synchronous.TryGetValue(out var syncValues, out var syncError))
        {
            asynchronous.TryGetValue(out var asyncValues, out _).Should().BeTrue();
            asyncValues.Should().Equal(syncValues);
        }
        else
        {
            asynchronous.TryGetValue(out _, out var asyncError).Should().BeFalse();
            asyncError.Should().Be(syncError);
        }
    }

    /// <summary>
    /// The mapper both sides share: it fails on a non-positive value, so the failure lands in the
    /// middle of a shape and the short-circuit has to match.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <param name="calls">Invocation counter owned by the caller.</param>
    /// <returns>The mapped result.</returns>
    private static Result<string> Map(int value, ref int calls)
    {
        calls++;
        return value > 0
            ? Result<string>.Success($"v{value}")
            : Result<string>.Failure(Error.Validation("bad"));
    }

    /// <summary>
    /// Builds a mixed batch large enough to make the pooled buffers grow on both sides.
    /// </summary>
    /// <param name="count">How many results to build.</param>
    /// <returns>The generated results.</returns>
    private static Result<int>[] Generated(int count)
    {
        var results = new Result<int>[count];
        for (var index = 0; index < count; index++)
        {
            results[index] = index % 3 == 0
                ? Result<int>.Failure(Error.Conflict($"fail-{index}"))
                : Result<int>.Success(index);
        }

        return results;
    }
}
