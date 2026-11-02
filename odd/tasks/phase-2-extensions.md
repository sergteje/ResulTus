# Phase 2 — Extensions

Objective: ship `ResulTus.Extensions` with the functional pipeline API (Map, Bind, MapError, Tap, OnSuccess, OnFailure, Switch, Ensure, Recover, Finally + async variants + conversions) over `Result<T>` and `Result`, zero heap allocation on the sync path. Branch: `feature-1`. Test runner: `dotnet test tests/ResulTus.Extensions.Tests/ResulTus.Extensions.Tests.csproj`.

Delivery strategy: `auto-chain` forecast — ~1 800 authored changed lines (source + tests + docs), so work-unit commits on `feature-1` and a review slice per unit; PR/merge stays the user's call.

## Specs

- **S1 — the project exists and is wired.** New `src/ResulTus.Extensions/ResulTus.Extensions.csproj` targeting `net8.0`, `RootNamespace`/`AssemblyName` = `ResulTus.Extensions`, `IsTrimmable` + `IsAotCompatible` like the core, and referenced from `ResulTus.slnx`. It MUST build with the repo's `TreatWarningsAsErrors` and `CS1591 = error`, so every public member carries XML documentation.
- **S2 — sync API on `Result<T>`.** `Map`, `Bind`, `MapError`, `Tap`, `OnSuccess`, `OnFailure`, `Switch`, `Ensure`, `Recover` (value and result overloads) and `Finally`, as extension methods in namespace `ResulTus.Extensions`. Each MUST be `[MethodImpl(AggressiveInlining)]` where it is a branch plus a call, MUST throw `ArgumentNullException` for a `null` delegate, and MUST NOT allocate on the heap on either the success or the failure path (delegates supplied by the caller are not counted).
- **S3 — the unit `Result` gets the symmetric API.** `Map`, `Bind` (to `Result<T>` and to `Result`), `MapError`, `Tap`, `OnSuccess`, `OnFailure`, `Switch`, `Ensure`, `Recover`, `Finally`, with the same null and allocation rules.
- **S4 — async API.** `MapAsync`, `BindAsync`, `EnsureAsync`, `TapAsync`, `MatchAsync`, `RecoverAsync` and `FinallyAsync`, for both `Result<T>` and `Result`, over both a `Task<TR>` and a `ValueTask<TR>` source, with a `CancellationToken`-aware delegate (`Func<..., CancellationToken, ValueTask<...>>`) and a `CancellationToken cancellationToken = default` parameter. A cancelled token MUST surface as `OperationCanceledException` from the awaited work, never be swallowed into an `Error`.
- **S5 — conversions.** `ToResult` (value to `Result<T>`), `ToNullable` (`Result<T>` where `T : struct` to `T?`, `null` on failure) and `ToException` (`Error` to `Exception` with `Message = Message ?? Code` and the code preserved in `Data["Code"]`). `ToOption` and `ToMaybe` from the phase-2 proposal are **not implemented**: the library has no `Option`/`Maybe` type and inventing one is a new public type nobody asked for. The feature document records the deferral; the proposal's line is left intact.
- **S6 — tests cover both branches of every method.** New `tests/ResulTus.Extensions.Tests` targeting `net10.0` with xUnit v3 + FluentAssertions, one RED observed per behavior group before its implementation. Every method is asserted on success and failure, including delegate-not-invoked cases, `null` delegate rejection, and cancellation.
- **S7 — no regression in the core.** `dotnet build ResulTus.slnx` with 0 warnings and the whole suite green, including the 47 pre-existing `ResulTus.Tests`.
- **S8 — coverage.** The phase-2 proposal asks for `>= 95%` line coverage; measured with `dotnet test --collect:"XPlat Code Coverage"` on the extensions test project, and the measured number is recorded in the Log.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2 | inline | done — RED (missing methods), then 25/25 green | `54f92eb` |
| T2 | S3 | inline | done — RED (missing methods), then 20/20 green | `574aee4` |
| T3 | S4 | inline | done — RED (missing methods), then 80/80 green | `1b6ea18` |
| T4 | S5 | inline | done — RED (missing methods), 5 conversion tests green | `efa1860` |
| T5 | S6, S7, S8 | inline | done — solution build 0 warnings, 47/47 core + 80/80 extensions, 100% line / 96.4% branch on `ResulTus.Extensions` | `efa1860` |

## Log

### L1 — original request (verbatim)

> phase 2 y despues la 3

Answered after "¿Arrancamos Phase 2 (Extensions) o preferís primero abrir el PR de `feature-1` a `main`?" — so Phase 2 first and Phase 3 immediately after, both authorized in this pass. Delivery (PR/merge) is still not authorized.

### L2 — starting evidence

