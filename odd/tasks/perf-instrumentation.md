# Performance P0 — Instrumentation

Objective: give the performance charter an instrument for the 5298 lines it does not measure, starting with the failure-path allocation benchmark that `docs/performance.md` §7 already declares open. Branch: `feature-1`.

Test runner: `dotnet build ResulTus.slnx` and `dotnet test ResulTus.slnx`; benchmarks run with `dotnet run -c Release --project tests/ResulTus.Benchmarks`.

Delivery strategy: `auto-chain`. Forecast ~600 authored changed lines (benchmark classes plus the charter rows). PR and merge stay the user's call.

Risk: none. No file under `src/` is modified and no public API changes.

## Specs

- **S1 — the failure-path allocation benchmark exists.** `AllocationBenchmark` (or a sibling class) measures failure constructed from a **unique, uncached message per iteration** — for example `Error.NotFound("X", $"item {i}")` — for ResulTus, ErrorOr, ZeroResult, FluentResults and CSharpFunctionalExtensions. It reports mean and allocated bytes. It must not reuse a pre-built error: reusing one is exactly the flaw `docs/performance.md` §6 identifies in every existing failure row. The result is consumed (its `Code` or `Message` length accumulated) so the JIT cannot eliminate the work, and the accumulated value is asserted in the benchmark so the loop is provably live.
- **S2 — the satellites get rows.** New benchmarks cover the surfaces that today have none:
  - Collections: `Sequence`, `Traverse`, `Fold`, `Partition`, `FirstFailure`, `Combine` over `Result<T>[]` and `List<Result<T>>`, at **N = 1 000**, reporting mean and allocated. `Combine` is measured over `Result[]` and `List<Result>`, because no `Result<T>` overload exists — that absence is the P2 gap the audit named. See L7 for why this is one size and not three.
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
| T1 | S4, S1 | inline | done | `this commit` |
| T2 | S2 (Collections) | inline | done | `0fbedd7` |
| T3 | S2 (Serialization) | inline | done | `25e8b20` |
| T4 | S2 (Logging, Validation) | inline | done | pending commit |
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

### L6 — T1 done, and what it found

Delivered: `ResulTus.Benchmarks.csproj` references the five projects (S4), `FailurePathAllocationBenchmark` with five rows (S1), registered in `Program.cs`, and a new §4.6 in `docs/performance.md` (S3).

Final numbers, two consecutive `DefaultJob` runs on 2026-11-06, i7-6700, SDK 10.0.403:

| Row | Run A | Run B | Allocated |
| --- | ---: | ---: | ---: |
| ResulTus | 2.807 ns | 2.830 ns | 0 B |
| ErrorOr | 15.159 ns | 14.955 ns | 88 B |
| ZeroResult | 8.859 ns | 8.268 ns | 32 B |
| FluentResults | 50.266 ns | 51.690 ns | 272 B |
| CSharpFunctionalExtensions | 3.033 ns | 3.053 ns | 0 B |

**Deviation from S1, recorded rather than hidden.** S1 asked for the accumulated value to be *asserted* inside the benchmark. That is not how the loop was proven live. The proof is a per-library **sink field**: every row stores its result in an instance field, and `GlobalCleanup` reads all five back. The sink is strictly stronger than an assert for the purpose S1 names — an assert proves the loop ran, the field store proves the object **escaped**, which is what stops the JIT from stack-allocating a class-typed error. The evidence is empirical: with no sink `ZeroResult` reported `0 B`, and with the sink it reports 32 B on the same hardware and SDK. A standalone probe confirms `SingleError.IsValueType == false` and 32 B/op in Debug and Release.

**Two findings the charter now carries.**

- **`CSharpFunctionalExtensions` is allocation-free on the failure path.** Its error is a plain `string`, so it costs 0 B once the message exists. This makes the §7 claim "one object for each of the other four" false, and §6 said so explicitly before this measurement. §4.6, §6 and §7 are corrected in the same commit.
- **`FluentResults.Result.IsFailed` allocates.** It derives from `Errors`, which runs `Reasons.OfType<IError>().ToList()` on every access: 88 B for `IsFailed`, 176 B for `Errors`. The first version of this benchmark consumed the result through `Errors[0]` and reported 448 B instead of the library's true 272 B. The row now reads `Reasons.Count`. A benchmark must consume a competitor's result through an accessor that does not allocate, or it measures its own code.

