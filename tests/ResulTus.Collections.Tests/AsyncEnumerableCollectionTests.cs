// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Tests for the asynchronous mirror of <c>Sequence</c>, <c>Traverse</c> and <c>Partition</c> over
/// <see cref="IAsyncEnumerable{T}"/>. Every stream is hand-written and synchronous underneath, so
/// the suite is deterministic and never waits on a real delay.
/// </summary>
public class AsyncEnumerableCollectionTests
{
    /// <summary>
    /// GIVEN an async stream of three successful results and a token
    /// WHEN <c>SequenceAsync()</c>
    /// THEN the result MUST be a success holding the values in stream order, the enumerator MUST
    /// receive that exact token and MUST be disposed.
    /// </summary>
    [Fact]
    public async Task SequenceAsync_AllSuccess_ShouldCollectValuesInOrder()
    {
        using var cts = new CancellationTokenSource();
        var stream = new TestAsyncStream<Result<int>>(
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Success(3));

        var sequenced = await stream.SequenceAsync(cts.Token);

        sequenced.IsSuccess.Should().BeTrue();
        sequenced.GetValueOrDefault().Should().Equal(1, 2, 3);
        stream.EnumeratorToken.Should().Be(cts.Token);
        stream.Delivered.Should().Be(3);
        stream.IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN an async stream whose second element fails and whose third fails too
    /// WHEN <c>SequenceAsync()</c>
    /// THEN the first failure MUST surface and enumeration MUST stop there.
    /// </summary>
    [Fact]
    public async Task SequenceAsync_OnFailure_ShouldStopAtTheFirstFailure()
    {
        var first = Error.Conflict("dup");
        var stream = new TestAsyncStream<Result<int>>(
            Result<int>.Success(1),
            Result<int>.Failure(first),
            Result<int>.Failure(Error.Unexpected("later")),
            Result<int>.Success(4));

        var sequenced = await stream.SequenceAsync();

        sequenced.IsFailure.Should().BeTrue();
        sequenced.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(first);
        stream.Delivered.Should().Be(2);
        stream.IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN an empty async stream
    /// WHEN <c>SequenceAsync()</c>
    /// THEN the result MUST be a success holding an empty collection.
    /// </summary>
    [Fact]
    public async Task SequenceAsync_Empty_ShouldBeEmptySuccess()
    {
        var stream = new TestAsyncStream<Result<int>>();

        var sequenced = await stream.SequenceAsync();

        sequenced.IsSuccess.Should().BeTrue();
        sequenced.GetValueOrDefault().Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a <c>null</c> stream
    /// WHEN <c>SequenceAsync()</c>
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task SequenceAsync_WithNullSource_ShouldThrow()
    {
        IAsyncEnumerable<Result<int>>? source = null;

        var act = async () => await source!.SequenceAsync();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN an already cancelled token
    /// WHEN <c>SequenceAsync()</c>
    /// THEN it MUST throw <see cref="OperationCanceledException"/> before enumerating at all.
    /// </summary>
    [Fact]
    public async Task SequenceAsync_WithCancelledToken_ShouldThrowBeforeEnumerating()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var stream = new TestAsyncStream<Result<int>>(Result<int>.Success(1));

        var act = async () => await stream.SequenceAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.Delivered.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a stream that cancels its token source halfway through and never looks at the token it
    /// is given
    /// WHEN <c>SequenceAsync()</c>
    /// THEN the cancellation MUST surface as <see cref="OperationCanceledException"/> rather than as
    /// a failed result, because the operation checks the token per element instead of trusting the
    /// source.
    /// </summary>
    [Fact]
    public async Task SequenceAsync_WhenCancelledDuringEnumeration_ShouldThrow()
    {
        using var cts = new CancellationTokenSource();
        var stream = new SelfCancellingStream<Result<int>>(
            cts,
            2,
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Success(3));

        var act = async () => await stream.SequenceAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.Delivered.Should().Be(2);
    }

    /// <summary>
    /// GIVEN an async stream of three source values and a mapper that always succeeds
    /// WHEN <c>TraverseAsync()</c>
    /// THEN the mapped values MUST be collected in stream order.
    /// </summary>
    [Fact]
    public async Task TraverseAsync_AllSuccess_ShouldCollectMappedValuesInOrder()
    {
        var stream = new TestAsyncStream<int>(1, 2, 3);

        var traversed = await stream.TraverseAsync(
            (value, _) => Task.FromResult(Result<string>.Success($"v{value}")));

        traversed.IsSuccess.Should().BeTrue();
        traversed.GetValueOrDefault().Should().Equal("v1", "v2", "v3");
        stream.IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN four source values and a mapper that fails on the second
    /// WHEN <c>TraverseAsync()</c>
    /// THEN the failure MUST surface and the mapper MUST NOT run for later values.
    /// </summary>
    [Fact]
    public async Task TraverseAsync_OnFailure_ShouldStopAtTheFirstFailure()
    {
        var error = Error.Validation("bad");
        var calls = 0;
        var stream = new TestAsyncStream<int>(1, 2, 3, 4);

        var traversed = await stream.TraverseAsync((value, _) =>
        {
            calls++;
            return Task.FromResult(value == 2
                ? Result<string>.Failure(error)
                : Result<string>.Success($"v{value}"));
        });

        traversed.IsFailure.Should().BeTrue();
        traversed.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
        calls.Should().Be(2);
        stream.Delivered.Should().Be(2);
    }

    /// <summary>
    /// GIVEN an empty async stream
    /// WHEN <c>TraverseAsync()</c>
    /// THEN the result MUST be a success holding an empty collection and the mapper MUST NOT run.
    /// </summary>
    [Fact]
    public async Task TraverseAsync_Empty_ShouldBeEmptySuccess()
    {
        var calls = 0;
        var stream = new TestAsyncStream<int>();

        var traversed = await stream.TraverseAsync((value, _) =>
        {
            calls++;
            return Task.FromResult(Result<int>.Success(value));
        });

        traversed.IsSuccess.Should().BeTrue();
        traversed.GetValueOrDefault().Should().BeEmpty();
        calls.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a <c>null</c> stream and, separately, a <c>null</c> mapper
    /// WHEN <c>TraverseAsync()</c>
    /// THEN each MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task TraverseAsync_WithNullArgument_ShouldThrow()
    {
        IAsyncEnumerable<int>? source = null;
        var stream = new TestAsyncStream<int>(1);

        var nullSource = async () => await source!.TraverseAsync(
            (value, _) => Task.FromResult(Result<int>.Success(value)));
        var nullSelector = async () => await stream.TraverseAsync(
            (Func<int, CancellationToken, Task<Result<int>>>)null!);

        await nullSource.Should().ThrowAsync<ArgumentNullException>();
        await nullSelector.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN a token and an async stream of one value
    /// WHEN <c>TraverseAsync()</c>
    /// THEN the mapper and the enumerator MUST both receive that exact token.
    /// </summary>
    [Fact]
    public async Task TraverseAsync_ShouldPassTheTokenToTheMapperAndTheEnumerator()
    {
        using var cts = new CancellationTokenSource();
        var mapperToken = default(CancellationToken);
        var stream = new TestAsyncStream<int>(1);

        var traversed = await stream.TraverseAsync(
            (value, token) =>
            {
                mapperToken = token;
                return Task.FromResult(Result<int>.Success(value * 10));
            },
            cts.Token);

        traversed.GetValueOrDefault().Should().Equal(10);
        mapperToken.Should().Be(cts.Token);
        stream.EnumeratorToken.Should().Be(cts.Token);
    }

    /// <summary>
    /// GIVEN a stream that cancels its token source halfway through
    /// WHEN <c>TraverseAsync()</c>
    /// THEN the cancellation MUST surface as <see cref="OperationCanceledException"/> instead of a
    /// failed result.
    /// </summary>
    [Fact]
    public async Task TraverseAsync_WhenCancelledDuringEnumeration_ShouldThrow()
    {
        using var cts = new CancellationTokenSource();
        var stream = new SelfCancellingStream<int>(
            cts,
            2,
            1,
            2,
            3);

        var act = async () => await stream.TraverseAsync(
            (value, _) => Task.FromResult(Result<int>.Success(value)),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.Delivered.Should().Be(2);
    }

    /// <summary>
    /// GIVEN an already cancelled token
    /// WHEN <c>TraverseAsync()</c>
    /// THEN it MUST throw <see cref="OperationCanceledException"/> without invoking the mapper.
    /// </summary>
    [Fact]
    public async Task TraverseAsync_WithCancelledToken_ShouldNotInvokeTheMapper()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var calls = 0;
        var stream = new TestAsyncStream<int>(1, 2);

        var act = async () => await stream.TraverseAsync(
            (value, _) =>
            {
                calls++;
                return Task.FromResult(Result<int>.Success(value));
            },
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a mixed async stream and a token
    /// WHEN <c>PartitionAsync()</c>
    /// THEN the successes and the failures MUST each keep stream order, MUST be exactly sized, and
    /// the enumerator MUST receive that exact token.
    /// </summary>
    [Fact]
    public async Task PartitionAsync_Mixed_ShouldSplitPreservingOrder()
    {
        using var cts = new CancellationTokenSource();
        var stream = new TestAsyncStream<Result<int>>(
            Result<int>.Failure(Error.NotFound("first")),
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Failure(Error.Conflict("last")),
            Result<int>.Success(3));

        var (successes, failures) = await stream.PartitionAsync(cts.Token);

        successes.Should().Equal(1, 2, 3);
        failures.Select(error => error.Code).Should().Equal("first", "last");
        stream.EnumeratorToken.Should().Be(cts.Token);
        stream.Delivered.Should().Be(5);
        stream.IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN an empty async stream
    /// WHEN <c>PartitionAsync()</c>
    /// THEN both sides MUST be empty arrays, not <c>null</c>.
    /// </summary>
    [Fact]
    public async Task PartitionAsync_Empty_ShouldReturnEmptyArrays()
    {
        var stream = new TestAsyncStream<Result<int>>();

        var (successes, failures) = await stream.PartitionAsync();

        successes.Should().NotBeNull().And.BeEmpty();
        failures.Should().NotBeNull().And.BeEmpty();
    }

    /// <summary>
    /// GIVEN a completed partition
    /// WHEN the caller overwrites both returned arrays
    /// THEN a later call MUST return untouched values and different array instances, because the
    /// result is a copy and never the rented buffer itself.
    /// </summary>
    [Fact]
    public async Task PartitionAsync_ShouldNotExposePooledBuffers()
    {
        var (firstSuccesses, firstFailures) = await Mixed(1_000).PartitionAsync();

        Array.Fill(firstSuccesses, -999);
        Array.Fill(firstFailures, Error.NotFound("SENTINEL"));

        var (secondSuccesses, secondFailures) = await Mixed(1_000).PartitionAsync();

        secondSuccesses.Should().NotContain(-999);
        secondFailures.Should().NotContain(error => error.Code == "SENTINEL");
        ReferenceEquals(firstSuccesses, secondSuccesses).Should().BeFalse();
        ReferenceEquals(firstFailures, secondFailures).Should().BeFalse();
        secondSuccesses.Should().HaveCount(500);
        secondFailures.Should().HaveCount(500);
    }

    /// <summary>
    /// GIVEN repeated partitions that grow their buffers
    /// WHEN the pool is asked for many arrays of the same size
    /// THEN no instance may come back twice, which is what a double return would cause.
    /// </summary>
    [Fact]
    public async Task PartitionAsync_ShouldNotReturnABufferTwice()
    {
        for (var iteration = 0; iteration < 4; iteration++)
        {
            _ = await Mixed(1_000).PartitionAsync();
        }

        foreach (var size in new[] { 8, 16, 512, 1_024 })
        {
            var rented = new int[32][];
            for (var index = 0; index < rented.Length; index++)
            {
                rented[index] = System.Buffers.ArrayPool<int>.Shared.Rent(size);
            }

            rented.Select(buffer => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(buffer))
                .Distinct()
                .Should().HaveCount(rented.Length, "a duplicate instance means a buffer was returned twice");

            foreach (var buffer in rented)
            {
                System.Buffers.ArrayPool<int>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// GIVEN a <c>null</c> stream
    /// WHEN <c>PartitionAsync()</c>
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task PartitionAsync_WithNullSource_ShouldThrow()
    {
        IAsyncEnumerable<Result<int>>? source = null;

        var act = async () => await source!.PartitionAsync();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN an already cancelled token and, separately, a stream that cancels halfway through
    /// WHEN <c>PartitionAsync()</c>
    /// THEN each MUST throw <see cref="OperationCanceledException"/> rather than return a partition.
    /// </summary>
    [Fact]
    public async Task PartitionAsync_WhenCancelled_ShouldThrow()
    {
        using var finished = new CancellationTokenSource();
        finished.Cancel();
        var cancelledStream = new TestAsyncStream<Result<int>>(Result<int>.Success(1));

        var beforeEnumeration = async () => await cancelledStream.PartitionAsync(finished.Token);

        using var midway = new CancellationTokenSource();
        var midwayStream = new SelfCancellingStream<Result<int>>(
            midway,
            1,
            Result<int>.Success(1),
            Result<int>.Success(2));

        var duringEnumeration = async () => await midwayStream.PartitionAsync(midway.Token);

        await beforeEnumeration.Should().ThrowAsync<OperationCanceledException>();
        await duringEnumeration.Should().ThrowAsync<OperationCanceledException>();
        cancelledStream.Delivered.Should().Be(0);
        midwayStream.Delivered.Should().Be(1);
    }

    private static TestAsyncStream<Result<int>> Mixed(int count)
    {
        var results = new Result<int>[count];
        for (var index = 0; index < count; index++)
        {
            results[index] = index % 2 == 0
                ? Result<int>.Success(index)
                : Result<int>.Failure(Error.Conflict($"fail-{index}"));
        }

        return new TestAsyncStream<Result<int>>(results);
    }
}
