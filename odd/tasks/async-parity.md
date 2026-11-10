# Async parity — close the documented gaps in the extensions surface

Objective: fix the three API-fact gaps the consistency sweep surfaced (audit-first-pass L3): the typed tier's missing async operators, the missing failure-side and error-mapping async terminals, and the `Ensure` arity asymmetry. Branch: `main`. Delivery strategy: single commit series on `main`, CI guards the suite. Test runner: `dotnet test ResulTus.slnx`.

Risk: item 3 (public API surface — new operators land in every tier's contracts). Every public member needs XML documentation and the README/docs must state the surface they claim, so the tests and the docs ride each slice.

## Specs

- **S1 — the typed tier gets the same asynchronous operators the value tier has**, with the typed error type: `MapAsync`, `BindAsync`, `TapAsync`/`OnSuccessAsync`, `MatchAsync`, `RecoverAsync` (both the value shape and the result shape, mirroring the typed sync `Recover`), `FinallyAsync`, `MapErrorAsync`, `OnFailureAsync`, `SwitchAsync`, over `Task` and over `ValueTask` sources, with cancellation-aware delegates that mirror the value tier's shapes exactly (delegate takes the success or error value plus the token; token also passed as an optional trailing parameter).
- **S2 — every existing async tier gains the missing operators**: `MapErrorAsync`, `OnFailureAsync`, `SwitchAsync` for the unit and the value tier, in the same Task/ValueTask pair.
- **S3 — failure short-circuiting, cancellation and null arguments mean the same thing in every new operator** as in the existing ones: a failure skips success delegates and preserves the error untouched; a cancelled token throws `OperationCanceledException` before awaiting (never converted into a failure); null delegates throw `ArgumentNullException`.
- **S4 — `Ensure` keeps one arity story per tier, documented**: the unit tier keeps two shapes (error, error-factory), the value tier keeps three (error, error-factory, value-factory) and the typed tier keeps one (error) plus a documented rationale — a typed `TError` is an arbitrary value type the caller already holds, so synthesizing errors from factories is not requested until a real use is named. The README packages table states which tiers carry the async operators, ending the unscoped "Task/ValueTask overloads" wording.
- **S5 — each slice ships with its covering tests**: the parity tests of the sweep (`CrossTierSemanticsTests.cs`) gain a section per tier/op asserting Task-source, ValueTask-source and the synchronous twin agree, on success and failure paths, without duplicating the existing behavioral batteries.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 (core ops: Map, Bind, Tap/OnSuccess, Match, Recover, Finally) | inline | done | `05f3160` |
| T2 | S1 (error ops: MapError, OnFailure, Switch) + S2 | inline | done | see L2 |
| T3 | S4 (Ensure story + README/docs wording) | inline | done | see L2 |
| T4 | S3 (null/cancel batteries covering the new surface) | inline | done | see L2 |

## Log

### L1 — owner decision (verbatim)

> hay que arreglar los 3

Decision taken after the sweep presented the three API facts: the typed tier gets async operators (not a README-scoping retreat), the failure-side terminals land in every async tier, and the `Ensure` asymmetry is resolved by documenting the tier rationale (typed keeps one shape). This document is the spec the implementation slices read.

### L2 — T2, T3 and T4 executed; the owner's README-first instruction honored (2026-11-10)

**Owner, verbatim:**

> README T2, T3, T4 Y README DE NUEVO

**The README-first part:** before any new API, the misleading text got fixed. The only false claim in the tree sat in `CHANGELOG.md`'s 0.0.1 Extensions bullet ("the `Task`/`ValueTask` overloads with cancellation" following "for the unit, `Result<T>` and generic tiers") — scope corrected to state the typed tier's async operators land in 0.0.2. `README.md` itself made no false claim: its Packages row already read "synchronous and asynchronous", which this work now makes true instead of aspirational.

**T2 — the error-side terminals, 18 methods.** `MapErrorAsync`, `OnFailureAsync` and `SwitchAsync` × two sources for each of the three tiers (`src/ResulTus.Extensions/UnitResultAsyncExtensions.cs`, `ResultAsyncExtensions.cs`, `TypedResultAsyncExtensions.cs`). Shapes mirror each tier's sync twin exactly — the typed tier's error type flows through `MapErrorAsync<TSuccess, TError, TNewError>`.

**T3 — the Ensure story + docs.** The unit tier gains its factory-shaped `EnsureAsync`, the value tier gains both factory shapes (plain and value-derived), the typed tier gains its `EnsureAsync` (the one documented shape, with the rationale stated in the XML remarks and now in both README editions: the typed error is a value the caller already holds, so a synthesizing factory is not requested until a real use is named). `CHANGELOG.md` `[Unreleased]` records the surface; the README async section now states the full operator list per tier with the Ensure note.

**T4 — batteries, 15 tests.** Parity tests for the new operators (Task/ValueTask/sync, success and failure paths) plus per-tier null-and-cancelled batteries: null delegates throw `ArgumentNullException`, a cancelled token throws `OperationCanceledException` before any delegate runs — for every new operator, both source types.

**One real defect found and fixed by the new tests.** The first value-tier `OnFailureAsync` ran the side effect on **success** — `TryGetValue(out _, out error)` returns true on the success path, and the follow-through guard was written without negating it. `Value_OnFailureAsync_TaskAndValueTaskShouldAgreeWithSync` caught the divergence from the sync twin immediately (observed `{"task","vtask"}` where only failure-path hits are legal). The fix is the negated guard; the parity test now pins the correct behavior. This is exactly what T4 exists for: the audit's "comportamiento coherente" claim is now enforceable, not assumed.

**Gates.** Extensions project: **186 passed**. Full suite: **707 passed, 0 failed, 0 skipped**, build 0 warnings / 0 errors.
