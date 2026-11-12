# ResulTus.Diagnostics — opt-in timing and tracing for result operations

[![NuGet](https://img.shields.io/nuget/v/ResulTus.Diagnostics?label=nuget)](https://www.nuget.org/packages/ResulTus.Diagnostics)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.Diagnostics?label=downloads)](https://www.nuget.org/packages/ResulTus.Diagnostics)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package instruments an operation that produces a
[ResulTus](https://github.com/sergteje/ResulTus) `Result`: it runs the operation once, times it,
publishes a `DiagnosticListener` start/complete pair and starts an `Activity` span. Both publish
through sources named `ResulTus`. Nothing is instrumented unless the caller writes `Measure`, which
is what makes the cost opt-in rather than ambient.

## Install

```bash
dotnet add package ResulTus.Diagnostics
```

## Dependencies

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
  types the measured operations return. It is the only dependency; no tracing or logging framework
  is pulled in.

## What you get

Four extension methods on `ResultDiagnosticsExtensions`, one per operation shape:

| Operation | Method | Returns |
| --- | --- | --- |
| `Func<Result>` | `Measure` | `TimedUnitResult` |
| `Func<Result<T>>` | `Measure<T>` | `TimedResult<T>` |
| `Func<Task<Result>>` | `MeasureAsync` | `Task<TimedUnitResult>` |
| `Func<Task<Result<T>>>` | `MeasureAsync<T>` | `Task<TimedResult<T>>` |

`TimedUnitResult(Result Result, TimeSpan Elapsed)` and `TimedResult<T>(Result<T> Result, TimeSpan
Elapsed)` are `readonly record struct`s, so measuring adds no heap wrapper on top of the operation.
Each overload runs or awaits the operation exactly once and rethrows whatever it throws.

```csharp
using System;
using ResulTus;
using ResulTus.Diagnostics;

Func<Result<int>> fetch = () => Result<int>.Success(42);
TimedResult<int> timed = fetch.Measure("fetch");

// timed.Result.IsSuccess == true
// timed.Elapsed >= TimeSpan.Zero
```

Unit and asynchronous operations use the same shape:

```csharp
using System;
using System.Threading.Tasks;
using ResulTus;
using ResulTus.Diagnostics;

Func<Result> save = () => Result.Success();
TimedUnitResult saved = save.Measure("save");

Func<Task<Result<int>>> load = () => Task.FromResult(Result<int>.Success(7));
TimedResult<int> loaded = await load.MeasureAsync("load");
```

The operation name is not validated beyond being non-`null`; a `null` operation or name throws
`ArgumentNullException` before anything is published.

## Events and spans

Both publishers live on the static `ResultDiagnostics` class — `Listener` and `ActivitySource` —
alongside the name constants `SourceName` (`"ResulTus"`), `OperationStartedEvent` and
`OperationCompletedEvent`. One measured call publishes:

| Event | Payload |
| --- | --- |
| `ResultOperationStarted` | `ResultOperationStarted(string OperationName)` |
| `ResultOperationCompleted` | `ResultOperationCompleted(string OperationName, TimeSpan Elapsed, bool IsSuccess, ErrorType ErrorType, string? ErrorCode)` |

The operation name travels in the payload, not in the event name, so a measured call does not build
a per-call event name string. On success the completed payload carries `ErrorType.None` and a
`null` error code.

The same call starts an `Activity` of kind `Internal` named after the operation. It always sets the
`resul_tus.success` tag; on failure it also sets `resul_tus.error_type` and `resul_tus.error_code`
and an error status carrying the error message, or the code when there is no message. Opt in to
spans by registering an `ActivityListener` that matches `ResultDiagnostics.SourceName`:

```csharp
using System;
using System.Diagnostics;
using ResulTus;
using ResulTus.Diagnostics;

using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == ResultDiagnostics.SourceName,
    Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStarted = activity => Console.WriteLine($"started {activity.OperationName}"),
    ActivityStopped = activity => Console.WriteLine($"stopped {activity.OperationName}"),
};
ActivitySource.AddActivityListener(listener);

Func<Result<int>> operation = () => Result<int>.Success(7);
operation.Measure("fetch-products");
```

Nothing is written when nobody listens: the events are published only when the listener is enabled
for that event, and the activity source returns `null` when no listener samples it.

### Exceptions

An operation that throws is recorded as an `ErrorType.Unexpected` failure with the code `exception`
and the exception message, the completed event is published, and the exception is rethrown. A span
that reported success because the exception escaped the wrapper would be worse than no span at all.

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — required
  dependency: the core result types.
- [`ResulTus.Logging`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Logging/README.md)
  — the other observability package, writing outcomes as structured `ILogger` fields.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [observability](https://github.com/sergteje/ResulTus/blob/main/docs/observability.md),
  [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
