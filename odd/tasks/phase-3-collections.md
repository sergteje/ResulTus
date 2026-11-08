# Phase 3 — Collections

Objective: ship `ResulTus.Collections` with collection-level operations over `IEnumerable<Result<T>>` and `IEnumerable<Result>` — sequencing, traversal, folding, partitioning, lazy flattening, queries and the async `WhenAll`/`WhenAny` — with a pooled intermediate buffer and no LINQ-name collisions. Branch: `feature-1`. Test runner: `dotnet test tests/ResulTus.Collections.Tests/ResulTus.Collections.Tests.csproj`.

Delivery strategy: `auto-chain` forecast — ~1 100 authored changed lines, so work-unit commits on `feature-1`; PR/merge stays the user's call.

## Specs

- **S1 — the project exists and is wired.** New `src/ResulTus.Collections/ResulTus.Collections.csproj` targeting `net8.0`, `RootNamespace`/`AssemblyName` = `ResulTus.Collections`, `IsTrimmable` + `IsAotCompatible`, referenced from `ResulTus.slnx`. It MUST build under `TreatWarningsAsErrors` and `CS1591 = error`, so every public member carries XML documentation.
- **S2 — sequencing and traversal over `IEnumerable<Result<T>>`.** `Sequence<T>()` returns a success holding every value in order, or the first failure with enumeration stopped at that element; `Traverse<TSource, TOut>(selector)` maps then sequences; `Fold<T, TAcc>(seed, accumulate)` folds the success values and short-circuits on the first failure. All three MUST allocate at most the returned collection, and MUST stop enumerating at the first failure.
- **S3 — partitioning and inspection.** `FirstFailure<T>()` returns the first error or `null`; `FirstSuccess<T>()` returns the first success, otherwise the first failure, and for an empty sequence a failure with code `"empty"`; `Partition<T>()` returns the successes and the failures as two exactly sized lists; `Flatten<T>()` lazily yields the success values and skips failures; `ForEach<T>(action)` runs the action per success and returns the first failure or success.
- **S4 — queries.** `AllSucceeded`, `AnySucceeded` and `NoneSucceeded`, with LINQ's vacuous-truth conventions on an empty sequence (`AllSucceeded` = `true`, `AnySucceeded` = `false`, `NoneSucceeded` = `true`).
- **S5 — the unit `Result` gets the same treatment.** `Combine()`, `Fold<TAcc>`, `FirstFailure()`, `ForEach(action)`, `AllSucceeded()`, `AnySucceeded()`, `NoneSucceeded()`.
- **S6 — async collection operations.** `WhenAllAsync<T>(IEnumerable<Task<Result<T>>>)` and the unit variant await every task and return the first failure in enumeration order, or a success holding the values; `WhenAnyAsync<T>` returns the first task to complete. All accept a `CancellationToken` and surface cancellation as `OperationCanceledException`.
- **S7 — the pooled buffer is returned.** `Sequence`/`Traverse` accumulate into an `ArrayPool<T>.Shared` buffer inside a `try/finally`, clear it when `T` may hold references, and return it exactly once. The returned collection is an exactly sized array, never the rented buffer.
- **S8 — no LINQ-name collisions.** `All`, `Any`, `None` and `Aggregate` from the phase-3 proposal are named `AllSucceeded`, `AnySucceeded`, `NoneSucceeded` and `Fold`: `Any()`/`All(...)`/`Aggregate(...)` already exist on `IEnumerable<T>`, and a more specific extension would silently capture those call sites with different semantics. `Merge` is not added because `Partition` already splits successes from failures; `Combine` exists only for the unit `Result`, where `Sequence` has nothing to collect.
- **S9 — tests cover both paths of every method.** New `tests/ResulTus.Collections.Tests` targeting `net10.0`, one RED observed per behavior group, every method asserted on the all-success, partial-failure, all-failure and empty cases.
- **S10 — no regression.** `dotnet build ResulTus.slnx` with 0 warnings and the whole suite green, including the 47 core and 80 extensions tests.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2, S7 | inline | done — RED (missing methods), 13/13 green | `c1f724d` |
| T2 | S3, S4 | inline | done — RED (missing methods), 24/24 green | `c1f724d` |
| T3 | S5 | inline | done — unit `Combine`/`Fold`/`FirstFailure`/`ForEach`/queries | `c1f724d` |
| T4 | S6 | inline | done — RED (missing methods), 31/31 green | `a922ac9` |
| T5 | S9, S10 | inline | done — solution build 0 warnings, 165 tests total, 95.2% line / 85.9% branch on `ResulTus.Collections` | `a922ac9` |

