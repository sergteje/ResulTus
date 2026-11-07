// Copyright (c) ResulTus. All rights reserved.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace ResulTus.Collections.Tests;

/// <summary>
/// S4: <c>Partition</c> returns exactly sized arrays and never leaks or double-returns a pooled
/// buffer, for every overload and on the doubling path.
/// </summary>
/// <remarks>
/// The pooled-buffer contract is not observable from the public API alone: a leaked buffer and a
/// correct exact copy look the same until the caller mutates the result or the pool hands the same
/// instance to two callers. These tests probe both.
/// </remarks>
public class PartitionPoolingTests
{
    private const int HalfOfThousand = 500;

    /// <summary>
    /// GIVEN a source that implements only <see cref="IReadOnlyList{T}"/>
    /// WHEN it is partitioned
    /// THEN both output arrays hold exactly the counted elements, which forces the doubling path.
    /// </summary>
    [Fact]
    public void Partition_OnDoublingPath_ShouldReturnExactlySizedArrays()
    {
        var source = new ReadOnlyListOnly<Result<int>>(Mixed(1_000));

        var (successes, failures) = source.Partition();

        successes.Should().HaveCount(HalfOfThousand);
        failures.Should().HaveCount(HalfOfThousand);
        successes.Should().Equal(Enumerable.Range(0, 1_000).Where(index => index % 2 == 0));
        failures.Select(error => error.Code)
            .Should().Equal(Enumerable.Range(0, 1_000).Where(index => index % 2 != 0).Select(index => $"fail-{index}"));
    }

    /// <summary>
    /// GIVEN a completed partition
    /// WHEN the caller overwrites both returned arrays
    /// THEN a later call must return untouched values and a different array instance.
    /// </summary>
    [Fact]
    public void Partition_ShouldNotExposePooledBuffers()
    {
        var source = new ReadOnlyListOnly<Result<int>>(Mixed(1_000));
        var (firstSuccesses, firstFailures) = source.Partition();

        Array.Fill(firstSuccesses, -999);
        Array.Fill(firstFailures, Error.NotFound("SENTINEL"));

        var (secondSuccesses, secondFailures) = source.Partition();

        secondSuccesses.Should().NotContain(-999);
        secondFailures.Should().NotContain(error => error.Code == "SENTINEL");
        ReferenceEquals(firstSuccesses, secondSuccesses).Should().BeFalse();
        ReferenceEquals(firstFailures, secondFailures).Should().BeFalse();
        secondSuccesses.Should().HaveCount(HalfOfThousand);
    }

    /// <summary>
    /// GIVEN repeated partitions that grow their buffers
    /// WHEN the pool is asked for many arrays of the same size
    /// THEN no instance may come back twice, which is what a double return would cause.
    /// </summary>
    [Fact]
    public void Partition_ShouldNotReturnABufferTwice()
    {
        var source = new ReadOnlyListOnly<Result<int>>(Mixed(1_000));
        for (var iteration = 0; iteration < 4; iteration++)
        {
            _ = source.Partition();
            _ = ((IEnumerable<Result<int>>)source).Partition();
        }

        foreach (var size in new[] { 8, 16, 512, 1_024 })
        {
            var rented = new int[32][];
            for (var index = 0; index < rented.Length; index++)
            {
                rented[index] = ArrayPool<int>.Shared.Rent(size);
            }

            rented.Select(buffer => RuntimeHelpers.GetHashCode(buffer))
                .Distinct()
                .Should().HaveCount(rented.Length, "a duplicate instance means a buffer was returned twice");

            foreach (var buffer in rented)
            {
                ArrayPool<int>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// GIVEN every source shape
    /// WHEN it is partitioned through each of the three overloads
    /// THEN values, order and array lengths are identical.
    /// </summary>
    [Fact]
    public void Partition_ShouldPreserveValuesForEveryShapeAndOverload()
    {
        var duplicate = Error.Conflict("dup");
        Result<int>[][] shapes =
        [
            [],
            [Result<int>.Success(7)],
            [Result<int>.Failure(Error.NotFound("nf"))],
            [Result<int>.Failure(Error.Conflict("first")), Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3)],
            [Result<int>.Success(0), Result<int>.Success(1), Result<int>.Success(2), Result<int>.Failure(Error.Conflict("last"))],
            [Result<int>.Success(1), Result<int>.Failure(duplicate), Result<int>.Success(3), Result<int>.Failure(duplicate)],
            Mixed(1_000),
        ];

        foreach (var shape in shapes)
        {
            AssertMatchesSource(shape.Partition(), shape);
            AssertMatchesSource(((IReadOnlyList<Result<int>>)shape).Partition(), shape);
            AssertMatchesSource(shape.AsEnumerable().Partition(), shape);
            AssertMatchesSource(new ReadOnlyListOnly<Result<int>>(shape).Partition(), shape);
        }
    }

    private static void AssertMatchesSource((int[] Successes, Error[] Failures) actual, Result<int>[] source)
    {
        var expectedSuccesses = new List<int>();
        var expectedFailures = new List<Error>();
        foreach (var result in source)
        {
            if (result.TryGetValue(out var value, out var error))
            {
                expectedSuccesses.Add(value!);
            }
            else
            {
                expectedFailures.Add(error);
            }
        }

        actual.Successes.Should().Equal(expectedSuccesses);
        actual.Failures.Should().Equal(expectedFailures);
        actual.Successes.Should().HaveCount(expectedSuccesses.Count);
        actual.Failures.Should().HaveCount(expectedFailures.Count);
    }

    private static Result<int>[] Mixed(int count)
    {
        var results = new Result<int>[count];
        for (var index = 0; index < count; index++)
        {
            results[index] = index % 2 == 0
                ? Result<int>.Success(index)
                : Result<int>.Failure(Error.Conflict($"fail-{index}"));
        }

        return results;
    }

    /// <summary>
    /// A read-only list that deliberately does not implement <see cref="ICollection{T}"/>, so the
    /// capacity heuristic cannot read a count and the buffers have to grow.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class ReadOnlyListOnly<T> : IReadOnlyList<T>
    {
        private readonly T[] _items;

        public ReadOnlyListOnly(T[] items) => _items = items;

        public T this[int index] => _items[index];

        public int Count => _items.Length;

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
