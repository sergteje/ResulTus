// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Tests for <c>Ensure</c>, <c>Recover</c> and <c>Finally</c> on <see cref="Result{T}"/>.
/// </summary>
public class SyncGuardAndRecoveryTests
{
    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c> holding 4
    /// WHEN <c>Ensure(v =&gt; v % 2 == 0, error)</c>
    /// THEN the result MUST stay a success holding 4.
    /// </summary>
    [Fact]
    public void Ensure_WhenPredicateHolds_ShouldKeepSuccess()
    {
        Result<int> result = 4;
        var error = Error.Validation("odd");

        var ensured = result.Ensure(v => v % 2 == 0, error);

        ensured.IsSuccess.Should().BeTrue();
        ensured.GetValueOrDefault().Should().Be(4);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c> holding 3
    /// WHEN <c>Ensure(v =&gt; v % 2 == 0, error)</c>
    /// THEN the result MUST become a failure carrying the given error.
    /// </summary>
    [Fact]
    public void Ensure_WhenPredicateFails_ShouldReturnGivenError()
    {
        Result<int> result = 3;
        var error = Error.Validation("odd");

        var ensured = result.Ensure(v => v % 2 == 0, error);

        ensured.IsFailure.Should().BeTrue();
        ensured.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>Ensure</c> is called
    /// THEN the predicate MUST NOT run and the original error MUST survive.
    /// </summary>
    [Fact]
    public void Ensure_OnFailure_ShouldShortCircuit()
    {
        var error = Error.Conflict("dup");
        var result = Result<int>.Failure(error);
        var invoked = 0;

        var ensured = result.Ensure(_ =>
        {
            invoked++;
            return true;
        }, Error.Validation("unused"));

        invoked.Should().Be(0);
        ensured.IsFailure.Should().BeTrue();
        ensured.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>Recover(e =&gt; -1)</c>
    /// THEN the result MUST become a success holding <c>-1</c>.
    /// </summary>
    [Fact]
    public void Recover_WithValue_ShouldTurnFailureIntoSuccess()
    {
        var result = Result<int>.Failure(Error.NotFound("missing"));

        var recovered = result.Recover(_ => -1);

        recovered.IsSuccess.Should().BeTrue();
        recovered.GetValueOrDefault().Should().Be(-1);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>Recover</c> returns another failure
    /// THEN that failure MUST be the result.
    /// </summary>
    [Fact]
    public void Recover_WithResult_ShouldPropagateNewFailure()
    {
        var result = Result<int>.Failure(Error.NotFound("missing"));
        var replacement = Error.Timeout("slow");

        var recovered = result.Recover(_ => Result<int>.Failure(replacement));

        recovered.IsFailure.Should().BeTrue();
        recovered.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(replacement);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>Recover</c> returns a success
    /// THEN that success MUST be the result.
    /// </summary>
    [Fact]
    public void Recover_WithResult_ShouldAcceptNewSuccess()
    {
        var result = Result<int>.Failure(Error.NotFound("missing"));

        var recovered = result.Recover(_ => Result<int>.Success(42));

        recovered.IsSuccess.Should().BeTrue();
        recovered.GetValueOrDefault().Should().Be(42);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c> holding 9
    /// WHEN <c>Recover</c> is called
    /// THEN the recovery function MUST NOT run and the value MUST survive.
    /// </summary>
    [Fact]
    public void Recover_OnSuccess_ShouldNotRun()
    {
        Result<int> result = 9;
        var invoked = 0;

        var recovered = result.Recover(_ =>
        {
            invoked++;
            return -1;
        });

        invoked.Should().Be(0);
        recovered.IsSuccess.Should().BeTrue();
        recovered.GetValueOrDefault().Should().Be(9);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c> holding 6
    /// WHEN <c>Finally</c> runs
    /// THEN it MUST observe the value and <see cref="Error.None"/>, and return the original result.
    /// </summary>
    [Fact]
    public void Finally_OnSuccess_ShouldObserveValueAndNone()
    {
        Result<int> result = 6;
        var calls = 0;
        var seenValue = 0;
        var seenError = Error.None;

        var finalized = result.Finally((value, error) =>
        {
            calls++;
            seenValue = value;
            seenError = error;
        });

        calls.Should().Be(1);
        seenValue.Should().Be(6);
        seenError.Should().Be(Error.None);
        finalized.IsSuccess.Should().BeTrue();
        finalized.GetValueOrDefault().Should().Be(6);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>Finally</c> runs
    /// THEN it MUST observe the error and return the original failure.
    /// </summary>
    [Fact]
    public void Finally_OnFailure_ShouldObserveError()
    {
        var error = Error.Unexpected("boom");
        var result = Result<int>.Failure(error);
        var calls = 0;
        Error? seenError = null;

        var finalized = result.Finally((_, e) =>
        {
            calls++;
            seenError = e;
        });

        calls.Should().Be(1);
        seenError.Should().Be(error);
        finalized.IsFailure.Should().BeTrue();
        finalized.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a <c>Result&lt;int&gt;</c>
    /// WHEN a guard or recovery method receives a <c>null</c> delegate
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void GuardAndRecoveryMethods_WithNullDelegate_ShouldThrow()
    {
        Result<int> result = 1;

        var calls = new Action[]
        {
            () => result.Ensure(null!, Error.Validation("x")),
            () => result.Recover((Func<Error, int>)null!),
            () => result.Finally(null!),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }
}
