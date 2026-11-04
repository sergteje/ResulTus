// Copyright (c) ResulTus. All rights reserved.

using System.Diagnostics;

namespace ResulTus.Diagnostics.Tests;

/// <summary>
/// Tests for the <see cref="Activity"/> a measured operation publishes.
/// </summary>
[Collection(DiagnosticsCollection.Name)]
public class ActivityTests
{
    private const string SuccessTag = "resul_tus.success";
    private const string ErrorTypeTag = "resul_tus.error_type";
    private const string ErrorCodeTag = "resul_tus.error_code";

    /// <summary>
    /// GIVEN a sampled activity source
    /// WHEN a unit operation is measured
    /// THEN exactly one activity MUST stop, named after the operation and of kind
    /// <see cref="ActivityKind.Internal"/>.
    /// </summary>
    [Fact]
    public void Measure_ShouldStartAnActivityNamedAfterTheOperation()
    {
        using var collector = new ActivityCollector();
        Func<Result> operation = () => Result.Success();

        operation.Measure("DoWork");

        collector.Activities.Should().ContainSingle();
        collector.Activities[0].DisplayName.Should().Be("DoWork");
        collector.Activities[0].Kind.Should().Be(ActivityKind.Internal);
    }

    /// <summary>
    /// GIVEN an ambient parent activity
    /// WHEN an operation is measured
    /// THEN the measured activity MUST be its child, so a trace connects across a handler.
    /// </summary>
    [Fact]
    public void Measure_ShouldNestUnderTheCurrentActivity()
    {
        using var collector = new ActivityCollector();
        using var parent = new Activity("parent").Start();
        Func<Result> operation = () => Result.Success();

        operation.Measure("DoWork");

        collector.Activities.Should().ContainSingle();
        collector.Activities[0].ParentId.Should().Be(parent.Id);
    }

    /// <summary>
    /// GIVEN a successful operation
    /// WHEN it is measured
    /// THEN the activity MUST record the outcome and stay out of the error status.
    /// </summary>
    [Fact]
    public void Measure_OnSuccess_ShouldTagSuccess()
    {
        using var collector = new ActivityCollector();
        Func<Result> operation = () => Result.Success();

        operation.Measure("DoWork");

        var activity = collector.Activities[0];
        activity.Tags[SuccessTag].Should().Be(true);
        activity.Tags.Should().NotContainKey(ErrorTypeTag);
        activity.Status.Should().Be(ActivityStatusCode.Unset);
    }

    /// <summary>
    /// GIVEN a failed operation
    /// WHEN it is measured
    /// THEN the activity MUST carry the error's identity and end in the error status.
    /// </summary>
    [Fact]
    public void Measure_OnFailure_ShouldTagTheErrorAndSetStatus()
    {
        using var collector = new ActivityCollector();
        Func<Result> operation = () => Result.Failure(Error.NotFound("missing", "gone"));

        operation.Measure("DoWork");

        var activity = collector.Activities[0];
        activity.Tags[SuccessTag].Should().Be(false);
        activity.Tags[ErrorTypeTag].Should().Be(nameof(ErrorType.NotFound));
        activity.Tags[ErrorCodeTag].Should().Be("missing");
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("gone");
    }

    /// <summary>
    /// GIVEN a failed generic operation whose error carries no message
    /// WHEN it is measured
    /// THEN the status description MUST fall back to the code instead of being empty.
    /// </summary>
    [Fact]
    public void MeasureGeneric_OnFailure_ShouldUseTheCodeAsStatusDescription()
    {
        using var collector = new ActivityCollector();
        Func<Result<int>> operation = () => Result<int>.Failure(Error.Conflict("duplicate"));

        operation.Measure("DoWork");

        var activity = collector.Activities[0];
        activity.Tags[ErrorTypeTag].Should().Be(nameof(ErrorType.Conflict));
        activity.StatusDescription.Should().Be("duplicate");
    }

    /// <summary>
    /// GIVEN a sampled activity source
    /// WHEN an asynchronous operation is measured
    /// THEN one activity MUST stop with the awaited operation's outcome.
    /// </summary>
    [Fact]
    public async Task MeasureAsync_ShouldStartAnActivity()
    {
        using var collector = new ActivityCollector();
        Func<Task<Result<string>>> operation = () => Task.FromResult(Result<string>.Success("ok"));

        await operation.MeasureAsync("DoWork");

        collector.Activities.Should().ContainSingle();
        collector.Activities[0].DisplayName.Should().Be("DoWork");
        collector.Activities[0].Tags[SuccessTag].Should().Be(true);
    }

    /// <summary>
    /// GIVEN an operation that throws
    /// WHEN it is measured
    /// THEN the activity MUST end in the error status carrying the exception message, and the
    /// exception MUST still reach the caller.
    /// </summary>
    [Fact]
    public void Measure_WhenTheOperationThrows_ShouldRecordFailure()
    {
        using var collector = new ActivityCollector();
        Func<Result> operation = () => throw new InvalidOperationException("boom");

        var act = () => operation.Measure("DoWork");

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");

        var activity = collector.Activities[0];
        activity.Tags[SuccessTag].Should().Be(false);
        activity.Tags[ErrorTypeTag].Should().Be(nameof(ErrorType.Unexpected));
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("boom");
    }

    /// <summary>
    /// GIVEN an asynchronous operation that throws
    /// WHEN it is measured
    /// THEN the activity MUST end in the error status, and the exception MUST still reach the
    /// caller.
    /// </summary>
    [Fact]
    public async Task MeasureAsync_WhenTheOperationThrows_ShouldRecordFailure()
    {
        using var collector = new ActivityCollector();
        Func<Task<Result>> operation = () => throw new InvalidOperationException("boom");

        var act = () => operation.MeasureAsync("DoWork");

        await act.Should().ThrowAsync<InvalidOperationException>();

        collector.Activities.Should().ContainSingle();
        collector.Activities[0].Status.Should().Be(ActivityStatusCode.Error);
        collector.Activities[0].StatusDescription.Should().Be("boom");
    }

    /// <summary>
    /// GIVEN no <see cref="ActivityListener"/> at all
    /// WHEN 1000 operations are measured
    /// THEN no activity MAY be created and the loop MUST allocate zero bytes, which is the whole
    /// point of making instrumentation opt-in.
    /// </summary>
    [Fact]
    public void WithNoListener_ShouldStartNoActivityAndAllocateNothing()
    {
        Func<Result> operation = () => Result.Success();

        operation.Measure("WarmUp");

        ResultDiagnostics.ActivitySource.HasListeners().Should().BeFalse();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++)
        {
            operation.Measure("DoWork");
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
    }
}
