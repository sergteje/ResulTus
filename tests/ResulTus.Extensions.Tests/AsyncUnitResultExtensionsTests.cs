// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Tests for the asynchronous pipeline extensions on the unit <see cref="Result"/>.
/// </summary>
public class AsyncUnitResultExtensionsTests
{
    /// <summary>
    /// GIVEN a <c>Task&lt;Result&gt;</c> that succeeded
    /// WHEN <c>MapAsync(() =&gt; 7)</c>
    /// THEN the result MUST be a success holding 7.
    /// </summary>
    [Fact]
    public async Task MapAsync_OnSuccess_ShouldProduceValue()
    {
        var source = Task.FromResult(Result.Success());

        var mapped = await source.MapAsync(_ => ValueTask.FromResult(7));

        mapped.IsSuccess.Should().BeTrue();
        mapped.GetValueOrDefault().Should().Be(7);
    }

    /// <summary>
    /// GIVEN a failed <c>Task&lt;Result&gt;</c>
    /// WHEN <c>MapAsync</c> is called
    /// THEN the mapper MUST NOT run.
    /// </summary>
    [Fact]
    public async Task MapAsync_OnFailure_ShouldShortCircuit()
    {
        var source = Task.FromResult(Result.Failure(Error.NotFound("missing")));
        var invoked = 0;

        var mapped = await source.MapAsync(_ =>
        {
            invoked++;
            return ValueTask.FromResult(7);
        });

        invoked.Should().Be(0);
        mapped.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&gt;</c> that succeeded
    /// WHEN <c>BindAsync</c> returns a failure
    /// THEN that failure MUST be the result.
    /// </summary>
    [Fact]
    public async Task BindAsync_WhenBinderFails_ShouldReturnBinderError()
    {
        var error = Error.Conflict("dup");
        var source = Task.FromResult(Result.Success());

        var bound = await source.BindAsync(_ => ValueTask.FromResult(Result<string>.Failure(error)));

        bound.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a <c>ValueTask&lt;Result&gt;</c> that succeeded
    /// WHEN <c>BindAsync</c> is called
    /// THEN the value task overload MUST chain the success.
    /// </summary>
    [Fact]
    public async Task BindAsync_OnValueTaskSource_ShouldChain()
    {
        var source = new ValueTask<Result>(Result.Success());

        var bound = await source.BindAsync(_ => ValueTask.FromResult(Result<string>.Success("ok")));

        bound.GetValueOrDefault().Should().Be("ok");
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&gt;</c>
    /// WHEN <c>TapAsync</c> is called on each path
    /// THEN the side effect MUST run only on success.
    /// </summary>
    [Fact]
    public async Task TapAsync_ShouldRunOnlyOnSuccess()
    {
        var success = Task.FromResult(Result.Success());
        var failure = Task.FromResult(Result.Failure(Error.Unexpected("boom")));
        var calls = 0;

        await success.TapAsync(_ =>
        {
            calls++;
            return ValueTask.CompletedTask;
        });

        await failure.TapAsync(_ =>
        {
            calls++;
            return ValueTask.CompletedTask;
        });

        calls.Should().Be(1);
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&gt;</c> that succeeded
    /// WHEN <c>EnsureAsync</c> fails its predicate
    /// THEN the result MUST carry the given error.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_WhenPredicateFails_ShouldReturnGivenError()
    {
        var error = Error.Validation("nope");
        var source = Task.FromResult(Result.Success());

        var ensured = await source.EnsureAsync(_ => ValueTask.FromResult(false), error);

        ensured.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&gt;</c>
    /// WHEN <c>MatchAsync</c> is called on each path
    /// THEN only the matching branch MUST run.
    /// </summary>
    [Fact]
    public async Task MatchAsync_ShouldInvokeMatchingBranch()
    {
        var success = Task.FromResult(Result.Success());
        var failure = Task.FromResult(Result.Failure(Error.NotFound("missing")));

        var fromSuccess = await success.MatchAsync(
            _ => ValueTask.FromResult("ok"),
            (_, _) => ValueTask.FromResult("failed"));

        var fromFailure = await failure.MatchAsync(
            _ => ValueTask.FromResult("ok"),
            (e, _) => ValueTask.FromResult(e.Code));

        fromSuccess.Should().Be("ok");
        fromFailure.Should().Be("missing");
    }

    /// <summary>
    /// GIVEN a failed <c>Task&lt;Result&gt;</c>
    /// WHEN <c>RecoverAsync</c> is called
    /// THEN the recovered result MUST be returned.
    /// </summary>
    [Fact]
    public async Task RecoverAsync_OnFailure_ShouldRecover()
    {
        var source = Task.FromResult(Result.Failure(Error.Timeout("slow")));

        var recovered = await source.RecoverAsync((_, _) => ValueTask.FromResult(Result.Success()));

        recovered.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&gt;</c>
    /// WHEN <c>FinallyAsync</c> runs on each path
    /// THEN it MUST observe <see cref="Error.None"/> on success and the error on failure.
    /// </summary>
    [Fact]
    public async Task FinallyAsync_ShouldObserveOutcome()
    {
        var error = Error.Unexpected("boom");
        var success = Task.FromResult(Result.Success());
        var failure = Task.FromResult(Result.Failure(error));
        var seen = new List<Error>();

        await success.FinallyAsync((e, _) =>
        {
            seen.Add(e);
            return ValueTask.CompletedTask;
        });

        await failure.FinallyAsync((e, _) =>
        {
            seen.Add(e);
            return ValueTask.CompletedTask;
        });

        seen.Should().HaveCount(2);
        seen[0].Should().Be(Error.None);
        seen[1].Should().Be(error);
    }

    /// <summary>
    /// GIVEN an already cancelled token
    /// WHEN an async unit pipeline method is called
    /// THEN it MUST throw <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public async Task AsyncMethods_WithCancelledToken_ShouldThrow()
    {
        var source = Task.FromResult(Result.Success());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await source.MapAsync(_ => ValueTask.FromResult(1), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

