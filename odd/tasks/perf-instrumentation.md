# Performance P0 — Instrumentation

Objective: give the performance charter an instrument for the 5298 lines it does not measure, starting with the failure-path allocation benchmark that `docs/performance.md` §7 already declares open. Branch: `feature-1`.

Test runner: `dotnet build ResulTus.slnx` and `dotnet test ResulTus.slnx`; benchmarks run with `dotnet run -c Release --project tests/ResulTus.Benchmarks`.

Delivery strategy: `auto-chain`. Forecast ~600 authored changed lines (benchmark classes plus the charter rows). PR and merge stay the user's call.

Risk: none. No file under `src/` is modified and no public API changes.

## Specs

- **S1 — the failure-path allocation benchmark exists.** `AllocationBenchmark` (or a sibling class) measures failure constructed from a **unique, uncached message per iteration** — for example `Error.NotFound("X", $"item {i}")` — for ResulTus, ErrorOr, ZeroResult, FluentResults and CSharpFunctionalExtensions. It reports mean and allocated bytes. It must not reuse a pre-built error: reusing one is exactly the flaw `docs/performance.md` §6 identifies in every existing failure row. The result is consumed (its `Code` or `Message` length accumulated) so the JIT cannot eliminate the work, and the accumulated value is asserted in the benchmark so the loop is provably live.
- **S2 — the satellites get rows.** New benchmarks cover the surfaces that today have none:
  - Collections: `Sequence`, `Traverse`, `Fold`, `Partition`, `FirstFailure`, `Combine` over `Result<T>[]` and `List<Result<T>>`, at N = 100, 1 000 and 10 000, reporting mean and allocated.
  - Serialization: `JsonSerializer.Serialize` and `Deserialize` for `Result<int>` success and failure, for `Result<int>[]` (this row is what exposes the per-element `GetConverter` cost), for `Error`, and for `ErrorType`.
  - Logging: one failure logged with a capturing `ILogger`, at an enabled and at a disabled level, reporting allocated bytes for each.
  - Validation: `ValidationErrors.ToDictionary` and `ValidationErrors.ToError` over a populated issue list.
- **S3 — every row lands in the charter.** Each benchmark row is added to `docs/performance.md` in the same commit that adds the benchmark, per §8, stamped with the date, the machine and the SDK version, and placed in the section that matches its area.
- **S4 — the benchmark project can see what it measures.** `tests/ResulTus.Benchmarks/ResulTus.Benchmarks.csproj` references `ResulTus`, `ResulTus.Collections`, `ResulTus.Serialization`, `ResulTus.Logging` and `ResulTus.Validation`. It never references a test project.
- **S5 — the run discipline of §5 is honored.** `Release` only, the pinned `DefaultJob`, never two benchmark processes at once, and any suspect row repeated at least twice before a verdict is written. Rows measured on different machines or SDKs are never compared.
- **S6 — P0 is measurement only.** No production behavior changes and no production file is touched. If a benchmark reveals a defect, it is recorded in the charter as a finding and fixed in `odd/tasks/perf-allocations.md`, not here.
- **S7 — no regression.** `dotnet build ResulTus.slnx` with 0 warnings and 0 errors, and `dotnet test ResulTus.slnx` green, including the 449 tests that exist today.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S4, S1 | inline | pending | — |
| T2 | S2 (Collections) | inline | pending | — |
| T3 | S2 (Serialization) | inline | pending | — |
| T4 | S2 (Logging, Validation) | inline | pending | — |
| T5 | S3, S5, S6, S7 | inline | pending | — |

## Log

### L1 — original request (verbatim)

> La fase 11 la vamos a postponer. Antes quiero que veamos todo el tema de rendimiento gc, heap, allocations de toda la libreia completa y tambien si hay cosas que podamos mejorar Span<T>,ConcurrentDictionary, etc etc ver que tienen otras librerias que nosotros no tengamos, en cual destaca la nuestra sobre otra, que tienen otras que podamos implementar y de paso mejorar aqui

### L2 — this document was requested (verbatim)

> Documenta los P0, p1 y p3 para que no se olvide para hacerlo por ODD

P0 is instrumentation. P1 is the allocation fixes and lives in `odd/tasks/perf-allocations.md`. P3 is the README and lives in `odd/tasks/docs-readme.md`. The P2 feature gaps from the audit are deliberately **not** documented: they are a product decision the owner has not taken yet.

### L3 — starting evidence

- `tests/ResulTus.Benchmarks/ResulTus.Benchmarks.csproj:13` references only `..\..\src\ResulTus\ResulTus.csproj`. The benchmark project measures the core and nothing else.
- Measured surface today: creation (`ResultCreationBenchmark`), match (`ResultMatchBenchmark`), allocation (`AllocationBenchmark`), array traversal (`ArrayTraversalBenchmark`), generic-tier traversal (`GenericTierTraversalBenchmark`). Five classes, all against the core.
- Unmeasured surface: `Extensions` 1120 lines, `Logging` 713, `Collections` 589, `Serialization` 558, `Diagnostics` 358, `AspNetCore` 325, `Validation` 184 + `Validation.FluentValidation` 125. Total 5298 of the library's 5992 lines.
- `docs/performance.md` §7 lists "**Failure-path allocation vs competitors** — Open, high value" and states that every existing failure row reuses a pre-built error, which hides the allocation a class-based error costs. That is the first row this phase adds, and the charter already authorizes it.
- `docs/performance.md` §8 requires a new benchmark row to be added to the document in the same commit that adds the benchmark.

### L4 — test-first exception

Benchmarks are not behavior tests, so the RED/GREEN discipline does not apply in its usual shape. The honest statement of RED here is: **the row does not exist in the charter and the number cannot be produced**. A task in this phase is done when the benchmark compiles, runs under the §5 discipline, and its row is in `docs/performance.md` with a real measurement. Any benchmark that produces `0.0000 ns` with `StdDev = 0` is recorded as eliminated code, not as a result, exactly as §4.1 Finding 3 requires.

### L5 — scope boundary

This phase does not fix anything it measures. Findings go into the charter's §7 table and, when they are already named in `odd/tasks/perf-allocations.md`, become that document's evidence. Adding a fix here would make the measurement and the change land in one commit, which is precisely what makes a regression unattributable later.
