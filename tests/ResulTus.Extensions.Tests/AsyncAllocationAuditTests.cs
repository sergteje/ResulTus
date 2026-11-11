// Copyright (c) ResulTus. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using ResulTus;
using ResulTus.Extensions;

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Quality-audit probes (S5 of odd/tasks/quality-audit.md): the async overloads must not
/// allocate on the synchronously-completing path. Every operator chain here runs over an
/// already-completed <see cref="ValueTask{TResult}"/> with a synchronously-completing mapper,
/// every delegate is <c>static</c> with explicit parameter types (no closures), and the chain
/// sums the operators — heap allocations cannot cancel, so a zero reading certifies every
/// operator inside it. A failing probe is an audit finding.
/// </summary>
/// <remarks>
/// Same hard rule as <see cref="EnsureFactoryAndProjectionTests"/>: <c>docs/performance.md</c> §2
/// makes 0 B the contract for non-materializing operations, on the success path and on the
/// failure path.
/// </remarks>
public class AsyncAllocationAuditTests
{
    private static readonly Error Boom = Error.Failure("E-BOOM", "boom");
    private static int _sink;

    /// <summary>
    /// GIVEN the generic tier
    /// THEN a full async pipeline over a sync-completed input MUST allocate 0 B on the
    /// success path and on the failure path, and the <c>Task</c> wrapper overload MUST
    /// add nothing beyond the task itself.
    /// </summary>
    [Fact]
    public async Task GenericTier_AsyncPipeline_AllocatesNothingOnSyncCompletion()
    {
        var success = Result<int>.Success(41);
        var failure = Result<int>.Failure(Boom);
        var successTask = Task.FromResult(success);

        (await MeasureAsync(() => GenericSuccessChain(success, successTask))).Should().Be(0);
        (await MeasureAsync(() => GenericFailureChain(failure))).Should().Be(0);
    }

    /// <summary>
    /// GIVEN the unit tier
    /// THEN its async <c>Map</c>/<c>Bind</c>/<c>MapError</c>/<c>Match</c> pipeline MUST
    /// allocate 0 B on both states.
    /// </summary>
    [Fact]
    public async Task UnitTier_AsyncPipeline_AllocatesNothingOnSyncCompletion()
    {
        (await MeasureAsync(UnitSuccessChain)).Should().Be(0);
        (await MeasureAsync(UnitFailureChain)).Should().Be(0);
    }

    /// <summary>
    /// GIVEN the typed tier
    /// THEN its async pipeline MUST allocate 0 B on both states.
    /// </summary>
    [Fact]
    public async Task TypedTier_AsyncPipeline_AllocatesNothingOnSyncCompletion()
    {
        (await MeasureAsync(TypedSuccessChain)).Should().Be(0);
        (await MeasureAsync(TypedFailureChain)).Should().Be(0);
    }

    private static async ValueTask<int> GenericSuccessChain(Result<int> success, Task<Result<int>> successTask)
    {
        var wrapped = await new ValueTask<Result<int>>(success)
            .MapAsync(static (int v, CancellationToken _) => new ValueTask<int>(v + 1))
            .EnsureAsync(static (int v, CancellationToken _) => new ValueTask<bool>(v > 0), Error.None)
            .RecoverAsync(static (Error _, CancellationToken _) => new ValueTask<int>(-1))
            .BindAsync(static (int v, CancellationToken _) => new ValueTask<Result<string>>(Result<string>.Success(v.ToString())))
            .TapAsync(static (string _, CancellationToken _) => default(ValueTask))
            .MapErrorAsync(static (Error e, CancellationToken _) => new ValueTask<Error>(e))
            .MatchAsync(static (string v, CancellationToken _) => new ValueTask<int>(v.Length), static (Error _, CancellationToken _) => new ValueTask<int>(0));

        // The Task-accepting wrapper must add nothing beyond the already-completed task.
        var throughTask = await successTask
            .MapAsync(static (int v, CancellationToken _) => new ValueTask<int>(v * 2))
            .MatchAsync(static (int v, CancellationToken _) => new ValueTask<int>(v), static (Error _, CancellationToken _) => new ValueTask<int>(0));

        _sink = wrapped + throughTask;
        return _sink;
    }

