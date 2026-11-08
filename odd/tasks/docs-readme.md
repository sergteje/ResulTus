# Documentation P3 — README

Objective: give ResulTus a front door a professional reader can evaluate in five minutes: what it is, what it guarantees, how the API reads in real chains, how it measures against the four competitors, and where the honest losses are. Two editions, English canonical and Spanish at parity. Branch: `feature-1`.

Test runner: none for the Markdown itself. The gate is the snippet compile check in S5, plus `dotnet build ResulTus.slnx` and `dotnet test ResulTus.slnx` staying green.

Delivery strategy: `auto-chain`. Forecast ~800 authored changed lines across two files, well over the 400-line review budget, so it lands in three slices: (1) English contract and quick start, (2) fluency showcase and benchmark series, (3) Spanish edition. PR and merge stay the user's call.

Risk: none. No code, no public API, no behavior.

## Specs

- **S1 — the README states what the library is and what it promises.** It opens with one sentence of purpose and then the contract, taken from `docs/performance.md` and never invented: zero heap allocation on the success path **and** on the failure path, `Error` as a 24-byte inline `readonly record struct`, `Result<int>` at 32 bytes, and the generic tier `Result<TSuccess, TError>` at 13 bytes. Every number carries a link to `docs/performance.md`.
- **S2 — quick start with compiling examples.** At minimum: creating a success and a failure, `Match`, `TryGetValue`, one extension pipeline, the generic tier with a custom error type, and one `System.Text.Json` round trip using the converters from `ResulTus.Serialization`.
- **S3 — the package table.** One row per project, each with a single line of purpose: `ResulTus`, `ResulTus.Extensions`, `ResulTus.Collections`, `ResulTus.Logging`, `ResulTus.Serialization`, `ResulTus.Diagnostics`, `ResulTus.AspNetCore`, `ResulTus.HttpClient`, `ResulTus.Validation`, `ResulTus.Validation.FluentValidation`, `ResulTus.Analyzers`, `ResulTus.SourceGenerators`.
- **S4 — differentiators are stated as verifiable facts.** The analyzer rule ids (`RESULT001`–`RESULT006`), the `[ErrorFactory]` source generator (`RESULTGEN001`), the structured logging state, the `Activity`/`DiagnosticSource` diagnostics, and the strict JSON that rejects numeric and unknown `ErrorType` names. Each claim must be traceable to a file in `src/` or to a row in `docs/performance.md`. No superlative that is not backed by a measurement.
- **S5 — every code block compiles.** The snippets are compiled against the built assemblies in a scratch project under `%TEMP%`, never committed, so a reader cannot copy a snippet that does not build. A snippet that does not compile is a defect in the README, not an exception.
- **S6 — artifact rules.** English, Conventional Commit message, no `Co-Authored-By` and no AI attribution. The comparison against ErrorOr, ZeroResult, FluentResults and CSharpFunctionalExtensions carries the verification note from the audit: ErrorOr and ZeroResult were checked against their published READMEs on 2026-11-06; the other two are stated as public-API knowledge.
- **S7 — the API's fluency is demonstrated, not claimed.** A dedicated section shows real chains, each one compiling, that a reader can adapt:
  - a validation pipeline that reads as a sentence: `Map` → `Bind` → `Ensure` → `Recover` → `Tap` → `Match`;
  - an error-transforming chain: `MapError` → `Recover` → `Finally`;
  - the unit `Result` used for a command with no return value;
  - the async chain, showing that `await` composes with the same operators;
  - a collections pipeline: `Traverse` → `Fold` → `Partition` over a real list;
  - the generic tier carrying a domain error type through the same operators.
  Each example carries one sentence of prose saying what it demonstrates. No example exists to show a method exists; each one shows a shape a reader would actually write.
