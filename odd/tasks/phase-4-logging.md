# Phase 4 — Logging

Objective: ship `ResulTus.Logging`, structured `ILogger` integration for `Result` and `Result<T>`, with per-call-site log levels. Branch: `feature-1`.

Test runner: `dotnet test tests/ResulTus.Logging.Tests/ResulTus.Logging.Tests.csproj`.

Delivery strategy: `auto-chain` forecast — ~700 authored changed lines, so work-unit commits on `feature-1`; PR/merge stays the user's call.

## What "structured" means here

A log entry is not a formatted string. `ILogger.Log` receives a state object that implements `IReadOnlyList<KeyValuePair<string, object?>>`, so a provider (Serilog, OpenTelemetry, the JSON console logger) sees named fields, not text it has to parse:

| Field | When | Value |
| --- | --- | --- |
| `ResultType` | always | `"Result"` or the `T` of `Result<T>` |
| `IsSuccess` | always | `true` / `false` |
| `ErrorType` | failure only | the `ErrorType` name |
| `ErrorCode` | failure only | `Error.Code` |
| `ErrorMessage` | failure, when present | `Error.Message` |

The rendered message is a convenience for text sinks. The fields are the contract.

## Level policy

| Method | Outcome | Level |
| --- | --- | --- |
| `LogSuccess` | success | `Debug` |
| `LogFailure` | failure | `Error` |
| `LogAll` | success | `Debug` |
| `LogAll` | failure | `Error` |
| `LogAll(selector)` | failure | whatever `Func<Error, LogLevel>` returns |
| `LogError` | failure | `Error` |
| `LogWarning` | failure | `Warning` |
| `LogInformation` | failure | `Information` |

Every method is a no-op for the outcome it does not describe: `LogSuccess` on a failure writes nothing, `LogFailure` on a success writes nothing. Nothing is logged twice.

The `LogAll(selector)` overload exists because the proposal asks for configurable levels *and* for per-error-type behaviour. A selector like `e => e.Type == ErrorType.NotFound ? LogLevel.Warning : LogLevel.Error` covers both in one call, and it is the only place a delegate is needed.

## Zero work when the level is off

Every method calls `ILogger.IsEnabled(level)` before building anything. A disabled level allocates nothing: no state object, no formatted message, no boxing. This matters because the default success level is `Debug`, which is off in production.

## Specs

- **S1 — the project exists and is wired.** `src/ResulTus.Logging` (net8.0, `Microsoft.Extensions.Logging.Abstractions` 8.0.3 from `Directory.Packages.props`), `tests/ResulTus.Logging.Tests` (net10.0). Both in `ResulTus.slnx`. Builds under `TreatWarningsAsErrors` and `CS1591 = error`.
- **S2 — the outcome gates the write.** `LogSuccess` writes only on success, `LogFailure` only on failure, `LogAll` always writes exactly once.
- **S3 — the levels are the table above.** Every method writes at its documented level; `LogAll(selector)` uses the selector's level.
- **S4 — the entry is structured.** The state exposes `ResultType` and `IsSuccess` always, and `ErrorType`/`ErrorCode`/`ErrorMessage` on failure. `ErrorMessage` is absent, not empty, when the error has no message.
- **S5 — the result is returned unchanged.** Every method returns the same result (or the same awaited result), so logging composes inside a chain without changing its value.
- **S6 — a null logger throws.** `ArgumentNullException` on every entry point, instead of silently dropping the log line.
- **S7 — async overloads.** `Task<Result>` and `Task<Result<T>>` have the same surface; the task is awaited once and the result is returned unchanged.
- **S8 — nothing is allocated when the level is disabled.** Proven by a logger that reports every level disabled and records whether its `Log` was reached.
- **S9 — tests cover every method and both outcomes.** One RED observed per behaviour group.
- **S10 — no regression.** `dotnet build ResulTus.slnx` with 0 warnings and the whole suite green, including the existing 293 tests.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2, S3, S4, S5, S6 | inline | done | `dc17567` |
| T2 | S7, S8, S9 | inline | done | this commit |
| T3 | S9, S10 | inline | done | this commit |

