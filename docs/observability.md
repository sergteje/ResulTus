# Observability

Two optional packages cover the "what happened" question without pulling a logging or tracing framework into the
core. `ResulTus.Logging` writes a result through the standard `ILogger` as **named fields, not a rendered string**.
`ResulTus.Diagnostics` instruments a result-producing operation with opt-in timing, `Activity` spans,
`DiagnosticListener` events and `System.Diagnostics.Metrics` instruments. Neither does anything unless the caller
asks it to.

## Structured logging — `ResulTus.Logging`

The extension methods live in the `ResulTus.Logging` namespace and apply to `Result` and `Result<T>`. The
entry handed to the logger is an `IReadOnlyList<KeyValuePair<string, object?>>`, the shape every
`Microsoft.Extensions.Logging` provider understands — so a provider reads `ErrorCode` as a named field instead of
parsing it back out of text.

```csharp
using Microsoft.Extensions.Logging;
using ResulTus;
using ResulTus.Logging;
```

The examples use this minimal capturing logger, which keeps the last structured state instead of formatting a
message:

```csharp
public sealed class CapturingLogger : ILogger
{
    public IReadOnlyList<KeyValuePair<string, object?>>? LastState { get; private set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        LastState = state as IReadOnlyList<KeyValuePair<string, object?>>;
    }
}
```

```csharp
var logger = new CapturingLogger();
Result<int> failed = Result<int>.Failure(Error.NotFound("USER_NOT_FOUND", "No user"));

Result<int> same = failed.LogFailure(logger, "User lookup");

foreach (var field in logger.LastState!)
{
    Console.WriteLine($"{field.Key} = {field.Value}");
}
```

This writes:

```
ResultType = Int32
IsSuccess = False
ErrorType = NotFound
ErrorCode = USER_NOT_FOUND
ErrorMessage = No user
```

The structured fields are fixed:

| Field | Present when |
| --- | --- |
| `ResultType` | Always — `"Result"` for the unit tier, the success type's name (`Int32`, …) for the generic tier |
| `IsSuccess` | Always |
| `ErrorType` | Failure only |
| `ErrorCode` | Failure only |
| `ErrorMessage` | Failure only, and omitted entirely when the error carries no message |

Omission is a signal: a provider can tell "no message" apart from "an empty message". `ResultType` and
`IsSuccess` always precede the failure fields.

### The extension surface

Both tiers expose the same verb set, and each returns the result it was given so a log call composes inside a
chain without changing the chain's value:

| Method | Level | Writes when |
| --- | --- | --- |
| `LogSuccess` | `Debug` | success only |
| `LogFailure` | `Error` | failure only |
| `LogError` | `Error` | failure only (alias of `LogFailure`) |
| `LogWarning` | `Warning` | failure only |
| `LogInformation` | `Information` | failure only |
| `LogAll` | `Debug` / `Error` | always, once |
| `LogAll(levelSelector)` | from the selector | always, once |

Three rules follow from the source. A method that does not describe the actual outcome writes nothing:
`LogSuccess` on a failure is a no-op, and so is `LogFailure` on a success — so nothing is written twice. Every
method checks `IsEnabled` before building anything, so a disabled level — the default `Debug` on success —
allocates nothing. The optional `message` argument replaces the default rendered text for plain-text sinks and
does not name the operation; the structured fields are unaffected by it. The `LogAll` selector overload maps the
error to a level and is never invoked on a success, where there is no error to classify.

```csharp
Result<int> maybe = Result<int>.Success(42);

maybe.LogSuccess(logger, "Fetched");        // writes at Debug
maybe.LogFailure(logger, "Fetched");        // no-op: nothing to fail

Result<int> chosen = maybe.LogAll(
    logger,
    error => error.Type == ErrorType.NotFound ? LogLevel.Warning : LogLevel.Error);
// success -> Debug; failure -> Warning for NotFound, Error otherwise
```

A `Task<Result>` or `Task<Result<T>>` source has the same verbs as `...Async` (`LogSuccessAsync`, `LogFailureAsync`,
`LogAllAsync`, …), which await the task exactly once, log through the synchronous overload, and return the awaited
value.

## Diagnostics — `ResulTus.Diagnostics`

`ResulTus.Diagnostics` publishes through one `ActivitySource` and one `DiagnosticListener`, both named
`ResulTus`, exposed as static members of `ResultDiagnostics`. Timing and tracing are **opt-in**: nothing is
instrumented unless the caller writes `Measure`, which is what makes the cost explicit rather than ambient.

```csharp
using System.Diagnostics;
using ResulTus;
using ResulTus.Diagnostics;
```

```csharp
Func<Result<int>> fetch = () => Result<int>.Success(42);
TimedResult<int> timed = fetch.Measure("fetch");
// timed.Result.IsSuccess == true, timed.Elapsed >= TimeSpan.Zero
```

`Measure` runs the operation exactly once and returns a value-type wrapper — `TimedUnitResult` for a
`Func<Result>`, `TimedResult<T>` for a `Func<Result<T>>` — so measuring does not allocate a wrapper on top of the
thing it measures. `MeasureAsync` does the same for `Func<Task<Result>>` and `Func<Task<Result<T>>>`.

