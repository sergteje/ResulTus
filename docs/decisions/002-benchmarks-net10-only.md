# Decision 002 — HttpClient macro benchmarks are net10-only

**Status:** **closed 2026-11-11 — net10-only.** The host `tests/ResulTus.Benchmarks` stays on `net10.0`; the macro benchmark is not multi-targeted and does not use a BenchmarkDotNet `RuntimeMatrix`.

**Date opened:** 2026-11-11

**Blocks:** Phase 11, S3/T3 — "BenchmarkDotNet suite runs and reports comparative results", realized as the `HttpClientMacroBenchmark` end-to-end pipeline.

## The question

The HttpClient macro benchmark could have been a .NET runtime matrix (`net8.0`, `net9.0`, `net10.0`) rather than a single-TFM row. The owner chose a single target. This records why.

## Decision in one table

| | net10-only (chosen) | net8/9/10 runtime matrix |
| --- | --- | --- |
| Run cost | 1 process per benchmark method | 3x — one process per method **per runtime** |
| Measured quantity | the shared `HttpClient` pipeline on the target runtime | per-runtime pipeline variance |
| Comparability with the existing series | row-comparable | breaks the "same machine and SDK" rule |
| Signal for a macro (end-to-end) benchmark | sufficient | diluted across runtimes |

## Why the matrix was rejected

1. **Tripled run cost.** A `RuntimeMatrix` spawns one BenchmarkDotNet process per method *per runtime*. For four macro rows that is twelve processes, each re-measuring an async `HttpClient.SendAsync` round trip whose baseline is dominated by the handler chain, not by the JIT tier.
2. **Macro numbers are dominated by the shared `HttpClient` implementation.** The point of this benchmark is the result-producing wrapper's cost on top of `System.Net.Http`, not the JIT's codegen for a result struct. The wrapper is the same IL on every target framework; a runtime matrix would report the BCL's variance, not the library's.
3. **Rows must stay comparable with the existing series.** Every row in [performance.md](../performance.md) §4 is measured on **one** machine and **one** SDK (`10.0.403`), per the charter's reproduction rule ("Compare only runs produced on the same machine and SDK"). Introducing three runtime columns would fork the series and break that invariant for the new rows.

## What net10-only still measures

- The **failure-path allocation column**, which is the number this repo's charter cares about. `HttpClientError` is deliberately a class (it owns an `Exception` and a `ProblemDetails` document), so the failure path allocates by design; the benchmark proves how that compares to the raw `EnsureSuccessStatusCode()` baseline that throws.
- The **success-path parity** against the raw `HttpClient` + `JsonSerializer` idiom, using the same AOT-safe `JsonTypeInfo<T>` route in both camps.

## Decided by / date

Owner, 2026-11-11. Recorded in the phase-11 task (L4) before the writer launched.

## References

- [performance.md](../performance.md) — §2 (allocation is a hard rule), §5 (reproduction rules), and the new `HttpClient macro` section.
- `tests/ResulTus.Benchmarks/HttpClientMacroBenchmark.cs` — the net10-only benchmark.
- [001-result-struct-size.md](001-result-struct-size.md) — the sibling decision that sets the allocation contract this benchmark reports against.
