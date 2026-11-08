// Copyright (c) ResulTus. All rights reserved.

using BenchmarkDotNet.Attributes;
using ErrorOr;
using FluentResults;
using ResulTus;
using ZeroResult;
using ZeroResult.Errors;

namespace ResulTus.Benchmarks;

/// <summary>
/// Benchmarks focused on memory allocation characteristics.
/// </summary>
[MemoryDiagnoser]
public class AllocationBenchmark
{
    /// <summary>
    /// Measures allocation for <see cref="Error.NotFound"/> string creation alone.
    /// </summary>
    [Benchmark]
    public void Error_NotFound_Allocation()
    {
        Error _ = Error.NotFound("x");
    }

    /// <summary>
    /// ResulTus <c>Result&lt;int&gt;.Success(42)</c> — should allocate 0 bytes (value-type result, no boxing).
    /// </summary>
    [Benchmark]
    public void ResulTus_Success_Allocation()
    {
        Result<int> _ = Result<int>.Success(42);
    }

    /// <summary>
    /// ResulTus <c>Result.Failure(Error.NotFound("x"))</c> — allocates only the error's string content.
    /// The <see cref="Result"/> struct itself is allocation-free.
    /// </summary>
    [Benchmark]
    public void ResulTus_Failure_Allocation()
    {
        Result _ = Result.Failure(Error.NotFound("x"));
    }

    /// <summary>
    /// FluentResults <c>Result.Ok(42)</c> — class-based result, expects heap allocation.
    /// </summary>
    [Benchmark]
    public void FluentResults_Success_Allocation()
    {
        FluentResults.Result<int> _ = FluentResults.Result.Ok(42);
    }

    /// <summary>
    /// ErrorOr <c>ErrorOrFactory.From(42)</c> — value-type result, should allocate 0 bytes.
    /// </summary>
    [Benchmark]
    public void ErrorOr_Success_Allocation()
    {
        ErrorOr.ErrorOr<int> _ = ErrorOrFactory.From<int>(42);
    }

    // ── ZeroResult ─────────────────────────────────────────────────────

    /// <summary>
    /// ZeroResult <c>Result.Success&lt;int, SingleError&gt;(42)</c> — should allocate 0 bytes.
    /// </summary>
    [Benchmark]
    public void ZeroResult_Success_Allocation()
    {
        ZeroResult.Result<int, SingleError> _ = ZeroResult.Result.Success<int, SingleError>(42);
    }

    /// <summary>
    /// ZeroResult <c>Result.Failure&lt;int, SingleError&gt;(new SingleError("x"))</c> — allocates only error content.
    /// </summary>
    [Benchmark]
    public void ZeroResult_Failure_Allocation()
    {
        ZeroResult.Result<int, SingleError> _ = ZeroResult.Result.Failure<int, SingleError>(new SingleError("x"));
    }
}
