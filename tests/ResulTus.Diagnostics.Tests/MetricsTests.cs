// Copyright (c) ResulTus. All rights reserved.

using System.Diagnostics.Metrics;

namespace ResulTus.Diagnostics.Tests;

/// <summary>
/// Tests for the meters a measured operation reports: the outcome counter and the duration
/// histogram, as a <see cref="MeterListener"/> observes them.
/// </summary>
[Collection(DiagnosticsCollection.Name)]
public class MetricsTests
{
    private const string OutcomeCounter = "resul_tus.result.outcomes";
    private const string DurationHistogram = "resul_tus.result.duration";
    private const string OutcomeTag = "outcome";
    private const string ErrorTypeTag = "error_type";

    /// <summary>
    /// GIVEN a publishing meter
    /// WHEN a successful operation is measured
    /// THEN the outcome counter MUST report one measurement tagged <c>outcome=success</c> and MUST
    /// NOT carry an <c>error_type</c> tag, because a success has no error to classify.
    /// </summary>
    [Fact]
    public void Measure_OnSuccess_ShouldCountOneSuccessWithoutAnErrorType()
    {
        using var collector = new MeterCollector();
        Func<Result> operation = () => Result.Success();

        operation.Measure("DoWork");

        var counted = collector.For(OutcomeCounter).Should().ContainSingle().Subject;
        counted.Value.Should().Be(1L);
        counted.Tags.Should().HaveCount(1);
        counted.Tags[OutcomeTag].Should().Be("success");
        counted.Tags.Should().NotContainKey(ErrorTypeTag);
    }

    /// <summary>
    /// GIVEN a publishing meter
    /// WHEN a failed operation is measured
    /// THEN the outcome counter MUST report one measurement tagged <c>outcome=failure</c> carrying
    /// the <see cref="ErrorType"/> name and nothing else, so the tag set stays bounded.
    /// </summary>
    [Fact]
    public void Measure_OnFailure_ShouldCountOneFailureWithTheErrorType()
    {
        using var collector = new MeterCollector();
        Func<Result> operation = () => Result.Failure(Error.NotFound("missing", "gone"));

        operation.Measure("DoWork");

        var counted = collector.For(OutcomeCounter).Should().ContainSingle().Subject;
        counted.Value.Should().Be(1L);
        counted.Tags[OutcomeTag].Should().Be("failure");
        counted.Tags[ErrorTypeTag].Should().Be(nameof(ErrorType.NotFound));
        counted.Tags.Should().HaveCount(2);
    }

    /// <summary>
    /// GIVEN a publishing meter
    /// WHEN two operations are measured
    /// THEN the counter MUST receive two measurements that add up to two, so a reading accumulates
    /// across operations instead of being overwritten by the last one.
    /// </summary>
    [Fact]
    public void Measure_Twice_ShouldAccumulateTwoMeasurements()
    {
        using var collector = new MeterCollector();
        Func<Result> operation = () => Result.Success();

        operation.Measure("First");
        operation.Measure("Second");

        var counted = collector.For(OutcomeCounter);
        counted.Select(measurement => measurement.Value).Should().Equal(1L, 1L);
        counted.Sum(measurement => (long)measurement.Value!).Should().Be(2L);
    }

    /// <summary>
    /// GIVEN a publishing meter
    /// WHEN one operation is measured
    /// THEN the histogram MUST record exactly one sample, in seconds, equal to the elapsed time the
    /// caller received back.
    /// </summary>
    [Fact]
    public void Measure_ShouldRecordOneDurationInSeconds()
    {
        using var collector = new MeterCollector();
        Func<Result<int>> operation = () => Result<int>.Success(42);

        var timed = operation.Measure("Fetch");

        var recorded = collector.For(DurationHistogram).Should().ContainSingle().Subject;
        recorded.Unit.Should().Be("s");
        recorded.Value.Should().Be(timed.Elapsed.TotalSeconds);
        recorded.Tags[OutcomeTag].Should().Be("success");
    }