- **S8 — benchmark results are presented as series, with the losses shown.** The README carries the measured surface grouped by series — creation, matching, allocation on the success path, allocation on the failure path, collections, serialization, logging, validation — each series a table with its own measurement stamp (machine, SDK, date) and a link to the matching `docs/performance.md` section. Rules:
  - every number comes from a row that exists in `docs/performance.md`; nothing is quoted from memory, from an older run, or from a different machine;
  - the losses are in the tables, not in a footnote: `Result<int>` is 32 B against 16 B, and array traversal is 1.25x–1.47x slower on the success path. A README that only shows wins is marketing, and this one is documentation;
  - a series that has not been measured yet is absent, never estimated.
- **S9 — a Spanish edition at parity.** `README.es.md` is a full translation, not a stub: same sections, same order, same numbers, same code blocks (identifiers and comments stay in English inside the code). Neutral professional Spanish — no voseo, no regional slang — because this is a public artifact, not a conversation. Both files link to each other at the top. `README.md` stays the canonical edition and the one NuGet points at.
- **S10 — the README documents only shipped features.** P2 was subsequently authorized. Document a P2 feature only after it is implemented and verified; never present a planned or unshipped item (`FailIf`, `Combine` for `Result<T>`, query syntax, multi-error, `StackResult`) as available.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2 | inline | done | — |
| T2 | S3, S4 | inline | done | — |
| T3 | S7 | inline | done | — |
| T4 | S8 | inline | done | — |
| T5 | S9 | inline | done | — |
| T6 | S5, S6, S10 | inline | pending | — |
| T7 | S11 | inline | done | — |

## Log

### L1 — original request (verbatim)

> La fase 11 la vamos a postponer. Antes quiero que veamos todo el tema de rendimiento gc, heap, allocations de toda la libreia completa y tambien si hay cosas que podamos mejorar Span<T>,ConcurrentDictionary, etc etc ver que tienen otras librerias que nosotros no tengamos, en cual destaca la nuestra sobre otra, que tienen otras que podamos implementar y de paso mejorar aqui

### L2 — this document was requested (verbatim)

> Documenta los P0, p1 y p3 para que no se olvide para hacerlo por ODD

### L3 — scope raised by the owner (verbatim)

> En que acabe haremos todos los puntos. El readme tiene que ser muy explicativo, necesitare otro readme en castellano, ademas tenemos que dar ejemplos lo fluida que es la api, encadenando Extensiones etc etc y mostrar los resultados del benchmark en otras series de cosas.
>
> Vamos tiene que ser muy profesional

That message replaced the original scope of this phase. What changed, and why the document was rewritten rather than appended to:

| Was | Now |
| --- | --- |
| One `README.md` | Two editions: `README.md` (English, canonical) and `README.es.md` (full parity) |
| S2 listed one extension pipeline among the quick-start items | S7 makes fluency its own section, with six chains that each demonstrate a shape a reader would write |
| Benchmark numbers were implied by S1 | S8 makes them a first-class section, grouped by series, with the losses shown |
| Forecast ~250 lines | Forecast ~800 lines, three review slices |

The previous S1–S6 kept their numbering and their meaning; S7–S10 are new. Nothing already written was deleted.

### L4 — starting evidence

- `README.md` contains exactly `# ResulTus` and two blank lines. There is no `CONTRIBUTING.md` and no `.github/` directory.
- `Directory.Build.props` already carries the package metadata a README has to agree with: `Description`, `PackageLicenseExpression` MIT, and the repository URL.
- The numbers S1 must quote are in `docs/performance.md` §4.2, §4.5 and §4.6, measured on an Intel Core i7-6700 with .NET SDK 10.0.403.
- The analyzer ids are `RESULT001`–`RESULT006` in `src/ResulTus.Analyzers/DiagnosticDescriptors.cs`; the generator id is `RESULTGEN001` in `src/ResulTus.SourceGenerators`.
- The extension operators S7 must chain live in `src/ResulTus.Extensions/ResultExtensions.cs` (`Map`, `Bind`, `Ensure`, `Recover`, `MapError`, `Tap`, `Switch`, `Finally`) and `src/ResulTus.Extensions/ResultAsyncExtensions.cs` for the async shapes.

### L5 — test-first exception

