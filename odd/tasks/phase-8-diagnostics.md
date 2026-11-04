# Phase 8 — Diagnostics

Objective: ship `ResulTus.Diagnostics` — opt-in timing, `DiagnosticSource` events and `Activity` tracing around a `Result`-producing operation. Branch: `feature-1`.

Test runner: `dotnet test tests/ResulTus.Diagnostics.Tests/ResulTus.Diagnostics.Tests.csproj`.

Delivery strategy: `auto-chain` forecast — ~550 authored changed lines, so work-unit commits on `feature-1`; PR/merge stays the user's call.

## Why delegates, not wrappers per library

The proposal asks for "opt-in wrappers for extensions, collections, validation". Taking that literally means an overload per library method, which would make `ResulTus.Diagnostics` depend on `ResulTus.Extensions`, `ResulTus.Collections` and `ResulTus.Validation` — four assemblies in every consumer's graph to time one call.

An operation is already a delegate. `Measure(() => result.Map(f), "MapOrder")` instruments an extension call, a collection fold or a validation run, because all three are operations that return a `Result`. So the package depends on `ResulTus` alone and still covers the three libraries the proposal names.

The consequence is honest: nothing is instrumented unless the caller writes `Measure`. That is exactly the proposal's own "manual opt-in" line.

## What a measured operation publishes

Three channels, all opt-in by construction:

| Channel | When it exists | What it carries |
| --- | --- | --- |
| Returned `TimedUnitResult` / `TimedResult<T>` | always | the untouched result plus `Elapsed` |
| `DiagnosticSource` events | only when a `DiagnosticListener` subscriber exists | `ResultOperationStarted`, `ResultOperationCompleted` |
| `Activity` | only when an `ActivityListener` samples the `"ResulTus"` source | name = operation name, kind `Internal`, tags below |

The two events have constant names, not `{operation}.Start` / `{operation}.Stop`. Building a per-call event name would allocate a string on every measured call even when nobody listens, which breaks the zero-allocation guarantee; the operation name travels in the payload instead, where a subscriber reads it without cost to callers that have none.

## Activity tags

| Tag | When | Value |
| --- | --- | --- |
| `resul_tus.success` | always | `true` / `false` |
| `resul_tus.error_type` | failure only | the `ErrorType` name |
| `resul_tus.error_code` | failure only | `Error.Code` |

A failed operation also sets `ActivityStatusCode.Error`, with the error's message — or its code when there is no message — as the status description. An operation that throws is recorded as an `Unexpected` failure carrying the exception message, and the exception is rethrown: a span that says "success" because the exception escaped the wrapper would be worse than no span at all.

Parent-child comes from `Activity.Current`. The wrapper never sets a parent explicitly; it starts an activity while the caller's is current, which is what makes an OTel trace connect across a handler.

## Zero work when nobody listens

`Measure` checks for a subscriber before building anything, and `ActivitySource.StartActivity` returns `null` when no listener samples the source. With neither attached, a measured call allocates nothing: no payload object, no activity, no string.

## Specs

- **S1 — the project exists and is wired.** `src/ResulTus.Diagnostics` (net8.0, references `ResulTus` only, no package: `System.Diagnostics.DiagnosticSource` is in-box), `tests/ResulTus.Diagnostics.Tests` (net10.0). Both in `ResulTus.slnx`. Builds under `TreatWarningsAsErrors` and `CS1591 = error`.
- **S2 — the operation runs once and its result survives.** `Measure`/`MeasureAsync` invoke the delegate exactly once and return a result equal to what the delegate produced, success or failure.
- **S3 — the wrapper is a value.** `TimedUnitResult` and `TimedResult<T>` are `readonly record struct`s, so wrapping allocates no heap object.
- **S4 — the events are ordered and typed.** A subscribed listener sees `ResultOperationStarted` before the operation and `ResultOperationCompleted` after it, and the completion payload carries the operation name, the elapsed time, the outcome and the error identity on failure.
- **S5 — the activity is named and nested.** An `Activity` named after the operation is started on the `"ResulTus"` source with kind `Internal`, and becomes a child of whatever `Activity.Current` was.
- **S6 — the activity records the outcome.** `resul_tus.success` is always present; a failure adds `resul_tus.error_type`, `resul_tus.error_code` and `ActivityStatusCode.Error`. A throwing operation produces the same failure recording and rethrows.
- **S7 — nothing happens when nobody listens.** With no `ActivityListener` and no `DiagnosticListener` subscriber, `ActivitySource.HasListeners()` is `false`, `Activity.Current` is untouched, no event is delivered, and 1000 measured calls allocate zero bytes on the calling thread.
- **S8 — a null operation or name throws.** `ArgumentNullException` from every entry point, before the operation runs.
- **S9 — no regression.** `dotnet build ResulTus.slnx` with 0 warnings and the whole suite green, including the 329 existing tests. Coverage on `ResulTus.Diagnostics` >= 95% lines and branches.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2, S3, S4, S5, S6, S8 | inline | done | this commit |
| T2 | S2, S4, S6, S7 | inline | done | this commit |
| T3 | S9 | inline | done | this commit |

## Log

### L1 — original request (verbatim)

> adelante con ambos

"Ambos" = Phase 4 (Logging) and Phase 8 (Diagnostics). Phase 4 is closed as `dc17567` and `ce11c8b`; this document starts Phase 8.

### L2 — starting evidence

