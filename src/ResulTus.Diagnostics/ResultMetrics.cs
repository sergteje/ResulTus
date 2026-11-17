// Copyright (c) ResulTus. All rights reserved.

using System.Diagnostics.Metrics;

namespace ResulTus.Diagnostics;

/// <summary>
/// The <see cref="Meter"/> that reports how many results the measured operations produced and how
/// long they took, together with the instruments it publishes.
/// </summary>
/// <remarks>
/// <para>
/// The meter is named <see cref="MeterName"/> and carries the assembly version, so a collector can
/// tell two releases apart. Subscribe to it with <c>AddMeter("ResulTus")</c> on an OpenTelemetry
/// meter provider, or with a <see cref="MeterListener"/> when no SDK is involved. Nothing is
/// exported unless a collector subscribes: a counter and a histogram discard their measurements
/// when no listener registered for them, which is what keeps the cost of the timing opt-in.
/// </para>
/// <para>
/// The tag set is bounded by construction. Every measurement carries <see cref="OutcomeTagName"/>,
/// whose value is exactly <see cref="SuccessOutcome"/> or <see cref="FailureOutcome"/>, and a
/// failure also carries <see cref="ErrorTypeTagName"/>, one of the <see cref="ErrorType"/> names.
/// The free-form <see cref="Error.Code"/> is deliberately absent: it is unbounded, so a metric
/// tagged with it creates one time series per distinct code in every collector that keeps a series
/// per attribute set, and that series never goes away — a memory leak that grows with traffic and
/// with the codes a caller happens to invent. Per-code detail belongs in the spans, which are not
/// aggregated.
/// </para>
/// <para>
/// The same instruments are recorded for every <c>Measure</c> and <c>MeasureAsync</c> call, so a
/// caller who already times an operation needs no second ceremony.
/// </para>
/// </remarks>
public static class ResultMetrics
{
    /// <summary>
    /// The name of the meter, and the name a collector subscribes to.
    /// </summary>
    public const string MeterName = "ResulTus";

    /// <summary>
    /// The name of the counter that counts results by outcome.
    /// </summary>
    public const string OutcomeCounterName = "resul_tus.result.outcomes";

    /// <summary>
    /// The name of the histogram that records how long a result-producing operation took.
    /// </summary>
    public const string DurationHistogramName = "resul_tus.result.duration";

    /// <summary>
    /// The tag that carries the outcome, whose value is <see cref="SuccessOutcome"/> or
    /// <see cref="FailureOutcome"/>.
    /// </summary>
    public const string OutcomeTagName = "outcome";

    /// <summary>
    /// The tag that carries the <see cref="ErrorType"/> name on a failure.
    /// </summary>
    public const string ErrorTypeTagName = "error_type";

    /// <summary>
    /// The <see cref="OutcomeTagName"/> value of a measurement that reported a success.
    /// </summary>
    public const string SuccessOutcome = "success";

    /// <summary>
    /// The <see cref="OutcomeTagName"/> value of a measurement that reported a failure.
    /// </summary>
    public const string FailureOutcome = "failure";

    private const string ResultUnit = "{result}";
    private const string SecondsUnit = "s";

    private const string CounterDescription =
        "Number of results produced by measured operations, by outcome and error type.";

    private const string HistogramDescription =
        "Duration of the operations that produced a result.";

    private static readonly string? AssemblyVersion =
        typeof(ResultMetrics).Assembly.GetName().Version?.ToString();

    private static readonly KeyValuePair<string, object?> SuccessTag =
        new(OutcomeTagName, SuccessOutcome);

    private static readonly KeyValuePair<string, object?> FailureTag =
        new(OutcomeTagName, FailureOutcome);

    private static readonly KeyValuePair<string, object?>[] ErrorTypeTags = CreateErrorTypeTags();

    /// <summary>
    /// Gets the meter every instrument below is published from.
    /// </summary>
    public static Meter Meter { get; } = new(MeterName, AssemblyVersion);

    /// <summary>
    /// Gets the counter that counts results by outcome: one increment of <c>1</c> per recorded
    /// result, tagged with <see cref="OutcomeTagName"/> and, on a failure, with
    /// <see cref="ErrorTypeTagName"/>.
    /// </summary>
    public static Counter<long> OutcomeCounter { get; } =
        Meter.CreateCounter<long>(OutcomeCounterName, ResultUnit, CounterDescription);

    /// <summary>
    /// Gets the histogram that records, in seconds, how long an operation that produced a result
    /// took.
    /// </summary>
    public static Histogram<double> DurationHistogram { get; } =
        Meter.CreateHistogram<double>(DurationHistogramName, SecondsUnit, HistogramDescription);

    /// <summary>
    /// Records one result on both instruments.
    /// </summary>
    /// <remarks>
    /// This is the entry point for a caller who already has a result and an elapsed time and wants
    /// the metrics without the <see cref="System.Diagnostics.Activity"/> and
    /// <see cref="System.Diagnostics.DiagnosticListener"/> ceremony: <c>Measure</c> calls it once
    /// per operation, and calling it directly is the same measurement. It allocates nothing: the
    /// tags are built once for the process, and the instruments discard the call when no collector
    /// is listening.
    /// </remarks>
    /// <param name="isSuccess">Whether the result was a success.</param>
    /// <param name="error">
    /// The error on a failure, read for its <see cref="Error.Type"/>; ignored on a success, where
    /// <c>default</c> is the natural value to pass. Its <see cref="Error.Code"/> is never recorded.
    /// </param>
    /// <param name="elapsed">How long the operation took; recorded on the histogram as seconds.</param>
    public static void Record(bool isSuccess, Error error, TimeSpan elapsed)
    {
        var seconds = elapsed.TotalSeconds;

        if (isSuccess)
        {
            OutcomeCounter.Add(1, SuccessTag);
            DurationHistogram.Record(seconds, SuccessTag);
            return;
        }

        // ErrorType is a contiguous byte enum and Error has no public constructor, so an Error's
        // type is always a name this table already holds.
        var errorTypeTag = ErrorTypeTags[(byte)error.Type];
        OutcomeCounter.Add(1, FailureTag, errorTypeTag);
        DurationHistogram.Record(seconds, FailureTag, errorTypeTag);
    }

    /// <summary>
    /// Builds one tag per <see cref="ErrorType"/> once for the process, so a measurement never
    /// formats an enum, never builds a collection and never allocates. The table is sized from the
    /// highest member rather than from the member count, so a later gap in the enum stays a hole
    /// instead of an out-of-range read.
    /// </summary>
    /// <returns>One tag per defined <see cref="ErrorType"/>, indexed by its value.</returns>
    private static KeyValuePair<string, object?>[] CreateErrorTypeTags()
    {
        var values = Enum.GetValues<ErrorType>();

        var highest = 0;
        foreach (var value in values)
        {
            highest = Math.Max(highest, (int)value);
        }

        var tags = new KeyValuePair<string, object?>[highest + 1];
        foreach (var value in values)
        {
            tags[(byte)value] = new KeyValuePair<string, object?>(ErrorTypeTagName, value.ToString());
        }

        return tags;
    }
}
