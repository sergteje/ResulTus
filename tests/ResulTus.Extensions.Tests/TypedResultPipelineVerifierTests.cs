// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Verifier pins for S1 (typed-result pipeline) and S2 (tier bridges): per-operator allocation on
/// both paths, default-error preservation, <c>Finally</c> sentinel semantics, bridge overload
/// binding and mapper short-circuiting.
/// </summary>
public class TypedResultPipelineVerifierTests
{
    private static readonly DomainError Boom = new(42, "boom");
    private static readonly DomainError Supplied = new(7, "supplied");

    private static readonly Func<int, long> Map = static value => value + 1L;
    private static readonly Func<int, Result<int, DomainError>> Bind = static value => Result<int, DomainError>.Success(value + 1);
    private static readonly Func<DomainError, int> MapError = static error => error.Code;
    private static readonly Action<int> Tap = static _ => { };
    private static readonly Action<DomainError> OnFailure = static _ => { };
    private static readonly Func<int, bool> Predicate = static value => value >= 0;
    private static readonly Func<long, bool> LongPredicate = static value => value >= 0;
    private static readonly Func<DomainError, int> RecoverValue = static error => error.Code;
    private static readonly Func<DomainError, Result<int, DomainError>> RecoverResult = static error => Result<int, DomainError>.Success(error.Code);
    private static readonly Action<int, DomainError> Finally = static (_, _) => { };
    private static readonly Action<long> LongTap = static _ => { };
    private static readonly Func<DomainError, Error> ToError = static error => Error.Failure("E", error.Message);
    private static readonly Func<Error, DomainError> ToGeneric = static error => new DomainError(1, error.Code);

    /// <summary>S1: every operator allocates 0 B on the success path and on the failure path.</summary>
    [Fact]
    public void EveryOperator_ShouldNotAllocate_OnEitherPath()
    {
        var ok = Result<int, DomainError>.Success(9);
        var fail = Result<int, DomainError>.Failure(Boom);
        var plainOk = Result<int>.Success(9);
        var plainFail = Result<int>.Failure(Error.NotFound("X"));

        Measure(() => { var r = ok.Map(Map); return r.IsSuccess ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = fail.Map(Map); return r.IsFailure ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = ok.Bind(Bind); return r.IsSuccess ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = fail.Bind(Bind); return r.IsFailure ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = fail.MapError(MapError); return r.IsFailure ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = ok.Tap(Tap); return r.IsSuccess ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = fail.Tap(Tap); return r.IsFailure ? 1 : 0; }).Should().Be(0);
        Measure(() => { ok.Switch(Tap, OnFailure); return 1; }).Should().Be(0);
        Measure(() => { fail.Switch(Tap, OnFailure); return 1; }).Should().Be(0);
        Measure(() => { var r = ok.Ensure(Predicate, Supplied); return r.IsSuccess ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = fail.Ensure(Predicate, Supplied); return r.IsFailure ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = fail.Recover(RecoverValue); return r.IsSuccess ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = fail.Recover(RecoverResult); return r.IsSuccess ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = ok.Finally(Finally); return r.IsSuccess ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = fail.Finally(Finally); return r.IsFailure ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = ok.ToErrorResult(ToError); return r.IsSuccess ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = fail.ToErrorResult(ToError); return r.IsFailure ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = plainFail.ToGenericResult(ToGeneric); return r.IsFailure ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = plainOk.ToGenericResult(ToGeneric); return r.IsSuccess ? 1 : 0; }).Should().Be(0);
    }

    /// <summary>S1: a chain of typed operators allocates 0 B on both paths.</summary>
    [Fact]
    public void ChainedPipeline_ShouldNotAllocate_OnEitherPath()
    {
        var ok = Result<int, DomainError>.Success(9);
        var fail = Result<int, DomainError>.Failure(Boom);

        Measure(() => { var r = ok.Map(Map).Ensure(LongPredicate, Supplied).Tap(LongTap); return r.IsSuccess ? 1 : 0; }).Should().Be(0);
        Measure(() => { var r = fail.Map(Map).Ensure(LongPredicate, Supplied).Tap(LongTap); return r.IsFailure ? 1 : 0; }).Should().Be(0);
    }

    /// <summary>S1: a failure holding <c>default(TError)</c> survives every non-mapping operator.</summary>
    [Fact]
    public void NonMappingOperators_ShouldPreserveADefaultTypedError()
    {
        var zero = Result<int, DomainError>.Failure(default);

        zero.Map(Map).Deconstruct(out var mappedOk, out _, out var mappedError);
        mappedOk.Should().BeFalse();
        mappedError.Should().Be(default(DomainError));

        zero.Bind(Bind).Deconstruct(out var boundOk, out _, out var boundError);
        boundOk.Should().BeFalse();
        boundError.Should().Be(default(DomainError));

        zero.Ensure(Predicate, Supplied).Deconstruct(out var ensuredOk, out _, out var ensuredError);
        ensuredOk.Should().BeFalse();
        ensuredError.Should().Be(default(DomainError));

        zero.Finally(Finally).Deconstruct(out var finalizedOk, out _, out var finalizedError);
        finalizedOk.Should().BeFalse();
        finalizedError.Should().Be(default(DomainError));
    }

