// Copyright (c) ResulTus. All rights reserved.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ResulTus.Diagnostics.Tests;

/// <summary>
/// Serializes the diagnostics tests. The <see cref="ActivitySource"/> and
/// <see cref="DiagnosticListener"/> under test are process-wide statics, so two test classes
/// attaching listeners at the same time would see each other's activities and events.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DiagnosticsCollection
{
    /// <summary>
    /// The collection name every diagnostics test class joins.
    /// </summary>
    public const string Name = "ResulTus.Diagnostics";
}

/// <summary>
/// A <see cref="DiagnosticListener"/> subscriber that records every event it receives.
/// </summary>
public sealed class EventObserver : IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly IDisposable _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventObserver"/> class and subscribes it to
    /// the library's listener.
    /// </summary>
    public EventObserver()
    {
        _subscription = ResultDiagnostics.Listener.Subscribe(this);
    }

    /// <summary>
    /// Gets the events received so far, in order.
    /// </summary>
    public List<KeyValuePair<string, object?>> Events { get; } = [];

    /// <inheritdoc />
    public void OnNext(KeyValuePair<string, object?> value) => Events.Add(value);

    /// <inheritdoc />
    public void OnError(Exception error)
    {
    }

    /// <inheritdoc />
    public void OnCompleted()
    {
    }

    /// <inheritdoc />
    public void Dispose() => _subscription.Dispose();
}

/// <summary>
/// One stopped activity, copied out of the pooled instance so assertions read stable values.
/// </summary>
/// <param name="DisplayName">The activity's display name.</param>
/// <param name="Kind">The activity's kind.</param>
/// <param name="ParentName">The display name of the activity's parent, when it had one.</param>
/// <param name="ParentId">The id of the activity's parent, when it had one.</param>
/// <param name="Status">The status the activity ended with.</param>
/// <param name="StatusDescription">The status description, when one was set.</param>
/// <param name="Tags">The tags the activity carried.</param>
public sealed record ActivitySnapshot(
    string DisplayName,
    ActivityKind Kind,
    string? ParentName,
    string? ParentId,
    ActivityStatusCode Status,
    string? StatusDescription,
    IReadOnlyDictionary<string, object?> Tags);

/// <summary>
/// An <see cref="ActivityListener"/> that records every stopped activity from the library's
/// source.
/// </summary>
public sealed class ActivityCollector : IDisposable
{
    private readonly ActivityListener _listener;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityCollector"/> class and registers it.
    /// </summary>
    public ActivityCollector()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ResultDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                var tags = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var tag in activity.TagObjects)
                {
                    tags[tag.Key] = tag.Value;
                }

                Activities.Add(new ActivitySnapshot(
                    activity.DisplayName,
                    activity.Kind,
                    activity.Parent?.DisplayName,
                    activity.ParentId,
                    activity.Status,
                    activity.StatusDescription,
                    tags));
            },
        };

        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>
    /// Gets the activities that stopped so far, in order.
    /// </summary>
    public List<ActivitySnapshot> Activities { get; } = [];

    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();
}

/// <summary>
/// One published instrument, copied out at the moment the listener enabled it.
/// </summary>
/// <param name="Name">The instrument's name.</param>
/// <param name="Unit">The unit the instrument reports in, when it declares one.</param>
/// <param name="Description">The instrument's description, when it declares one.</param>
public sealed record InstrumentSnapshot(string Name, string? Unit, string? Description);

/// <summary>
/// One measurement, copied out of the listener's span so assertions read stable values and can
/// outlive the callback.
/// </summary>
/// <param name="InstrumentName">The name of the instrument that reported it.</param>
/// <param name="Unit">The reporting instrument's unit, when it declares one.</param>
/// <param name="Value">The measured value: the counter's delta or the histogram's sample.</param>
/// <param name="Tags">The tags the measurement carried.</param>
public sealed record MeasurementSnapshot(
    string InstrumentName,
    string? Unit,
    object? Value,
    IReadOnlyDictionary<string, object?> Tags);

/// <summary>
/// A <see cref="MeterListener"/> that records every instrument and measurement published by the
/// library's meter. It is the only external observer the metrics tests use, so what they assert is
/// what a real collector receives rather than what the library believes it sent.
/// </summary>
/// <remarks>
/// A disposed collector stops recording: the .NET 8 runtime keeps delivering measurements to a
/// disposed listener, and a double that kept collecting them would allocate inside whatever test
/// ran next.
/// </remarks>
public sealed class MeterCollector : IDisposable
{
    private readonly MeterListener _listener;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeterCollector"/> class and registers it.
    /// </summary>
    public MeterCollector()
    {
        // The meters and their instruments are static, so they may not exist yet. Touching one
        // before the listener starts means Start() discovers both, whichever test ran first.
        _ = ResultMetrics.OutcomeCounter;

        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, published) =>
            {
                if (!ReferenceEquals(instrument.Meter, ResultMetrics.Meter))
                {
                    return;
                }

                Instruments.Add(new InstrumentSnapshot(instrument.Name, instrument.Unit, instrument.Description));
                published.EnableMeasurementEvents(instrument);
            },
        };

        _listener.SetMeasurementEventCallback<long>(OnMeasurement);
        _listener.SetMeasurementEventCallback<double>(OnMeasurement);
        _listener.Start();
    }

    /// <summary>
    /// Gets the instruments published by the library's meter, in publication order.
    /// </summary>
    public List<InstrumentSnapshot> Instruments { get; } = [];

    /// <summary>
    /// Gets every measurement received so far, in the order the library reported it.
    /// </summary>
    public List<MeasurementSnapshot> Measurements { get; } = [];

    /// <summary>
    /// Gets the measurements one instrument reported.
    /// </summary>
    /// <param name="instrumentName">The instrument's name.</param>
    /// <returns>The matching measurements, in order.</returns>
    public IReadOnlyList<MeasurementSnapshot> For(string instrumentName)
        => Measurements.Where(measurement => measurement.InstrumentName == instrumentName).ToArray();

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _listener.Dispose();
    }

    private void OnMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        if (_disposed)
        {
            return;
        }

        var copied = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            copied[tag.Key] = tag.Value;
        }

        Measurements.Add(new MeasurementSnapshot(
            instrument.Name,
            instrument.Unit,
            measurement,
            copied));
    }
}