This phase changes no behavior, so there is no RED/GREEN. The verification is: every snippet compiles against the built assemblies in a throwaway project, `dotnet build ResulTus.slnx` stays at 0 warnings, and `dotnet test ResulTus.slnx` stays green.

### L6 — dependencies and ordering

- **Ordering:** P0 is closed. Complete P1, then P2, the remaining API-completion tasks, and `ResulTus.HttpClient` before writing the final README. T4 (S8) uses only benchmark rows already recorded in `docs/performance.md`; T5 translates the completed English edition; T6 compiles both editions and verifies that every advertised feature has shipped.
- **Language of the Spanish edition is a decision recorded here, not a default left implicit**: neutral professional Spanish, no voseo. The owner writes Rioplatense Spanish in conversation, and that register does not belong in a public README.

### L7 — risk

**Risk: none.** Documentation only. The one real risk is factual: a number or a claim that the charter does not support. S4 and S8 close it by requiring traceability to a row or a file, and T6 verifies it.

### L8 — verification

- Every code block compiles in a scratch project under `%TEMP%` (S5), both editions.
- `dotnet build ResulTus.slnx` — 0 warnings, 0 errors.
- `dotnet test ResulTus.slnx` — green.
- Verify both READMEs advertise only implemented P2 features and include the shipped `ResulTus.HttpClient` package consistently (S3, S10).

### L9 — P2 and HttpClient scope (verbatim)

> vale cuando acabes p1 comienzas p2
> Esto si se hace:El único candidato que investigaría más adelante es ResulTus.HttpClient: convertir respuestas HTTP, ProblemDetails y fallos de transporte en resultados. Pero el mapeo de status, errores y cuerpos JSON es una decisión de política; sin usos reales repetidos, sería una capa opinada de más.
> el recomendado

The final README depends on the P2, API-completion and HttpClient tasks closing before T1–T6 begin. `ResulTus.HttpClient` is a separate optional package with a typed `HttpClientError`; its package-table row and examples are included only after the module ships.

### L10 — owner adds the AGENTS.md refresh to P3 (verbatim)

> Pero hagamos ahora los readme por cierto Agents.md no se ha creado y/o actualizado nunca??

`AGENTS.md` exists — created in an early SDD/openspec phase — and is stale: it describes an `sdd-*` agent roster and an OpenSpec artifact store, while the project now runs ODD with `odd/tasks/*.md` documents and the runtime's actual agents. It also predates the two newest packages and several behavior rules. Authorized as **S11** below and routed T7.