    /// <summary>S1: <c>Finally</c> passes <c>default</c> for the missing payload and never throws.</summary>
    [Fact]
    public void Finally_ShouldPassDefaultsForTheMissingPayload()
    {
        var valueSeen = -1;
        var errorSeen = Boom;
        var calls = 0;

        Result<int, DomainError>.Success(5).Finally((value, error) => { valueSeen = value; errorSeen = error; calls++; });
        valueSeen.Should().Be(5);
        errorSeen.Should().Be(default(DomainError));
        calls.Should().Be(1);

        Result<int, DomainError>.Failure(Boom).Finally((value, error) => { valueSeen = value; errorSeen = error; calls++; });
        valueSeen.Should().Be(0);
        errorSeen.Should().Be(Boom);
        calls.Should().Be(2);

        string? stringValue = "untouched";
        string? stringError = "untouched";

        Result<string, string>.Success(null!).Finally((value, error) => { stringValue = value; stringError = error; });
        stringValue.Should().BeNull();
        stringError.Should().BeNull();

        Result<string, string>.Failure("boom").Finally((value, error) => { stringValue = value; stringError = error; });
        stringValue.Should().BeNull();
        stringError.Should().Be("boom");
    }

    /// <summary>S2: <c>ToErrorResult()</c> binds the parameterless overload and keeps value and error.</summary>
    [Fact]
    public void ToErrorResult_WithoutMapper_ShouldBindTheParameterlessOverload()
    {
        // The declared type is the compile-time proof: the mapper overload requires an argument.
        Result<int> success = Result<int, Error>.Success(42).ToErrorResult();
        success.IsSuccess.Should().BeTrue();
        success.GetValueOrDefault().Should().Be(42);

        var error = Error.NotFound("Missing", "gone");
        Result<int> failure = Result<int, Error>.Failure(error).ToErrorResult();
        failure.Deconstruct(out var failureOk, out _, out var kept);
        failureOk.Should().BeFalse();
        kept.Should().Be(error);

        var mapped = Result<int, Error>.Failure(error).ToErrorResult(static e => Error.Failure("Mapped", e.Code));
        mapped.Deconstruct(out _, out _, out var mappedError);
        mappedError.Should().Be(Error.Failure("Mapped", "Missing"));
    }

    /// <summary>S2: both bridge mappers are skipped on success and the value is preserved.</summary>
    [Fact]
    public void BridgeMappers_ShouldNotRunOnSuccess()
    {
        var errorMappings = 0;
        var genericMappings = 0;

        var bridged = Result<int, DomainError>.Success(9).ToErrorResult(error =>
        {
            errorMappings++;
            return Error.Failure("never", error.Message);
        });
        bridged.IsSuccess.Should().BeTrue();
        bridged.GetValueOrDefault().Should().Be(9);
        errorMappings.Should().Be(0);

        var lifted = Result<int>.Success(9).ToGenericResult(error =>
        {
            genericMappings++;
            return new DomainError(1, error.Code);
        });
        lifted.Deconstruct(out var liftedOk, out var liftedValue, out _);
        liftedOk.Should().BeTrue();
        liftedValue.Should().Be(9);
        genericMappings.Should().Be(0);
    }

    /// <summary>S1/S2: only the applicable callback fires, exactly once.</summary>
    [Fact]
    public void EveryCallback_ShouldOnlyRunForItsOwnOutcome()
    {
        var ok = Result<int, DomainError>.Success(9);
        var fail = Result<int, DomainError>.Failure(Boom);
        var plainOk = Result<int>.Success(9);
        var hits = new Counter();

        _ = fail.Map(value => { hits.Map++; return value + 1; });
        _ = fail.Bind(value => { hits.Bind++; return Result<int, DomainError>.Success(value); });
        _ = ok.MapError(error => { hits.MapError++; return error.Code; });
        _ = fail.Tap(_ => hits.Tap++);
        _ = fail.OnSuccess(_ => hits.OnSuccess++);
        _ = ok.OnFailure(_ => hits.OnFailure++);
        _ = fail.Ensure(_ => { hits.Ensure++; return true; }, Supplied);
        _ = ok.Recover(error => { hits.RecoverValue++; return error.Code; });
        _ = ok.Recover(error => { hits.RecoverResult++; return Result<int, DomainError>.Success(error.Code); });
        _ = ok.ToErrorResult(error => { hits.ToError++; return Error.Failure("never", error.Message); });
        _ = plainOk.ToGenericResult(error => { hits.ToGeneric++; return new DomainError(1, error.Code); });

        hits.Total.Should().Be(0);

        fail.Switch(_ => hits.SwitchSuccess++, _ => hits.SwitchFailure++);
        hits.SwitchSuccess.Should().Be(0);
        hits.SwitchFailure.Should().Be(1);

        ok.Switch(_ => hits.SwitchSuccess++, _ => hits.SwitchFailure++);
        hits.SwitchSuccess.Should().Be(1);
        hits.SwitchFailure.Should().Be(1);

        _ = ok.Finally((_, _) => hits.Finally++);
        _ = fail.Finally((_, _) => hits.Finally++);
        hits.Finally.Should().Be(2);
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
        return allocated;
    }

    private sealed class Counter
    {
        public int Map;
        public int Bind;
        public int MapError;
        public int Tap;
        public int OnSuccess;
        public int OnFailure;
        public int SwitchSuccess;
        public int SwitchFailure;
        public int Ensure;
        public int RecoverValue;
        public int RecoverResult;
        public int Finally;
        public int ToError;
        public int ToGeneric;

        public int Total => Map + Bind + MapError + Tap + OnSuccess + OnFailure + Ensure + RecoverValue + RecoverResult + ToError + ToGeneric;
    }

    private readonly record struct DomainError(int Code, string Message);
}