A measured call allocates nothing when nobody is listening: the two `DiagnosticListener` events are written only
when the listener is enabled, and the activity source returns `null` when no listener samples it. The elapsed time
is read from `Stopwatch` without allocating.

### Events and spans

Two `DiagnosticListener` events bracket every measured call, and their payloads are public record structs:

| Event | Payload |
| --- | --- |
| `"ResultOperationStarted"` | `ResultOperationStarted(string OperationName)` |
| `"ResultOperationCompleted"` | `ResultOperationCompleted(string OperationName, TimeSpan Elapsed, bool IsSuccess, ErrorType ErrorType, string? ErrorCode)` |

The operation name travels in the payload, not in the event name — a per-call event name such as
`{operation}.Stop` would allocate a string on every measured call even when nobody is subscribed, which is exactly
the cost the opt-in design exists to avoid. On completion, `ErrorType` is `ErrorType.None` and `ErrorCode` is
`null` on a success.

The same call starts an `Activity` of kind `Internal`. On a failure it sets three tags — `resul_tus.success`
(`false`), `resul_tus.error_type`, and `resul_tus.error_code` — and an error status carrying the message or the
code. Opt in to sampling by registering an `ActivityListener` that matches `ResultDiagnostics.SourceName`:

```csharp
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == ResultDiagnostics.SourceName,
    Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStarted = activity => Console.WriteLine($"started {activity.OperationName}"),
    ActivityStopped = activity => Console.WriteLine($"stopped {activity.OperationName}"),
};
ActivitySource.AddActivityListener(listener);

Func<Result<int>> measured = () => Result<int>.Success(7);
TimedResult<int> observed = measured.Measure("observed-fetch");
// "started observed-fetch" then "stopped observed-fetch"
```

### Exceptions

An operation that throws is recorded as an `ErrorType.Unexpected` failure carrying the exception message, and the
exception is rethrown. A span that reported success because the exception escaped the wrapper would be worse than
no span at all, so the completed event and the activity status both report the failure before it propagates.

### Metrics

Every measured call also reports through a `System.Diagnostics.Metrics.Meter` named `ResulTus`, versioned after the
assembly and exposed as `ResultMetrics.Meter`. The instruments ship with the framework, so nothing extra is
referenced.

| Instrument | Type | Unit | Tags |
| --- | --- | --- | --- |
| `resul_tus.result.outcomes` | `Counter<long>` | `{result}` | `outcome`, plus `error_type` on a failure |
| `resul_tus.result.duration` | `Histogram<double>` | `s` | `outcome`, plus `error_type` on a failure |

`outcome` is exactly `success` or `failure`, and `error_type` is the `ErrorType` name. Durations are recorded in
seconds, the unit OpenTelemetry expects a duration histogram to use.

**The tag set is bounded by construction.** Those two tags are the whole set, and both take their values from a
closed list: two outcomes and the ten `ErrorType` names. The free-form `Error.Code` is deliberately not a tag,
because it is chosen by the caller and a metric tagged with it creates one series per distinct code in every
collector that keeps a series per attribute set — series that are never removed, so a handful of counters becomes
an unbounded, permanent memory cost. Per-code detail stays in the spans, which are sampled rather than aggregated.

Add the meter to an OpenTelemetry meter provider by name:

```text
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter("ResulTus"));
```

A `MeterListener` reads the same measurements when no SDK is involved:

```csharp
using System;
using System.Diagnostics.Metrics;
using ResulTus;
using ResulTus.Diagnostics;

using var metricsListener = new MeterListener
{
    InstrumentPublished = (instrument, published) =>
    {
        if (instrument.Meter.Name == ResultMetrics.MeterName)
        {
            published.EnableMeasurementEvents(instrument);
        }
    },
};

metricsListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
    Console.WriteLine($"counter {instrument.Name}: +{value} over {tags.Length} tag(s)"));

metricsListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
    Console.WriteLine($"histogram {instrument.Name}: {value}"));

metricsListener.Start();

Func<Result<int>> gauged = () => Result<int>.Failure(Error.NotFound("USER_404", "No user"));
gauged.Measure("gauged-fetch");
```

That call reports `counter resul_tus.result.outcomes: +1 over 2 tag(s)` — the tags are `outcome=failure` and
`error_type=NotFound`, never the code — followed by `histogram resul_tus.result.duration: <elapsed seconds>`.

An instrument discards its measurements when no listener registered for it, so with no subscriber the metrics cost a
listener check and no allocation, and the tag lists are built once for the process rather than once per measurement.

`Measure` and `MeasureAsync` record both instruments for you. When the result already exists and only the timing is
yours, `ResultMetrics.Record(bool isSuccess, Error error, TimeSpan elapsed)` makes the same measurement without an
`Activity` or an event:

```csharp
using System;
using System.Diagnostics;
using ResulTus;
using ResulTus.Diagnostics;

var startedAt = Stopwatch.GetTimestamp();
Result<int> reloaded = Result<int>.Success(7);
var elapsed = Stopwatch.GetElapsedTime(startedAt);

ResultMetrics.Record(reloaded.IsSuccess, default, elapsed);
```

Pass the error on a failure and `default` on a success, where there is no error to classify.