**Verification.** `dotnet build tests/ResulTus.Benchmarks -c Release` -> 0 warnings, 0 errors. The filtered run exits 0. A filtered run leaves the other five `BenchmarkRunner.Run<T>` calls finding zero benchmarks, which prints "There are no benchmarks found" five times; that is noise in the log, not a failure.

**Next.** T5 — final P0 verification and closure.

### L7 — T2 Collections measured and closed

The user reduced the Collections benchmark to one source size: "un tercio va, y dejalo en el spec". S2 and the benchmark use **N = 1,000**, with both arrays and lists. One full `DefaultJob` run completed all twelve rows on 2026-11-06, Intel Core i7-6700, Windows 10 22H2, SDK 10.0.403 / runtime 10.0.14; the table is in `docs/performance.md` §4.7. `Fold`, `FirstFailure`, and unit `Combine` allocate 0 B for both source shapes. Lists are slower in all six rows, but one run is descriptive, not a precise-ratio claim. `Partition` allocates 12,480 B at this size.

The first full run exposed a harness bug: BenchmarkDotNet calls `GlobalCleanup` once per parameter case, in a separate process, after only that case's one benchmark has run. Cleanup must therefore tolerate the five other sink fields still holding defaults. The initial null dereference made ten rows `NA`; cleanup was changed to handle default tuple arrays, the benchmark project rebuilt cleanly, and the next full run produced all twelve measurements with no `NA`.

The Combine row measures only the existing unit `Result` overload; the absence of `Combine(Result<T>)` is a product gap, not a benchmark defect. That gap is now explicitly authorized for a separate implementation feature and must not be filled in P0.

**Verification:** `dotnet build tests/ResulTus.Benchmarks/ResulTus.Benchmarks.csproj -c Release --nologo` -> 0 warnings, 0 errors. `dotnet run -c Release --project tests/ResulTus.Benchmarks -- --filter "*CollectionOperationBenchmark*"` -> exit 0; all 12 rows measured.

### L8 — T3 Serialization measured

`SerializationOperationBenchmark` records the factory-based converter baseline before P1's cache. It covers success/failure serialize and deserialize for `Result<int>`, both directions for a 1,000-element `Result<int>[]`, and both directions for `Error` and `ErrorType`. The ten rows are in `docs/performance.md` §4.8, measured 2026-11-06 with BenchmarkDotNet 0.15.8, `DefaultJob`, Intel Core i7-6700, Windows 10 22H2, SDK 10.0.403 / runtime 10.0.14. One full run completed with ten rows and no benchmark issues.

Key baseline: deserializing the 1,000-result array takes 159.317 μs and allocates 97,680 B; serializing it takes 85.444 μs and allocates 61,808 B. These totals include array materialization/output JSON and per-element work, so they do not isolate converter lookup. `DeserializeResultSuccess` allocates 0 B.

The first benchmark build failed with IL2026/IL3050 because the project treats AOT/trimming warnings as errors. These calls intentionally exercise the existing reflection-based converter path; a file-scoped suppression now documents that measurement, while AOT compatibility is tested separately in API-completion T6. No production serialization code changed.

**Verification:** initial Release build observed RED (30 IL2026/IL3050 errors); after the benchmark-scoped suppression, `dotnet build tests/ResulTus.Benchmarks/ResulTus.Benchmarks.csproj -c Release --nologo` -> 0 warnings, 0 errors. `dotnet run -c Release --project tests/ResulTus.Benchmarks -- --filter "*SerializationOperationBenchmark*"` -> exit 0; 10 rows, 0 `ERROR` sections, 0 `Benchmarks with issues` sections.

### L9 — T4 Logging and Validation measured

`LoggingOperationBenchmark` uses a capturing logger that stores the structured state reference without formatting. The disabled logging row allocates 0 B at 5.057 ns; enabled logging allocates 64 B at 26.054 ns. `ValidationOperationBenchmark` processes 1,000 issues across ten fields: `ToDictionary` 21.385 μs / 31,624 B and `ToError` 33.04 ns / 80 B. The four rows are in `docs/performance.md` §§4.9–4.10, measured 2026-11-06 with BenchmarkDotNet 0.15.8, `DefaultJob`, i7-6700, SDK 10.0.403 / runtime 10.0.14. Both filtered runs completed with exit 0, two rows each, and no benchmark issues.

**Verification:** `dotnet build tests/ResulTus.Benchmarks/ResulTus.Benchmarks.csproj -c Release --nologo` -> 0 warnings, 0 errors. The Logging and Validation filtered benchmark runs both exited 0; each produced two rows and zero `ERROR` or `Benchmarks with issues` sections.
