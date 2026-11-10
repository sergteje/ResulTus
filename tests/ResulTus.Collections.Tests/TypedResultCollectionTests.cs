// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Tests for sequencing, traversal and folding over collections of the generic typed
/// <see cref="Result{TSuccess, TError}"/> tier.
/// </summary>
public class TypedResultCollectionTests
{
    private const int MeasurementIterations = 16;

    private static readonly Func<int, int, int> Sum = static (accumulator, value) => accumulator + value;
    private static readonly Func<int, Result<int, string>> Double = static value => Result<int, string>.Success(value * 2);

    private static IReadOnlyList<int>? _materializedSink;
    private static byte[]? _sensitivitySink;

    /// <summary>
    /// GIVEN three successful typed results in an array
    /// WHEN <c>Sequence()</c>
    /// THEN the result MUST be a success holding the three values in source order.
    /// </summary>
    [Fact]
    public void Sequence_AllSuccess_ShouldCollectValuesInOrder()
    {
        var results = new[]
        {
            Result<int, string>.Success(1),
            Result<int, string>.Success(2),
            Result<int, string>.Success(3),
        };

        var sequenced = results.Sequence();

        sequenced.IsSuccess.Should().BeTrue();
        ExpectSuccess(sequenced).Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// GIVEN empty array, list and enumerable typed-result sources
    /// WHEN <c>Sequence()</c>
    /// THEN each MUST return a success holding an empty collection.
    /// </summary>
    [Fact]
    public void Sequence_EmptySources_ShouldBeEmptySuccess()
    {
        ExpectSuccess(Array.Empty<Result<int, string>>().Sequence()).Should().BeEmpty();
        ExpectSuccess(new List<Result<int, string>>().Sequence()).Should().BeEmpty();
        ExpectSuccess(Enumerable.Empty<Result<int, string>>().Sequence()).Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN an enumerable whose third element fails
    /// WHEN <c>Sequence()</c>
    /// THEN the first error MUST surface and enumeration MUST stop at that element.
    /// </summary>
    [Fact]
    public void Sequence_OnFailure_ShouldShortCircuitEnumeration()
    {
        var visited = 0;

        IEnumerable<Result<int, string>> Source()
        {
            for (var i = 0; i < 5; i++)
            {
                visited++;
                yield return i == 2
                    ? Result<int, string>.Failure("dup")
                    : Result<int, string>.Success(i);
            }
        }

        var sequenced = Source().Sequence();

        sequenced.IsFailure.Should().BeTrue();
        ExpectFailure(sequenced).Should().Be("dup");
        visited.Should().Be(3);
    }

    /// <summary>
    /// GIVEN an array and a read-only list with the failure at the end
    /// WHEN <c>Sequence()</c>
    /// THEN the failure MUST surface without disturbing the earlier values.
    /// </summary>
    [Fact]
    public void Sequence_ArrayAndList_ShouldPreserveOrderAndFailure()
    {
        var error = "late";
        Result<int, string>[] array =
        [
            Result<int, string>.Success(1),
            Result<int, string>.Success(2),
            Result<int, string>.Failure(error),
        ];
        IReadOnlyList<Result<int, string>> list =
        [
            Result<int, string>.Success(1),
            Result<int, string>.Success(2),
            Result<int, string>.Failure(error),
        ];

        ExpectFailure(array.Sequence()).Should().Be(error);
        ExpectFailure(list.Sequence()).Should().Be(error);
    }

    /// <summary>
    /// GIVEN an array with two distinct failures
    /// WHEN <c>Sequence()</c>
    /// THEN the error returned MUST be the exact object of the first failure, never an aggregate.
    /// </summary>
    [Fact]
    public void Sequence_OnMultipleFailures_ShouldReturnTheFirstErrorObjectInSourceOrder()
    {
        var first = new TaggedError("first");
        var second = new TaggedError("second");
        Result<int, TaggedError>[] results =
        [
            Result<int, TaggedError>.Success(1),
            Result<int, TaggedError>.Failure(first),
            Result<int, TaggedError>.Failure(second),
        ];

        var error = ExpectFailure(results.Sequence());

        error.Should().BeSameAs(first);
    }

    /// <summary>
    /// GIVEN a <c>null</c> enumerable, array or read-only-list source
    /// WHEN <c>Sequence()</c>
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Sequence_WithNullSources_ShouldThrow()
    {
        IEnumerable<Result<int, string>>? nullEnumerable = null;
        Result<int, string>[]? nullArray = null;
        IReadOnlyList<Result<int, string>>? nullList = null;

        var calls = new Action[]
        {
            () => nullEnumerable!.Sequence(),
            () => nullArray!.Sequence(),
            () => nullList!.Sequence(),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN three source values and a selector that always succeeds
    /// WHEN <c>Traverse()</c>
    /// THEN the mapped values MUST be collected in source order across all three overloads.
    /// </summary>
    [Fact]
    public void Traverse_AllSuccess_ShouldCollectMappedValuesInOrder()
    {
        ExpectSuccess(new[] { 1, 2, 3 }.Traverse(Double)).Should().Equal(2, 4, 6);
        ExpectSuccess(new List<int> { 1, 2, 3 }.Traverse(Double)).Should().Equal(2, 4, 6);
        ExpectSuccess(Enumerable.Range(1, 3).Traverse(Double)).Should().Equal(2, 4, 6);
    }

    /// <summary>
    /// GIVEN empty array, list and enumerable sources
    /// WHEN <c>Traverse()</c>
    /// THEN each MUST return a success holding an empty collection and never invoke the selector.
    /// </summary>
    [Fact]
    public void Traverse_EmptySources_ShouldBeEmptySuccessWithoutInvokingSelector()
    {
        var calls = 0;
        Result<int, string> CountingSelector(int value)
        {
            calls++;
            return Result<int, string>.Success(value);
        }

        ExpectSuccess(Array.Empty<int>().Traverse(CountingSelector)).Should().BeEmpty();
        ExpectSuccess(new List<int>().Traverse(CountingSelector)).Should().BeEmpty();
        ExpectSuccess(Enumerable.Empty<int>().Traverse(CountingSelector)).Should().BeEmpty();
        calls.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a selector that fails on the second value
    /// WHEN <c>Traverse()</c>
    /// THEN the failure MUST surface and the selector MUST NOT run for later values.
    /// </summary>
    [Fact]
    public void Traverse_OnFailure_ShouldShortCircuitSelector()
    {
        var visited = 0;
        Result<int, string> Select(int value)
        {
            visited++;
            return value == 2
                ? Result<int, string>.Failure("bad")
                : Result<int, string>.Success(value);
        }

        var traversed = new[] { 1, 2, 3, 4 }.Traverse(Select);

        ExpectFailure(traversed).Should().Be("bad");
        visited.Should().Be(2);
    }

    /// <summary>
    /// GIVEN a selector that fails on several values
    /// WHEN <c>Traverse()</c>
    /// THEN the error returned MUST be the exact object of the first failure in source order.
    /// </summary>
    [Fact]
    public void Traverse_OnMultipleFailures_ShouldReturnTheFirstErrorInSourceOrder()
    {
        var first = new TaggedError("first");
        var second = new TaggedError("second");
        Result<int, TaggedError> Select(int value) => value switch
        {
            1 => Result<int, TaggedError>.Failure(first),
            2 => Result<int, TaggedError>.Failure(second),
            _ => Result<int, TaggedError>.Success(value),
        };

        var error = ExpectFailure(new[] { 0, 1, 2 }.Traverse(Select));

        error.Should().BeSameAs(first);
    }

    /// <summary>
    /// GIVEN a <c>null</c> source or a <c>null</c> selector
    /// WHEN <c>Traverse()</c>
    /// THEN it MUST throw <see cref="ArgumentNullException"/> before any work.
    /// </summary>
    [Fact]
    public void Traverse_WithNullSourceOrSelector_ShouldThrow()
    {
        int[]? nullArray = null;
        IReadOnlyList<int>? nullList = null;
        IEnumerable<int>? nullEnumerable = null;

        var calls = new Action[]
        {
            () => nullArray!.Traverse(Double),
            () => nullList!.Traverse(Double),
            () => nullEnumerable!.Traverse(Double),
            () => new[] { 1 }.Traverse((Func<int, Result<int, string>>)null!),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN three successful typed results across array, list and enumerable sources
    /// WHEN <c>Fold(0, (acc, v) =&gt; acc + v)</c>
    /// THEN the accumulator MUST be the sum.
    /// </summary>
    [Fact]
    public void Fold_AllSuccess_ShouldAccumulate()
    {
        var results = new[]
        {
            Result<int, string>.Success(1),
            Result<int, string>.Success(2),
            Result<int, string>.Success(3),
        };

        ExpectSuccess(results.Fold(0, Sum)).Should().Be(6);
        ExpectSuccess(new List<Result<int, string>>(results).Fold(0, Sum)).Should().Be(6);
        ExpectSuccess(((IEnumerable<Result<int, string>>)results).Fold(0, Sum)).Should().Be(6);
    }

    /// <summary>
    /// GIVEN an empty typed-result source
    /// WHEN <c>Fold(seed, ...)</c>
    /// THEN the seed MUST be returned as a success without invoking the accumulator.
    /// </summary>
    [Fact]
    public void Fold_Empty_ShouldReturnSeedWithoutInvokingAccumulator()
    {
        var calls = 0;
        int Count(int accumulator, int value)
        {
            calls++;
            return accumulator + value;
        }

        ExpectSuccess(Array.Empty<Result<int, string>>().Fold(7, Count)).Should().Be(7);
        calls.Should().Be(0);
    }

    /// <summary>
    /// GIVEN an array with a failure
    /// WHEN <c>Fold</c> is called
    /// THEN the first failure MUST surface and the accumulator MUST NOT consume later values.
    /// </summary>
    [Fact]
    public void Fold_OnFailure_ShouldShortCircuitAndReturnFirstError()
    {
        var calls = 0;
        int Count(int accumulator, int value)
        {
            calls++;
            return accumulator + value;
        }

        var results = new[]
        {
            Result<int, string>.Success(1),
            Result<int, string>.Failure("slow"),
            Result<int, string>.Success(3),
        };

        ExpectFailure(results.Fold(0, Count)).Should().Be("slow");
        calls.Should().Be(1);
    }

    /// <summary>
    /// GIVEN a <c>null</c> source or a <c>null</c> accumulator
    /// WHEN <c>Fold()</c>
    /// THEN it MUST throw <see cref="ArgumentNullException"/> before any work.
    /// </summary>
    [Fact]
    public void Fold_WithNullSourceOrAccumulator_ShouldThrow()
    {
        Result<int, string>[]? nullArray = null;
        IReadOnlyList<Result<int, string>>? nullList = null;
        IEnumerable<Result<int, string>>? nullEnumerable = null;

        var calls = new Action[]
        {
            () => nullArray!.Fold(0, Sum),
            () => nullList!.Fold(0, Sum),
            () => nullEnumerable!.Fold(0, Sum),
            () => new[] { Result<int, string>.Success(1) }.Fold(0, (Func<int, int, int>)null!),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN an array of single-parameter results
    /// WHEN <c>Sequence()</c> is called on it
    /// THEN it MUST bind the existing generic-tier overload and return
    /// <c>Result&lt;IReadOnlyList&lt;T&gt;&gt;</c>.
    /// </summary>
    [Fact]
    public void OverloadResolution_ResultOfTArray_ShouldBindGenericTierOverload()
    {
        Result<int>[] results = [Result<int>.Success(1), Result<int>.Success(2)];

        Result<IReadOnlyList<int>> sequenced = results.Sequence();

        sequenced.IsSuccess.Should().BeTrue();
        sequenced.GetValueOrDefault().Should().Equal(1, 2);
    }

    /// <summary>
    /// GIVEN an array of two-parameter typed results
    /// WHEN <c>Sequence()</c> is called on it
    /// THEN it MUST bind the new typed-tier overload and return
    /// <c>Result&lt;IReadOnlyList&lt;TSuccess&gt;, TError&gt;</c>.
    /// </summary>
    [Fact]
    public void OverloadResolution_TypedResultArray_ShouldBindTypedTierOverload()
    {
        Result<int, string>[] results =
        [
            Result<int, string>.Success(1),
            Result<int, string>.Success(2),
        ];

        Result<IReadOnlyList<int>, string> sequenced = results.Sequence();

        ExpectSuccess(sequenced).Should().Equal(1, 2);
    }

    /// <summary>
    /// GIVEN a known-allocating probe
    /// WHEN measured over a warm loop
    /// THEN the sensor MUST read a non-zero byte count, proving a 0 B reading is not a dead sensor.
    /// </summary>
    [Fact]
    public void AllocationSensor_ShouldDetectKnownAllocations()
    {
        Measure(AllocateKnownBytes, MeasurementIterations).Should().BeGreaterThan(0);
    }

    /// <summary>
    /// GIVEN successful array and read-only-list sources
    /// WHEN <c>Fold</c> is repeated with a static accumulator
    /// THEN each overload MUST allocate zero bytes per call.
    /// </summary>
    [Fact]
    public void NonMaterializingArrayAndListFold_ShouldAllocateZeroBytes()
    {
        var array = Enumerable.Range(1, 4).Select(Result<int, string>.Success).ToArray();
        IReadOnlyList<Result<int, string>> list = new List<Result<int, string>>(array);

        var arrayWarmupChecksum = RunFold(array, MeasurementIterations);
        var listWarmupChecksum = RunFold(list, MeasurementIterations);
        var arrayAllocated = Measure(() => RunFold(array, MeasurementIterations), MeasurementIterations);
        var listAllocated = Measure(() => RunFold(list, MeasurementIterations), MeasurementIterations);

        arrayWarmupChecksum.Should().Be(listWarmupChecksum);
        arrayAllocated.Should().Be(0);
        listAllocated.Should().Be(0);
    }

    /// <summary>
    /// GIVEN successful array and read-only-list sources
    /// WHEN <c>Sequence</c> and <c>Traverse</c> materialize their output
    /// THEN each overload MUST allocate only its output array, matching a hand-written indexed loop.
    /// </summary>
    [Fact]
    public void MaterializingSequenceAndTraverse_ShouldAllocateOnlyOutputArrays()
    {
        var results = Enumerable.Range(0, 1_000).Select(Result<int, string>.Success).ToArray();
        IReadOnlyList<Result<int, string>> resultList = new List<Result<int, string>>(results);
        var values = Enumerable.Range(0, 1_000).ToArray();
        IReadOnlyList<int> valueList = new List<int>(values);

        Func<int> arraySequence = () => Collect(results.Sequence());
        Func<int> listSequence = () => Collect(resultList.Sequence());
        Func<int> arrayTraverse = () => Collect(values.Traverse(Double));
        Func<int> listTraverse = () => Collect(valueList.Traverse(Double));
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

    private static TSuccess ExpectSuccess<TSuccess, TError>(Result<TSuccess, TError> result)
    {
        result.Deconstruct(out var isSuccess, out var value, out _);
        isSuccess.Should().BeTrue();
        return value!;
    }

    private static TError ExpectFailure<TSuccess, TError>(Result<TSuccess, TError> result)
    {
        result.Deconstruct(out var isSuccess, out _, out var error);
        isSuccess.Should().BeFalse();
        return error!;
    }

    private static int RunFold(Result<int, string>[] source, int iterations)
    {
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            source.Fold(0, Sum).Deconstruct(out _, out var accumulator, out _);
            checksum += accumulator!;
        }

        return checksum;
    }

    private static int RunFold(IReadOnlyList<Result<int, string>> source, int iterations)
    {
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            source.Fold(0, Sum).Deconstruct(out _, out var accumulator, out _);
            checksum += accumulator!;
        }

        return checksum;
    }

    private static int Collect(Result<IReadOnlyList<int>, string> result)
    {
        result.Deconstruct(out _, out var values, out _);
        _materializedSink = values;
        return values!.Count;
    }

    private static int SequenceWithLoop(IReadOnlyList<Result<int, string>> source)
    {
        var values = new int[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            source[index].Deconstruct(out _, out var value, out _);
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

    private static int AllocateKnownBytes()
    {
        var bytes = new byte[64];
        _sensitivitySink = bytes;
        return bytes.Length;
    }

    private static long Measure(Func<int> operation, int iterations)
        => AllocationProbe.Measure(operation, warmups: MeasurementIterations, iterations: iterations);

    private sealed class TaggedError
    {
        public TaggedError(string tag) => Tag = tag;

        public string Tag { get; }
    }
}
