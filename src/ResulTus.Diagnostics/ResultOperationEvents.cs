// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Diagnostics;

/// <summary>
/// The payload of the event published before a measured operation runs.
/// </summary>
/// <param name="OperationName">The name the caller gave the operation.</param>
public readonly record struct ResultOperationStarted(string OperationName);

/// <summary>
/// The payload of the event published after a measured operation finished, whether it returned a
/// result or threw.
/// </summary>
/// <remarks>
/// The operation name travels here rather than in the event name. A per-call event name such as
/// <c>{operation}.Stop</c> would allocate a string on every measured call even when nobody is
/// subscribed, which is exactly the cost the opt-in design exists to avoid.
/// </remarks>
/// <param name="OperationName">The name the caller gave the operation.</param>
/// <param name="Elapsed">How long the operation took.</param>
/// <param name="IsSuccess">Whether the operation produced a successful result.</param>
/// <param name="ErrorType">The error's category, or <see cref="ErrorType.None"/> on success.</param>
/// <param name="ErrorCode">The error's code, or <c>null</c> on success.</param>
public readonly record struct ResultOperationCompleted(
    string OperationName,
    TimeSpan Elapsed,
    bool IsSuccess,
    ErrorType ErrorType,
    string? ErrorCode);