    private static async ValueTask<int> GenericFailureChain(Result<int> failure)
    {
        var result = await new ValueTask<Result<int>>(failure)
            .MapAsync(static (int v, CancellationToken _) => new ValueTask<int>(v + 1))
            .EnsureAsync(static (int v, CancellationToken _) => new ValueTask<bool>(v > 0), Error.None)
            .BindAsync(static (int v, CancellationToken _) => new ValueTask<Result<string>>(Result<string>.Success(v.ToString())))
            .MapErrorAsync(static (Error e, CancellationToken _) => new ValueTask<Error>(e))
            .MatchAsync(static (string _, CancellationToken _) => new ValueTask<int>(0), static (Error e, CancellationToken _) => new ValueTask<int>(e.Code.Length));

        _sink = result;
        return result;
    }

    private static async ValueTask<int> UnitSuccessChain()
    {
        var result = await new ValueTask<Result>(Result.Success())
            .MapAsync(static (CancellationToken _) => new ValueTask<int>(41))
            .BindAsync(static (int v, CancellationToken _) => new ValueTask<Result<string>>(Result<string>.Success(v.ToString())))
            .MatchAsync(static (string v, CancellationToken _) => new ValueTask<int>(v.Length), static (Error _, CancellationToken _) => new ValueTask<int>(0));

        _sink = result;
        return result;
    }

    private static async ValueTask<int> UnitFailureChain()
    {
        var result = await new ValueTask<Result>(Result.Failure(Boom))
            .MapAsync(static (CancellationToken _) => new ValueTask<int>(41))
            .MapErrorAsync(static (Error e, CancellationToken _) => new ValueTask<Error>(e))
            .MatchAsync(static (int _, CancellationToken _) => new ValueTask<int>(0), static (Error e, CancellationToken _) => new ValueTask<int>(e.Code.Length));

        _sink = result;
        return result;
    }

    private static async ValueTask<int> TypedSuccessChain()
    {
        var result = await new ValueTask<Result<int, Error>>(Result<int, Error>.Success(41))
            .MapAsync(static (int v, CancellationToken _) => new ValueTask<int>(v + 1))
            .RecoverAsync(static (Error _, CancellationToken _) => new ValueTask<int>(-1))
            .BindAsync(static (int v, CancellationToken _) => new ValueTask<Result<string, Error>>(Result<string, Error>.Success(v.ToString())))
            .TapAsync(static (string _, CancellationToken _) => default(ValueTask))
            .MapErrorAsync(static (Error e, CancellationToken _) => new ValueTask<Error>(e))
            .MatchAsync(static (string v, CancellationToken _) => new ValueTask<int>(v.Length), static (Error _, CancellationToken _) => new ValueTask<int>(0));

        _sink = result;
        return result;
    }

    private static async ValueTask<int> TypedFailureChain()
    {
        var result = await new ValueTask<Result<int, Error>>(Result<int, Error>.Failure(Boom))
            .MapAsync(static (int v, CancellationToken _) => new ValueTask<int>(v + 1))
            .BindAsync(static (int v, CancellationToken _) => new ValueTask<Result<string, Error>>(Result<string, Error>.Success(v.ToString())))
            .MapErrorAsync(static (Error e, CancellationToken _) => new ValueTask<Error>(e))
            .MatchAsync(static (string _, CancellationToken _) => new ValueTask<int>(0), static (Error e, CancellationToken _) => new ValueTask<int>(e.Code.Length));

        _sink = result;
        return result;
    }

    private static async Task<long> MeasureAsync(Func<ValueTask<int>> operation, int iterations = 10_000)
    {
        _ = await operation();
        _ = await operation();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            checksum += await operation();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(checksum);
        GC.KeepAlive(_sink);
        return allocated;
    }
}
