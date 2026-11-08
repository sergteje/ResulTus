// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Tests for the synchronous pipeline of the typed <c>Result&lt;TSuccess, TError&gt;</c> tier.
/// </summary>
/// <remarks>
/// The error type is <see cref="string"/> in most cases so a propagated failure is observable by
/// value, and <see cref="DomainError"/> where the point is that an arbitrary error type survives.
/// </remarks>
public class TypedResultExtensionsTests
{
    /// <summary>
    /// GIVEN a successful typed result
    /// WHEN <c>Map</c> runs
    /// THEN the success value is transformed and the result stays successful.
    /// </summary>
    [Fact]
    public void Map_OnSuccess_ShouldTransformTheValue()
    {
        var result = Result<int, string>.Success(7);

        var mapped = result.Map(static value => value * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Match(static value => value, static _ => -1).Should().Be(14);
    }

    /// <summary>
    /// GIVEN a failed typed result
    /// WHEN <c>Map</c> runs
    /// THEN the typed error is preserved exactly and the mapper never runs.
    /// </summary>
    [Fact]
    public void Map_OnFailure_ShouldPreserveTheTypedErrorWithoutInvokingTheMapper()
    {
        var failure = Result<int, string>.Failure("bad");
        var invoked = false;

        var mapped = failure.Map(value =>
        {
            invoked = true;
            return value * 2;
        });

        mapped.IsFailure.Should().BeTrue();
        mapped.Match(static _ => "unexpected", static error => error).Should().Be("bad");
        invoked.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a successful typed result
    /// WHEN <c>Bind</c> runs
    /// THEN the bound result replaces the receiver.
    /// </summary>
    [Fact]
    public void Bind_OnSuccess_ShouldReturnTheProducedResult()
    {
        var result = Result<int, string>.Success(7);

        var bound = result.Bind(static value => Result<string, string>.Success($"v{value}"));

        bound.Match(static value => value, static _ => "fail").Should().Be("v7");
    }

    /// <summary>
    /// GIVEN a failed typed result
    /// WHEN <c>Bind</c> runs
    /// THEN the typed error is preserved and the binder never runs.
    /// </summary>
    [Fact]
    public void Bind_OnFailure_ShouldPreserveTheTypedErrorWithoutInvokingTheBinder()
    {
        var failure = Result<int, string>.Failure("bad");
        var invoked = false;

        var bound = failure.Bind(value =>
        {
            invoked = true;
            return Result<string, string>.Success($"v{value}");
        });

        bound.Match(static _ => "fail", static error => error).Should().Be("bad");
        invoked.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a failed typed result
    /// WHEN <c>MapError</c> runs
    /// THEN only the error is transformed and the failure stays a failure.
    /// </summary>
    [Fact]
    public void MapError_OnFailure_ShouldTransformTheTypedError()
    {
        var failure = Result<int, string>.Failure("bad");

        var mapped = failure.MapError(static error => error.Length);

        mapped.IsFailure.Should().BeTrue();
        mapped.Match(static _ => -1, static length => length).Should().Be(3);
    }

    /// <summary>
    /// GIVEN a successful typed result
    /// WHEN <c>MapError</c> runs
    /// THEN the success value is preserved and the mapper never runs.
    /// </summary>
    [Fact]
    public void MapError_OnSuccess_ShouldNotInvokeTheMapper()
    {
        var result = Result<int, string>.Success(7);
        var invoked = false;

        var mapped = result.MapError(error =>
        {
            invoked = true;
            return error.Length;
        });

        mapped.Match(static value => value, static _ => -1).Should().Be(7);
        invoked.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a successful typed result
    /// WHEN <c>Tap</c> runs
    /// THEN the side effect observes the value and the receiver is returned unchanged.
    /// </summary>
    [Fact]
    public void Tap_OnSuccess_ShouldRunTheSideEffectAndReturnTheReceiver()
    {
        var result = Result<int, string>.Success(7);
        var observed = 0;

        var returned = result.Tap(value => observed = value);

        observed.Should().Be(7);
        returned.Match(static value => value, static _ => -1).Should().Be(7);
    }

    /// <summary>
    /// GIVEN a failed typed result
    /// WHEN <c>Tap</c> runs
    /// THEN the side effect never runs and the receiver is returned unchanged.
    /// </summary>
    [Fact]
    public void Tap_OnFailure_ShouldNotRunTheSideEffect()
    {
        var failure = Result<int, string>.Failure("bad");
        var invoked = false;

        var returned = failure.Tap(_ => invoked = true);

        invoked.Should().BeFalse();
        returned.Match(static _ => "fail", static error => error).Should().Be("bad");
    }

    /// <summary>
    /// GIVEN a successful typed result
    /// WHEN <c>OnSuccess</c> and <c>OnFailure</c> run
    /// THEN only the matching observer runs.
    /// </summary>
    [Fact]
    public void Observers_ShouldOnlyRunForTheirOwnOutcome()
    {
        var success = Result<int, string>.Success(7);
        var failure = Result<int, string>.Failure("bad");
        var successes = 0;
        var failures = 0;

        _ = success.OnSuccess(_ => successes++).OnFailure(_ => failures++);
        _ = failure.OnSuccess(_ => successes++).OnFailure(_ => failures++);

        successes.Should().Be(1);
        failures.Should().Be(1);
    }

    /// <summary>
    /// GIVEN both outcomes
    /// WHEN <c>Switch</c> runs
    /// THEN exactly one branch is invoked with its own payload.
    /// </summary>
    [Fact]
    public void Switch_ShouldInvokeExactlyOneBranch()
    {
        string? successBranch = null;
        string? failureBranch = null;

        Result<int, string>.Success(7).Switch(
            value => successBranch = $"ok:{value}",
            error => failureBranch = $"fail:{error}");
        Result<int, string>.Failure("bad").Switch(
            value => successBranch = $"ok:{value}",
            error => failureBranch = $"fail:{error}");

        successBranch.Should().Be("ok:7");
        failureBranch.Should().Be("fail:bad");
    }

    /// <summary>
    /// GIVEN a successful typed result that satisfies the predicate
    /// WHEN <c>Ensure</c> runs
    /// THEN the success is preserved.
    /// </summary>
    [Fact]
    public void Ensure_WhenThePredicateHolds_ShouldKeepTheSuccess()
    {
        var result = Result<int, string>.Success(7);

        var ensured = result.Ensure(static value => value > 0, "not positive");

        ensured.Match(static value => value, static _ => -1).Should().Be(7);
    }

    /// <summary>
    /// GIVEN a successful typed result that fails the predicate
    /// WHEN <c>Ensure</c> runs
    /// THEN the supplied typed error becomes the failure.
    /// </summary>
    [Fact]
    public void Ensure_WhenThePredicateFails_ShouldReturnTheSuppliedTypedError()
    {
        var result = Result<int, string>.Success(7);

        var ensured = result.Ensure(static value => value > 10, "too small");

        ensured.IsFailure.Should().BeTrue();
        ensured.Match(static _ => "ok", static error => error).Should().Be("too small");
    }

    /// <summary>
    /// GIVEN a failed typed result
    /// WHEN <c>Ensure</c> runs
    /// THEN its own error wins and the predicate never runs.
    /// </summary>
    [Fact]
    public void Ensure_OnFailure_ShouldNotInvokeThePredicate()
    {
        var failure = Result<int, string>.Failure("original");
        var invoked = false;

        var ensured = failure.Ensure(
            _ =>
            {
                invoked = true;
                return true;
            },
            "ignored");

        invoked.Should().BeFalse();
        ensured.Match(static _ => "ok", static error => error).Should().Be("original");
    }

    /// <summary>
    /// GIVEN a failed typed result
    /// WHEN <c>Recover</c> runs with a value factory
    /// THEN the failure becomes a success holding the recovered value.
    /// </summary>
    [Fact]
    public void Recover_WithValue_OnFailure_ShouldProduceASuccess()
    {
        var failure = Result<int, string>.Failure("bad");

        var recovered = failure.Recover(static error => error.Length);

        recovered.Match(static value => value, static _ => -1).Should().Be(3);
    }

    /// <summary>
    /// GIVEN a failed typed result
    /// WHEN <c>Recover</c> runs with a result factory
    /// THEN the produced result replaces the failure.
    /// </summary>
    [Fact]
    public void Recover_WithResult_OnFailure_ShouldReturnTheProducedResult()
    {
        var failure = Result<int, string>.Failure("bad");

        var recovered = failure.Recover(static error => Result<int, string>.Failure($"recovered:{error}"));

        recovered.Match(static _ => "ok", static error => error).Should().Be("recovered:bad");
    }

    /// <summary>
    /// GIVEN a successful typed result
    /// WHEN either <c>Recover</c> overload runs
    /// THEN the success is preserved and neither factory runs.
    /// </summary>
    [Fact]
    public void Recover_OnSuccess_ShouldNotInvokeEitherFactory()
    {
        var result = Result<int, string>.Success(7);
        var invoked = false;
        Func<string, int> valueFactory = error =>
        {
            invoked = true;
            return error.Length;
        };
        Func<string, Result<int, string>> resultFactory = error =>
        {
            invoked = true;
            return Result<int, string>.Failure(error);
        };

        var withValue = result.Recover(valueFactory);
        var withResult = result.Recover(resultFactory);

        invoked.Should().BeFalse();
        withValue.Match(static value => value, static _ => -1).Should().Be(7);
        withResult.Match(static value => value, static _ => -1).Should().Be(7);
    }

    /// <summary>
    /// GIVEN both outcomes
    /// WHEN <c>Finally</c> runs
    /// THEN it observes exactly one payload and returns the receiver unchanged.
    /// </summary>
    [Fact]
    public void Finally_ShouldObserveOnePayloadAndReturnTheReceiver()
    {
        var success = Result<int, string>.Success(7);
        var failure = Result<int, string>.Failure("bad");
        var observedValues = new List<int>();
        var observedErrors = new List<string?>();

        var returnedSuccess = success.Finally((value, error) =>
        {
            observedValues.Add(value);
            observedErrors.Add(error);
        });
        var returnedFailure = failure.Finally((value, error) =>
        {
            observedValues.Add(value);
            observedErrors.Add(error);
        });

        observedValues.Should().Equal(7, 0);
        observedErrors.Should().Equal(null, "bad");
        returnedSuccess.Match(static value => value, static _ => -1).Should().Be(7);
        returnedFailure.Match(static _ => "ok", static error => error).Should().Be("bad");
    }

    /// <summary>
    /// GIVEN a result whose error type is a custom struct
    /// WHEN every pipeline operator runs
    /// THEN the exact error instance survives the whole chain.
    /// </summary>
    [Fact]
    public void EveryOperator_ShouldPreserveTheTypedErrorInstance()
    {
        var error = new DomainError(404, "missing");

        var chained = Result<Order, DomainError>.Failure(error)
            .Map(static order => order.Total)
            .Bind(static total => Result<int, DomainError>.Success(total))
            .Ensure(static total => total > 0, new DomainError(1, "guard"))
            .Tap(static _ => { })
            .OnSuccess(static _ => { })
            .OnFailure(static _ => { })
            .MapError(static domainError => domainError);

        chained.Match(static total => total, static domainError => domainError.Code).Should().Be(404);
    }

    /// <summary>
    /// GIVEN a typed result
    /// WHEN any pipeline method receives a <c>null</c> delegate
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void EveryMethod_WithNullDelegate_ShouldThrow()
    {
        var result = Result<int, string>.Success(1);

        var calls = new Action[]
        {
            () => result.Map<int, string, int>(null!),
            () => result.Bind<int, string, int>(null!),
            () => result.MapError<int, string, int>(null!),
            () => result.Tap<int, string>(null!),
            () => result.OnSuccess<int, string>(null!),
            () => result.OnFailure<int, string>(null!),
            () => result.Switch<int, string>(null!, _ => { }),
            () => result.Switch<int, string>(_ => { }, null!),
            () => result.Ensure<int, string>(null!, "error"),
            () => result.Recover<int, string>((Func<string, int>)null!),
            () => result.Recover<int, string>((Func<string, Result<int, string>>)null!),
            () => result.Finally<int, string>(null!),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN a naturally typed lambda that returns the success value
    /// WHEN both <c>Recover</c> overloads are applicable in principle
    /// THEN the value overload MUST win by conversion betterness, without a cast.
    /// </summary>
    [Fact]
    public void Recover_WithANaturallyTypedLambda_ShouldBindTheValueOverload()
    {
        var failure = Result<int, DomainError>.Failure(new DomainError(9, "boom"));

        // No cast: the identity conversion into TSuccess beats the user-defined conversion into
        // Result<TSuccess, TError>, so this call binds the value overload.
        var recovered = failure.Recover(error => error.Code);

        recovered.Match(static value => value, static _ => -1).Should().Be(9);
    }

    /// <summary>
    /// GIVEN both outcomes
    /// WHEN a pipeline built from non-capturing delegates runs over a warm loop
    /// THEN it MUST allocate nothing on the heap.
    /// </summary>
    /// <remarks>
    /// <c>docs/performance.md</c> §2 makes 0 B a hard rule for core operations. The delegates here are
    /// <c>static</c> and capture nothing, so any non-zero result is the operator itself allocating.
    /// </remarks>
    [Fact]
    public void Pipeline_WithNonCapturingDelegates_ShouldNotAllocate()
    {
        var success = Result<int, string>.Success(7);
        var failure = Result<int, string>.Failure("bad");

        Measure(() =>
        {
            _successSink = success
                .Map(static value => value + 1)
                .Bind(static value => Result<int, string>.Success(value))
                .Ensure(static value => value > 0, "non-positive")
                .Tap(static _ => { });
            _failureSink = failure
                .Map(static value => value + 1)
                .Bind(static value => Result<int, string>.Success(value))
                .Ensure(static value => value > 0, "non-positive")
                .Tap(static _ => { });
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
        GC.KeepAlive(_successSink);
        GC.KeepAlive(_failureSink);
        return allocated;
    }

    private static Result<int, string> _successSink;
    private static Result<int, string> _failureSink;

    private readonly record struct Order(int Total);

    private readonly record struct DomainError(int Code, string Message);
}