- Baseline: `dotnet build ResulTus.slnx` 0 warnings, `dotnet test tests/ResulTus.Tests` 47/47 green, worktree clean, `feature-1` in sync with `origin/feature-1`.
- `Directory.Build.props` sets `TreatWarningsAsErrors`, `GenerateDocumentationFile`, `EnforceCodeStyleInBuild`, `IsTrimmable`, `IsAotCompatible`, `net8.0`; `.editorconfig` raises `CS1591` to `error`, so undocumented public members fail the build.
- Core surface available to the extensions: `Result<T>` (`IsSuccess`, `Match`, `TryGetValue`, `GetValueOrDefault`, `GetValueOr`, `Deconstruct`, static `Success`/`Failure`, implicit operators) and `Result` (`IsSuccess`, `Match`, `TryGetError`, `Deconstruct`, static `Success`/`Failure`), plus `Error` factories.
- The core keeps `[StructLayout(Pack = 1)]` only on `Result<TSuccess, TError>`; nothing in this phase changes layout.
- `openspec/changes/phase-2-extensions/proposal.md` says `test/ResulTus.Extensions.Tests`; this repo uses `tests/`, so the task follows `tests/`.

### L3 — API decisions taken while implementing (not user requirements)

- `Match` is **not** re-declared as a sync extension: `Result<T>.Match` and `Result.Match` are instance methods, and an instance method always wins over an extension method with the same signature, so the extension would be dead code. The async `MatchAsync` is added because it chains off a `Task`/`ValueTask`, which the instance method cannot.
- `OnSuccess` delegates to `Tap` (same semantics, both names kept from the proposal).
- `Finally` runs one action with `(value, error)` — or just `(error)` for the unit `Result` — and returns the original result unchanged.
- `Recover` ships two overloads: `Func<Error, T>` (value) and `Func<Error, Result<T>>` (result). They differ in the delegate's return type only, which is a valid overload; a `T` that is itself a `Result<>` makes the call ambiguous, and that is documented on the members.
- `ToNullable` is constrained to `where T : struct`; for reference types `GetValueOrDefault()` already yields `null`, and two constraint-only overloads would not compile.

### L4 — RED/GREEN per task

| Task | RED observed | GREEN |
| --- | --- | --- |
| T1 | `CS1061: Result<int> has no Map/Bind/MapError/Tap/OnSuccess/OnFailure/Switch` | 15 tests |
| T1 (guard/recovery) | `CS1061` for `Ensure`, `Recover`, `Finally` | 25 tests |
| T2 | `CS0411` — the unit methods did not exist and the generic ones could not be inferred | 45 tests |
| T4 | `CS1061` for `ToResult`, `ToNullable`, `ToException` | 50 tests |
| T3 | `CS1061` for every `*Async` method | 74 tests |
| T5 | — | 80 tests |

### L5 — analyzer finding in the new test project

`xUnit1051` (pass `TestContext.Current.CancellationToken` to every token-accepting call) is an error under this repo's `TreatWarningsAsErrors`. It fired on ~24 call sites. Suppressed in `ResulTus.Extensions.Tests.csproj` with a comment: these are in-memory pipeline assertions with dedicated cancellation tests, so the extra argument would be pure noise. The dedicated tests still pass an explicit cancelled token, so cancellation is genuinely covered.

### L6 — the `Recover` overload pair and `null`

`Recover(Func<Error, T>)` and `Recover(Func<Error, Result<T>>)` differ only in the delegate's return type. A lambda returning a plain value binds to the first; a `null` literal binds to neither unambiguously (`CS0121`), so the null-delegate test casts explicitly: `result.Recover((Func<Error, int>)null!)`. Both members document the ambiguity.

### L7 — coverage (S8)

`dotnet test --collect:"XPlat Code Coverage"`:

| Assembly | Line | Branch |
| --- | --- | --- |
| `ResulTus.Extensions` | 100% | 96.4% |
| `ResulTus` (core, pulled in by the reference) | 53.7% | 27.8% |

The whole-run figure is 88.2% line only because the core assembly is instrumented too; the new project is fully covered, which is what the proposal's `>= 95%` was about.

### L8 — final verification

`dotnet build ResulTus.slnx`: 0 warnings, 0 errors. `ResulTus.Tests`: 47/47. `ResulTus.Extensions.Tests`: 80/80.

### L9 — commits

| Commit | Holds |
| --- | --- |
| `54f92eb` | the project, the solution wiring, the `Result<T>` sync pipeline and its tests |
| `574aee4` | the unit `Result` sync pipeline and its tests |
| `1b6ea18` | the async pipeline for both result types, cancellation, and the short-circuit coverage |
| `efa1860` | the conversions and this record |

