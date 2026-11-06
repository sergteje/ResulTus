// Copyright (c) ResulTus. All rights reserved.

using System.Globalization;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using ErrorOr;
using FluentResults;
using ResulTus;
using ZeroResult;
using ZeroResult.Errors;

namespace ResulTus.Benchmarks;

/// <summary>
/// Measures the failure path built from a distinct, uncached message on every invocation.
/// </summary>
/// <remarks>
/// <para>
/// Every pre-existing failure row reuses a single pre-built error object, which hides the
/// allocation a class-based error costs — the gap <c>docs/performance.md</c> §7 records as
/// "Open, high value". This benchmark builds the error on every invocation and cycles through a
/// pool of distinct messages, so no library can serve the call from a cache. The pool is
/// materialized in <see cref="Setup"/>, so the caller's string allocation is paid identically by
/// every row and is excluded from the comparison.
/// </para>
/// <para>
/// Each result is stored in a per-library sink field and read back in <see cref="Cleanup"/>.
/// Without that store the JIT proves the error never escapes and stack-allocates it: the first
/// version of this benchmark reported <c>0 B</c> for <c>ZeroResult</c>, whose <c>SingleError</c> is
/// a class, while a standalone probe measured 32 B per call in both Debug and Release. The sink is
/// a field store, so the object graph stays reachable from a live object and must live on the heap.
/// </para>
/// <para>
/// Every row consumes the result through an accessor that does not allocate. This is not
/// decoration: <c>FluentResults.Result.IsFailed</c> derives from <c>Errors</c>, which runs
/// <c>Reasons.OfType&lt;IError&gt;().ToList()</c> on every access and costs 88 B, and reading
/// <c>Errors</c> costs 176 B. Consuming the result that way would attribute a consumer's cost to
/// the library's construction row, which is exactly the artifact this benchmark exists to remove.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class FailurePathAllocationBenchmark
{
    private const int MessagePoolSize = 1024;

    private string[] _messages = [];
    private int _cursor;

    private Result<int> _sinkResulTus;
    private ErrorOr<int> _sinkErrorOr;
    private ZeroResult.Result<int, SingleError> _sinkZeroResult;
    private FluentResults.Result<int>? _sinkFluentResults;
    private CSharpFunctionalExtensions.Result<int> _sinkCSharpFunctionalExtensions;

    private int _checksum;

    /// <summary>
    /// Materializes the message pool before the measurement starts.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _messages = new string[MessagePoolSize];
        for (var index = 0; index < MessagePoolSize; index++)
        {
            _messages[index] = "item " + index.ToString(CultureInfo.InvariantCulture) + " was not found";
        }
    }

    /// <summary>
    /// Reads every sink back so the stores made by the benchmarks cannot be removed.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _checksum += _sinkResulTus.IsFailure ? 1 : 0;
        _checksum += _sinkErrorOr.IsError ? 1 : 0;
        _checksum += _sinkZeroResult.IsFailure ? 1 : 0;
        _checksum += _sinkFluentResults is null ? 0 : 1;
        _checksum += _sinkCSharpFunctionalExtensions.IsFailure ? 1 : 0;
    }

    /// <summary>
    /// ResulTus failure from a distinct message. The <c>Error</c> struct and the result are inline.
    /// </summary>
    /// <returns>The error code's length, consumed by the harness.</returns>
    [Benchmark(Baseline = true)]
    public int ResulTus_Failure_Unique_Message()
    {
        var result = Result<int>.Failure(Error.NotFound("RESOURCE_NOT_FOUND", NextMessage()));
        _sinkResulTus = result;
        result.TryGetValue(out _, out var error);
        return error.Code.Length;
    }

    /// <summary>
    /// ErrorOr failure from a distinct message. The struct holds a <c>List&lt;Error&gt;</c>.
    /// </summary>
    /// <returns>The first error's description length, consumed by the harness.</returns>
    [Benchmark]
    public int ErrorOr_Failure_Unique_Message()
    {
        var result = ErrorOrFactory.From<int>(
            ErrorOr.Error.NotFound(code: "RESOURCE_NOT_FOUND", description: NextMessage()));
        _sinkErrorOr = result;
        return result.FirstError.Description.Length;
    }

    /// <summary>
    /// ZeroResult failure from a distinct message. <c>SingleError</c> is a class.
    /// </summary>
    /// <returns>The error message's length, consumed by the harness.</returns>
    [Benchmark]
    public int ZeroResult_Failure_Unique_Message()
    {
        var result = ZeroResult.Result.Failure<int, SingleError>(new SingleError(NextMessage()));
        _sinkZeroResult = result;
        return result.Error.Message.Length;
    }

    /// <summary>
    /// FluentResults failure from a distinct message. The result, its reason list and the error
    /// are all heap objects.
    /// </summary>
    /// <returns>The reason count, consumed by the harness.</returns>
    [Benchmark]
    public int FluentResults_Failure_Unique_Message()
    {
        var result = FluentResults.Result.Fail<int>(NextMessage());
        _sinkFluentResults = result;
        return result.Reasons.Count;
    }

    /// <summary>
    /// CSharpFunctionalExtensions failure from a distinct message. Its error is a plain string,
    /// so this row is expected to be allocation-free as well.
    /// </summary>
    /// <returns>The error message's length, consumed by the harness.</returns>
    [Benchmark]
    public int CSharpFunctionalExtensions_Failure_Unique_Message()
    {
        var result = CSharpFunctionalExtensions.Result.Failure<int>(NextMessage());
        _sinkCSharpFunctionalExtensions = result;
        return result.Error.Length;
    }

    /// <summary>
    /// Returns the next distinct message in the pool, cycling without division.
    /// </summary>
    /// <returns>A message that no earlier invocation in the current cycle used.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string NextMessage()
    {
        var index = _cursor;
        _cursor = index + 1 == MessagePoolSize ? 0 : index + 1;
        return _messages[index];
    }
}
