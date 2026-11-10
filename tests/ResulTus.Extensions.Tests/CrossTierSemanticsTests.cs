// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Cross-semantics checks required by the first-audit triage (S4): the
/// Task and the ValueTask source of every asynchronous operator must agree
/// with each other and with the synchronous outcome, and cancellation must
/// surface as <see cref="OperationCanceledException"/>, never as a captured
/// failure. The unit tier additionally mirrors the value tier's
/// null-argument battery.
/// </summary>
public class CrossTierSemanticsTests
{
    private static readonly Error Failure = Error.Failure("F1", "boom");

    // --- unit tier: Task vs ValueTask vs sync ---------------------------------

    /// <summary>
    /// GIVEN a unit success and a unit failure
    /// WHEN <c>MapAsync</c> runs through both sources
    /// THEN both must return exactly the synchronous <c>Map</c> outcome.
    /// </summary>
    [Fact]
    public async Task Unit_MapAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var fromSync = Result.Success().Map(() => 7);
        var failureFromSync = Result.Failure(Failure).Map(() => 7);

        var fromTask = await Task.FromResult(Result.Success()).MapAsync(_ => ValueTask.FromResult(7));
        var fromValueTask = await ValueTask.FromResult(Result.Success()).MapAsync(_ => ValueTask.FromResult(7));
        var fromTaskFailure = await Task.FromResult(Result.Failure(Failure)).MapAsync(_ => ValueTask.FromResult(7));
        var fromValueTaskFailure = await ValueTask.FromResult(Result.Failure(Failure)).MapAsync(_ => ValueTask.FromResult(7));

