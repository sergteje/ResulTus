# Docs split — professional guides, scoped to reality

Objective: turn the README from a 470-line tour into a professional entry point and move the
deep content into focused `docs/` guides — **documenting only what exists in the code**. The
external review proposed a 29-section structure whose Union/Option/State-Monad/Events/Async-Streams
sections describe features that do not exist (verified: zero occurrences in the repo); this feature
supersedes that proposal with a real-feature scope. Branch: `main`. Delivery strategy: work-unit
commits per unit; LinkedIn promotion happens only after this lands. Test runner: doc snippets are
compile-verified in throwaway projects (repo rule: every code block in a document must compile).

Risk: none (documentation only; the README numbers are already observed and charter-governed).

## Specs

- **S1 — reality scope**: every guide documents only features that exist in the shipped code;
  verified against the source (Union/Option-as-type/State Monad/Domain Events/IAsyncEnumerable
  are absent and stay absent from the docs).
- **S2 — compile-verified snippets**: every code block compiles and, where it produces output,
  runs with the documented output (throwaway project outside the worktree).
- **S3 — README as entry point**: the slimmed README keeps branding, the contract, packages +
  install, the 30-second quick start, what-sets-it-apart, benchmark tables (credibility core),
  runtime compatibility, and an index of guides; the deep tours move to `docs/`.
- **S4 — parity**: `README.es.md` mirrors `README.md` in neutral professional Spanish. The
  `docs/` guides are English-only (the existing `docs/httpclient.md` and `docs/performance.md`
  already are); `README.es.md` links the same guides.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2 | delegated writer W1 | done | `1c89336` |
| T2 | S1, S2 | delegated writer W2 | done | `3515a9d` |
| T3 | S1, S2 | delegated writer W3 | done | `c8b0279` |
| T4 | S3, S4 | inline (parent) | done | see L2 |

## Log

### L1 — owner approves the corrected plan (verbatim)

> dale

(said after the four hard README corrections landed in `c265ec7` and the corrected docs-split
proposal). The reviewers' 29-section structure is superseded: guides exist only for real
features — getting started, core concepts, functional operations, collections, observability
(logging + diagnostics), error handling, serialization, testing — plus the already-existing
`docs/performance.md`, `docs/httpclient.md` and `docs/decisions/`.

### L2 — feature closed: nine guides live, READMEs are entry points (2026-11-12)

- **T1/T2/T3 done** (commits `1c89336`, `3515a9d`, `c8b0279`): eight new guides (getting-started,
  core-concepts, functional-operations, collections, observability, error-handling, serialization,
  testing), 1,490 lines, every snippet compile- and run-verified in throwaway projects against the
  real sources; scratch removed.
- **T4 done**: both READMEs restructured to entry points — the long "Fluent by construction" tour
  replaced by a compact pipeline section plus the guides index; `README.es.md` mirrors at parity.
  **Two source-vs-README discrepancies the writers found, fixed in T4:** (1) the async symmetry was
  overstated — `OnSuccessAsync` is typed-tier-only (unit/value tiers expose `TapAsync`) and the unit
  tier's `BindAsync` is value-producing only; both READMEs now state the deliberate asymmetries.
  (2) the README tour setup block did not compile as top-level statements (CS8803: type declarations
  before the static local functions) — `docs/getting-started.md` carries the reordered, verified
  version.
- One correction to the writers' own report: W1's shorthand said `ResultConversions` had four
  members; the source exposes **seven** (`ToResult`, `ToNullable`, `ToUnitResult`, `ToGenericResult`,
  `ToErrorResult` ×2 overloads, `ToException`) — and `docs/core-concepts.md` documents all seven.
- T3 found no source-vs-README discrepancies; its high-stakes AOT wording (direct converter vs
  carrier vs the explicitly-not-AOT options overload) was spot-checked in the file and verified.
  `docs/serialization.md` also states the typed tier `Result<TSuccess, TError>` is not serialized
  by the shipped converters.
