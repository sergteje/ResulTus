// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Diagnostics.Tests;

/// <summary>
/// Tests for the value a measured operation returns: the untouched result plus the elapsed time.
/// </summary>
[Collection(DiagnosticsCollection.Name)]
public class TimingTests
{
    /// <summary>
    /// GIVEN a successful unit operation
    /// WHEN it is measured
    /// THEN the operation MUST run exactly once and its result MUST come back unchanged.
    /// </summary>
    [Fact]
    public void Measure_ShouldReturnTheResultAndRunTheOperationOnce()
    {
        var calls = 0;
        Func<Result> operation = () =>
        {
            calls++;
            return Result.Success();
        };

        var timed = operation.Measure("DoWork");

        calls.Should().Be(1);
        timed.Result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN an operation that takes measurable time
    /// WHEN it is measured
    /// THEN the elapsed time MUST reflect the work, not a constant.
    /// </summary>
    [Fact]
    public void Measure_ShouldTimeTheOperation()
    {
        Func<Result> operation = () =>
        {
            Thread.Sleep(20);
            return Result.Success();
        };

        var timed = operation.Measure("DoWork");

        timed.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(15));
    }

    /// <summary>
    /// GIVEN a failed unit operation
    /// WHEN it is measured
    /// THEN the error MUST survive with its code and type intact.
    /// </summary>
    [Fact]
    public void Measure_OnFailure_ShouldKeepTheError()
    {
        Func<Result> operation = () => Result.Failure(Error.NotFound("missing", "gone"));

        var timed = operation.Measure("DoWork");

        timed.Result.TryGetError(out var error).Should().BeTrue();
        error.Code.Should().Be("missing");
        error.Type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// GIVEN a successful generic operation
    /// WHEN it is measured
    /// THEN the value MUST come back untouched.
    /// </summary>
    [Fact]
    public void MeasureGeneric_ShouldReturnTheValue()
    {
        Func<Result<int>> operation = () => Result<int>.Success(42);

        var timed = operation.Measure("DoWork");

        timed.Result.GetValueOrDefault().Should().Be(42);
        timed.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    /// <summary>
    /// GIVEN a failed generic operation
    /// WHEN it is measured
    /// THEN the error MUST survive with its code intact.
    /// </summary>
    [Fact]
    public void MeasureGeneric_OnFailure_ShouldKeepTheError()
    {
        Func<Result<string>> operation = () => Result<string>.Failure(Error.Conflict("duplicate"));

        var timed = operation.Measure("DoWork");

        timed.Result.TryGetValue(out _, out var error).Should().BeFalse();
        error.Code.Should().Be("duplicate");
    }

    /// <summary>
    /// GIVEN a successful asynchronous unit operation
    /// WHEN it is measured
    /// THEN the awaited result MUST come back unchanged.
    /// </summary>
    [Fact]
    public async Task MeasureAsync_ShouldReturnTheResult()
    {
        Func<Task<Result>> operation = () => Task.FromResult(Result.Success());

        var timed = await operation.MeasureAsync("DoWork");

        timed.Result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN an asynchronous operation that takes measurable time
    /// WHEN it is measured
    /// THEN the elapsed time MUST include the awaited delay.
    /// </summary>
    [Fact]
    public async Task MeasureAsync_ShouldTimeTheOperation()
    {
        Func<Task<Result>> operation = async () =>
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
            return Result.Success();
        };

        var timed = await operation.MeasureAsync("DoWork");

        timed.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(15));
    }

    /// <summary>
    /// GIVEN a failed asynchronous generic operation
    /// WHEN it is measured
    /// THEN the error MUST survive with its code intact.
    /// </summary>
    [Fact]
    public async Task MeasureAsyncGeneric_OnFailure_ShouldKeepTheError()
    {
        Func<Task<Result<int>>> operation = () =>
            Task.FromResult(Result<int>.Failure(Error.Timeout("slow", "took too long")));

        var timed = await operation.MeasureAsync("DoWork");

        timed.Result.TryGetValue(out _, out var error).Should().BeFalse();
        error.Code.Should().Be("slow");
        error.Type.Should().Be(ErrorType.Timeout);
    }

    /// <summary>
    /// GIVEN a successful asynchronous generic operation
    /// WHEN it is measured
    /// THEN the value MUST come back untouched.
    /// </summary>
    [Fact]
    public async Task MeasureAsyncGeneric_ShouldReturnTheValue()
    {
        Func<Task<Result<string>>> operation = () => Task.FromResult(Result<string>.Success("ok"));

        var timed = await operation.MeasureAsync("DoWork");

        timed.Result.GetValueOrDefault().Should().Be("ok");
    }

    /// <summary>
    /// GIVEN a generic operation that throws
    /// WHEN it is measured
    /// THEN the exception MUST still reach the caller.
    /// </summary>
    [Fact]
    public void MeasureGeneric_WhenTheOperationThrows_ShouldRethrow()
    {
        Func<Result<int>> operation = () => throw new InvalidOperationException("boom");

        var act = () => operation.Measure("DoWork");

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    /// <summary>
    /// GIVEN an asynchronous generic operation that throws
    /// WHEN it is measured
    /// THEN the exception MUST still reach the caller.
    /// </summary>
    [Fact]
    public async Task MeasureAsyncGeneric_WhenTheOperationThrows_ShouldRethrow()
    {
        Func<Task<Result<int>>> operation = () => throw new InvalidOperationException("boom");

        var act = () => operation.MeasureAsync("DoWork");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// GIVEN two measured wrappers around the same result and elapsed time
    /// WHEN they are compared
    /// THEN they MUST be equal, both through the record's generated equality and through a
    /// boxed comparison, because the wrapper is a value and callers will compare it.
    /// </summary>
    [Fact]
    public void TimedResults_ShouldCompareByValue()
    {
        var elapsed = TimeSpan.FromMilliseconds(3);
        var unit = new TimedUnitResult(Result.Success(), elapsed);
        var sameUnit = new TimedUnitResult(Result.Success(), elapsed);
        var otherUnit = new TimedUnitResult(Result.Success(), TimeSpan.FromMilliseconds(4));

        unit.Should().Be(sameUnit);
        unit.Equals((object)sameUnit).Should().BeTrue();
        unit.Equals((object)"not a timed result").Should().BeFalse();
        (unit == sameUnit).Should().BeTrue();
        (unit != otherUnit).Should().BeTrue();

        var generic = new TimedResult<int>(Result<int>.Success(7), elapsed);
        var sameGeneric = new TimedResult<int>(Result<int>.Success(7), elapsed);

        generic.Should().Be(sameGeneric);
        generic.Equals((object)sameGeneric).Should().BeTrue();
        generic.Equals((object)unit).Should().BeFalse();
        generic.GetHashCode().Should().Be(sameGeneric.GetHashCode());
    }

    /// <summary>
    /// GIVEN a <c>null</c> operation or operation name
    /// WHEN a synchronous entry point is called
    /// THEN it MUST throw <see cref="ArgumentNullException"/> before running anything.
    /// </summary>
    [Fact]
    public void NullArguments_ShouldThrow()
    {
        Func<Result> unit = () => Result.Success();
        Func<Result<int>> generic = () => Result<int>.Success(1);

        var calls = new List<Action>
        {
            () => unit.Measure(null!),
            () => generic.Measure(null!),
            () => ResultDiagnosticsExtensions.Measure((Func<Result>)null!, "DoWork"),
            () => ResultDiagnosticsExtensions.Measure((Func<Result<int>>)null!, "DoWork"),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN a <c>null</c> operation or operation name
    /// WHEN an asynchronous entry point is called
    /// THEN the returned task MUST fault with <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task NullAsyncArguments_ShouldThrow()
    {
        Func<Task<Result>> asyncUnit = () => Task.FromResult(Result.Success());
        Func<Task<Result<int>>> asyncGeneric = () => Task.FromResult(Result<int>.Success(1));

        var calls = new List<Func<Task>>
        {
            () => asyncUnit.MeasureAsync(null!),
            () => asyncGeneric.MeasureAsync(null!),
            () => ResultDiagnosticsExtensions.MeasureAsync((Func<Task<Result>>)null!, "DoWork"),
            () => ResultDiagnosticsExtensions.MeasureAsync(
                (Func<Task<Result<int>>>)null!,
                "DoWork"),
        };

        foreach (var call in calls)
        {
            await call.Should().ThrowAsync<ArgumentNullException>();
        }
    }
}
