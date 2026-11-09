# First external audit — triage, verified items and work plan

Objective: turn the owner's first external audit (Nov 2026, "Primera auditoría de ResulTus") into verified facts and a workable plan. Branch: `main`. Delivery strategy: single commit series on `main`, CI guards the suite. Test runner: `dotnet test ResulTus.slnx`.

Every audit claim below was re-verified against the repository before being accepted or refuted; the verification evidence is recorded with path:line or tool output. Nothing was accepted on the auditor's word, including the items marked done.

## Specs

- **S1 — Native AOT proof is produced by a publish-and-run in CI, closing the longest-standing open proof in the project.** The evidence gap: `odd/tasks/api-completion.md` S7/T6 landed the AOT-safe `ResultJsonConverter<T>(JsonTypeInfo<T>)` path, but T7's closer in L6 of that record left the smoke check "unavailable on this host (no MSVC linker)". The host limitation is irrelevant on a GitHub `windows-latest` runner, which ships the MSVC toolchain. The proof must publish a small console app that references the library, exercises the source-generated JSON round trip, and runs natively — both a trimmed publish and a Native AOT publish. No claim of AOT support before that run is observed green.
- **S2 — the repository metadata is non-empty.** Verified empty: `description=''`, `topics=''` via `gh repo view`. The audit's suggestion becomes: a one-line description, and topics `dotnet, csharp, result-pattern, performance, nuget, error-handling, functional-programming`.
- **S3 — the changelog records post-tag work in `[Unreleased]`, not by rewriting the 0.0.1 section.** The 0.0.1 section stays the truth of the tag (`90a0e8a` vintage); CI, branding and packaging metadata landed after the tag and belong to `[Unreleased]` until they ship. An AOT-support entry is added only after S1's run is observed.
- **S4 — the three Result variants hold one consistent surface.** The audit's "lo que comprobaría": Map/Bind/Ensure/Recover/etc. equivalents across `Result`, `Result<T>` and `Result<TSuccess,TError>`; sync and `Task`/`ValueTask` keeping identical semantics; cancellation propagated on every async overload; null delegates with coherent behavior; no accidental nested results from implicit conversions. Each claim becomes a named test in the sweep task, or is already covered — in which case the covering test is named. Opinions do not count as coverage; tests do.
- **S5 — the source generator processes changes incrementally.** Verified: `src/ResulTus.SourceGenerators/ErrorFactoryGenerator.cs:43` calls `.Collect()`, so one changed declaration reprocesses every candidate. The task: a granular pipeline through `IIncrementalGenerator`'s caching (per-candidate equatable nodes), not a rewrite, with before/after behavior pinned by the existing generator tests.
- **S6 — analyzer evolution is gated on false-positive tests.** Before RESULT007+ exists, each current rule (RESULT001–RESULT006) must show a loveable-day negative case: code that *looks* like the pattern but is correct, and must not fire. The audit's premise is accepted: an analyzer that annoys gets disabled.
- **S7 — the benchmark charter publishes what MemoryDiagnoser already computes.** Verified: every benchmark class carries `[MemoryDiagnoser]`, and its output includes Gen0/Gen1/Gen2; `docs/performance.md` tables publish only `Mean | Allocated`. The fix is editorial: publish the generation columns where they carry signal (comparison rows, async pipelines) and leave the zero-allocation proof rows as they are (where they are always 0, noise). No re-benchmarks required for this change.
- **S8 — multi-targeting is a studied decision, not an adoption.** `net8.0` libraries with `net10.0` tests is the current state. Three TFMs triple the CI matrix; the audit says "estudiaría". The study criteria are recorded here: (1) a concrete user demand for net9-consuption of an API that does not exist in net8; (2) a BCL surface worth gating; until either exists, single-TFM ships and the answer to "should we multi-target" is "not yet, and here is the trigger".
- **S9 — API-compatibility testing is a 1.0 prerequisite, not a 0.x chore.** Keep a public-surface contract test (snapshot or `PublicApiAnalyzer`-style baseline) before the first stable version, so "V2 compatible with V1" is enforced by CI, not by intention. Backlog until the 1.0 scope starts.
- **S10 — the refuted item stays refuted in writing.** The audit's claim *"Habíamos hablado de una suite con BenchmarkDotNet, k6 y profiling de GC"* has no record in any repository document, task record or memory: it is an invented prior agreement, corrected here. k6 exercises a running HTTP server — a demo API does not exist and is out of scope for a library. The defensible neighbor (macro benchmark scenarios over async pipelines, in-proc) is kept under T5.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 | inline | done | [run 42653755212](https://github.com/sergteje/ResulTus/actions/runs/42653755212), see L2 |
| T2 | S2, S3 | inline | done | see L2 |
| T3 | S4 | inline | pending | — |
| T4 | S5 | inline | pending | — |
| T5 | S6, S7 | inline | pending | — |
| T6 | S8 | owner | pending | decision criteria, no build |
| T7 | S9 | owner | pending | backlog for 1.0 |

## Log

### L1 — the audit, in short

The owner submitted a first external audit: 13 production packages reviewed, core design endorsed, scores from 9/10 (architecture) to 6.5/10 (publication readiness), and a suggested next-step order. The auditor states they could not build or test the repository. Their framing matches the owner's own bar: "defender cada decisión con código y mediciones". The full verbatim text lives in the owner's message of this session and is not duplicated here.

The audit's one blocking factual error — "no CI yet" — was caused by reading `CHANGELOG.md`'s 0.0.1 `Notes`, which was true when the tag existed and one day later was not. No correction to history is needed; S3 records post-tag work separately.

### L2 — T1 and T2 executed and observed (2026-11-09)

**T2, S2.** Repository metadata set through `gh repo edit`: description *"High-performance Result Pattern library for .NET - inline readonly record struct, zero heap allocation on success and failure."* and topics `csharp, dotnet, error-handling, functional-programming, nuget, performance, result-pattern`, read back from the API.

**T1, S1.** `samples/AotSmoke/` — a console app whose only serializer calls run through the AOT-safe surface of api-completion S7 (`new ResultJsonConverter<int>(SmokeJsonContext.Default.Int32)` with `Write`/`Read` over `Utf8JsonWriter`/`Utf8JsonReader`), exercising the success and failure wire formats, a fixed wire read, and `Match`, exiting non-zero on any assertion. The build itself is a guard: the first attempt used `JsonSerializer.Deserialize<TValue>(string)` and the repo's AOT analyzers rejected it with **IL2026/IL3050** — the smoke app compiles only through the safe surface, which is the discipline S7 asks to demonstrate.

The CI `aot-smoke` job publishes the app twice on `windows-latest` and executes both artifacts:

- **Trimmed**: `/p:PublishTrimmed=true` — also run locally first (exit 0).
- **Native AOT**: `-r win-x64 /p:PublishAot=true` — impossible on this host (no MSVC linker), proven on the runner, which ships it.

Observed: CI run **`42653755212`** — `build-and-test: success`, `aot-smoke: success`. `CHANGELOG.md` records the proof with the run id; README and any public claim of AOT support still wait for a deliberate wording decision by the owner, in line with the repo rule that no support claim ships without observed proof — now there *is* observed proof, but the claim itself is the owner's wording to choose.

Commit: `426d7c4`.
