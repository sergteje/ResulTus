// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Tests for the synchronous pipeline extensions on <see cref="Result{T}"/>.
/// </summary>
public class SyncResultExtensionsTests
{
    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c> holding 2
    /// WHEN <c>Map(v =&gt; v * 10)</c>
    /// THEN the result MUST be a success holding 20.
    /// </summary>
    [Fact]
    public void Map_OnSuccess_ShouldTransformValue()
    {
        Result<int> result = 2;

        var mapped = result.Map(v => v * 10);

        mapped.IsSuccess.Should().BeTrue();
        mapped.GetValueOrDefault().Should().Be(20);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c> with error <c>e</c>
    /// WHEN <c>Map(v =&gt; v * 10)</c>
    /// THEN the result MUST stay a failure carrying <c>e</c> and the mapper MUST NOT run.
    /// </summary>
    [Fact]
    public void Map_OnFailure_ShouldPropagateErrorWithoutInvokingMapper()
    {
        var error = Error.NotFound("missing");
        var result = Result<int>.Failure(error);
        var invoked = 0;

        var mapped = result.Map(v =>
        {
            invoked++;
            return v * 10;
        });

        mapped.IsFailure.Should().BeTrue();
        invoked.Should().Be(0);
        mapped.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c> holding 2
    /// WHEN <c>Bind(v =&gt; Result&lt;string&gt;.Success($"n={v}"))</c>
    /// THEN the result MUST be a success holding <c>"n=2"</c>.
    /// </summary>
    [Fact]
    public void Bind_OnSuccess_ShouldChainResult()
    {
        Result<int> result = 2;

        var bound = result.Bind(v => Result<string>.Success($"n={v}"));

        bound.IsSuccess.Should().BeTrue();
        bound.GetValueOrDefault().Should().Be("n=2");
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN <c>Bind</c> returns a failure
    /// THEN the chained failure MUST be the result.
    /// </summary>
    [Fact]
    public void Bind_WhenBinderFails_ShouldReturnBinderError()
    {
        Result<int> result = 2;
        var error = Error.Validation("bad");

        var bound = result.Bind(_ => Result<string>.Failure(error));

        bound.IsFailure.Should().BeTrue();
        bound.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>Bind</c> is called
    /// THEN the binder MUST NOT run and the original error MUST survive.
    /// </summary>
    [Fact]
    public void Bind_OnFailure_ShouldShortCircuit()
    {
        var error = Error.Conflict("dup");
        var result = Result<int>.Failure(error);
        var invoked = 0;

        var bound = result.Bind(_ =>
        {
            invoked++;
            return Result<string>.Success("never");
        });

        bound.IsFailure.Should().BeTrue();
        invoked.Should().Be(0);
        bound.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>MapError(e =&gt; Error.Timeout(e.Code))</c>
    /// THEN the failure MUST carry the mapped error.
    /// </summary>
    [Fact]
    public void MapError_OnFailure_ShouldTransformError()
    {
        var result = Result<int>.Failure(Error.NotFound("missing"));

        var mapped = result.MapError(e => Error.Timeout(e.Code));

        mapped.IsFailure.Should().BeTrue();
        mapped.TryGetValue(out _, out var error).Should().BeFalse();
        error.Type.Should().Be(ErrorType.Timeout);
        error.Code.Should().Be("missing");
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN <c>MapError</c> is called
    /// THEN the mapper MUST NOT run and the success value MUST survive.
    /// </summary>
    [Fact]
    public void MapError_OnSuccess_ShouldKeepValue()
    {
        Result<int> result = 7;
        var invoked = 0;

        var mapped = result.MapError(e =>
        {
            invoked++;
            return e;
        });

        mapped.IsSuccess.Should().BeTrue();
        mapped.GetValueOrDefault().Should().Be(7);
        invoked.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c> holding 5
    /// WHEN <c>Tap</c> observes the value
    /// THEN the side effect MUST run and the original result MUST be returned unchanged.
    /// </summary>
    [Fact]
    public void Tap_OnSuccess_ShouldRunSideEffectAndKeepResult()
    {
        Result<int> result = 5;
        var seen = 0;

        var tapped = result.Tap(v => seen = v);

        seen.Should().Be(5);
        tapped.IsSuccess.Should().BeTrue();
        tapped.GetValueOrDefault().Should().Be(5);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>Tap</c> is called
    /// THEN the side effect MUST NOT run and the failure MUST survive.
    /// </summary>
    [Fact]
    public void Tap_OnFailure_ShouldNotRunSideEffect()
    {
        var error = Error.Unexpected("boom");
        var result = Result<int>.Failure(error);
        var invoked = 0;

        var tapped = result.Tap(_ => invoked++);

        invoked.Should().Be(0);
        tapped.IsFailure.Should().BeTrue();
        tapped.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c> holding 5
    /// WHEN <c>OnSuccess</c> observes the value
    /// THEN it MUST behave exactly like <c>Tap</c>.
    /// </summary>
    [Fact]
    public void OnSuccess_ShouldBehaveLikeTap()
    {
        Result<int> result = 5;
        var seen = 0;

        var observed = result.OnSuccess(v => seen = v);

        seen.Should().Be(5);
        observed.IsSuccess.Should().BeTrue();
        observed.GetValueOrDefault().Should().Be(5);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c> with error <c>e</c>
    /// WHEN <c>OnFailure</c> observes the error
    /// THEN it MUST receive <c>e</c> and the failure MUST survive.
    /// </summary>
    [Fact]
    public void OnFailure_OnFailure_ShouldObserveError()
    {
        var error = Error.Forbidden("nope");
        var result = Result<int>.Failure(error);
        Error? seen = null;

        var observed = result.OnFailure(e => seen = e);

        seen.Should().Be(error);
        observed.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN <c>OnFailure</c> is called
    /// THEN the observer MUST NOT run.
    /// </summary>
    [Fact]
    public void OnFailure_OnSuccess_ShouldNotObserve()
    {
        Result<int> result = 5;
        var invoked = 0;

        var observed = result.OnFailure(_ => invoked++);

        invoked.Should().Be(0);
        observed.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN <c>Switch</c> is called
    /// THEN only the success branch MUST run.
    /// </summary>
    [Fact]
    public void Switch_OnSuccess_ShouldRunOnlySuccessBranch()
    {
        Result<int> result = 3;
        var success = 0;
        var failure = 0;

        result.Switch(v => success = v, _ => failure++);

        success.Should().Be(3);
        failure.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>Switch</c> is called
    /// THEN only the failure branch MUST run.
    /// </summary>
    [Fact]
    public void Switch_OnFailure_ShouldRunOnlyFailureBranch()
    {
        var result = Result<int>.Failure(Error.Timeout("slow"));
        var success = 0;
        var failure = 0;

        result.Switch(_ => success++, _ => failure++);

        success.Should().Be(0);
        failure.Should().Be(1);
    }

    /// <summary>
    /// GIVEN a <c>Result&lt;int&gt;</c>
    /// WHEN any synchronous pipeline method receives a <c>null</c> delegate
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void EveryMethod_WithNullDelegate_ShouldThrow()
    {
        Result<int> result = 1;

        var calls = new Action[]
        {
            () => result.Map<int, int>(null!),
            () => result.Bind<int, int>(null!),
            () => result.MapError(null!),
            () => result.Tap(null!),
            () => result.OnSuccess(null!),
            () => result.OnFailure(null!),
            () => result.Switch(null!, _ => { }),
            () => result.Switch(_ => { }, null!),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }
}
