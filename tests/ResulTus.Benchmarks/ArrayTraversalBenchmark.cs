// Copyright (c) ResulTus. All rights reserved.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using CSharpFunctionalExtensions;
using ErrorOr;
using FluentResults;
using ResulTus;
using ZeroResult;
using ZeroResult.Errors;

namespace ResulTus.Benchmarks;

/// <summary>
/// Benchmarks comparing sequential array traversal over equivalent result arrays across libraries.
/// </summary>
/// <remarks>
/// Every row walks a fully populated array in order and checksums the integer payload
/// (success arrays) or counts the failures (failure arrays). Each benchmark returns its
/// checksum and also routes it into a non-inlinable sink, so the JIT cannot delete the loop.
/// <para>
/// A checksum that is identical across all five libraries is the proof that every row does
/// the same amount of useful work; <see cref="Setup"/> fails the run if the payloads differ.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class ArrayTraversalBenchmark
{
    private static long _sink;

    private Result<int>[] _resulTusSuccess = null!;
    private Result<int>[] _resulTusFailure = null!;

    private ErrorOr<int>[] _errorOrSuccess = null!;
    private ErrorOr<int>[] _errorOrFailure = null!;

    private ZeroResult.Result<int, SingleError>[] _zeroResultSuccess = null!;
    private ZeroResult.Result<int, SingleError>[] _zeroResultFailure = null!;

    private FluentResults.Result<int>[] _fluentResultsSuccess = null!;

    private CSharpFunctionalExtensions.Result<int>[] _csharpFuncExtSuccess = null!;

    /// <summary>
    /// Gets or sets the number of elements in every traversed array.
    /// </summary>
    /// <remarks>
    /// 1 000 (~32 KiB at 32 B/element) sits comfortably inside a 256 KiB L2.
    /// 7 000 (~224 KiB at 32 B/element versus ~280 KiB at 40 B/element) is the size at which
    /// only the 40-byte layout crosses the L2 boundary.
    /// 10 000 (~313 KiB versus ~391 KiB) puts both layouts over it.
    /// </remarks>
    [Params(1000, 7000, 10000)]
    public int N { get; set; }

    /// <summary>
    /// Builds every array up front and verifies that all libraries carry the same payloads.
    /// </summary>
    /// <exception cref="InvalidOperationException">The payloads differ between libraries.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var error = ResulTus.Error.NotFound("x");
        var errorOrError = ErrorOr.Error.NotFound(code: "NotFound");
        var singleError = new SingleError("x");

        _resulTusSuccess = new Result<int>[N];
        _resulTusFailure = new Result<int>[N];
        _errorOrSuccess = new ErrorOr<int>[N];
        _errorOrFailure = new ErrorOr<int>[N];
        _zeroResultSuccess = new ZeroResult.Result<int, SingleError>[N];
        _zeroResultFailure = new ZeroResult.Result<int, SingleError>[N];
        _fluentResultsSuccess = new FluentResults.Result<int>[N];
        _csharpFuncExtSuccess = new CSharpFunctionalExtensions.Result<int>[N];

        for (var i = 0; i < N; i++)
        {
            _resulTusSuccess[i] = Result<int>.Success(i);
            _resulTusFailure[i] = Result<int>.Failure(error);

            _errorOrSuccess[i] = ErrorOrFactory.From(i);
            _errorOrFailure[i] = ErrorOrFactory.From<int>(errorOrError);

            _zeroResultSuccess[i] = ZeroResult.Result.Success<int, SingleError>(i);
            _zeroResultFailure[i] = ZeroResult.Result.Failure<int, SingleError>(singleError);

            _fluentResultsSuccess[i] = FluentResults.Result.Ok(i);
            _csharpFuncExtSuccess[i] = CSharpFunctionalExtensions.Result.Success(i);
        }

        var expectedSum = (long)N * (N - 1) / 2;

        EnsureEqual("ResulTus success sum", _resulTusSuccess.Sum(r => (long)r.GetValueOrDefault()), expectedSum);
        EnsureEqual("ErrorOr success sum", _errorOrSuccess.Sum(r => (long)(r.IsError ? 0 : r.Value)), expectedSum);
        EnsureEqual("ZeroResult success sum", _zeroResultSuccess.Sum(r => (long)(r.IsSuccess ? r.Value : 0)), expectedSum);
        EnsureEqual("FluentResults success sum", _fluentResultsSuccess.Sum(r => (long)(r.IsSuccess ? r.Value : 0)), expectedSum);
        EnsureEqual("CSharpFunctionalExtensions success sum", _csharpFuncExtSuccess.Sum(r => (long)(r.IsSuccess ? r.Value : 0)), expectedSum);

        EnsureEqual("ResulTus failure count", _resulTusFailure.Count(r => r.IsFailure), N);
        EnsureEqual("ErrorOr failure count", _errorOrFailure.Count(r => r.IsError), N);
        EnsureEqual("ZeroResult failure count", _zeroResultFailure.Count(r => r.IsFailure), N);
    }

    // ── ResulTus ──────────────────────────────────────────────────────

    /// <summary>
    /// Traverses an all-successful <see cref="Result{T}"/> array and sums the payloads.
    /// </summary>
    [Benchmark]
    public long ResulTus_Traverse_Success()
    {
        var array = _resulTusSuccess;
        long sum = 0;
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i].TryGetValue(out var value))
            {
                sum += value;
            }
        }

        Consume(sum);
        return sum;
    }

    /// <summary>
    /// Traverses an all-failed <see cref="Result{T}"/> array and counts the failures.
    /// </summary>
    [Benchmark]
    public long ResulTus_Traverse_Failure()
    {
        var array = _resulTusFailure;
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

    // ── ErrorOr ───────────────────────────────────────────────────────

    /// <summary>
    /// Traverses an all-successful <see cref="ErrorOr{T}"/> array and sums the payloads.
    /// </summary>
    [Benchmark]
    public long ErrorOr_Traverse_Success()
    {
        var array = _errorOrSuccess;
        long sum = 0;
        for (var i = 0; i < array.Length; i++)
        {
            if (!array[i].IsError)
            {
                sum += array[i].Value;
            }
        }

        Consume(sum);
        return sum;
    }

    /// <summary>
    /// Traverses an all-failed <see cref="ErrorOr{T}"/> array and counts the failures.
    /// </summary>
    [Benchmark]
    public long ErrorOr_Traverse_Failure()
    {
        var array = _errorOrFailure;
        long count = 0;
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i].IsError)
            {
                count++;
            }
        }

        Consume(count);
        return count;
    }

    // ── ZeroResult ────────────────────────────────────────────────────

    /// <summary>
    /// Traverses an all-successful <c>Result&lt;int, SingleError&gt;</c> array and sums the payloads.
    /// </summary>
    [Benchmark]
    public long ZeroResult_Traverse_Success()
    {
        var array = _zeroResultSuccess;
        long sum = 0;
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i].IsSuccess)
            {
                sum += array[i].Value;
            }
        }

        Consume(sum);
        return sum;
    }

    /// <summary>
    /// Traverses an all-failed <c>Result&lt;int, SingleError&gt;</c> array and counts the failures.
    /// </summary>
    [Benchmark]
    public long ZeroResult_Traverse_Failure()
    {
        var array = _zeroResultFailure;
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

    // ── FluentResults ─────────────────────────────────────────────────

    /// <summary>
    /// Traverses an all-successful FluentResults array and sums the payloads.
    /// </summary>
    [Benchmark]
    public long FluentResults_Traverse_Success()
    {
        var array = _fluentResultsSuccess;
        long sum = 0;
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i].IsSuccess)
            {
                sum += array[i].Value;
            }
        }

        Consume(sum);
        return sum;
    }

    // ── CSharpFunctionalExtensions ────────────────────────────────────

    /// <summary>
    /// Traverses an all-successful CSharpFunctionalExtensions array and sums the payloads.
    /// </summary>
    [Benchmark]
    public long CSharpFunctionalExtensions_Traverse_Success()
    {
        var array = _csharpFuncExtSuccess;
        long sum = 0;
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i].IsSuccess)
            {
                sum += array[i].Value;
            }
        }

        Consume(sum);
        return sum;
    }

    // ── Layout report ─────────────────────────────────────────────────

    /// <summary>
    /// Writes the measured element layout for every traversed element type.
    /// </summary>
    /// <remarks>
    /// Printed from the host process, once per run, immediately before the traversal tables.
    /// The numbers are measured with <see cref="Unsafe.SizeOf{T}"/> and
    /// <see cref="Unsafe.ByteOffset{T}"/>, never derived from field arithmetic.
    /// </remarks>
    public static void PrintLayoutTable()
    {
        Console.WriteLine();
        Console.WriteLine("================ ELEMENT LAYOUT — measured via Unsafe.SizeOf / array stride ================");
        Console.WriteLine($"{"element type",-52}{"kind",-8}{"SizeOf",10}{"stride",10}");
        Console.WriteLine(new string('-', 80));

        WriteRow<Result<int>>("ResulTus.Result<int>");
        WriteRow<ResulTus.Error>("ResulTus.Error");
        WriteRow<Result<int, EightByteError>>("ResulTus.Result<int, EightByteError>");
        WriteRow<ErrorOr<int>>("ErrorOr.ErrorOr<int>");
        WriteRow<ErrorOr.Error>("ErrorOr.Error");
        WriteRow<ZeroResult.Result<int, SingleError>>("ZeroResult.Result<int, SingleError>");
        WriteRow<SingleError>("ZeroResult.Errors.SingleError");
        WriteRow<FluentResults.Result<int>>("FluentResults.Result<int>");
        WriteRow<CSharpFunctionalExtensions.Result<int>>("CSharpFunctionalExtensions.Result<int>");

        Console.WriteLine(new string('-', 80));
        Console.WriteLine("stride = byte distance between consecutive array elements (what the loop actually walks)");
        Console.WriteLine();
    }

    private static void WriteRow<T>(string name)
    {
        var kind = typeof(T).IsValueType ? "struct" : "class";
        var stride = typeof(T).IsValueType ? ArrayStride<T>() : IntPtr.Size;
        Console.WriteLine($"{name,-52}{kind,-8}{Unsafe.SizeOf<T>(),10}{stride,10}");
    }

    private static long ArrayStride<T>()
    {
        var array = new T[2];
        return Unsafe.ByteOffset(ref array[0], ref array[1]);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Consume(long checksum) => Volatile.Write(ref _sink, checksum);

    private static void EnsureEqual(string what, long actual, long expected)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException($"{what}: expected {expected}, got {actual}.");
        }
    }

    /// <summary>
    /// An error type with no references, used to measure the generic tier's packed layout.
    /// </summary>
    private readonly record struct EightByteError(int Code, int Detail);
}