    /// <summary>
    /// GIVEN a failure whose <see cref="Error.Code"/> is distinctive
    /// WHEN it is measured
    /// THEN no measurement MAY carry that code in a tag key or value: the code is free-form, so a
    /// tag built from it would grow without bound in every collector that keeps a series per value.
    /// </summary>
    [Fact]
    public void Measure_OnFailure_ShouldCarryNoUnboundedErrorCode()
    {
        const string Code = "USER_12345_NOT_FOUND";
        using var collector = new MeterCollector();
        Func<Result> operation = () => Result.Failure(Error.NotFound(Code, "gone"));

        operation.Measure("DoWork");

        collector.Measurements.Should().NotBeEmpty();
        foreach (var measurement in collector.Measurements)
        {
            measurement.Tags.Should().NotContainKey("resul_tus.error_code");
            measurement.Tags.Keys.Should().OnlyContain(key => key == OutcomeTag || key == ErrorTypeTag);
            measurement.Tags.Values.OfType<string>().Should()
                .NotContain(text => text.Contains(Code, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// GIVEN an operation that throws
    /// WHEN it is measured
    /// THEN the counter MUST report it as a failure of type <see cref="ErrorType.Unexpected"/>, the
    /// same classification the span and the completed event already publish.
    /// </summary>
    [Fact]
    public void Measure_WhenTheOperationThrows_ShouldCountAnUnexpectedFailure()
    {
        using var collector = new MeterCollector();
        Func<Result> operation = () => throw new InvalidOperationException("boom");

        var act = () => operation.Measure("DoWork");

        act.Should().Throw<InvalidOperationException>();

        var counted = collector.For(OutcomeCounter).Should().ContainSingle().Subject;
        counted.Tags[OutcomeTag].Should().Be("failure");
        counted.Tags[ErrorTypeTag].Should().Be(nameof(ErrorType.Unexpected));
        collector.For(DurationHistogram).Should().ContainSingle();
    }

    /// <summary>
    /// GIVEN no trace listener at all
    /// WHEN a caller records a result through <see cref="ResultMetrics.Record"/>
    /// THEN both instruments MUST report it, which is what makes metrics usable without the span
    /// and event ceremony.
    /// </summary>
    [Fact]
    public void Record_WithoutAnyTraceListener_ShouldMeasureTheResult()
    {
        using var collector = new MeterCollector();

        ResultDiagnostics.ActivitySource.HasListeners().Should().BeFalse();

        ResultMetrics.Record(true, default, TimeSpan.FromSeconds(2));

        collector.For(OutcomeCounter).Should().ContainSingle()
            .Which.Tags[OutcomeTag].Should().Be("success");
        collector.For(DurationHistogram).Should().ContainSingle()
            .Which.Value.Should().Be(2.0);
    }

    /// <summary>
    /// GIVEN no measurement yet
    /// WHEN the listener attaches to the library's meter
    /// THEN the meter MUST be named <c>ResulTus</c> and versioned after the assembly, and both
    /// instruments MUST publish the documented names, types and units.
    /// </summary>
    [Fact]
    public void Instruments_ShouldBePublishedOnTheVersionedMeter()
    {
        using var collector = new MeterCollector();

        ResultMetrics.Meter.Name.Should().Be(ResultMetrics.MeterName);
        ResultMetrics.Meter.Name.Should().Be("ResulTus");
        ResultMetrics.Meter.Version.Should().Be(
            typeof(ResultMetrics).Assembly.GetName().Version!.ToString());

        ResultMetrics.OutcomeCounter.Should().BeOfType<Counter<long>>();
        ResultMetrics.DurationHistogram.Should().BeOfType<Histogram<double>>();

        var counter = collector.Instruments.Should().ContainSingle(i => i.Name == OutcomeCounter).Subject;
        counter.Unit.Should().Be("{result}");
        counter.Description.Should().NotBeNullOrWhiteSpace();

        var histogram = collector.Instruments.Should().ContainSingle(i => i.Name == DurationHistogram).Subject;
        histogram.Unit.Should().Be("s");
        histogram.Description.Should().NotBeNullOrWhiteSpace();
    }
}