- **S11 — refresh `AGENTS.md` to describe the project as it actually runs.** Keep what is still true (net8.0/net10.0, nullable, warnings as errors, Conventional Commits, MIT, XML documentation required) and rewrite what is not: the ODD workflow with `odd/tasks/*.md` feature documents as the specification artifact, the runtime agents actually used (`gentle-orchestrator` orchestrating, with read-only explore/verify and bounded writer roles), the current package list (the 13 projects in the README's package table, including `ResulTus.Testing`), and the behavior rules that decide how code is written here: 0 B on the success **and** failure path for core operations, cancellation as control flow (`OperationCanceledException` rethrown, never converted into an error), typing via `readonly record struct` for the inline tiers with the documented exception (`HttpClientError`/`HttpProblemDetails` are classes because they own references), and test discipline (public-interface `Given/When/Then` tests, one per rule, plus the compile-the-snippet gate for docs). No process narrative beyond what helps a fresh agent work correctly tomorrow.

### L11 — English edition: draft audited and completed inline (2026-11-08)

A delegated writer produced a draft of `README.md` before its session was cancelled. The owner then instructed to continue without delegation, so the draft was audited line by line against `src/` and `docs/performance.md` rather than trusted. What the audit changed:

| Found | Fix |
| --- | --- |
| Two generic-tier `Match` examples passed `void` lambdas (`Console.WriteLine`) | `Match<TResult>` is the only shape — no `Action` overload exists — so no `TResult` can be inferred from a `void` lambda and the block did not compile. Both now return a string. |
| `Parse` and `ParseAge` had identical bodies | one helper, used by both sections |
| `RESULT002` described as "a value is read without checking" | `UnguardedOutcomeAnalyzer` reports reading a **default value** (`GetValueOrDefault`) without checking; the sentence was corrected |
| Licence link pointed at `blob/feature-1/LICENSE` | relative `LICENSE`, valid after merge |
| Collections readout said `Partition` "allocates its two output arrays" while the table shows 12,480 B | the P0 row grows two lists; the readout now states the P1/T5 cut to 4,048 B and surfaces the two documented losses (array `Traverse`, pooled `Partition` on lists) |
| The emitted factory body was shown as a `csharp` block | it cannot compile as source (it would duplicate the method); it is now an inline code span |

**S5 evidence.** The 13 `csharp` blocks were extracted from the file by regex into a scratch `net10.0` project referencing six `src/` projects plus the source generator as an analyzer, with `TreatWarningsAsErrors=true`: **0 warnings / 0 errors**. The same harness compiled `README.es.md`.

**S8 provenance, series by series.** Creation §4 · Matching §4 · Allocation §4 · Failure path §4.6 · Struct size §4.2 · Array traversal §4.4 · Collections §4 baseline plus the P1/T3–T5 subsections · Serialization §4 · Logging §4 · Validation §4. Every row quoted verbatim; no series estimated, and a series with no recorded rows is absent.

**S10 discipline.** The document mentions no `FailIf`, no query syntax, no multi-error `Error` and no `StackResult`. The Native AOT route is presented as the `JsonTypeInfo<T>` design path, never as a published-AOT measurement.

**Delivery.** Two commits instead of three slices: the English edition is one coherent artifact and the Spanish edition the second. Splitting the English file between "contract" and "fluency" would have served a review a document whose `[Benchmarks](#benchmarks)` links point at a section that does not exist yet.

### L12 — Spanish edition at parity (2026-11-08)

`README.es.md` is a full translation, not a stub. Parity was checked mechanically instead of by reading both files:

- **13 code blocks in each edition, byte-for-byte identical** (identifiers and comments stay English inside the code, as S9 requires).
- **24 headings in each**, same sections in the same order.
- **151 numeric tokens in each, and the two multisets are equal** — no number was rounded, retyped or dropped in translation.
- **The same harness compiled `README.es.md`**: 0 warnings / 0 errors.

Register: neutral professional Spanish, no voseo and no regional slang, per the decision recorded in L6. `README.md` stays canonical and carries `**English** · [Español](README.es.md)` at the top; the Spanish edition mirrors the switcher and points back.

### L13 — `AGENTS.md` rewritten to describe the running project (2026-11-08)

The old file was written in an early SDD/openspec phase: it described an `sdd-*` agent roster and an openspec artifact store that the project no longer uses, and it predated five packages and several behavior rules. It was replaced, not patched.

What the new file states, each claim checked against the repository rather than remembered:

- **Stack and gates** — `Directory.Build.props` sets `net8.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true`, `IsTrimmable=true`, `IsAotCompatible=true`; tests target `net10.0`.
- **Workflow** — ODD with `odd/tasks/<feature>.md` as the specification artifact (header, `## Specs` with verbatim `S#`, `## Tasks`, `## Log` with `L1` verbatim) and the Engram mirror under `odd/<feature>/tasks`.
- **Agents** — the four the runtime actually runs, with the writer's rule that commits stay with the orchestrator.
- **Behavior rules** — 0 B on both paths as a hard rule with time comparative; struct-versus-class decided by reference ownership; the 24 B / 32 B / 13 B sizes as documented decisions; cancellation as control flow; no naked `Value`; the narrow typed tier; no opinionated resilience layer without a real repeated use; no AOT claim without observed proof.
- **Tests** — public-interface Given/When/Then, fakes over mocks, determinism, scratch outside the worktree.
- **Documentation** — both editions at parity and every code block must compile.
- **Benchmarks** — filtered runs by default, `docs/performance.md` as the authority.
- **Source control** — Conventional Commits, no attribution, no push or merge without instruction.

Thirteen `src/` projects confirmed on disk, matching the README package table.
