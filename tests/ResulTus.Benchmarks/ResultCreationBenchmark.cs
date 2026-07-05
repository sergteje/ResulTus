// Copyright (c) ResulTus. All rights reserved.

using BenchmarkDotNet.Attributes;
using CSharpFunctionalExtensions;
using ErrorOr;
using FluentResults;
using ResulTus;
using ZeroResult;
using ZeroResult.Errors;

// ReSharper disable NotAccessedVariable

namespace ResulTus.Benchmarks;

/// <summary>
/// Benchmarks comparing creation of success and failure results across libraries.
/// </summary>
[MemoryDiagnoser]
public class ResultCreationBenchmark
{
    // ── ResulTus ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates a successful <see cref="Result{T}"/> via the static factory.
    /// </summary>
    [Benchmark]
    public void ResulTus_Create_Success()
    {
        Result<int> _ = Result<int>.Success(42);
    }

    /// <summary>
    /// Creates a failed <see cref="Result{T}"/> via the static factory.
    /// </summary>
    [Benchmark]
    public void ResulTus_Create_Failure()
    {
        Result<int> _ = Result<int>.Failure(Error.NotFound("x"));
    }

    /// <summary>
    /// Creates a successful <see cref="Result{T}"/> via implicit conversion from value.
    /// </summary>
    [Benchmark]
    public void ResulTus_Create_Implicit_From_Value()
    {
        Result<int> _ = 42;
    }

    /// <summary>
    /// Creates a failed <see cref="Result{T}"/> via implicit conversion from <see cref="Error"/>.
    /// </summary>
    [Benchmark]
    public void ResulTus_Create_Implicit_From_Error()
    {
        Result<int> _ = Error.NotFound("x");
    }

    // ── FluentResults ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a successful FluentResults result via <c>Result.Ok</c>.
    /// </summary>
    [Benchmark]
    public void FluentResults_Create_Success()
    {
        FluentResults.Result<int> _ = FluentResults.Result.Ok(42);
    }

    /// <summary>
    /// Creates a failed FluentResults result via <c>Result.Fail</c>.
    /// </summary>
    [Benchmark]
    public void FluentResults_Create_Failure()
    {
        FluentResults.Result<int> _ = FluentResults.Result.Fail<int>("x");
    }

    // ── ErrorOr ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a successful <see cref="ErrorOr{T}"/> via <c>ErrorOrFactory.From</c>.
    /// </summary>
    [Benchmark]
    public void ErrorOr_Create_Success()
    {
        ErrorOr<int> _ = ErrorOrFactory.From<int>(42);
    }

    /// <summary>
    /// Creates a failed <see cref="ErrorOr{T}"/> via <c>ErrorOrFactory.From</c>.
    /// </summary>
    [Benchmark]
    public void ErrorOr_Create_Failure()
    {
        ErrorOr<int> _ = ErrorOrFactory.From<int>(ErrorOr.Error.NotFound(code: "NotFound"));
    }

    // ── CSharpFunctionalExtensions ────────────────────────────────────

    /// <summary>
    /// Creates a successful <see cref="CSharpFunctionalExtensions.Result{T}"/> via <c>Result.Success</c>.
    /// </summary>
    [Benchmark]
    public void CSharpFunctionalExtensions_Create_Success()
    {
        CSharpFunctionalExtensions.Result<int> _ = CSharpFunctionalExtensions.Result.Success<int>(42);
    }

    /// <summary>
    /// Creates a failed <see cref="CSharpFunctionalExtensions.Result{T}"/> via <c>Result.Failure</c>.
    /// </summary>
    [Benchmark]
    public void CSharpFunctionalExtensions_Create_Failure()
    {
        CSharpFunctionalExtensions.Result<int> _ = CSharpFunctionalExtensions.Result.Failure<int>("x");
    }

    // ── ZeroResult ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates a successful ZeroResult <c>Result&lt;int, SingleError&gt;</c> via the static factory.
    /// </summary>
    [Benchmark]
    public void ZeroResult_Create_Success()
    {
        ZeroResult.Result<int, SingleError> _ = ZeroResult.Result.Success<int, SingleError>(42);
    }

    /// <summary>
    /// Creates a failed ZeroResult <c>Result&lt;int, SingleError&gt;</c> via the static factory.
    /// </summary>
    [Benchmark]
    public void ZeroResult_Create_Failure()
    {
        ZeroResult.Result<int, SingleError> _ = ZeroResult.Result.Failure<int, SingleError>(new SingleError("x"));
    }
}