- `openspec/changes/phase-8-diagnostics/proposal.md` scopes Stopwatch timing, `DiagnosticSource` integration, `Activity` tracing, opt-in wrappers, project `src/ResulTus.Diagnostics` on `net8.0`, tests on `net10.0`, dependency `System.Diagnostics.DiagnosticSource` (in-box) plus Phase 1 core and Phase 2 extensions, coverage >= 95%, and the success criteria "emits start/stop events", "proper parent-child relationships", "zero allocation when diagnostics not in use".
- The proposal says `test/` and `ResulTus.sln`; this repo uses `tests/` and `ResulTus.slnx`.
- `ErrorType.None = 0` is the "no error" member, so a success payload can carry `ErrorType.None` instead of a nullable enum.
- `Result` exposes `IsSuccess` and `TryGetError(out Error)`; `Result<T>` exposes `IsSuccess` and `TryGetValue(out T?, out Error)`. Neither throws on a failed read.
- The repo convention is `readonly record struct` for value types and full XML documentation on public members, with `CS1591`/`CS1573` as errors.

### L3 — decisions taken while implementing (not user requirements)

- Constant event names instead of `{operation}.Start` / `{operation}.Stop`: a per-call name allocates a string even when nobody listens, contradicting the zero-allocation criterion. The payload carries the operation name.
- The package references `ResulTus` only. Delegate wrappers instrument extension, collection and validation calls without making those assemblies transitive.
- A throwing operation is recorded as an `Unexpected` failure and rethrown. Instrumentation that hides an exception behind a successful span is worse than no instrumentation.
- `ActivityKind.Internal` is fixed at start; a library cannot know whether the caller is a server or a client, and claiming otherwise would corrupt a trace's shape.
- Sync and async `Measure` live in one class, `ResultDiagnosticsExtensions`, unlike Phase 4's split. Phase 4 split because 28 methods in one file is a real navigation cost; here there are four methods and they share four private helpers (`StartActivity`, `WriteStarted`, `RecordOutcome`, `RecordException`). Splitting two of them into a second class would mean either duplicating those helpers or opening an internal seam between two public classes for no reader benefit.
- Both `DiagnosticSource.Write` overloads are annotated `[RequiresUnreferencedCode]` (IL2026), so the call is legal only behind a suppression. The suppression is `[UnconditionalSuppressMessage]` on the two private helpers, not on `Measure`: the payload is this assembly's own public `ResultOperationStarted`/`ResultOperationCompleted`, so the trimmed shape is known statically at that call site. Annotating `Measure` would push the warning onto every caller, including the ones that never subscribe and only use the trim-safe `Activity` path.
- `ResultDiagnostics.ActivitySource` is created with the name only, no version. `AssemblyName.Version?.ToString() ?? "0.0.0"` compiles to a null check whose "absent" arm cannot run for an assembly this project builds, so it would leave a permanently uncovered branch — and the 95% branch target in S9 is a real requirement, not a decoration. The source version is optional metadata that no exporter routes on; subscribers match by `SourceName`.

- The whole phase lands as one commit rather than the ~400-line slices the roadmap used for earlier phases. The advisory budget is a planning heuristic, and splitting here would mean either a commit of type declarations with no behaviour or tests, or slicing the sync and async halves of four methods that share four private helpers. Neither produces a unit a reviewer can judge on its own; the package is one deliverable.

### L4 — RED/GREEN evidence

- **RED:** the test project compiled against the intended API and failed with `CS0103` (`ResultDiagnostics` does not exist), `CS1061` (`Func<Result>` has no `Measure`) and `CS0246` (`ResultOperationStarted` not found). The whole surface was missing, observed before any implementation file existed.
- **GREEN:** 29 tests pass; `dotnet build` on the project is 0 warnings / 0 errors under `TreatWarningsAsErrors` and `CS1591`-as-error.
- **Coverage:** `ResulTus.Diagnostics` at **100% lines, 100% branches**. Getting there took three iterations, and the first two diagnoses were wrong, which is worth recording:
  1. First measurement was 93.2% lines / 88.9% branches. The uncovered *lines* were the `catch` blocks of the two generic overloads — genuinely uncovered, fixed by `MeasureGeneric_WhenTheOperationThrows_ShouldRethrow` and `MeasureAsyncGeneric_WhenTheOperationThrows_ShouldRethrow`.
  2. After that, lines hit 100% but branches stayed at 88.9% (8/9) with **no** source line reporting less than 100%. The working hypothesis was compiler-generated `record` equality on `TimedUnitResult`/`TimedResult<T>`, and a value-equality test (`TimedResults_ShouldCompareByValue`) was added on that basis. The hypothesis was wrong: the number did not move.
  3. The real cause was found by reading Cobertura's per-class branch rate instead of per-line: `ResultDiagnostics` sat at `0.5` — the `Version?.ToString() ?? "0.0.0"` in its static initializer, one reachable arm out of two. Removing the version argument (see L3) took the package to 100%/100%.
- The equality test from step 2 was kept even though it did not fix the number. `readonly record struct` wrappers are compared by callers, and the test pins that the comparison is by value — it just was never the coverage gap.
- The zero-allocation criterion (S7) is a real assertion, not a smoke test: `WithNoListener_ShouldStartNoActivityAndAllocateNothing` asserts `ActivitySource.HasListeners()` is `false`, then compares `GC.GetAllocatedBytesForCurrentThread()` across 1000 `Measure` calls and requires a delta of exactly 0. It passed on the first run, which is the evidence that the constant event names and the listener guard actually do what L3 claims.
