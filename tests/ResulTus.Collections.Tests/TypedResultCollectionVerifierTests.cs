// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Independent probes for S9 (typed-tier <c>Sequence</c>/<c>Traverse</c>/<c>Fold</c>), promoted from
/// the delegated verification of <c>0a961e0</c>.
/// </summary>
public class TypedResultCollectionVerifierTests
{
    private sealed class TaggedError
    {
        public TaggedError(string code) => Code = code;

        public string Code { get; }
    }

    private static readonly TaggedError First = new("first");
    private static readonly TaggedError Second = new("second");
    private static readonly Func<int, int, int> Sum = static (accumulator, value) => accumulator + value;
    private static IReadOnlyList<int>? _sink;
    private static int _intSink;

    private static Result<int, TaggedError> S(int value) => Result<int, TaggedError>.Success(value);

    private static Result<int, TaggedError> F(TaggedError error) => Result<int, TaggedError>.Failure(error);

    private static (bool Ok, int[] Values, TaggedError? Error) Read(
        Result<IReadOnlyList<int>, TaggedError> result)
    {
        result.Deconstruct(out var ok, out var values, out var error);
        return (ok, values is int[] array ? array : Array.Empty<int>(), error);
    }

    private static long Measure(Action action, int iterations)
    {
        action();
        action();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            action();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>
    /// GIVEN one failing element at any position
    /// WHEN every operator runs over every source shape
    /// THEN the exact first-failure object MUST be returned by reference.
    /// </summary>
    /// <param name="position">The failing index under test.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FirstFailure_InAnyPosition_IsReturnedByReferenceFromEveryShape(int position)
    {
        var items = new Result<int, TaggedError>[3];
        for (var i = 0; i < 3; i++)
        {
            items[i] = i == position ? F(First) : S(i);
        }

        var list = new List<Result<int, TaggedError>>(items);
        IEnumerable<Result<int, TaggedError>> lazy = items;

        Assert.Same(First, Read(items.Sequence()).Error);
        Assert.Same(First, Read(list.Sequence()).Error);
        Assert.Same(First, Read(lazy.Sequence()).Error);
        Assert.Same(First, Read(items.Traverse(static r => r)).Error);
        Assert.Same(First, Read(list.Traverse(static r => r)).Error);
        Assert.Same(First, Read(lazy.Traverse(static r => r)).Error);

        items.Fold(0, Sum).Deconstruct(out var ok, out _, out var error);
        Assert.False(ok);
        Assert.Same(First, error);
    }

    /// <summary>
    /// GIVEN two distinct failures
    /// WHEN an operator runs
    /// THEN the lowest-index failure MUST win and the accumulator MUST have stopped there.
    /// </summary>
    [Fact]
    public void TwoFailures_ReturnTheLowestIndex_AndStopTheAccumulator()
    {
        var items = new[] { S(1), F(First), S(3), F(Second) };
        Assert.Same(First, Read(items.Sequence()).Error);
        Assert.Same(First, Read(new List<Result<int, TaggedError>>(items).Sequence()).Error);

        var calls = 0;
        items.Fold(0, (accumulator, value) => { calls++; return accumulator + value; })
            .Deconstruct(out _, out _, out var error);
        Assert.Same(First, error);
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// GIVEN a lazy source whose failing element is not last
    /// WHEN <c>Sequence</c> runs
    /// THEN enumeration MUST stop exactly at the failing element.
    /// </summary>
    [Fact]
    public void EnumerableSequence_StopsAtTheFailingElement()
    {
        var consumed = 0;
        IEnumerable<Result<int, TaggedError>> Source()
        {
            for (var i = 0; i < 10; i++)
            {
                consumed++;
                yield return i == 4 ? F(First) : S(i);
            }
        }

        Assert.Same(First, Read(Source().Sequence()).Error);
        Assert.Equal(5, consumed);
    }

    /// <summary>
    /// GIVEN a source long enough to grow the pooled buffer
    /// WHEN <c>Sequence</c> runs repeatedly
    /// THEN the returned list MUST be an exactly sized copy that never aliases the pool.
    /// </summary>
    [Fact]
    public void GrownEnumerable_ReturnsAnExactlySizedArrayThatDoesNotAliasThePool()
    {
        static IEnumerable<Result<int, TaggedError>> Source()
        {
            for (var i = 0; i < 1000; i++)
            {
                yield return S(i);
            }
        }

        var (ok, values, _) = Read(Source().Sequence());
        Assert.True(ok);
        Assert.Equal(1000, values.Length);
        Assert.Equal(0, values[0]);
        Assert.Equal(999, values[999]);

        for (var i = 0; i < 100; i++)
        {
            _sink = Read(Source().Sequence()).Values;
        }

        Assert.Equal(0, values[0]);
        Assert.Equal(500, values[500]);
    }

    /// <summary>
    /// GIVEN a live allocation sensor with a control that provably allocates
    /// WHEN array and list <c>Fold</c> run
    /// THEN both MUST read 0 B.
    /// </summary>
    [Fact]
    public void Fold_OnArrayAndList_AllocatesNothing_WithALiveSensitivityControl()
    {
        var array = new Result<int, TaggedError>[64];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = S(i);
        }

        var list = new List<Result<int, TaggedError>>(array);
        static void Allocate64() => _ = new byte[64];

        Assert.True(Measure(Allocate64, 256) > 0, "the allocation sensor is live");
        Assert.Equal(0L, Measure(() => array.Fold(0, Sum).Deconstruct(out _, out _intSink, out _), 256));
        Assert.Equal(0L, Measure(() => list.Fold(0, Sum).Deconstruct(out _, out _intSink, out _), 256));
    }

    /// <summary>
    /// GIVEN the materializing operators with a success-only source
    /// WHEN measured against hand-written equivalents
    /// THEN library allocations MUST equal the equivalent's allocations.
    /// </summary>
    [Fact]
    public void MaterializingOverloads_AllocateOnlyTheirOutputArray()
    {
        var array = new Result<int, TaggedError>[64];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = S(i);
        }

        var list = new List<Result<int, TaggedError>>(array);

        static int HandWritten(Result<int, TaggedError>[] source)
        {
            var values = new int[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                source[i].Deconstruct(out var ok, out var value, out _);
                if (!ok)
                {
                    return 0;
                }

                values[i] = value;
            }

            _sink = values;
            return values.Length;
        }

        static int HandWrittenLazy(Result<int, TaggedError>[] source)
        {
            var values = new int[source.Length];
            var index = 0;
            foreach (var item in (IEnumerable<Result<int, TaggedError>>)source)
            {
                item.Deconstruct(out var ok, out var value, out _);
                if (!ok)
                {
                    return -1;
                }

                values[index++] = value;
            }

            _sink = values;
            return values.Length;
        }

        var library = Measure(() => Read(array.Sequence()), 128);
        Assert.Equal(Measure(() => HandWritten(array), 128), library);

        var arrayFromList = list.ToArray();
        Assert.Equal(
            Measure(() => HandWritten(arrayFromList), 128),
            Measure(() => Read(list.Sequence()), 128));

        Assert.Equal(
            Measure(() => HandWrittenLazy(array), 128),
            Measure(() => Read(((IEnumerable<Result<int, TaggedError>>)array).Sequence()), 128));
    }

    /// <summary>
    /// GIVEN a selector that throws mid-enumeration over a lazy source
    /// WHEN <c>Traverse</c> runs many times
    /// THEN the exception MUST surface unchanged and the pool MUST stay healthy.
    /// </summary>
    [Fact]
    public void ThrowingSelector_SurfacesUnchanged_AndThePoolStaysBounded()
    {
        static IEnumerable<int> Source()
        {
            for (var i = 0; i < 512; i++)
            {
                yield return i;
            }
        }

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var thrown = Assert.Throws<InvalidOperationException>(
                () => Source().Traverse(static value =>
                    value == 300 ? throw new InvalidOperationException("boom") : S(value)));
            Assert.Equal("boom", thrown.Message);
        }
    }
}
