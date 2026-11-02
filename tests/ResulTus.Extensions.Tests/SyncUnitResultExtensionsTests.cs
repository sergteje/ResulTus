// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Tests for the synchronous pipeline extensions on the unit <see cref="Result"/>.
/// </summary>
public class SyncUnitResultExtensionsTests
{
    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>Map(() =&gt; 7)</c>
    /// THEN the result MUST be a success holding 7 and the mapper MUST run.
    /// </summary>
    [Fact]
    public void Map_OnSuccess_ShouldProduceValue()
    {
        var result = Result.Success();

        var mapped = result.Map(() => 7);

        mapped.IsSuccess.Should().BeTrue();
        mapped.GetValueOrDefault().Should().Be(7);
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN <c>Map</c> is called
    /// THEN the mapper MUST NOT run and the error MUST survive.
    /// </summary>
    [Fact]
    public void Map_OnFailure_ShouldPropagateError()
    {
        var error = Error.NotFound("missing");
        var result = Result.Failure(error);
        var invoked = 0;

        var mapped = result.Map(() =>
        {
            invoked++;
            return 7;
        });

        invoked.Should().Be(0);
        mapped.IsFailure.Should().BeTrue();
        mapped.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>Bind(() =&gt; Result&lt;string&gt;.Success("ok"))</c>
    /// THEN the chained success MUST be returned.
    /// </summary>
    [Fact]
    public void Bind_ToResultOfT_ShouldChain()
    {
        var result = Result.Success();

        var bound = result.Bind(() => Result<string>.Success("ok"));

        bound.IsSuccess.Should().BeTrue();
        bound.GetValueOrDefault().Should().Be("ok");
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>Bind(() =&gt; Result.Failure(error))</c>
    /// THEN the chained failure MUST be returned.
    /// </summary>
    [Fact]
    public void Bind_ToUnitResult_ShouldChainFailure()
    {
        var error = Error.Conflict("dup");
        var result = Result.Success();

        var bound = result.Bind(() => Result.Failure(error));

        bound.IsFailure.Should().BeTrue();
        bound.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN <c>Bind</c> is called
    /// THEN the binder MUST NOT run.
    /// </summary>
    [Fact]
    public void Bind_OnFailure_ShouldShortCircuit()
    {
        var result = Result.Failure(Error.Timeout("slow"));
        var invoked = 0;

        var bound = result.Bind(() =>
        {
            invoked++;
            return Result.Success();
        });

        invoked.Should().Be(0);
        bound.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN <c>MapError</c> is called
    /// THEN the failure MUST carry the mapped error.
    /// </summary>
    [Fact]
    public void MapError_OnFailure_ShouldTransformError()
    {
        var result = Result.Failure(Error.NotFound("missing"));

        var mapped = result.MapError(e => Error.Timeout(e.Code));

        mapped.TryGetError(out var error).Should().BeTrue();
        error.Type.Should().Be(ErrorType.Timeout);
        error.Code.Should().Be("missing");
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>Tap</c> is called
    /// THEN the side effect MUST run and the result MUST stay successful.
    /// </summary>
    [Fact]
    public void Tap_OnSuccess_ShouldRunSideEffect()
    {
        var result = Result.Success();
        var calls = 0;

        var tapped = result.Tap(() => calls++);

        calls.Should().Be(1);
        tapped.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN <c>Tap</c> is called
    /// THEN the side effect MUST NOT run.
    /// </summary>
    [Fact]
    public void Tap_OnFailure_ShouldNotRun()
    {
        var result = Result.Failure(Error.Unexpected("boom"));
        var calls = 0;

        result.Tap(() => calls++).IsFailure.Should().BeTrue();
        calls.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>OnSuccess</c> is called
    /// THEN it MUST behave exactly like <c>Tap</c>.
    /// </summary>
    [Fact]
    public void OnSuccess_ShouldBehaveLikeTap()
    {
        var result = Result.Success();
        var calls = 0;

        var observed = result.OnSuccess(() => calls++);

        calls.Should().Be(1);
        observed.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN <c>OnFailure</c> is called
    /// THEN the observer MUST receive the error.
    /// </summary>
    [Fact]
    public void OnFailure_OnFailure_ShouldObserveError()
    {
        var error = Error.Forbidden("nope");
        var result = Result.Failure(error);
        Error? seen = null;

        var observed = result.OnFailure(e => seen = e);

        seen.Should().Be(error);
        observed.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>OnFailure</c> is called
    /// THEN the observer MUST NOT run.
    /// </summary>
    [Fact]
    public void OnFailure_OnSuccess_ShouldNotObserve()
    {
        var result = Result.Success();
        var calls = 0;

        result.OnFailure(_ => calls++).IsSuccess.Should().BeTrue();
        calls.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>Switch</c> is called
    /// THEN only the success branch MUST run.
    /// </summary>
    [Fact]
    public void Switch_OnSuccess_ShouldRunOnlySuccessBranch()
    {
        var result = Result.Success();
        var success = 0;
        var failure = 0;

        result.Switch(() => success++, _ => failure++);

        success.Should().Be(1);
        failure.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN <c>Switch</c> is called
    /// THEN only the failure branch MUST run.
    /// </summary>
    [Fact]
    public void Switch_OnFailure_ShouldRunOnlyFailureBranch()
    {
        var result = Result.Failure(Error.Timeout("slow"));
        var success = 0;
        var failure = 0;

        result.Switch(() => success++, _ => failure++);

        success.Should().Be(0);
        failure.Should().Be(1);
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>Ensure</c> fails its predicate
    /// THEN the result MUST carry the given error.
    /// </summary>
    [Fact]
    public void Ensure_WhenPredicateFails_ShouldReturnGivenError()
    {
        var result = Result.Success();
        var error = Error.Validation("nope");

        var ensured = result.Ensure(() => false, error);

        ensured.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>Ensure</c> holds its predicate
    /// THEN the result MUST stay successful.
    /// </summary>
    [Fact]
    public void Ensure_WhenPredicateHolds_ShouldKeepSuccess()
    {
        var result = Result.Success();

        result.Ensure(() => true, Error.Validation("nope")).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN <c>Recover</c> returns a success
    /// THEN the recovered success MUST be the result.
    /// </summary>
    [Fact]
    public void Recover_OnFailure_ShouldReturnRecoveredResult()
    {
        var result = Result.Failure(Error.NotFound("missing"));

        var recovered = result.Recover(_ => Result.Success());

        recovered.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>Recover</c> is called
    /// THEN the recovery MUST NOT run.
    /// </summary>
    [Fact]
    public void Recover_OnSuccess_ShouldNotRun()
    {
        var result = Result.Success();
        var invoked = 0;

        var recovered = result.Recover(_ =>
        {
            invoked++;
            return Result.Failure(Error.Unexpected("never"));
        });

        invoked.Should().Be(0);
        recovered.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN <c>Finally</c> runs
    /// THEN it MUST observe the error and return the original failure.
    /// </summary>
    [Fact]
    public void Finally_OnFailure_ShouldObserveError()
    {
        var error = Error.Unexpected("boom");
        var result = Result.Failure(error);
        var calls = 0;
        Error? seen = null;

        var finalized = result.Finally(e =>
        {
            calls++;
            seen = e;
        });

        calls.Should().Be(1);
        seen.Should().Be(error);
        finalized.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>Finally</c> runs
    /// THEN it MUST observe <see cref="Error.None"/>.
    /// </summary>
    [Fact]
    public void Finally_OnSuccess_ShouldObserveNone()
    {
        var result = Result.Success();
        var seen = Error.Unexpected("not-none");

        var finalized = result.Finally(e => seen = e);

        seen.Should().Be(Error.None);
        finalized.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a <see cref="Result"/>
    /// WHEN any unit pipeline method receives a <c>null</c> delegate
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void EveryMethod_WithNullDelegate_ShouldThrow()
    {
        var result = Result.Success();

        var calls = new Action[]
        {
            () => result.Map<int>(null!),
            () => result.Bind<int>(null!),
            () => result.Bind((Func<Result>)null!),
            () => result.MapError(null!),
            () => result.Tap(null!),
            () => result.OnSuccess(null!),
            () => result.OnFailure(null!),
            () => result.Switch(null!, _ => { }),
            () => result.Switch(() => { }, null!),
            () => result.Ensure(null!, Error.Validation("x")),
            () => result.Recover(null!),
            () => result.Finally(null!),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }
}
