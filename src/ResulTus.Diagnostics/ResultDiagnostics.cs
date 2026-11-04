// Copyright (c) ResulTus. All rights reserved.

using System.Diagnostics;

namespace ResulTus.Diagnostics;

/// <summary>
/// The <see cref="ActivitySource"/> and <see cref="DiagnosticListener"/> that the diagnostics
/// extensions publish through.
/// </summary>
/// <remarks>
/// Both are static on purpose. An <see cref="ActivitySource"/> is created once per library and
/// discovered by name, and a <see cref="DiagnosticListener"/> is a single process-wide
/// broadcaster; creating either per call would mean every subscriber missed every event.
/// </remarks>
public static class ResultDiagnostics
{
    /// <summary>
    /// The name shared by the activity source and the diagnostic listener.
    /// </summary>
    public const string SourceName = "ResulTus";

    /// <summary>
    /// The name of the event published before a measured operation runs.
    /// </summary>
    public const string OperationStartedEvent = "ResultOperationStarted";

    /// <summary>
    /// The name of the event published after a measured operation finished, whether it returned
    /// or threw.
    /// </summary>
    public const string OperationCompletedEvent = "ResultOperationCompleted";

    /// <summary>
    /// Gets the source every measured operation starts an activity on.
    /// </summary>
    /// <remarks>
    /// The source carries no version. <see cref="ActivitySource"/> accepts an optional version
    /// string, but reading it from <c>AssemblyName.Version</c> needs a null check whose "absent"
    /// arm can never run for an assembly this project builds, which would leave a permanently
    /// uncovered branch in exchange for metadata no exporter routes on. Subscribers match the
    /// source by <see cref="SourceName"/>.
    /// </remarks>
    public static ActivitySource ActivitySource { get; } = new(SourceName);

    /// <summary>
    /// Gets the listener every measured operation writes its events to.
    /// </summary>
    public static DiagnosticListener Listener { get; } = new(SourceName);
}
