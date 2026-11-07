// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Independent verification probes for S5: deferred <c>Ensure</c> error creation, <c>ToUnitResult</c>
/// and the unit-returning <c>Bind</c>.
/// </summary>
public class EnsureFactoryAndProjectionVerifierTests
{
    private static Result<int> _valueSink;
    private static Result _unitSink;
    private static int _factoryCalls;

    private static readonly Func<int, bool> CachedPredicate = static value => value > 0;
    private static readonly Func<int, Error> CachedValueFactory = static value => Error.Validation("small");

    /// <summary>
    /// GIVEN an existing failure
    /// WHEN <c>Ensure</c> receives a method group as the factory
    /// THEN the group binds the deferred overload, never runs, and the receiver is unchanged.
    /// </summary>
    [Fact]
    public void Ensure_WithAMethodGroupFactory_OnFailure_ShouldNotInvokeIt()
    {
        var original = Error.NotFound("original");
        _factoryCalls = 0;

        var ensured = Result<int>.Failure(original).Ensure(CachedPredicate, CountingFactory);

        _factoryCalls.Should().Be(0);
        ensured.TryGetValue(out _, out var error).Should().BeFalse();
        error.Should().Be(original);
    }

    /// <summary>
    /// GIVEN an explicitly typed eager argument
    /// WHEN <c>Ensure</c> is called with <c>default(Error)</c>
    /// THEN it MUST still bind the eager overload and store that error.
    /// </summary>
    [Fact]
    public void Ensure_WithAnExplicitlyTypedDefault_ShouldStillBindTheEagerOverload()
    {
        var ensured = Result<int>.Success(1).Ensure(static value => value > 10, default(Error));

        ensured.TryGetValue(out _, out var error).Should().BeFalse();
        error.Should().Be(default(Error));
    }

    /// <summary>
    /// GIVEN a success whose value is <c>default(T)</c>
    /// WHEN the value-aware factory runs
    /// THEN it MUST receive that exact value.
    /// </summary>
    [Fact]
    public void EnsureWithValueFactory_ShouldReceiveADefaultValue()
    {
        var observed = -1;

        var ensured = Result<int>.Success(0).Ensure(
            static value => value > 10,
            value =>
            {
                observed = value;
                return Error.Validation("too-small");
            });

        observed.Should().Be(0);
        ensured.IsSuccess.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a failure carrying <c>default(Error)</c>
    /// WHEN <c>ToUnitResult</c> runs
    /// THEN it MUST stay a failure and keep that exact error instead of becoming a success.
    /// </summary>
    [Fact]
    public void ToUnitResult_WithADefaultError_ShouldStayAFailure()
    {
        var projected = Result<string>.Failure(default(Error)).ToUnitResult();

        projected.TryGetError(out var error).Should().BeTrue();
        error.Should().Be(default(Error));
    }

    /// <summary>
    /// GIVEN failures carrying <c>Error.None</c> and <c>default(Error)</c>
    /// WHEN the unit-returning <c>Bind</c> runs
    /// THEN each MUST stay a failure and keep its exact error.
    /// </summary>
    [Fact]
    public void BindToUnit_WithSentinelErrors_ShouldStayFailures()
    {
        var none = Result<int>.Failure(Error.None).Bind(static _ => Result.Success());
        var dflt = Result<int>.Failure(default(Error)).Bind(static _ => Result.Success());

        none.TryGetError(out var noneError).Should().BeTrue();
        noneError.Should().Be(Error.None);
        dflt.TryGetError(out var defaultError).Should().BeTrue();
        defaultError.Should().Be(default(Error));
    }

    /// <summary>
    /// GIVEN both outcomes and non-capturing delegates
    /// WHEN the new members run over a warm loop
    /// THEN they MUST allocate nothing on the heap.
    /// </summary>
    [Fact]
    public void NewMembers_ShouldNotAllocate()
    {
        var success = Result<int>.Success(5);
        var failure = Result<int>.Failure(Error.Conflict("original"));
        var unitFailure = Result.Failure(Error.Timeout("slow"));
        _factoryCalls = 0;

        Measure(() =>
        {
            _valueSink = success.Ensure(static value => value > 10, CachedValueFactory);
            return 0;
        }).Should().Be(0);

        Measure(() =>
        {
            _valueSink = failure.Ensure(CachedPredicate, CachedValueFactory);
            return 0;
        }).Should().Be(0);

        Measure(() =>
        {
            _unitSink = unitFailure.Ensure(static () => true, CountingFactory);
            return 0;
        }).Should().Be(0);

        _factoryCalls.Should().Be(0);
    }

    private static Error CountingFactory()
    {
        _factoryCalls++;
        return Error.Validation("x" + 1);
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
