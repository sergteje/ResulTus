# ResulTus.Logging — structured logging for results

[![NuGet](https://img.shields.io/nuget/v/ResulTus.Logging?label=nuget)](https://www.nuget.org/packages/ResulTus.Logging)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.Logging?label=downloads)](https://www.nuget.org/packages/ResulTus.Logging)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package writes [ResulTus](https://github.com/sergteje/ResulTus) outcomes through the standard
`ILogger` as **named fields, not a rendered string**, so a provider reads `ErrorCode` instead of
parsing it back out of text. It extends `Result` and `Result<T>` (and their task-returning forms)
from the `ResulTus.Logging` namespace. Every method returns the result it was given, so a log call
composes inside a chain without changing the chain's value.

## Install

```bash
dotnet add package ResulTus.Logging
```

## Dependencies

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
  types these extensions log.
- [`Microsoft.Extensions.Logging.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)
  — the `ILogger` contract the extensions target.

## What you get

The entry handed to the logger is an `IReadOnlyList<KeyValuePair<string, object?>>`: the shape every
`Microsoft.Extensions.Logging` provider understands. Its fields are fixed:

| Field | Present when |
| --- | --- |
| `ResultType` | Always — `"Result"` for the unit tier, the success type's name (`Int32`, …) for the generic tier |
| `IsSuccess` | Always |
| `ErrorType` | Failure only |
| `ErrorCode` | Failure only |
| `ErrorMessage` | Failure only, and omitted entirely when the error carries no message |

Omission is a signal: a provider can tell "no message" apart from "an empty message". `ResultType`
and `IsSuccess` always precede the failure fields.

Both tiers expose the same verb set:

| Method | Level | Writes when |
| --- | --- | --- |
| `LogSuccess` | `Debug` | Success only |
| `LogFailure` | `Error` | Failure only |
| `LogError` | `Error` | Failure only (alias of `LogFailure`) |
| `LogWarning` | `Warning` | Failure only |
| `LogInformation` | `Information` | Failure only |
| `LogAll` | `Debug` / `Error` | Always, once |
| `LogAll(levelSelector)` | From the selector | Always, once |

A method that does not describe the actual outcome writes nothing: `LogSuccess` on a failure is a
no-op, and so is `LogFailure` on a success — nothing is written twice. Every method checks whether
its level is enabled before building anything, so a disabled level builds nothing. The optional
`message` argument replaces the default rendered text for plain-text sinks; the structured fields
are unaffected by it.

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ResulTus;
using ResulTus.Logging;

ILogger logger = NullLogger.Instance;

Result<int> loaded = Result<int>.Failure(Error.NotFound("USER_NOT_FOUND", "No user"));

Result<int> same = loaded.LogFailure(logger, "User lookup");       // writes at Error

Result<int> chosen = loaded.LogAll(
    logger,
    error => error.Type == ErrorType.NotFound ? LogLevel.Warning : LogLevel.Error);
// failure -> Warning for NotFound, Error otherwise; success -> Debug
```

### Async verbs

A `Task<Result>` or `Task<Result<T>>` source has the same verbs as `...Async` — `LogSuccessAsync`,
`LogFailureAsync`, `LogAllAsync`, and so on. Each awaits the task exactly once, logs through the
synchronous overload and returns the awaited value.

```csharp
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ResulTus;
using ResulTus.Logging;

ILogger logger = NullLogger.Instance;
Task<Result<int>> task = Task.FromResult(Result<int>.Success(42));

Result<int> value = await task.LogSuccessAsync(logger);   // writes at Debug
```

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — required
  dependency: the core result types.
- [`ResulTus.Diagnostics`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Diagnostics/README.md)
  — opt-in `Activity` spans and `DiagnosticListener` events for measured operations.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [observability](https://github.com/sergteje/ResulTus/blob/main/docs/observability.md),
  [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md),
  [getting started](https://github.com/sergteje/ResulTus/blob/main/docs/getting-started.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
