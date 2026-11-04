// Copyright (c) ResulTus. All rights reserved.

using System.Diagnostics;

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
