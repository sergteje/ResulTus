// Copyright (c) ResulTus. All rights reserved.

using BenchmarkDotNet.Attributes;
using ResulTus;
using ResulTus.Collections;

namespace ResulTus.Benchmarks;

/// <summary>
/// The source shape a collection benchmark feeds to an operation.
/// </summary>
public enum CollectionSourceShape
{
    /// <summary>A <c>T[]</c> source, whose <c>IEnumerable&lt;T&gt;</c> enumerator is a class.</summary>
    Array,

    /// <summary>A <c>List&lt;T&gt;</c> source, whose enumerator is a struct boxed by the interface.</summary>
    List,
}

/// <summary>
/// Measures the collection operations in <c>ResulTus.Collections</c> over arrays and lists.
/// </summary>
/// <remarks>
/// <para>
/// The original IEnumerable-based methods allocated an enumerator for these sources. P1 added
/// indexed array and IReadOnlyList overloads; <see cref="TraverseEnumerable"/> retains the original
/// path as a control for the new array and list implementations. The allocation column is the point
/// of this benchmark, and the nanosecond column remains secondary.
/// </para>
/// <para>
/// Every input is all-success, which is the worst case for the materializing operations: nothing
/// short-circuits, so the full input is traversed and the full output is built. The failure path
/// would stop at the first failure and measure a prefix.
/// </para>
/// <para>
/// Results are stored in sink fields and read back in <see cref="Cleanup"/>, for the reason
/// <c>FailurePathAllocationBenchmark</c> documents: without a store the JIT can prove the result
/// never escapes and remove the allocation the row is supposed to measure.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class CollectionOperationBenchmark
{
    private Result<int>[] _resultArray = [];
    private List<Result<int>> _resultList = [];
    private Result[] _unitArray = [];
    private List<Result> _unitList = [];
    private int[] _intArray = [];
    private List<int> _intList = [];

    private Func<int, Result<int>> _selector = null!;
    private Func<int, int, int> _accumulate = null!;

    private Result<IReadOnlyList<int>> _sinkSequence;
    private Result<IReadOnlyList<int>> _sinkTraverse;
    private Result<int> _sinkFold;
    private Error? _sinkFirstFailure;
    private (int[] Successes, Error[] Failures) _sinkPartition;
    private Result _sinkCombine;

    private int _checksum;

    /// <summary>
    /// The single source size every row is measured at.
    /// </summary>
    /// <remarks>
    /// One size, not a sweep. The per-call allocation these rows exist to expose is a constant: it
    /// does not grow with the number of elements, so a second and third size would add runtime
    /// without adding a verdict. The scaling question is answered by P1's deterministic
    /// <c>GC.GetAllocatedBytesForCurrentThread</c> test, not here. See L7 in
    /// <c>odd/tasks/perf-instrumentation.md</c>.
    /// </remarks>
    private const int N = 1000;

    /// <summary>
    /// Gets or sets the source shape fed to every operation.
    /// </summary>
    [Params(CollectionSourceShape.Array, CollectionSourceShape.List)]
    public CollectionSourceShape Source { get; set; }

    /// <summary>
    /// Builds the sources for the current <see cref="N"/> and <see cref="Source"/>.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _resultArray = new Result<int>[N];
        _resultList = new List<Result<int>>(N);
        _unitArray = new Result[N];
        _unitList = new List<Result>(N);
        _intArray = new int[N];
        _intList = new List<int>(N);

        for (var index = 0; index < N; index++)
        {
            _resultArray[index] = Result<int>.Success(index);
            _resultList.Add(Result<int>.Success(index));
            _unitArray[index] = Result.Success();
            _unitList.Add(Result.Success());
            _intArray[index] = index;
            _intList.Add(index);
        }

        _selector = static value => Result<int>.Success(value + 1);
        _accumulate = static (accumulator, value) => accumulator + value;
    }

    /// <summary>
    /// Reads every sink back so the stores made by the benchmarks cannot be removed.
    /// </summary>
    /// <remarks>
    /// Every read tolerates an unassigned sink. BenchmarkDotNet runs each parameter case in its own
    /// process and calls this method for every case, so exactly one benchmark has executed when it
    /// runs — the other five sinks still hold their default values. A default
    /// <see cref="ValueTuple{T1, T2}"/> carries <c>null</c> arrays, which is why the partition read
    /// is null-conditional; without it this method throws and every row but <c>Partition</c>
    /// reports <c>NA</c>.
    /// </remarks>
    [GlobalCleanup]
    public void Cleanup()
    {
        _checksum += _sinkSequence.IsSuccess ? 1 : 0;
        _checksum += _sinkTraverse.IsSuccess ? 1 : 0;
        _checksum += _sinkFold.IsSuccess ? 1 : 0;
        _checksum += _sinkFirstFailure is null ? 1 : 0;
        _checksum += _sinkPartition.Successes?.Length ?? 0;
        _checksum += _sinkCombine.IsSuccess ? 1 : 0;
    }

    /// <summary>
    /// Collects every success value into a materialized list, or returns the first failure.
    /// </summary>
    /// <returns>The number of collected values, consumed by the harness.</returns>
    [Benchmark]
    public int Sequence()
    {
        _sinkSequence = Source == CollectionSourceShape.Array
            ? _resultArray.Sequence()
            : _resultList.Sequence();

        return _sinkSequence.IsSuccess ? _sinkSequence.GetValueOrDefault()!.Count : 0;
    }

    /// <summary>
    /// Maps every element through a selector and collects the successes, or returns the first
    /// failure.
    /// </summary>
    /// <returns>The number of collected values, consumed by the harness.</returns>
    [Benchmark]
    public int Traverse()
    {
        _sinkTraverse = Source == CollectionSourceShape.Array
            ? _intArray.Traverse(_selector)
            : _intList.Traverse(_selector);

        return _sinkTraverse.IsSuccess ? _sinkTraverse.GetValueOrDefault()!.Count : 0;
    }

    /// <summary>
    /// Runs Traverse through the existing IEnumerable overload as a control for the array/list overloads.
    /// </summary>
    /// <returns>The number of mapped values, consumed by the harness.</returns>
    [Benchmark]
    public int TraverseEnumerable()
    {
        IEnumerable<int> source = Source == CollectionSourceShape.Array ? _intArray : _intList;
        _sinkTraverse = source.Traverse(_selector);

        return _sinkTraverse.IsSuccess ? _sinkTraverse.GetValueOrDefault()!.Count : 0;
    }

    /// <summary>
    /// Folds every success value into an accumulator, or returns the first failure.
    /// </summary>
    /// <returns>The accumulator, consumed by the harness.</returns>
    [Benchmark]
    public int Fold()
    {
        _sinkFold = Source == CollectionSourceShape.Array
            ? _resultArray.Fold(0, _accumulate)
            : _resultList.Fold(0, _accumulate);

        return _sinkFold.GetValueOrDefault();
    }

    /// <summary>
    /// Scans for the first failed element. Every input element succeeds, so the whole source is
    /// traversed and the result is <c>null</c>.
    /// </summary>
    /// <returns>The number of elements scanned, consumed by the harness.</returns>
    [Benchmark]
    public int FirstFailure()
    {
        _sinkFirstFailure = Source == CollectionSourceShape.Array
            ? _resultArray.FirstFailure()
            : _resultList.FirstFailure();

        return _sinkFirstFailure is null ? N : 0;
    }

    /// <summary>
    /// Splits the source into exactly sized arrays of success values and errors.
    /// </summary>
    /// <returns>The number of success values, consumed by the harness.</returns>
    [Benchmark]
    public int Partition()
    {
        _sinkPartition = Source == CollectionSourceShape.Array
            ? _resultArray.Partition()
            : _resultList.Partition();

        return _sinkPartition.Successes.Length;
    }

    /// <summary>
    /// Reduces a sequence of unit results to a single result. <c>Combine</c> exists only for the
    /// unit <see cref="Result"/>; there is no <c>Result&lt;T&gt;</c> overload.
    /// </summary>
    /// <returns>The number of elements combined, consumed by the harness.</returns>
    [Benchmark]
    public int Combine()
    {
        _sinkCombine = Source == CollectionSourceShape.Array
            ? _unitArray.Combine()
            : _unitList.Combine();

        return _sinkCombine.IsSuccess ? N : 0;
    }
}
