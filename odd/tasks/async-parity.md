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
| T1 | S1 (core ops: Map, Bind, Tap/OnSuccess, Match, Recover, Finally) | inline | pending | — |
| T2 | S1 (error ops: MapError, OnFailure, Switch) + S2 | inline | pending | — |
| T3 | S4 (Ensure story + README/docs wording) | inline | pending | — |
| T4 | S3 (null/cancel batteries covering the new surface) | inline | pending | — |

## Log

### L1 — owner decision (verbatim)

> hay que arreglar los 3

Decision taken after the sweep presented the three API facts: the typed tier gets async operators (not a README-scoping retreat), the failure-side terminals land in every async tier, and the `Ensure` asymmetry is resolved by documenting the tier rationale (typed keeps one shape). This document is the spec the implementation slices read.
