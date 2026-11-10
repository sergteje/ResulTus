# API compatibility and the 1.0 strategy

Objective: make the public surface a checkable asset — a checked-in snapshot of every public
member per shipped assembly, enforced by tests — and document the versioning strategy that
governs it up to and past 1.0. Branch: `main`. Delivery strategy: direct commit series;
the contract test runs in CI. Test runner: `dotnet test ResulTus.slnx`.

Risk: item 3 (public API contract). The snapshot tests are audit-grade by design: any change to
any public member fails CI with a diff until the change is reviewed deliberately.

## Specs

- **S1 — the versioning strategy is a durable specification** committed at `odd/specs/versioning.md`,
  covering: SemVer + Keep a Changelog; the 0.x contract (breaking changes allowed on minor bumps,
  communicated in the changelog); the 1.0 promise (the public surface is the checked-in snapshot —
  a change without a new major version is by definition impossible to merge); the deprecation
  policy (`[Obsolete]` one version before removal, documented in the changelog, never silent);
  and the statement that analyzers/error-factory `RESULTGEN` messages are part of the contract.
- **S2 — the public surface is snapshotted per assembly**: `tests/ResulTus.ApiCompat.Tests/PublicApi`
  holds one `.txt` per shipped assembly listing every public type and member (constructors,
  operators, properties with their accessor shape, fields, events, nested public types), sorted,
  with source order irrelevant. The analyzers, the source generator and the eleven libraries are
  all shipped; the `netstandard2.0` assemblies are included in the same terms.
- **S3 — the contract test fails on any undeclared change**: `tests/ResulTus.ApiCompat.Tests`
  re-derives the surface from the loaded assemblies and diffs it against the snapshots; a missing,
  added or changed member fails with the diff, and the fix path is documented (review, then
  regenerate) rather than making the test whimsical.
- **S4 — the snapshot generation is a deterministic tool**, not a test-side artifact: a
  `scripts/ApiSnapshot` console program (`--write` to emit the snapshot files, `--check` to diff)
  that reuses the same reflection surface code the contract test links, so one implementation
  powers both sides. It is deliberately not part of the solution: nothing else may depend on it.
- **S5 — the surface is committed today, before 1.0**, as the origin of the contract: the
  snapshots in this series are the baseline every future diff is diffed against, including during
  the 0.x era where the freedom to break means the snapshot must be regenerated deliberately,
  per change.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 | inline | done | `odd/specs/versioning.md` |
| T2 | S4 | inline | done | scripts/ApiSnapshot tool |
| T3 | S2 | inline | done | PublicApi/or a snapshot per assembly (-11) |
| T4 | S3 | inline | done | ResulTus.ApiCompat.Tests in the slnx, green in CI |

## Log

### L1 — owner authorizes (verbatim)

> si adelante

in reply to the proposal of "Compat de API + estrategia 1.0", closing the owner-ordered sequence:
multi-targeting → coverage gaps → coverage gate → this.