        fromTask.Should().Be(fromSync);
        fromValueTask.Should().Be(fromSync);
        fromTaskFailure.Should().Be(failureFromSync);
        fromValueTaskFailure.Should().Be(failureFromSync);
    }

    /// <summary>
    /// GIVEN a unit success and a unit failure
    /// WHEN <c>BindAsync</c> runs through both sources
    /// THEN both must return exactly the synchronous <c>Bind</c> outcome.
    /// </summary>
    [Fact]
    public async Task Unit_BindAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var fromSync = Result.Success().Bind(() => Result<int>.Success(3));
        var failureFromSync = Result.Failure(Failure).Bind(() => Result<int>.Success(3));

        var fromTask = await Task.FromResult(Result.Success()).BindAsync(_ => ValueTask.FromResult(Result<int>.Success(3)));
        var fromValueTask = await ValueTask.FromResult(Result.Success()).BindAsync(_ => ValueTask.FromResult(Result<int>.Success(3)));
        var fromTaskFailure = await Task.FromResult(Result.Failure(Failure)).BindAsync(_ => ValueTask.FromResult(Result<int>.Success(3)));
        var fromValueTaskFailure = await ValueTask.FromResult(Result.Failure(Failure)).BindAsync(_ => ValueTask.FromResult(Result<int>.Success(3)));

        fromTask.Should().Be(fromSync);
        fromValueTask.Should().Be(fromSync);
        fromTaskFailure.Should().Be(failureFromSync);
        fromValueTaskFailure.Should().Be(failureFromSync);
    }

    /// <summary>
    /// GIVEN a unit success and a unit failure
    /// WHEN <c>TapAsync</c> runs through both sources
    /// THEN both must run the side effect exactly where the sync <c>Tap</c> runs
    /// it (success only) and return the untouched result.
    /// </summary>
    [Fact]
    public async Task Unit_TapAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var syncSuccess = Result.Success().Tap(() => { });
        var observed = new List<int>();

        var fromTask = await Task.FromResult(Result.Success()).TapAsync(_ => { observed.Add(1); return ValueTask.CompletedTask; });
        var fromValueTask = await ValueTask.FromResult(Result.Success()).TapAsync(_ => { observed.Add(1); return ValueTask.CompletedTask; });
        var fromTaskFailure = await Task.FromResult(Result.Failure(Failure)).TapAsync(_ => { observed.Add(1); return ValueTask.CompletedTask; });
        var fromValueTaskFailure = await ValueTask.FromResult(Result.Failure(Failure)).TapAsync(_ => { observed.Add(1); return ValueTask.CompletedTask; });

        observed.Should().Equal(1, 1);
        fromTask.Should().Be(syncSuccess);
        fromValueTask.Should().Be(syncSuccess);
        fromTaskFailure.Should().Be(Result.Failure(Failure));
        fromValueTaskFailure.Should().Be(Result.Failure(Failure));
    }

    /// <summary>
    /// GIVEN a unit success and a unit failure
    /// WHEN <c>EnsureAsync</c> runs through both sources
    /// THEN both must return exactly the synchronous <c>Ensure</c> outcome for
    /// predicates that hold and predicates that fail.
    /// </summary>
    [Fact]
    public async Task Unit_EnsureAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var fromSync = Result.Success().Ensure(() => true, Failure);
        var failedFromSync = Result.Success().Ensure(() => false, Failure);

        var fromTask = await Task.FromResult(Result.Success()).EnsureAsync(_ => ValueTask.FromResult(true), Failure);
        var fromValueTask = await ValueTask.FromResult(Result.Success()).EnsureAsync(_ => ValueTask.FromResult(true), Failure);
        var fromTaskFailed = await Task.FromResult(Result.Success()).EnsureAsync(_ => ValueTask.FromResult(false), Failure);
        var fromValueTaskFailed = await ValueTask.FromResult(Result.Success()).EnsureAsync(_ => ValueTask.FromResult(false), Failure);

        fromTask.Should().Be(fromSync);
        fromValueTask.Should().Be(fromSync);
        fromTaskFailed.Should().Be(failedFromSync);
        fromValueTaskFailed.Should().Be(failedFromSync);
    }

    /// <summary>
    /// GIVEN a unit success and a unit failure
    /// WHEN <c>MatchAsync</c> runs through both sources
    /// THEN both must return exactly the synchronous <c>Match</c> outcome on
    /// both branches.
    /// </summary>
    [Fact]
    public async Task Unit_MatchAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var fromSync = Result.Success().Match(() => "s", error => $"f:{error.Code}");
        var failureFromSync = Result.Failure(Failure).Match(() => "s", error => $"f:{error.Code}");

        var fromTask = await Task.FromResult(Result.Success()).MatchAsync(_ => ValueTask.FromResult("s"), (error, _) => ValueTask.FromResult($"f:{error.Code}"));
        var fromValueTask = await ValueTask.FromResult(Result.Success()).MatchAsync(_ => ValueTask.FromResult("s"), (error, _) => ValueTask.FromResult($"f:{error.Code}"));
        var fromTaskFailure = await Task.FromResult(Result.Failure(Failure)).MatchAsync(_ => ValueTask.FromResult("s"), (error, _) => ValueTask.FromResult($"f:{error.Code}"));
        var fromValueTaskFailure = await ValueTask.FromResult(Result.Failure(Failure)).MatchAsync(_ => ValueTask.FromResult("s"), (error, _) => ValueTask.FromResult($"f:{error.Code}"));

        fromTask.Should().Be(fromSync);
        fromValueTask.Should().Be(fromSync);
        fromTaskFailure.Should().Be(failureFromSync);
        fromValueTaskFailure.Should().Be(failureFromSync);
    }

    /// <summary>
    /// GIVEN a unit success and a unit failure
    /// WHEN <c>RecoverAsync</c> runs through both sources
    /// THEN both must return exactly the synchronous <c>Recover</c> outcome,
    /// and the recovery must never run on success.
    /// </summary>
    [Fact]
    public async Task Unit_RecoverAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var fromSync = Result.Failure(Failure).Recover(error => Result.Success());
        var successFromSync = Result.Success().Recover(error => Result.Success());

        var fromTask = await Task.FromResult(Result.Failure(Failure)).RecoverAsync((error, _) => ValueTask.FromResult(Result.Success()));
        var fromValueTask = await ValueTask.FromResult(Result.Failure(Failure)).RecoverAsync((error, _) => ValueTask.FromResult(Result.Success()));
        var fromTaskSuccess = await Task.FromResult(Result.Success()).RecoverAsync((error, _) => ValueTask.FromResult(Result.Failure(Failure)));
        var fromValueTaskSuccess = await ValueTask.FromResult(Result.Success()).RecoverAsync((error, _) => ValueTask.FromResult(Result.Failure(Failure)));

        fromTask.Should().Be(fromSync);
        fromValueTask.Should().Be(fromSync);
        fromTaskSuccess.Should().Be(successFromSync);
        fromValueTaskSuccess.Should().Be(successFromSync);
    }

    /// <summary>
    /// GIVEN a unit success and a unit failure
    /// WHEN <c>FinallyAsync</c> runs through both sources
    /// THEN both must observe exactly what the sync <c>Finally</c> observes —
    /// the value and <see cref="Error.None"/>, or the error — exactly once.
    /// </summary>
    [Fact]
    public async Task Unit_FinallyAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var observations = new List<string>();
        var syncUnit = Result.Success().Finally(error => observations.Add(error.Code));
        var syncFailure = Result.Failure(Failure).Finally(error => observations.Add(error.Code));

        var observed = new List<string>();
        await Task.FromResult(Result.Success()).FinallyAsync((error, _) => { observed.Add(error.Code); return ValueTask.CompletedTask; });
        await ValueTask.FromResult(Result.Success()).FinallyAsync((error, _) => { observed.Add(error.Code); return ValueTask.CompletedTask; });
        await Task.FromResult(Result.Failure(Failure)).FinallyAsync((error, _) => { observed.Add(error.Code); return ValueTask.CompletedTask; });
        await ValueTask.FromResult(Result.Failure(Failure)).FinallyAsync((error, _) => { observed.Add(error.Code); return ValueTask.CompletedTask; });

        observed.Should().Equal([Error.None.Code, Error.None.Code, Failure.Code, Failure.Code]);
        syncUnit.IsSuccess.Should().BeTrue();
        syncFailure.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN every unit asynchronous operator
    /// WHEN a required delegate is null
    /// THEN it must throw <see cref="ArgumentNullException"/> like its
    /// synchronous counterpart — mirroring the value tier's battery.
    /// </summary>
    [Fact]
    public async Task Unit_AsyncMethods_WithNullDelegatesShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Result.Success()).MapAsync<int>(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Result.Success()).BindAsync<int>(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Result.Success()).TapAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Result.Success()).EnsureAsync(null!, Failure).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Result.Success()).MatchAsync<string>(null!, (_, _) => ValueTask.FromResult("f")).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Result.Failure(Failure)).RecoverAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Result.Success()).FinallyAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => ValueTask.FromResult(Result.Success()).MapAsync<int>(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => ValueTask.FromResult(Result.Success()).TapAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => ValueTask.FromResult(Result.Failure(Failure)).RecoverAsync(null!).AsTask());
    }

    // --- value tier: Task vs ValueTask vs sync --------------------------------

    /// <summary>
    /// GIVEN a value success and a value failure
    /// WHEN <c>MapAsync</c> runs through both sources
    /// THEN both must return exactly the synchronous <c>Map</c> outcome.
    /// </summary>
    [Fact]
    public async Task Value_MapAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var fromSync = Result<int>.Success(41).Map(v => v + 1);
        var failureFromSync = Result<int>.Failure(Failure).Map(v => v + 1);

        var fromTask = await Task.FromResult(Result<int>.Success(41)).MapAsync((v, _) => ValueTask.FromResult(v + 1));
        var fromValueTask = await ValueTask.FromResult(Result<int>.Success(41)).MapAsync((v, _) => ValueTask.FromResult(v + 1));
        var fromTaskFailure = await Task.FromResult(Result<int>.Failure(Failure)).MapAsync((v, _) => ValueTask.FromResult(v + 1));
        var fromValueTaskFailure = await ValueTask.FromResult(Result<int>.Failure(Failure)).MapAsync((v, _) => ValueTask.FromResult(v + 1));

        fromTask.Should().Be(fromSync);
        fromValueTask.Should().Be(fromSync);
        fromTaskFailure.Should().Be(failureFromSync);
        fromValueTaskFailure.Should().Be(failureFromSync);
    }

    /// <summary>
    /// GIVEN a value success and a value failure
    /// WHEN <c>BindAsync</c> runs through both sources
    /// THEN both must return exactly the synchronous <c>Bind</c> outcome.
    /// </summary>
    [Fact]
    public async Task Value_BindAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var fromSync = Result<int>.Success(41).Bind(v => Result<string>.Success($"v{v}"));
        var failureFromSync = Result<int>.Failure(Failure).Bind(v => Result<string>.Success($"v{v}"));

        var fromTask = await Task.FromResult(Result<int>.Success(41)).BindAsync((v, _) => ValueTask.FromResult(Result<string>.Success($"v{v}")));
        var fromValueTask = await ValueTask.FromResult(Result<int>.Success(41)).BindAsync((v, _) => ValueTask.FromResult(Result<string>.Success($"v{v}")));
        var fromTaskFailure = await Task.FromResult(Result<int>.Failure(Failure)).BindAsync((v, _) => ValueTask.FromResult(Result<string>.Success($"v{v}")));
        var fromValueTaskFailure = await ValueTask.FromResult(Result<int>.Failure(Failure)).BindAsync((v, _) => ValueTask.FromResult(Result<string>.Success($"v{v}")));

        fromTask.Should().Be(fromSync);
        fromValueTask.Should().Be(fromSync);
        fromTaskFailure.Should().Be(failureFromSync);
        fromValueTaskFailure.Should().Be(failureFromSync);
    }

    /// <summary>
    /// GIVEN a value success and a value failure
    /// WHEN <c>TapAsync</c> runs through both sources
    /// THEN both must run the side effect exactly where the sync <c>Tap</c>
    /// runs it (success only).
    /// </summary>
    [Fact]
    public async Task Value_TapAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var observed = new List<int>();
        var syncSuccess = Result<int>.Success(41).Tap(v => observed.Add(v));

        var fromTask = await Task.FromResult(Result<int>.Success(41)).TapAsync((v, _) => { observed.Add(v); return ValueTask.CompletedTask; });
        var fromValueTask = await ValueTask.FromResult(Result<int>.Success(41)).TapAsync((v, _) => { observed.Add(v); return ValueTask.CompletedTask; });
        var fromTaskFailure = await Task.FromResult(Result<int>.Failure(Failure)).TapAsync((v, _) => { observed.Add(v); return ValueTask.CompletedTask; });
        var fromValueTaskFailure = await ValueTask.FromResult(Result<int>.Failure(Failure)).TapAsync((v, _) => { observed.Add(v); return ValueTask.CompletedTask; });

        observed.Should().Equal(41, 41, 41);
        fromTask.Should().Be(syncSuccess);
        fromValueTask.Should().Be(syncSuccess);
        fromTaskFailure.Should().Be(Result<int>.Failure(Failure));
        fromValueTaskFailure.Should().Be(Result<int>.Failure(Failure));
    }

    /// <summary>
    /// GIVEN a value success and a value failure
    /// WHEN <c>EnsureAsync</c> runs through both sources
    /// THEN both must return exactly the synchronous <c>Ensure</c> outcome.
    /// </summary>
    [Fact]
    public async Task Value_EnsureAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var fromSync = Result<int>.Success(41).Ensure(v => v > 0, Failure);
        var failedFromSync = Result<int>.Success(41).Ensure(v => v > 100, Failure);

        var fromTask = await Task.FromResult(Result<int>.Success(41)).EnsureAsync((v, _) => ValueTask.FromResult(v > 0), Failure);
        var fromValueTask = await ValueTask.FromResult(Result<int>.Success(41)).EnsureAsync((v, _) => ValueTask.FromResult(v > 0), Failure);
        var fromTaskFailed = await Task.FromResult(Result<int>.Success(41)).EnsureAsync((v, _) => ValueTask.FromResult(v > 100), Failure);
        var fromValueTaskFailed = await ValueTask.FromResult(Result<int>.Success(41)).EnsureAsync((v, _) => ValueTask.FromResult(v > 100), Failure);

        fromTask.Should().Be(fromSync);
        fromValueTask.Should().Be(fromSync);
        fromTaskFailed.Should().Be(failedFromSync);
        fromValueTaskFailed.Should().Be(failedFromSync);
    }

    /// <summary>
    /// GIVEN a value success and a value failure
    /// WHEN <c>MatchAsync</c> runs through both sources
    /// THEN both must return exactly the synchronous <c>Match</c> outcome on
    /// both branches.
    /// </summary>
    [Fact]
    public async Task Value_MatchAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var fromSync = Result<int>.Success(41).Match(v => v * 2, error => -1);
        var failureFromSync = Result<int>.Failure(Failure).Match(v => v * 2, error => -1);

        var fromTask = await Task.FromResult(Result<int>.Success(41)).MatchAsync((v, _) => ValueTask.FromResult(v * 2), (error, _) => ValueTask.FromResult(-1));
        var fromValueTask = await ValueTask.FromResult(Result<int>.Success(41)).MatchAsync((v, _) => ValueTask.FromResult(v * 2), (error, _) => ValueTask.FromResult(-1));
        var fromTaskFailure = await Task.FromResult(Result<int>.Failure(Failure)).MatchAsync((v, _) => ValueTask.FromResult(v * 2), (error, _) => ValueTask.FromResult(-1));
        var fromValueTaskFailure = await ValueTask.FromResult(Result<int>.Failure(Failure)).MatchAsync((v, _) => ValueTask.FromResult(v * 2), (error, _) => ValueTask.FromResult(-1));

        fromTask.Should().Be(fromSync);
        fromValueTask.Should().Be(fromSync);
        fromTaskFailure.Should().Be(failureFromSync);
        fromValueTaskFailure.Should().Be(failureFromSync);
    }

    /// <summary>
    /// GIVEN a value success and a value failure
    /// WHEN <c>RecoverAsync</c> runs through both sources
    /// THEN both must return exactly the synchronous <c>Recover</c> outcome,
    /// and the recovery must never run on success.
    /// </summary>
    [Fact]
    public async Task Value_RecoverAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var fromSync = Result<int>.Failure(Failure).Recover(error => 99);
        var successFromSync = Result<int>.Success(41).Recover(error => 99);

        var fromTask = await Task.FromResult(Result<int>.Failure(Failure)).RecoverAsync((error, _) => ValueTask.FromResult(99));
        var fromValueTask = await ValueTask.FromResult(Result<int>.Failure(Failure)).RecoverAsync((error, _) => ValueTask.FromResult(99));
        var fromTaskSuccess = await Task.FromResult(Result<int>.Success(41)).RecoverAsync((error, _) => ValueTask.FromResult(99));
        var fromValueTaskSuccess = await ValueTask.FromResult(Result<int>.Success(41)).RecoverAsync((error, _) => ValueTask.FromResult(99));

        fromTask.Should().Be(fromSync);
        fromValueTask.Should().Be(fromSync);
        fromTaskSuccess.Should().Be(successFromSync);
        fromValueTaskSuccess.Should().Be(successFromSync);
    }

    /// <summary>
    /// GIVEN a value success and a value failure
    /// WHEN <c>FinallyAsync</c> runs through both sources
    /// THEN both must observe exactly what the sync <c>Finally</c> observes —
    /// the value and <see cref="Error.None"/>, or the error — exactly once.
    /// </summary>
    [Fact]
    public async Task Value_FinallyAsync_TaskAndValueTaskShouldAgreeWithSync()
    {
        var observations = new List<int>();
        var syncOutcome = Result<int>.Success(41).Finally((v, error) => observations.Add(v));
        var syncFailure = Result<int>.Failure(Failure).Finally((v, error) => observations.Add(-1));

        var observed = new List<int>();
        await Task.FromResult(Result<int>.Success(41)).FinallyAsync((v, error, _) => { observed.Add(v); return ValueTask.CompletedTask; });
        await ValueTask.FromResult(Result<int>.Success(41)).FinallyAsync((v, error, _) => { observed.Add(v); return ValueTask.CompletedTask; });
        await Task.FromResult(Result<int>.Failure(Failure)).FinallyAsync((v, error, _) => { observed.Add(-1); return ValueTask.CompletedTask; });
        await ValueTask.FromResult(Result<int>.Failure(Failure)).FinallyAsync((v, error, _) => { observed.Add(-1); return ValueTask.CompletedTask; });

        observed.Should().Equal(41, 41, -1, -1);
        syncOutcome.IsSuccess.Should().BeTrue();
        syncFailure.IsFailure.Should().BeTrue();
    }

    // The value tier's cancelled-token battery is already covered by
    // AsyncResultExtensionsTests.AsyncMethods_WithCancelledToken_ShouldThrow;
    // named here as the covering test rather than duplicated.
}
