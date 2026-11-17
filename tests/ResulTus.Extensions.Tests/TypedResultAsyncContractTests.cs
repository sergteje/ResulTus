// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Contract tests for the typed asynchronous tier that
/// <see cref="CrossTierSemanticsTests"/> does not already pin: the
/// <c>OnSuccessAsync</c> alias, the <c>Task</c>-source null guard of every operator, and the
/// cancellation contract of the operators whose cancelled-token battery was missing.
/// </summary>
/// <remarks>
/// The remaining typed operators (<c>MapErrorAsync</c>, <c>OnFailureAsync</c>,
/// <c>SwitchAsync</c>, <c>EnsureAsync</c>) already have their null and cancelled battery in
/// <c>Typed_AsyncMethods_NullAndCancelledShouldFollowTheContract</c>, so it is not duplicated here.
/// </remarks>
public class TypedResultAsyncContractTests
{
    private static readonly Error Failure = Error.Failure("F1", "boom");

    /// <summary>
    /// GIVEN a typed success and a typed failure behind a <c>Task</c>
    /// WHEN <c>OnSuccessAsync</c> runs
    /// THEN the effect MUST run once with the success value and both results MUST come back
    /// unchanged, because the operator is an alias of <c>TapAsync</c>.
    /// </summary>
    [Fact]
    public async Task OnSuccessAsync_TaskSource_ShouldRunOnlyOnSuccessAndKeepTheResult()
    {
        var success = Task.FromResult(Result<int, string>.Success(41));
        var failure = Task.FromResult(Result<int, string>.Failure("E1"));
        var observed = 0;

        var pipedSuccess = await success.OnSuccessAsync((value, _) =>
        {
            observed = value;
            return ValueTask.CompletedTask;
        });

        var pipedFailure = await failure.OnSuccessAsync((_, _) =>
        {
            observed = -1;
            return ValueTask.CompletedTask;
        });

        observed.Should().Be(41);
        pipedSuccess.Deconstruct(out var isSuccess, out var keptValue, out _);
        isSuccess.Should().BeTrue();
        keptValue.Should().Be(41);

        pipedFailure.Deconstruct(out var isFailure, out _, out var keptError);
        isFailure.Should().BeFalse();
        keptError.Should().Be("E1");
    }

    /// <summary>
    /// GIVEN a typed success and a typed failure behind a <c>ValueTask</c>
    /// WHEN <c>OnSuccessAsync</c> runs
    /// THEN it MUST behave exactly like the <c>Task</c> source.
    /// </summary>
    [Fact]
    public async Task OnSuccessAsync_ValueTaskSource_ShouldRunOnlyOnSuccessAndKeepTheResult()
    {
        var success = ValueTask.FromResult(Result<int, string>.Success(41));
        var failure = ValueTask.FromResult(Result<int, string>.Failure("E1"));
        var observed = 0;

        var pipedSuccess = await success.OnSuccessAsync((value, _) =>
        {
            observed = value;
            return ValueTask.CompletedTask;
        });

        var pipedFailure = await failure.OnSuccessAsync((_, _) =>
        {
            observed = -1;
            return ValueTask.CompletedTask;
        });

        observed.Should().Be(41);
        pipedSuccess.Deconstruct(out var isSuccess, out var keptValue, out _);
        isSuccess.Should().BeTrue();
        keptValue.Should().Be(41);

        pipedFailure.Deconstruct(out var isFailure, out _, out var keptError);
        isFailure.Should().BeFalse();
        keptError.Should().Be("E1");
    }

