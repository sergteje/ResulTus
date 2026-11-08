// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Tests for the asynchronous pipeline extensions on <see cref="Result{T}"/>.
/// </summary>
public class AsyncResultExtensionsTests
{
    /// <summary>
    /// GIVEN a <c>Task&lt;Result&lt;int&gt;&gt;</c> holding 2
    /// WHEN <c>MapAsync(v =&gt; v * 10)</c>
    /// THEN the result MUST be a success holding 20.
    /// </summary>
    [Fact]
    public async Task MapAsync_OnSuccess_ShouldTransformValue()
    {
        var source = Task.FromResult(Result<int>.Success(2));

        var mapped = await source.MapAsync((v, _) => ValueTask.FromResult(v * 10));

        mapped.IsSuccess.Should().BeTrue();
        mapped.GetValueOrDefault().Should().Be(20);
    }

    /// <summary>
    /// GIVEN a failed <c>Task&lt;Result&lt;int&gt;&gt;</c>
    /// WHEN <c>MapAsync</c> is called
    /// THEN the mapper MUST NOT run and the error MUST survive.
    /// </summary>
    [Fact]
    public async Task MapAsync_OnFailure_ShouldShortCircuit()
    {
        var error = Error.NotFound("missing");
        var source = Task.FromResult(Result<int>.Failure(error));
        var invoked = 0;

        var mapped = await source.MapAsync((v, _) =>
        {
            invoked++;
            return ValueTask.FromResult(v);
        });

        invoked.Should().Be(0);
        mapped.IsFailure.Should().BeTrue();
        mapped.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a <c>ValueTask&lt;Result&lt;int&gt;&gt;</c> holding 3
    /// WHEN <c>MapAsync</c> is called
    /// THEN the value task overload MUST produce the mapped success.
    /// </summary>
    [Fact]
    public async Task MapAsync_OnValueTaskSource_ShouldTransformValue()
    {
        var source = new ValueTask<Result<int>>(Result<int>.Success(3));

        var mapped = await source.MapAsync((v, _) => ValueTask.FromResult(v + 1));

        mapped.GetValueOrDefault().Should().Be(4);
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&lt;int&gt;&gt;</c> holding 2
    /// WHEN <c>BindAsync</c> returns a failure
    /// THEN that failure MUST be the result.
    /// </summary>
    [Fact]
    public async Task BindAsync_WhenBinderFails_ShouldReturnBinderError()
    {
        var error = Error.Validation("bad");
        var source = Task.FromResult(Result<int>.Success(2));

        var bound = await source.BindAsync((_, _) => ValueTask.FromResult(Result<string>.Failure(error)));

        bound.IsFailure.Should().BeTrue();
        bound.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a failed <c>Task&lt;Result&lt;int&gt;&gt;</c>
    /// WHEN <c>BindAsync</c> is called
    /// THEN the binder MUST NOT run.
    /// </summary>
    [Fact]
    public async Task BindAsync_OnFailure_ShouldShortCircuit()
    {
        var source = Task.FromResult(Result<int>.Failure(Error.Conflict("dup")));
        var invoked = 0;

        var bound = await source.BindAsync((_, _) =>
        {
            invoked++;
            return ValueTask.FromResult(Result<string>.Success("never"));
        });

        invoked.Should().Be(0);
        bound.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&lt;int&gt;&gt;</c> holding 4
    /// WHEN <c>EnsureAsync</c> fails its predicate
    /// THEN the result MUST carry the given error.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_WhenPredicateFails_ShouldReturnGivenError()
    {
        var error = Error.Validation("odd");
        var source = Task.FromResult(Result<int>.Success(3));

        var ensured = await source.EnsureAsync((v, _) => ValueTask.FromResult(v % 2 == 0), error);

        ensured.IsFailure.Should().BeTrue();
        ensured.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&lt;int&gt;&gt;</c> holding 4
    /// WHEN <c>EnsureAsync</c> holds its predicate
    /// THEN the success MUST survive.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_WhenPredicateHolds_ShouldKeepSuccess()
    {
        var source = Task.FromResult(Result<int>.Success(4));

        var ensured = await source.EnsureAsync((v, _) => ValueTask.FromResult(v % 2 == 0), Error.Validation("odd"));

        ensured.IsSuccess.Should().BeTrue();
        ensured.GetValueOrDefault().Should().Be(4);
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&lt;int&gt;&gt;</c> holding 5
    /// WHEN <c>TapAsync</c> is called
    /// THEN the side effect MUST run and the original result MUST survive.
    /// </summary>
    [Fact]
    public async Task TapAsync_OnSuccess_ShouldRunSideEffect()
    {
        var source = Task.FromResult(Result<int>.Success(5));
        var seen = 0;

        var tapped = await source.TapAsync((v, _) =>
        {
            seen = v;
            return ValueTask.CompletedTask;
        });

        seen.Should().Be(5);
        tapped.IsSuccess.Should().BeTrue();
        tapped.GetValueOrDefault().Should().Be(5);
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&lt;int&gt;&gt;</c>
    /// WHEN <c>MatchAsync</c> is called on each path
    /// THEN only the matching branch MUST run.
    /// </summary>
    [Fact]
    public async Task MatchAsync_ShouldInvokeMatchingBranch()
    {
        var success = Task.FromResult(Result<int>.Success(2));
        var failure = Task.FromResult(Result<int>.Failure(Error.NotFound("missing")));

        var fromSuccess = await success.MatchAsync(
            (v, _) => ValueTask.FromResult(v * 2),
            (_, _) => ValueTask.FromResult(-1));

        var fromFailure = await failure.MatchAsync(
            (v, _) => ValueTask.FromResult(v * 2),
            (e, _) => ValueTask.FromResult(e.Code.Length));

        fromSuccess.Should().Be(4);
        fromFailure.Should().Be(7);
    }

    /// <summary>
    /// GIVEN a failed <c>Task&lt;Result&lt;int&gt;&gt;</c>
    /// WHEN <c>RecoverAsync</c> is called
    /// THEN the recovered value MUST become a success.
    /// </summary>
    [Fact]
    public async Task RecoverAsync_OnFailure_ShouldRecover()
    {
        var source = Task.FromResult(Result<int>.Failure(Error.NotFound("missing")));

        var recovered = await source.RecoverAsync((_, _) => ValueTask.FromResult(-1));

        recovered.IsSuccess.Should().BeTrue();
        recovered.GetValueOrDefault().Should().Be(-1);
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&lt;int&gt;&gt;</c> holding 9
    /// WHEN <c>RecoverAsync</c> is called
    /// THEN the recovery MUST NOT run.
    /// </summary>
    [Fact]
    public async Task RecoverAsync_OnSuccess_ShouldNotRun()
    {
        var source = Task.FromResult(Result<int>.Success(9));
        var invoked = 0;

        var recovered = await source.RecoverAsync((_, _) =>
        {
            invoked++;
            return ValueTask.FromResult(-1);
        });

        invoked.Should().Be(0);
        recovered.GetValueOrDefault().Should().Be(9);
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&lt;int&gt;&gt;</c> on each path
    /// WHEN <c>FinallyAsync</c> runs
    /// THEN it MUST observe the outcome once and return the original result.
    /// </summary>
    [Fact]
    public async Task FinallyAsync_ShouldObserveOnceAndKeepResult()
    {
        var error = Error.Unexpected("boom");
        var success = Task.FromResult(Result<int>.Success(6));
        var failure = Task.FromResult(Result<int>.Failure(error));
        var calls = 0;
        Error? seenError = null;

        var finalizedSuccess = await success.FinallyAsync((_, _, _) =>
        {
            calls++;
            return ValueTask.CompletedTask;
        });

        var finalizedFailure = await failure.FinallyAsync((_, e, _) =>
        {
            calls++;
            seenError = e;
            return ValueTask.CompletedTask;
        });

        calls.Should().Be(2);
        seenError.Should().Be(error);
        finalizedSuccess.GetValueOrDefault().Should().Be(6);
        finalizedFailure.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN an already cancelled token
    /// WHEN any async pipeline method is called
    /// THEN it MUST throw <see cref="OperationCanceledException"/> instead of turning it into an error.
    /// </summary>
    [Fact]
    public async Task AsyncMethods_WithCancelledToken_ShouldThrow()
    {
        var source = Task.FromResult(Result<int>.Success(1));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await source.MapAsync((v, _) => ValueTask.FromResult(v), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// GIVEN a <c>Task&lt;Result&lt;int&gt;&gt;</c>
    /// WHEN an async pipeline method receives a <c>null</c> delegate or a <c>null</c> source
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task AsyncMethods_WithNullArguments_ShouldThrow()
    {
        var source = Task.FromResult(Result<int>.Success(1));
        Task<Result<int>>? nullSource = null;

        var calls = new Func<Task>[]
        {
            async () => await source.MapAsync<int, int>(null!),
            async () => await source.BindAsync<int, int>(null!),
            async () => await source.TapAsync(null!),
            async () => await source.EnsureAsync(null!, Error.Validation("x")),
            async () => await source.MatchAsync((Func<int, CancellationToken, ValueTask<int>>)null!, (_, _) => ValueTask.FromResult(0)),
            async () => await source.RecoverAsync((Func<Error, CancellationToken, ValueTask<int>>)null!),
            async () => await source.FinallyAsync(null!),
            async () => await nullSource!.MapAsync((v, _) => ValueTask.FromResult(v)),
        };

        foreach (var call in calls)
        {
            await call.Should().ThrowAsync<ArgumentNullException>();
        }
    }
}
