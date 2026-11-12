# ResulTus — the core `Result` types

[![NuGet](https://img.shields.io/nuget/v/ResulTus?label=nuget)](https://www.nuget.org/packages/ResulTus)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus?label=downloads)](https://www.nuget.org/packages/ResulTus)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package is the core of [ResulTus](https://github.com/sergteje/ResulTus): success and failure
as one inline `readonly record struct` with zero heap allocation on both paths. Every other
ResulTus package — the fluent operators, collections, serialization, logging, validation, HTTP —
builds on these types.

## The contract

- **Zero heap allocation on the success path *and* on the failure path.** Building a success and
  building a failure both allocate [0 B](https://github.com/sergteje/ResulTus/blob/main/docs/performance.md#allocation--measured-2026-10-29).
- **`Error` is a 24-byte inline `readonly record struct`** — code, optional message, category; no
  untyped payload, no heap.
- **`Result<T>` is 32 bytes**, the deliberate price of the inline error ([§4.2](https://github.com/sergteje/ResulTus/blob/main/docs/performance.md#42-struct-size-is-a-design-problem-not-a-match-problem--resolved-2026-10-28)).
- **The generic tier `Result<TSuccess, TError>` is 13 bytes** with a reference-free error type.

Every number links to the measured row in the [performance charter](https://github.com/sergteje/ResulTus/blob/main/docs/performance.md).

## Install

```bash
dotnet add package ResulTus
```

The core alone gives you the types and `Match`/`TryGetValue`. Add
[`ResulTus.Extensions`](https://www.nuget.org/packages/ResulTus.Extensions) for the fluent
operators (`Map`, `Bind`, `Ensure`, …).

## What you get

`Result` (the unit tier), `Result<T>` (the value tier), `Result<TSuccess, TError>` (the generic
tier), `Error`, `ErrorType`. Success and failure are two states of one struct — no exceptions, no
heap:

```csharp
using ResulTus;

Result<int> ok = Result<int>.Success(42);
Result<int> notFound = Result<int>.Failure(Error.NotFound("USER_NOT_FOUND", "No user with that id"));

string verdict = notFound.Match(
    onSuccess: value => $"found {value}",
    onFailure: error => error.Code);        // "USER_NOT_FOUND"

if (ok.TryGetValue(out var value, out var error))
{
    // use value
}
```

Errors are built inline from the factory methods on `Error`:

```csharp
using ResulTus;

Error validation = Error.Validation("NOT_A_NUMBER");
Error notFound  = Error.NotFound("USER_NOT_FOUND");          // failure, no exception
```

## Sibling packages

| Package | Adds | README |
| --- | --- | --- |
| `ResulTus.Extensions` | The fluent operators (`Map`, `Bind`, `Ensure`, `Recover`, …), sync and async | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Extensions/README.md) |
| `ResulTus.Collections` | `Traverse`, `Sequence`, `Fold`, `Partition` over collections of results | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Collections/README.md) |
| `ResulTus.Serialization` | System.Text.Json converters with a fixed, strict wire format | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Serialization/README.md) |
| `ResulTus.Logging` | Structured `ILogger` extensions emitting outcomes as named fields | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Logging/README.md) |
| `ResulTus.Diagnostics` | Opt-in `Activity`/`DiagnosticListener` timing and tracing | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Diagnostics/README.md) |
| `ResulTus.AspNetCore` | Action results and RFC 9457 ProblemDetails | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.AspNetCore/README.md) |
| `ResulTus.HttpClient` | HTTP responses and failures as `Result<TSuccess, HttpClientError>` | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.HttpClient/README.md) |
| `ResulTus.Validation` | `ValidationErrors`/`ValidationIssue`, DataAnnotations | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Validation/README.md) |
| `ResulTus.Validation.FluentValidation` | `IValidator<T>` to `Result<T, ValidationErrors>` | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Validation.FluentValidation/README.md) |
| `ResulTus.Analyzers` | Roslyn analyzers `RESULT001`–`RESULT006` | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Analyzers/README.md) |
| `ResulTus.SourceGenerators` | The `[ErrorFactory]` source generator | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.SourceGenerators/README.md) |
| `ResulTus.Testing` | Framework-agnostic result assertions | [GitHub](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Testing/README.md) |

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, benchmarks, guides.
- Guides: [getting started](https://github.com/sergteje/ResulTus/blob/main/docs/getting-started.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md),
  [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md),
  [functional operations](https://github.com/sergteje/ResulTus/blob/main/docs/functional-operations.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