    /// <summary>
    /// GIVEN an already-cancelled token
    /// WHEN <c>OnSuccessAsync</c> runs through either source
    /// THEN it MUST throw <see cref="OperationCanceledException"/> before the effect is invoked,
    /// so cancellation is control flow and never a silent skip.
    /// </summary>
    [Fact]
    public async Task OnSuccessAsync_WithCancelledToken_ShouldThrowWithoutRunningTheEffect()
    {
        var cancelled = new CancellationToken(canceled: true);
        var invoked = 0;

        var taskSource = Task.FromResult(Result<int, string>.Success(41));
        var valueTaskSource = ValueTask.FromResult(Result<int, string>.Success(41));

        await Assert.ThrowsAsync<OperationCanceledException>(() => taskSource
            .OnSuccessAsync((_, _) => { invoked++; return ValueTask.CompletedTask; }, cancelled)
            .AsTask());

        await Assert.ThrowsAsync<OperationCanceledException>(() => valueTaskSource
            .OnSuccessAsync((_, _) => { invoked++; return ValueTask.CompletedTask; }, cancelled)
            .AsTask());

        invoked.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a <c>null</c> <c>Task</c> source
    /// WHEN any typed asynchronous operator is called on it
    /// THEN it MUST throw <see cref="ArgumentNullException"/> instead of failing later with a
    /// <see cref="NullReferenceException"/>, for every overload of the tier.
    /// </summary>
    [Fact]
    public void TypedAsyncMethods_WithNullTaskSource_ShouldThrow()
    {
        Task<Result<int, string>>? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.MapAsync((value, _) => ValueTask.FromResult(value)));
        Assert.Throws<ArgumentNullException>(() => source!.BindAsync((value, _) => ValueTask.FromResult(Result<int, string>.Success(value))));
        Assert.Throws<ArgumentNullException>(() => source!.TapAsync((_, _) => ValueTask.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => source!.OnSuccessAsync((_, _) => ValueTask.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => source!.MatchAsync(
            (value, _) => ValueTask.FromResult(value),
            (_, _) => ValueTask.FromResult(0)));
        Assert.Throws<ArgumentNullException>(() => source!.RecoverAsync((_, _) => ValueTask.FromResult(0)));
        Assert.Throws<ArgumentNullException>(() => source!.RecoverAsync(
            (_, _) => ValueTask.FromResult(Result<int, string>.Success(0))));
        Assert.Throws<ArgumentNullException>(() => source!.FinallyAsync((_, _, _) => ValueTask.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => source!.MapErrorAsync((error, _) => ValueTask.FromResult(error)));
        Assert.Throws<ArgumentNullException>(() => source!.OnFailureAsync((_, _) => ValueTask.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => source!.SwitchAsync(
            (_, _) => ValueTask.CompletedTask,
            (_, _) => ValueTask.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => source!.EnsureAsync((_, _) => ValueTask.FromResult(true), "NEGATIVE"));
    }

    /// <summary>
    /// GIVEN an already-cancelled token
    /// WHEN the typed asynchronous operators that had no cancelled-token battery run
    /// THEN each MUST throw <see cref="OperationCanceledException"/> before awaiting the source or
    /// invoking the delegate, so a cancelled pipeline never performs the work.
    /// </summary>
    [Fact]
    public async Task TypedAsyncMethods_WithCancelledToken_ShouldThrow()
    {
        var cancelled = new CancellationToken(canceled: true);
        var invoked = 0;

        var success = Task.FromResult(Result<int, string>.Success(41));
        var failure = Task.FromResult(Result<int, string>.Failure("E1"));

        await Assert.ThrowsAsync<OperationCanceledException>(() => success
            .MapAsync((value, _) => { invoked++; return ValueTask.FromResult(value); }, cancelled)
            .AsTask());

        await Assert.ThrowsAsync<OperationCanceledException>(() => success
            .BindAsync((value, _) => { invoked++; return ValueTask.FromResult(Result<int, string>.Success(value)); }, cancelled)
            .AsTask());

        await Assert.ThrowsAsync<OperationCanceledException>(() => success
            .TapAsync((_, _) => { invoked++; return ValueTask.CompletedTask; }, cancelled)
            .AsTask());

        await Assert.ThrowsAsync<OperationCanceledException>(() => success
            .MatchAsync(
                (value, _) => { invoked++; return ValueTask.FromResult(value); },
                (_, _) => { invoked++; return ValueTask.FromResult(0); },
                cancelled)
            .AsTask());

        await Assert.ThrowsAsync<OperationCanceledException>(() => failure
            .RecoverAsync((_, _) => { invoked++; return ValueTask.FromResult(0); }, cancelled)
            .AsTask());

        await Assert.ThrowsAsync<OperationCanceledException>(() => failure
            .RecoverAsync(
                (_, _) => { invoked++; return ValueTask.FromResult(Result<int, string>.Success(0)); },
                cancelled)
            .AsTask());

        await Assert.ThrowsAsync<OperationCanceledException>(() => success
            .FinallyAsync((_, _, _) => { invoked++; return ValueTask.CompletedTask; }, cancelled)
            .AsTask());

        invoked.Should().Be(0);
    }
}
