// Copyright (c) ResulTus. All rights reserved.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ResulTus.Diagnostics;

/// <summary>
/// Opt-in timing, <see cref="System.Diagnostics.DiagnosticListener"/> events and
/// <see cref="Activity"/> tracing around an operation that produces a <see cref="Result"/>.
/// </summary>
/// <remarks>
/// <para>
/// An operation is already a delegate, so these overloads instrument an extension call, a
/// collection fold or a validation run without this package depending on any of those libraries.
/// Nothing is instrumented unless the caller writes <c>Measure</c>, which is what makes the cost
/// opt-in rather than ambient.
/// </para>
/// <para>
/// A measured call allocates nothing when nobody is listening: the events are written only when
/// the listener is enabled, and the activity source returns <c>null</c> when no listener samples
/// it. The elapsed time is read from <see cref="Stopwatch"/> without allocating.
/// </para>
/// <para>
/// An operation that throws is recorded as an <see cref="ErrorType.Unexpected"/> failure carrying
/// the exception message, and the exception is rethrown. A span that reported success because the
/// exception escaped the wrapper would be worse than no span at all.
/// </para>
/// </remarks>
public static class ResultDiagnosticsExtensions
{
    private const string SuccessTag = "resul_tus.success";
    private const string ErrorTypeTag = "resul_tus.error_type";
    private const string ErrorCodeTag = "resul_tus.error_code";
    private const string ExceptionCode = "exception";

    /// <summary>
    /// Runs a unit-result operation, times it and publishes its start, its outcome and an
    /// <see cref="Activity"/>.
    /// </summary>
    /// <param name="operation">The operation to run exactly once.</param>
    /// <param name="operationName">
    /// The name given to the activity and reported in both events. It is not validated beyond
    /// being non-<c>null</c>.
    /// </param>
    /// <returns>The result the operation returned, together with the elapsed time.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation"/> or <paramref name="operationName"/> is <c>null</c>.
    /// </exception>
    public static TimedUnitResult Measure(this Func<Result> operation, string operationName)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(operationName);

        WriteStarted(operationName);
        using var activity = StartActivity(operationName);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var result = operation();
            var elapsed = Stopwatch.GetElapsedTime(startedAt);

            result.TryGetError(out var error);
            RecordOutcome(activity, result.IsSuccess, error, elapsed, operationName);

            return new TimedUnitResult(result, elapsed);
        }
        catch (Exception exception)
        {
            RecordException(activity, exception, startedAt, operationName);
            throw;
        }
    }

    /// <summary>
    /// Runs a generic-result operation, times it and publishes its start, its outcome and an
    /// <see cref="Activity"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="operation">The operation to run exactly once.</param>
    /// <param name="operationName">The name given to the activity and reported in both events.</param>
    /// <returns>The result the operation returned, together with the elapsed time.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation"/> or <paramref name="operationName"/> is <c>null</c>.
    /// </exception>
    public static TimedResult<T> Measure<T>(this Func<Result<T>> operation, string operationName)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(operationName);

        WriteStarted(operationName);
        using var activity = StartActivity(operationName);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var result = operation();
            var elapsed = Stopwatch.GetElapsedTime(startedAt);

            result.TryGetValue(out _, out var error);
            RecordOutcome(activity, result.IsSuccess, error, elapsed, operationName);

            return new TimedResult<T>(result, elapsed);
        }
        catch (Exception exception)
        {
            RecordException(activity, exception, startedAt, operationName);
            throw;
        }
    }

    /// <summary>
    /// Awaits a unit-result operation, times it and publishes its start, its outcome and an
    /// <see cref="Activity"/>.
    /// </summary>
    /// <param name="operation">The operation to await exactly once.</param>
    /// <param name="operationName">The name given to the activity and reported in both events.</param>
    /// <returns>The awaited result, together with the elapsed time.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation"/> or <paramref name="operationName"/> is <c>null</c>.
    /// </exception>
    public static async Task<TimedUnitResult> MeasureAsync(
        this Func<Task<Result>> operation,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(operationName);

        WriteStarted(operationName);
        using var activity = StartActivity(operationName);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var result = await operation().ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(startedAt);

            result.TryGetError(out var error);
            RecordOutcome(activity, result.IsSuccess, error, elapsed, operationName);

            return new TimedUnitResult(result, elapsed);
        }
        catch (Exception exception)
        {
            RecordException(activity, exception, startedAt, operationName);
            throw;
        }
    }

    /// <summary>
    /// Awaits a generic-result operation, times it and publishes its start, its outcome and an
    /// <see cref="Activity"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="operation">The operation to await exactly once.</param>
    /// <param name="operationName">The name given to the activity and reported in both events.</param>
    /// <returns>The awaited result, together with the elapsed time.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation"/> or <paramref name="operationName"/> is <c>null</c>.
    /// </exception>
    public static async Task<TimedResult<T>> MeasureAsync<T>(
        this Func<Task<Result<T>>> operation,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(operationName);

        WriteStarted(operationName);
        using var activity = StartActivity(operationName);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var result = await operation().ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(startedAt);

            result.TryGetValue(out _, out var error);
            RecordOutcome(activity, result.IsSuccess, error, elapsed, operationName);

            return new TimedResult<T>(result, elapsed);
        }
        catch (Exception exception)
        {
            RecordException(activity, exception, startedAt, operationName);
            throw;
        }
    }

    private static Activity? StartActivity(string operationName)
        => ResultDiagnostics.ActivitySource.StartActivity(operationName, ActivityKind.Internal);

    // The DiagnosticSource contract is reflection-based by design: DiagnosticSourceEventSource
    // discovers the payload's properties at runtime, which is why both Write overloads are
    // annotated. The payload written here is this assembly's own public ResultOperationStarted, so
    // the shape is known statically and suppressing at this one call site is precise. Annotating
    // Measure instead would force the warning on every caller, including the ones that never
    // subscribe and only use the trim-safe Activity path.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The payload type is declared in this assembly and is part of its public API.")]
    private static void WriteStarted(string operationName)
    {
        if (ResultDiagnostics.Listener.IsEnabled(ResultDiagnostics.OperationStartedEvent))
        {
            ResultDiagnostics.Listener.Write(
                ResultDiagnostics.OperationStartedEvent,
                new ResultOperationStarted(operationName));
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The payload type is declared in this assembly and is part of its public API.")]
    private static void RecordOutcome(
        Activity? activity,
        bool isSuccess,
        Error error,
        TimeSpan elapsed,
        string operationName)
    {
        if (activity is not null)
        {
            activity.SetTag(SuccessTag, isSuccess);

            if (!isSuccess)
            {
                activity.SetTag(ErrorTypeTag, error.Type.ToString());
                activity.SetTag(ErrorCodeTag, error.Code);
                activity.SetStatus(ActivityStatusCode.Error, error.Message ?? error.Code);
            }
        }

        if (ResultDiagnostics.Listener.IsEnabled(ResultDiagnostics.OperationCompletedEvent))
        {
            ResultDiagnostics.Listener.Write(
                ResultDiagnostics.OperationCompletedEvent,
                new ResultOperationCompleted(
                    operationName,
                    elapsed,
                    isSuccess,
                    isSuccess ? ErrorType.None : error.Type,
                    isSuccess ? null : error.Code));
        }
    }

    private static void RecordException(
        Activity? activity,
        Exception exception,
        long startedAt,
        string operationName)
        => RecordOutcome(
            activity,
            false,
            Error.Unexpected(ExceptionCode, exception.Message),
            Stopwatch.GetElapsedTime(startedAt),
            operationName);
}
