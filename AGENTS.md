# ResulTus — Agent Instructions

ResulTus is a high-performance `Result` library for .NET. Success and failure are one inline
`readonly record struct`: zero heap allocation on the success path **and** on the failure path.
Libraries target `net8.0`, `net9.0`, `net10.0` and `net11.0`; test projects target `net10.0` and
`net11.0` (the coverage gate measures on `net10.0`); the analyzers and the source generator target
`netstandard2.0`.

## Build and test

```bash
dotnet build ResulTus.slnx
dotnet test ResulTus.slnx
```

Warnings are errors and every public member requires XML documentation, so a "green" build means
0 warnings and 0 errors. Never run the benchmark project as part of a normal build or test; it
compiles through the solution and executes only when explicitly asked.

## Workflow: ODD

Work runs as Organic Driven Development. `odd/tasks/<feature>.md` is the specification artifact,
not a scratchpad, and it has a fixed shape:

- a header stating objective, branch, delivery strategy and test runner;
- `## Specs` — numbered `S#` requirements that quote the owner's exact strings verbatim;
- `## Tasks` — one row per task with its linked `S#`, route, status and commit;
- `## Log` — `L1` is the owner's original request verbatim; later entries record corrections,
  decisions and observed evidence.

Read the document top through the end of `## Log` before touching code, and report which `S#`
your work covered. On resume, reconcile the file with the Engram mirror under
`odd/<feature>/tasks` before continuing. Small, understood changes stay inline; large ones get
the document and work-unit commits on the feature branch. Push, pull-request creation and merge
always remain the owner's decision.

`odd/specs/` holds the durable specifications that outlive a single feature — today the
`result-core` contract migrated from the retired SDD phase. `docs/` holds guides and measured
numbers instead; do not mix the two.

## Agents

- `gentle-orchestrator` — plans, writes inline by default, owns product decisions and git.
  Delegates only when a trigger truly holds.
- `gentle-ai-explore` — read-only mapping when understanding needs more evidence than one
  bounded batch. Returns path:line evidence, not opinions.
- `gentle-ai-worker` — bounded writer for one independent unit of behavior, with an explicit
  allowed edit surface. Workers leave commits to the orchestrator.
- `gentle-ai-verify` — read-only verification of a change that names a high-risk item, probing
  the specs from the outside.

## Rules that decide how code is written here

- **Allocation is a hard rule, time is comparative.** Core non-materializing operations allocate
  **0 B** on the success path and on the failure path (`docs/performance.md` §2). A slower row is a
  discussion; an allocation regression is a defect.
- **Typing follows ownership.** Inline tiers are `readonly record struct` (`Result`, `Result<T>`,
  `Result<TSuccess, TError>`, `Error`, `ErrorType`). A type that owns references is a class:
  `HttpClientError` and `HttpProblemDetails` own an `Exception` and a document, and
  `ValidationErrors` owns a collection.
- **Measured sizes are documented decisions.** `Error` is 24 B, `Result<int>` is 32 B, the generic
  tier is 13 B with a reference-free error. The 32 B is the deliberate price of the inline error;
  `docs/decisions/001-result-struct-size.md` holds the reasoning. Do not "fix" it into 16 B.
- **Cancellation is control flow, not an error.** `OperationCanceledException` is rethrown and
  never converted into a `Result` failure or an HTTP failure kind.
- **The value is not a naked property.** `Result<T>` exposes `Match`, `TryGetValue`,
  `GetValueOrDefault` and `GetValueOr`, not `Value`. `Error` is a record struct with value
  equality and no reference identity, so assertion libraries must compare by value, never by
  reference.
- **The typed tier is intentionally narrow.** `Result<TSuccess, TError>` exposes `Deconstruct`,
  `IsSuccess`, `IsFailure` and `Match`; the fluent operators come from `ResulTus.Extensions`.
- **No opinionated layers without a real use.** Retry policies, circuit breakers and similar
  stateful machinery stay out until the owner names a repeated real use. `Recover` already covers
  fallback.
- **Never claim Native AOT or trimming support without observed proof.** The `JsonTypeInfo<T>`
  overload is the AOT-safe route; that is a design claim, not a published publish-and-run result.

## Tests

Public-interface tests, one per rule the spec names, written as Given/When/Then. Exercise the
observable effect — output, exit code, persisted data — and reuse existing helpers instead of
re-implementing shared state. Use hand-written fakes and fake `HttpMessageHandler`s rather than
mocking frameworks, and keep everything deterministic: no real network, no wall-clock dependence
(use `TimeProvider`). Test scratch state lives outside the worktree.

## Documentation

`README.md` is canonical and `README.es.md` mirrors it at parity: same sections, same numbers, same
code blocks, neutral professional Spanish. Module guides live in `docs/`, the performance charter in
`docs/performance.md`, and durable specifications in `odd/specs/`. `CHANGELOG.md` records every
released version in Keep a Changelog form; a change that ships updates it in the same commit.
Generated technical artifacts — code, comments, docs, commit messages — are English; conversation
follows the owner's language.

**Every code block in a document must compile.** Extract the blocks and build them against the
real source projects in a throwaway project outside the repository; a snippet that does not build
is a documentation defect, not an exception.

## Benchmarks

`docs/performance.md` is the authority for every number, stamp and comparison rule. A run measures
**only the rows the change affects** (`--filter "*Class.Method*"`); run the whole suite only when
the owner explicitly asks. Never quote a number from memory, from another machine, or from an older
run, and never present a series that was not measured.

## Source control

Conventional Commits, no `Co-Authored-By` and no AI attribution. Commit in reviewable work units
with tests and docs alongside the behavior they describe. Never commit on the owner's behalf
outside an authorized feature branch, and never push, open a pull request or merge without an
explicit instruction.