## Log

### L1 — original request (verbatim)

> adelante con ambos

Continuing the roadmap after Phase 6. "Ambos" = Phase 4 (Logging) and Phase 8 (Diagnostics), which the previous turn offered as the next two phases.

### L2 — starting evidence

- `openspec/changes/phase-4-logging/proposal.md` scopes `LogSuccess`, `LogFailure`, `LogAll`, `LogError`, `LogWarning`, `LogInformation`, async overloads for `Task<Result<T>>`, project `src/ResulTus.Logging` on `net8.0`, tests on `net10.0`, dependency `Microsoft.Extensions.Logging.Abstractions`, default Success → `Debug` and Failure → `Error`, "configurable via optional delegate parameter", coverage >= 95%.
- The proposal still says `test/` and `ResulTus.sln`; this repo uses `tests/` and `ResulTus.slnx`.
- There is no existing logging surface in the repo: `ResulTus` and `ResulTus.Extensions` reference nothing from `Microsoft.Extensions.*`.
- `Microsoft.Extensions.Logging.Abstractions` 8.0.3 is the last 8.0 patch, which is the right floor for a `net8.0` library: a consumer on .NET 8 or later resolves it, and it does not force a 10.x dependency on a net8 app.
- `Result` exposes `IsSuccess`, `IsFailure`, `TryGetError(out Error)`. `Result<T>` adds `TryGetValue`, `GetValueOrDefault`, `GetValueOr`. Neither has a `Value` property that throws.

### L3 — decisions taken while implementing (not user requirements)

- `LogError`, `LogWarning` and `LogInformation` are the failure-at-a-fixed-level variants; `LogFailure` is the `Error` default. The proposal names all four, so all four ship, and each is a one-line delegation to the same core. No behaviour is duplicated, only the call-site vocabulary.
- The delegate the proposal asks for lives on `LogAll(Func<Error, LogLevel>)` only. Adding it to every method would multiply the surface for the same capability.
- Async overloads cover `Task<Result>` as well as `Task<Result<T>>`. The proposal mentions only the generic form, but a command handler returning `Task<Result>` is the common case and would otherwise have no logging at all.
- The state is a purpose-built `IReadOnlyList<KeyValuePair<string, object?>>` rather than an anonymous object, so the field names are stable and the shape is identical for both result types.
- The sync and async surfaces live in two classes, `ResultLoggingExtensions` and `ResultLoggingAsyncExtensions`, in two files. That is the convention `ResulTus.Extensions` already uses (`UnitResultExtensions` / `UnitResultAsyncExtensions`), and it keeps each work-unit commit self-consistent instead of landing 28 methods in one slice.
- The rendered default message does not name the operation. `"Result failed: missing"` is what a text sink gets; the operation belongs in the caller's `message`, and the structured fields carry the type, the error type and the code. A template that changed per call site would defeat log grouping.

### L4 — RED/GREEN evidence

- **RED (T1, T2):** the test project compiled against `Result` / `Result<T>` / `Task<Result>` / `Task<Result<T>>` and failed with `CS1061` — no `LogSuccess`, `LogFailure`, `LogSuccessAsync` or `LogFailureAsync` member existed. That is the whole surface missing, observed before any implementation file existed.
- **GREEN (T1, T2):** 36 tests pass. Coverage on `ResulTus.Logging`: **100% lines, 95.5% branches**. The one uncovered branch is a compiler-generated arm inside the async state machines, not a reachable path in the source.
- Assumption the code disproved: `ILogger.Log` accepts `TState` by generic parameter, so passing the state as a struct would avoid boxing at the call site. It does not matter here, because the state is built only after `IsEnabled` returns true — the allocation that matters is the one that never happens when the level is off. A class with computed fields was chosen over a struct enumerable: the struct would need a hand-written `IEnumerator<T>` implementation whose only benefit is an allocation on a path that already allocates.

*The `openspec/` paths cited above belong to the retired SDD store. The originals are recoverable with `git show 75b0962:<path>`; this record quotes the scoped parts inline.*
