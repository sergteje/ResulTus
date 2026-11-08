// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Tests for S5: deferred <c>Ensure</c> error creation, the value-aware error factory,
/// <c>ToUnitResult</c> and the unit-returning <c>Bind</c>.
/// </summary>
public class EnsureFactoryAndProjectionTests
{
    private static Result<int> _valueSink;
    private static Result _unitSink;

    /// <summary>
    /// GIVEN a successful result whose predicate holds
    /// WHEN <c>Ensure</c> runs with a deferred error factory
    /// THEN the success is preserved and the factory never runs.
    /// </summary>
    [Fact]
    public void EnsureWithFactory_WhenPredicateHolds_ShouldNotInvokeTheFactory()
    {
        Result<int> result = 5;
        var invocations = 0;

        var ensured = result.Ensure(static value => value > 0, () =>
        {
            invocations++;
            return Error.Validation("never");
        });

        ensured.IsSuccess.Should().BeTrue();
        ensured.GetValueOrDefault().Should().Be(5);
        invocations.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a successful result whose predicate does not hold
    /// WHEN <c>Ensure</c> runs with a deferred error factory
    /// THEN the factory runs exactly once and its error becomes the failure.
    /// </summary>
    [Fact]
    public void EnsureWithFactory_WhenPredicateFails_ShouldInvokeTheFactoryOnce()
    {
        Result<int> result = 5;
        var invocations = 0;

        var ensured = result.Ensure(static value => value > 10, () =>
        {
            invocations++;
            return Error.Validation("too-small");
        });

        invocations.Should().Be(1);
        ensured.TryGetValue(out _, out var error).Should().BeFalse();
        error.Code.Should().Be("too-small");
        error.Type.Should().Be(ErrorType.Validation);
    }

    /// <summary>
    /// GIVEN an existing failure
    /// WHEN <c>Ensure</c> runs with a deferred error factory
    /// THEN neither the predicate nor the factory runs and the original error survives.
    /// </summary>
    [Fact]
    public void EnsureWithFactory_OnExistingFailure_ShouldNotInvokePredicateOrFactory()
    {
        var original = Error.NotFound("original");
        var failure = Result<int>.Failure(original);
        var predicateInvocations = 0;
        var factoryInvocations = 0;

        var ensured = failure.Ensure(
            _ =>
            {
                predicateInvocations++;
                return true;
            },
            () =>
            {
                factoryInvocations++;
                return Error.Validation("ignored");
            });

        predicateInvocations.Should().Be(0);
        factoryInvocations.Should().Be(0);
        ensured.TryGetValue(out _, out var error).Should().BeFalse();
        error.Should().Be(original);
    }

    /// <summary>
    /// GIVEN a value-aware error factory
    /// WHEN <c>Ensure</c> runs and the predicate does not hold
    /// THEN the factory receives the success value and can build the message from it.
    /// </summary>
    [Fact]
    public void EnsureWithValueFactory_ShouldReceiveTheSuccessValue()
    {
        Result<int> result = 5;
        var observed = 0;

        var ensured = result.Ensure(static value => value > 10, value =>
        {
            observed = value;
            return Error.Validation("too-small", $"got {value}");
        });

        observed.Should().Be(5);
        ensured.TryGetValue(out _, out var error).Should().BeFalse();
        error.Message.Should().Be("got 5");
    }

    /// <summary>
    /// GIVEN a value-aware error factory
    /// WHEN the predicate holds or the result was already a failure
    /// THEN the factory never runs.
    /// </summary>
    [Fact]
    public void EnsureWithValueFactory_ShouldOnlyRunWhenThePredicateFails()
    {
        Result<int> success = 5;
        var failure = Result<int>.Failure(Error.Conflict("original"));
        var invocations = 0;

        var kept = success.Ensure(static value => value > 0, value =>
        {
            invocations++;
            return Error.Validation("never", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });
        var untouched = failure.Ensure(static value => value > 0, value =>
        {
            invocations++;
            return Error.Validation("never", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });

        invocations.Should().Be(0);
        kept.IsSuccess.Should().BeTrue();
        untouched.TryGetValue(out _, out var error).Should().BeFalse();
        error.Code.Should().Be("original");
    }

    /// <summary>
    /// GIVEN a successful unit result whose predicate holds
    /// WHEN <c>Ensure</c> runs with a deferred error factory
    /// THEN the success is preserved and the factory never runs.
    /// </summary>
    [Fact]
    public void UnitEnsureWithFactory_WhenPredicateHolds_ShouldNotInvokeTheFactory()
    {
        var result = Result.Success();
        var invocations = 0;

        var ensured = result.Ensure(static () => true, () =>
        {
            invocations++;
            return Error.Validation("never");
        });

        ensured.IsSuccess.Should().BeTrue();
        invocations.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a successful unit result whose predicate does not hold
    /// WHEN <c>Ensure</c> runs with a deferred error factory
    /// THEN the factory runs exactly once and its error becomes the failure.
    /// </summary>
    [Fact]
    public void UnitEnsureWithFactory_WhenPredicateFails_ShouldInvokeTheFactoryOnce()
    {
        var result = Result.Success();
        var invocations = 0;

        var ensured = result.Ensure(static () => false, () =>
        {
            invocations++;
            return Error.Validation("rejected");
        });

        invocations.Should().Be(1);
        ensured.TryGetError(out var error).Should().BeTrue();
        error.Code.Should().Be("rejected");
        error.Type.Should().Be(ErrorType.Validation);
    }

    /// <summary>
    /// GIVEN an existing unit failure
    /// WHEN <c>Ensure</c> runs with a deferred error factory
    /// THEN neither the predicate nor the factory runs and the original error survives.
    /// </summary>
    [Fact]
    public void UnitEnsureWithFactory_OnExistingFailure_ShouldNotInvokePredicateOrFactory()
    {
        var failure = Result.Failure(Error.Timeout("slow"));
        var predicateInvocations = 0;
        var factoryInvocations = 0;

        var ensured = failure.Ensure(
            () =>
            {
                predicateInvocations++;
                return true;
            },
            () =>
            {
                factoryInvocations++;
                return Error.Validation("ignored");
            });

        predicateInvocations.Should().Be(0);
        factoryInvocations.Should().Be(0);
        ensured.TryGetError(out var error).Should().BeTrue();
        error.Code.Should().Be("slow");
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN <c>ToUnitResult</c> runs
    /// THEN the value is dropped and the result stays successful.
    /// </summary>
    [Fact]
    public void ToUnitResult_OnSuccess_ShouldDiscardTheValue()
    {
        Result<int> result = 5;

        var unit = result.ToUnitResult();

        unit.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>ToUnitResult</c> runs
    /// THEN the exact error survives and the result stays a failure.
    /// </summary>
    [Fact]
    public void ToUnitResult_OnFailure_ShouldPreserveTheError()
    {
        var error = Error.Unauthorized("denied", "no credentials");
        var result = Result<int>.Failure(error);

        var unit = result.ToUnitResult();

        unit.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN the unit-returning <c>Bind</c> runs
    /// THEN the produced unit result replaces the receiver.
    /// </summary>
    [Fact]
    public void BindToUnit_OnSuccess_ShouldReturnTheProducedUnitResult()
    {
        Result<int> result = 5;

        var bound = result.Bind(static _ => Result.Success());

        bound.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN the unit-returning <c>Bind</c> runs and the binder fails
    /// THEN that failure is returned.
    /// </summary>
    [Fact]
    public void BindToUnit_OnSuccess_WhenTheBinderFails_ShouldReturnThatFailure()
    {
        Result<int> result = 5;

        var bound = result.Bind(static _ => Result.Failure(Error.Conflict("already")));

        bound.TryGetError(out var error).Should().BeTrue();
        error.Code.Should().Be("already");
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN the unit-returning <c>Bind</c> runs
    /// THEN the original error survives and the binder never runs.
    /// </summary>
    [Fact]
    public void BindToUnit_OnFailure_ShouldPreserveTheErrorAndNotInvokeTheBinder()
    {
        var failure = Result<int>.Failure(Error.Forbidden("nope"));
        var invoked = false;

        var bound = failure.Bind(_ =>
        {
            invoked = true;
            return Result.Success();
        });

        invoked.Should().BeFalse();
        bound.TryGetError(out var error).Should().BeTrue();
        error.Code.Should().Be("nope");
    }

    /// <summary>
    /// GIVEN a result
    /// WHEN any new member receives a <c>null</c> delegate
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void EveryNewMember_WithNullDelegate_ShouldThrow()
    {
        Result<int> result = 1;
        var unit = Result.Success();

        Action[] calls =
        [
            () => result.Ensure(null!, () => Error.None),
            () => result.Ensure(static _ => true, (Func<Error>)null!),
            () => result.Ensure(static _ => true, (Func<int, Error>)null!),
            () => unit.Ensure(null!, () => Error.None),
            () => unit.Ensure(static () => true, (Func<Error>)null!),
            () => result.Bind((Func<int, Result>)null!),
        ];

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN both outcomes and non-capturing delegates
    /// WHEN the new members run over a warm loop
    /// THEN they MUST allocate nothing on the heap.
    /// </summary>
    /// <remarks>
    /// <c>docs/performance.md</c> §2 makes 0 B a hard rule for core operations. Every delegate here is
    /// <c>static</c> and the errors use interned literals, so a non-zero reading is the operator itself.
    /// </remarks>
    [Fact]
    public void NewMembers_ShouldNotAllocate()
    {
        Result<int> success = 5;
        var failure = Result<int>.Failure(Error.Conflict("original"));

        Measure(() =>
        {
            _valueSink = success.Ensure(static value => value > 0, static () => Error.Validation("never"));
            return 0;
        }).Should().Be(0);

        Measure(() =>
        {
            _valueSink = success.Ensure(static value => value > 10, static () => Error.Validation("small"));
            return 0;
        }).Should().Be(0);

        Measure(() =>
        {
            _valueSink = failure.Ensure(static value => value > 0, static () => Error.Validation("never"));
            return 0;
        }).Should().Be(0);

        Measure(() =>
        {
            _unitSink = Result.Success().Ensure(static () => true, static () => Error.Validation("never"));
            return 0;
        }).Should().Be(0);

        Measure(() =>
        {
            _unitSink = success.ToUnitResult();
            return 0;
        }).Should().Be(0);

        Measure(() =>
        {
            _unitSink = failure.ToUnitResult();
            return 0;
        }).Should().Be(0);

        Measure(() =>
        {
            _unitSink = success.Bind(static _ => Result.Success());
            return 0;
        }).Should().Be(0);
    }

    private static long Measure(Func<int> operation, int iterations = 10_000)
    {
        _ = operation();
        _ = operation();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            checksum += operation();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(checksum);
        GC.KeepAlive(_valueSink);
        GC.KeepAlive(_unitSink);
        return allocated;
    }
}
