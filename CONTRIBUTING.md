# Contributing to ResulTus

Thank you for considering a contribution. ResulTus is a high-performance `Result` library for
.NET with a measured, documented design — reading [`docs/performance.md`](docs/performance.md)
and [`README.md`](README.md) first will make every other rule below obvious.

## Prerequisites

- **.NET SDK 11** (`global.json` pins 11.0.x with `rollForward: latestFeature`) — it compiles
  every target framework the libraries multi-target.
- **The .NET 10 runtime** — the test projects target `net10.0`, and an SDK 11 installation does
  not provide that runtime. Installing the .NET 10 SDK alongside 11 (what CI does) covers it.

## Build and test

```bash
dotnet build ResulTus.slnx
dotnet test  ResulTus.slnx
```

Warnings are errors and every public member requires XML documentation, so a green build means
0 warnings and 0 errors. Never run the benchmark project as part of a normal build or test; it
compiles through the solution and executes only when explicitly asked.

## Ground rules that decide how code is written here

- **Allocation is a hard rule, time is comparative.** Core non-materializing operations allocate
  **0 B** on the success path and on the failure path (`docs/performance.md` §2). A slower row is
  a discussion; an allocation regression is a defect.
- **Typing follows ownership.** Inline tiers are `readonly record struct`. A type that owns
  references is a class. Do not "fix" the documented 32 B `Result<T>` — the size is a deliberate
  price for the inline error, reasoned in `docs/decisions/001-result-struct-size.md`.
- **The value is not a naked property.** `Result<T>` exposes `Match`, `TryGetValue`,
  `GetValueOrDefault` and `GetValueOr`, never a `Value` property.
- **Cancellation is control flow, not an error.** `OperationCanceledException` is rethrown and
  never converted into a `Result` failure or an HTTP failure kind.
- **No opinionated layers without a real use.** Retry policies, circuit breakers and similar
  stateful machinery stay out until a repeated real use is named. `Recover` already covers
  fallback.
- **Never claim Native AOT or trimming support without observed proof.** The `JsonTypeInfo<T>`
  overload is the AOT-safe route; publish-and-run proof lives in the CI `aot-smoke` job.

## Tests

- Public-interface tests, one per rule the specification names, written as **Given/When/Then**.
- Exercise the observable effect — output, exit code, persisted data — and reuse existing helpers
  instead of re-implementing shared state.
- Use hand-written fakes and fake `HttpMessageHandler`s rather than mocking frameworks.
- Keep everything deterministic: no real network, no wall-clock dependence (use `TimeProvider`).
- Keep test scratch state outside the worktree.

## The public surface is a contract

The surface of every shipped assembly is snapshotted under
`tests/ResulTus.ApiCompat.Tests/PublicApi` and enforced per commit: a member that is added,
changed or removed without a deliberate snapshot regeneration fails CI. If your change
deliberately alters the public surface, regenerate the snapshots with
`dotnet run --project scripts/ApiSnapshot -- --write` and explain the change in the pull request — the versioning
strategy that governs when this requires a MAJOR bump lives in
[`odd/specs/versioning.md`](odd/specs/versioning.md).

## Coverage

CI enforces a **90% lines per package** gate measured per module
(`scripts/coverage-gate.ps1`, run by the `coverage-gate` job). The analyzers and the source
generator are excluded by design: Roslyn projects earn coverage in their own contract suites.

## Documentation

- `README.md` is canonical and `README.es.md` mirrors it at parity: same sections, same numbers,
  same code blocks, neutral professional Spanish.
- Module guides live in `docs/`, the performance charter in `docs/performance.md`, and durable
  specifications in `odd/specs/`.
- `CHANGELOG.md` records every released version in Keep a Changelog form; a change that ships
  updates it in the same commit.
- **Every code block in a document must compile.** Extract the blocks and build them against the
  real source projects in a throwaway project; a snippet that does not build is a documentation
  defect.
- The API reference site is built by DocFX (`docs/docfx.json`) on every push — the CI `docs` job
  is the proof that it builds without errors.

## Benchmarks

`docs/performance.md` is the authority for every number, stamp and comparison rule. A run
measures **only the rows the change affects** (`--filter "*Class.Method*"`); run the whole suite
only when explicitly asked. Never quote a number from memory, from another machine, or from an
older run, and never present a series that was not measured.

## Source control

- Conventional Commits only — `feat:`, `fix:`, `docs:`, `ci:`, `test:`, `chore:` — with no
  `Co-Authored-By` and no AI attribution.
- Commit in reviewable work units with tests and docs alongside the behavior they describe.
- Changes are merged from pull requests by the maintainers; a change that ships updates the
  changelog in the same commit.

## Found a bug?

Open an issue with the observed behavior, the expected behavior, and a minimal reproduction —
a failing test is the best reproduction.