## Log

### L1 — original request (verbatim)

> phase 2 y despues la 3

Same authorization as `phase-2-extensions.md` L1: Phase 3 runs immediately after Phase 2, delivery (PR/merge) still not authorized.

### L2 — starting evidence

- Phase 2 closed at `358045e`: `ResulTus.Extensions` builds clean, 80/80 tests, 100% line coverage of the new assembly, core still 47/47.
- `openspec/changes/phase-3-collections/proposal.md` lists Combine, Aggregate, WhenAll, WhenAny, FirstFailure, FirstSuccess, Merge, Partition, Flatten, Sequence, Traverse, ForEach, All, Any, None, `ArrayPool<T>` usage, and `test/ResulTus.Collections.Tests` (this repo uses `tests/`).
- The proposal's risk table already anticipates the `WhenAll` semantics question and answers it: return the first error, and leave an all-errors variant for later.

### L3 — API decisions taken while implementing (not user requirements)

- Renames forced by LINQ: `All`/`Any`/`None` → `AllSucceeded`/`AnySucceeded`/`NoneSucceeded`, `Aggregate` → `Fold`. An extension on `IEnumerable<Result<T>>` is more specific than the LINQ one on `IEnumerable<T>`, so the LINQ names would silently win over existing call sites with different semantics — a compile-time trap worth avoiding.
- `Combine` only for `IEnumerable<Result>`; `Sequence` is the value-collecting name. `Merge` is dropped: `Partition` returns the same two sets without pretending to be a single result.
- `Sequence`/`Traverse` return `Result<IReadOnlyList<T>>` backed by an exactly sized array, so callers get `Count` and indexing without a second copy.
- `FirstSuccess` on an empty sequence returns `Error.Unexpected("empty", "The sequence contains no results.")`; the code is documented on the member.

### L4 — RED/GREEN per group

| Group | RED observed | GREEN |
| --- | --- | --- |
| Sequence / Traverse / Fold / unit Combine | `CS1061` for `Sequence`, `Traverse`, `Fold`, `Combine` | 13 tests |
| FirstFailure / FirstSuccess / Partition / Flatten / ForEach / queries | `CS1061` for `FirstFailure`, `FirstSuccess` | 24 tests |
| WhenAll / WhenAny | `CS1061` for `WhenAllAsync` | 31 tests |
| Guards, unit WhenAny, traversal growth | — | 38 tests |

### L5 — the pooled buffer

`Sequence` and `Traverse` rent from `ArrayPool<T>.Shared`, double the buffer when it fills, and return the rented array in a `finally` with `clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>()`. The returned collection is a fresh exactly sized array, so the caller never holds a pooled buffer. The "buffer returned" criterion (S7) is structural — `try/finally` with one return per rent — and is not observable from a test, so it is recorded here instead of being asserted by a fake test. What the tests do pin is the behaviour that depends on it: the 10 000-element `Sequence` and the 5 000-element `Traverse` both exercise the growth path and return every value in order.

### L6 — coverage

| Assembly | Line | Branch |
| --- | --- | --- |
| `ResulTus.Collections` | 95.2% | 85.9% |
| `ResulTus` (core, instrumented by reference) | 48.4% | 27.8% |

First measurement was 90.8% line: the unit `WhenAnyAsync`, the `Fold` failure path and the null-argument guards were untested. `GuardAndEdgeCoverageTests` closed them.

### L7 — final verification

`dotnet build ResulTus.slnx`: 0 warnings, 0 errors. `dotnet test ResulTus.slnx`: 47 core + 80 extensions + 38 collections = 165 green.

### L8 — commits

| Commit | Holds |
| --- | --- |
| `c1f724d` | the project, the solution wiring, the sync collection surface for both result types and its tests |
| `a922ac9` | the async `WhenAll`/`WhenAny`, the guards and the growth coverage |

*The `openspec/` paths cited above belong to the retired SDD store. The originals are recoverable with `git show 75b0962:<path>`; this record quotes the scoped parts inline.*
