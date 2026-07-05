// Copyright (c) ResulTus. All rights reserved.

using BenchmarkDotNet.Attributes;
using CSharpFunctionalExtensions;
using ErrorOr;
using FluentResults;
using ResulTus;
using ZeroResult;
using ZeroResult.Errors;

namespace ResulTus.Benchmarks;

/// <summary>
/// Benchmarks comparing <c>Match</c> operations across result libraries.
/// </summary>
[MemoryDiagnoser]
public class ResultMatchBenchmark
{
    private Result<int> _resulTusSuccess;
    private Result<int> _resulTusFailure;

    private FluentResults.Result<int> _fluentResultsSuccess = null!;
    private FluentResults.Result<int> _fluentResultsFailure = null!;

    private ErrorOr<int> _errorOrSuccess;
    private ErrorOr<int> _errorOrFailure;

    private CSharpFunctionalExtensions.Result<int> _csharpFuncExtSuccess;
    private CSharpFunctionalExtensions.Result<int> _csharpFuncExtFailure;

    private ZeroResult.Result<int, SingleError> _zeroResultSuccess;
    private ZeroResult.Result<int, SingleError> _zeroResultFailure;

    /// <summary>
    /// Initializes all result instances before each benchmark iteration.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _resulTusSuccess = Result<int>.Success(42);
        _resulTusFailure = Result<int>.Failure(Error.NotFound("x"));

        _fluentResultsSuccess = FluentResults.Result.Ok(42);
        _fluentResultsFailure = FluentResults.Result.Fail<int>("x");

        _errorOrSuccess = ErrorOrFactory.From<int>(42);
        _errorOrFailure = ErrorOrFactory.From<int>(ErrorOr.Error.NotFound(code: "NotFound"));

        _csharpFuncExtSuccess = CSharpFunctionalExtensions.Result.Success<int>(42);
        _csharpFuncExtFailure = CSharpFunctionalExtensions.Result.Failure<int>("x");

        _zeroResultSuccess = ZeroResult.Result.Success<int, SingleError>(42);
        _zeroResultFailure = ZeroResult.Result.Failure<int, SingleError>(new SingleError("x"));
    }

    // ── ResulTus ──────────────────────────────────────────────────────

    /// <summary>
    /// Matches a successful <see cref="Result{T}"/>.
    /// </summary>
    [Benchmark]
    public int ResulTus_Match_Success()
    {
        return _resulTusSuccess.Match(v => v, _ => -1);
    }

    /// <summary>
    /// Matches a failed <see cref="Result{T}"/>.
    /// </summary>
    [Benchmark]
    public int ResulTus_Match_Failure()
    {
        return _resulTusFailure.Match(v => v, _ => -1);
    }

    // ── FluentResults ─────────────────────────────────────────────────

    /// <summary>
    /// Matches a successful FluentResults result via switch expression.
    /// </summary>
    [Benchmark]
    public int FluentResults_Match_Success()
    {
        return _fluentResultsSuccess switch { { IsSuccess: true } => _fluentResultsSuccess.Value, _ => -1 };
    }

    /// <summary>
    /// Matches a failed FluentResults result via switch expression.
    /// </summary>
    [Benchmark]
    public int FluentResults_Match_Failure()
    {
        return _fluentResultsFailure switch { { IsSuccess: true } => _fluentResultsFailure.Value, _ => -1 };
    }

    // ── ErrorOr ───────────────────────────────────────────────────────

    /// <summary>
    /// Matches a successful <see cref="ErrorOr{T}"/>.
    /// </summary>
    [Benchmark]
    public int ErrorOr_Match_Success()
    {
        return _errorOrSuccess.Match(v => v, _ => -1);
    }

    /// <summary>
    /// Matches a failed <see cref="ErrorOr{T}"/>.
    /// </summary>
    [Benchmark]
    public int ErrorOr_Match_Failure()
    {
        return _errorOrFailure.Match(v => v, _ => -1);
    }

    // ── CSharpFunctionalExtensions ────────────────────────────────────

    /// <summary>
    /// Matches a successful <see cref="CSharpFunctionalExtensions.Result{T}"/>.
    /// </summary>
    [Benchmark]
    public int CSharpFunctionalExtensions_Match_Success()
    {
        return _csharpFuncExtSuccess.Match(v => v, _ => -1);
    }

    /// <summary>
    /// Matches a failed <see cref="CSharpFunctionalExtensions.Result{T}"/>.
    /// </summary>
    [Benchmark]
    public int CSharpFunctionalExtensions_Match_Failure()
    {
        return _csharpFuncExtFailure.Match(v => v, _ => -1);
    }

    // ── ZeroResult ─────────────────────────────────────────────────────

    /// <summary>
    /// Matches a successful ZeroResult.
    /// </summary>
    [Benchmark]
    public int ZeroResult_Match_Success()
    {
        return _zeroResultSuccess.Match(v => v, _ => -1);
    }

    /// <summary>
    /// Matches a failed ZeroResult.
    /// </summary>
    [Benchmark]
    public int ZeroResult_Match_Failure()
    {
        return _zeroResultFailure.Match(v => v, _ => -1);
    }
}
