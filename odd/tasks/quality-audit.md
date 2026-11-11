# Quality audit — certify the core before anything new grows

Objective: a full adversarial audit of the shipped core, answering the external review's seven
questions with executable evidence (probes that assert the CORRECT behavior; a failing probe is a
finding, and a finding becomes a fix with its own test). No new features land during the audit.
Branch: `main`. Delivery strategy: work-unit commits; each unit's probes and fixes commit
together. Test runner: `dotnet test ResulTus.slnx` (Release; scratch state outside the worktree).

Risk: findings may touch the 1.0.0 public surface (item 3 of the high-risk list) — any fix that
would change observable behavior of a shipped member needs the versioning spec's ladder, not a
silent change.

## Specs

The owner's directive: "quick wins y despues auditoria" — the audit answers the external review's
questions verbatim:

- **S1** — "Si existen bugs en los estados internos de Result."
- **S2** — "Si hay casos donde se puede obtener un valor inválido."
- **S3** — "Si default(Result<T>) tiene un comportamiento seguro y coherente."
- **S4** — "Si las operaciones funcionales respetan las leyes de composición."
- **S5** — "Si los overloads async realmente evitan asignaciones innecesarias."
- **S6** — "Si los generadores y analizadores funcionan correctamente en soluciones grandes."
- **S7** — "Si la API pública está preparada para permanecer estable durante años."

Scope notes (recorded assumptions, not owner quotes): every probe asserts the behavior the
existing documented charter already promises (zero allocation on non-materializing operations,
cancellation as control flow, no naked `Value`); a probe that exposes a defect routes to a fix
under the versioning spec, never a silent behavior change. S7's judgment lever: the committed
ApiCompat snapshots make the 1.0 promise mechanical — the audit adds the judgment the snapshots
cannot make (symmetry, naming, deprecation ladder readiness).

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S3, S2 | inline | done | `probes green` — see L2 |
| T2 | S1 | inline | done | `ad9b56e` — see L3 |
| T3 | S4 | inline | done | see L3 |
| T4 | S5 | inline | done | see L4 |
| T5 | S6 | delegated writer (scratch solution) | pending | generators/analyzers under a large synthetic solution |
| T6 | S7 | inline | pending | long-term API stability judgment |

## Log

### L1 — owner's order (verbatim)

> quick wins y despues auditoria

The quick wins landed first (commit `9c474e2`: tag/version guard, Dependabot, pinned runners,
current actions). This document governs the audit that follows. The external review's questions
are quoted verbatim in the specs; its data stale spots (already-done items) were corrected in
conversation and are not repeated here.

### L2 — T1 done: the default state and the null-success are coherent by design; both are now pinned (2026-11-12)

Probe-first run against the core source (`Result{T}.cs`, `Result.cs`, `Result{TSuccess,TError}.cs`,
`Error.cs`) — `tests/ResulTus.Tests/DefaultAndNullStateAuditTests.cs`, 9 probes, **all green on the
first run**: the preserved behavior already matches the contract, so no RED finding surfaced. The
audit value is the pin: these states cannot silently change anymore.

- **S3 — `default` is coherent.** All three tiers: `default` is a *failure* (the flag byte reads
  0), every accessor behaves like an ordinary failure, `Match` routes to the failure branch, and
  `default(Result<T>) == Result<T>.Failure(Error.None)` — the same value. `Error` was already
  built for it: `Code` null-coalesces and `Equals` follows the public contract, so
  `default(Error) == Error.None` by design.
- **S2 — the only "invalid value obtainable" is a designed state.** `Success(T)` is one erased
  signature shared by `T` and `T?`, so the runtime cannot reject a null success for a
  non-nullable annotation without breaking the sanctioned `Result<string?>.Success(null)` (which
  the serialization, Combine and AspNetCore suites already exercise). Probes pin the
  null-success contract: a real success carrying null, `TryGetValue` true, `GetValueOrDefault`
  returning null, `Match` handing null to the success branch. The poison-value exposure is
  contained: on failure the `TryGetValue` out value is `default(T)`, never garbage — a caller
  that ignores the bool at worst observes a default.

**Verdict for S2/S3: no defect.** The pinned suite is the guarantee. One residual note for S7:
the null-success state deserves a sentence in the XML remarks of `Success(T)`/the type remarks —
doc work, not a behavior change; folded into T6's API-stability pass.

### L3 — T2 and T3 done: record semantics and the composition laws hold; one probe-design lesson recorded (2026-11-12)

- **T2 — S1, no defect** (`ad9b56e`): `tests/ResulTus.Tests/InternalStateAuditTests.cs`, 7
  probes green on first run. The state flag separates success from failure regardless of carried
  fields; equality follows the payload within a state; `GetHashCode` matches equality; a
  `Result<Error>` keeps the payload state and the error state distinct; the unit tier mirrors the
  generic contract; a success never reports a stale error; typed-tier error comparison uses the
  `Error` value semantics.
- **T3 — S4, no defect**: `tests/ResulTus.Extensions.Tests/CompositionLawAuditTests.cs`, 12
  law probes across all three tiers. Functor identity and composition (`Map`), monad
  associativity and short-circuit (`Bind` — binders never run on a failure, the first failure
  wins a chain), `MapError` identity, recovery idempotence and failure propagation,
  `Ensure` idempotence and failure short-circuit, side-effect operators returning the original
  value. First run was RED (3 of 12) — the defect was in the probes, not the library: the
  short-circuit law was asserted as cross-type equality (`Result<int>.Failure` vs
  `Result<string>.Failure`), which is not the law's form; the correct form is the preserved
  state plus the preserved error, and that is what now passes.

**Verdict for S1/S4: no defect.** Lesson recorded for the remaining audit units: value
equality only asserts laws within one type instantiation; laws that change `T` assert state +
carried error.

### L4 — T4 done: the async overloads allocate nothing on the sync-completing path (2026-11-12)

`tests/ResulTus.Extensions.Tests/AsyncAllocationAuditTests.cs` — 3 chain probes (generic, unit,
typed tiers, success and failure states each), 10 000 iterations each over
`GC.GetAllocatedBytesForCurrentThread`, **0 B everywhere**. Each chain composes
`MapAsync` → `EnsureAsync`/`RecoverAsync` → `BindAsync` → `TapAsync` → `MapErrorAsync` →
`MatchAsync` over an already-completed `ValueTask` with a synchronously-completing mapper and
static delegates (no closures), so the zero reading certifies every operator in the chain —
allocations cannot cancel. The `Task`-accepting wrapper overload adds nothing beyond the
already-completed task.

The async design holds: `async ValueTask` cores whose state machines complete synchronously
allocate nothing, and the `Task` overloads wrap without re-boxing. **Verdict for S5: no
defect.** Probe-authoring notes for the record: the unit tier's `MapAsync` mapper takes only a
`CancellationToken`, and after a unit-tier `MapAsync` the receiver is `ValueTask<Result<int>>`,
so the follow-up operators bind the generic tier's extensions — explicit lambda parameter types
are the only way to keep overload resolution deterministic in these chains.
