// Copyright (c) ResulTus. All rights reserved.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using ResulTus;

namespace ResulTus.Benchmarks;

/// <summary>
/// Compares the generic tier's array stride against itself under two layouts.
/// </summary>
/// <remarks>
/// The generic tier carries <c>[StructLayout(LayoutKind.Sequential, Pack = 1)]</c>, which the
/// runtime honors when the error type holds no references. <c>Result&lt;int, EightByteError&gt;</c>
/// therefore walks a 13-byte stride: smaller than the competitors' 16 bytes, but not a multiple
/// of 4, so each element's 8-byte error starts on a different word boundary and roughly one in
/// five elements straddles a cache line.
/// <para>
/// This benchmark measures one build. The comparison is run twice — with the pack attribute and
/// without it (16-byte aligned stride) — and both builds run this identical code, so the only
/// variable is the layout. Values from different builds are only comparable on the same machine,
/// with no other benchmark process running.
/// </para>
/// <para>
/// Element type is identical to the one pinned in <c>LayoutTests</c>, so the printed layout table
/// is the evidence that the two builds really ran 13 B and 16 B.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class GenericTierTraversalBenchmark
{
    private static long _sink;

    private Result<int, EightByteError>[] _success = null!;
    private Result<int, EightByteError>[] _failure = null!;

    /// <summary>
    /// Gets or sets the number of elements in every traversed array.
    /// </summary>
    /// <remarks>
    /// 1 000 is 13 KiB packed versus 16 KiB aligned — both far inside the 256 KiB L2.
    /// 7 000 is 89 KiB versus 109 KiB — still inside L2, so this parameter isolates the
    /// per-element cost from memory capacity and only the stride shape changes.
    /// </remarks>
    [Params(1000, 7000)]
    public int N { get; set; }

    /// <summary>
    /// Builds both arrays and verifies the payloads.
    /// </summary>
    /// <exception cref="InvalidOperationException">A payload does not match the expected value.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var error = new EightByteError(7, 11);

        _success = new Result<int, EightByteError>[N];
        _failure = new Result<int, EightByteError>[N];

        for (var i = 0; i < N; i++)
        {
            _success[i] = Result<int, EightByteError>.Success(i);
            _failure[i] = Result<int, EightByteError>.Failure(error);
        }

        var expectedSum = (long)N * (N - 1) / 2;
        long sum = 0;
        for (var i = 0; i < N; i++)
        {
            if (_success[i].IsSuccess)
            {
                _success[i].Deconstruct(out _, out var value, out _);
                sum += value;
            }
        }

        if (sum != expectedSum)
        {
            throw new InvalidOperationException($"generic tier success sum: expected {expectedSum}, got {sum}.");
        }

        var failures = 0;
        for (var i = 0; i < N; i++)
        {
            if (_failure[i].IsFailure)
            {
                failures++;
            }
        }

        if (failures != N)
        {
            throw new InvalidOperationException($"generic tier failure count: expected {N}, got {failures}.");
        }
    }

    /// <summary>
    /// Traverses an all-successful generic-tier array and sums the payloads.
    /// </summary>
    /// <returns>The checksum.</returns>
    [Benchmark]
    public long ResulTus_Generic_Traverse_Success()
    {
        var array = _success;
        long sum = 0;
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i].IsSuccess)
            {
                array[i].Deconstruct(out _, out var value, out _);
                sum += value;
            }
        }

        Consume(sum);
        return sum;
    }

    /// <summary>
    /// Traverses an all-failed generic-tier array and counts the failures.
    /// </summary>
    /// <returns>The failure count.</returns>
    [Benchmark]
    public long ResulTus_Generic_Traverse_Failure()
    {
        var array = _failure;
        long count = 0;
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i].IsFailure)
            {
                count++;
            }
        }

        Consume(count);
        return count;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Consume(long checksum) => Volatile.Write(ref _sink, checksum);
}

/// <summary>
/// An eight-byte error with no references, the shape that makes the pack attribute effective.
/// </summary>
internal readonly record struct EightByteError(int Code, int Detail);
