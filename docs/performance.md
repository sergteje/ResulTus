# Performance Charter

ResulTus exists to be the **fastest zero-allocation result type in .NET**. That is not a marketing line — it is an acceptance criterion. This document defines what "fastest" means in a way that can be measured, reviewed, and enforced.

> **North star:** ResulTus must not lose a single benchmark against any library in the comparison set.

---

## 1. Comparison set

| Library | Why it is here |
| --- | --- |
| [ZeroResult](https://www.nuget.org/packages/ZeroResult) | Closest design — `readonly record struct` result, but its `SingleError` error type is a **class**, so its failure path allocates unless the caller reuses a cached instance. The real adversary. |
| [ErrorOr](https://www.nuget.org/packages/ErrorOr) | Popular, allocation-free on success. |
| [CSharpFunctionalExtensions](https://www.nuget.org/packages/CSharpFunctionalExtensions) | Popular functional result type. |
| [FluentResults](https://www.nuget.org/packages/FluentResults) | Class-based reference point. Baseline for "what we must never become". |

## 2. Definition of victory

A benchmark row is judged on **mean time** and **allocation**. Three outcomes are possible:

| Outcome | Rule | Consequence |
| --- | --- | --- |
| **Win** | Strictly lowest mean, difference resolvable | Ship it |
| **Tie** | Within the noise floor of the lowest mean | Acceptable — counts as not losing |
| **Loss** | Slower than the lowest mean, outside the noise floor | **Defect.** Tracked, explained, or fixed |

### Noise floor

BenchmarkDotNet reports a 99.9% confidence interval. Below that threshold a difference is not a fact, it is a coin flip.

- Both means `< 1.0 ns` → **always a tie.** At sub-nanosecond scale the JIT has eliminated the work and the stopwatch is measuring its own loop overhead. Do not chase these.
- Either mean `>= 1.0 ns` → the gap is a candidate, but **only counts if the confidence intervals do not overlap AND the result reproduces in a second run**. Measured evidence (2026-10-27): a non-overlapping 33% gap in one run vanished in the next, on the same machine, SDK, and package. One run proves nothing.
- Secondary check: a `MultimodalDistribution` warning invalidates the row's mean until the bimodality is explained.
- Tertiary check: a row that collapses to `0.0000 ns` with `StdDev = 0` is **JIT elimination**, not a speedup. Report it as unmeasurable, never as a win.

### Allocation — hard rule, no tolerance

`0 B` on **both** the success and the failure path of every core operation. This is not a comparison, it is a floor. A competitor allocating where we allocate nothing is a marketing win; us allocating where a competitor does not is a defect.

## 3. Fair comparison rule

Benchmarks must compare **equivalent semantics**. When a competitor's default type is structurally cheaper than ours, both readings are recorded:

- **As-used** — default type of each library, as a user would write it.
- **Fair** — same error payload richness on both sides.

A loss in *as-used* with a win in *fair* is a documentation task, not an engineering task. A loss in *fair* is an engineering task.

> Current example: ResulTus `Result<int>` is **32 B** — a 24-byte `Error` inline (code, message, type) plus the payload and a flag. ZeroResult's `Result<int, SingleError>` is **16 B**, but those 16 bytes hold a **reference to a class**, not an inline error: `SingleError` is a class. ErrorOr's 16 B holds a `List<Error>`, and `ErrorOr.Error` itself is 32 B. These are not the same type. The comparison is still valid, but the reason for a gap must be stated, never hand-waved.

## 4. Baseline

Machine: Windows, .NET SDK 10.0.403, `Release`, `DefaultJob`, `MemoryDiagnoser`.
Benchmarks: `tests/ResulTus.Benchmarks/`.

### Result creation — measured 2026-10-29

Post-B1: `Error` is 24 B and `Result<int>` is 32 B, zero allocation on both paths.

| Method | Mean | Allocated |
| --- | ---: | ---: |
| **ResulTus_Create_Success** | 0.0118 ns | **0 B** |
| **ResulTus_Create_Failure** | 0.0192 ns | **0 B** |
| ResulTus_Create_Implicit_From_Value | 0.0130 ns | 0 B |
| ResulTus_Create_Implicit_From_Error | 0.1153 ns | 0 B |
| ZeroResult_Create_Success | 0.0150 ns | 0 B |
| ZeroResult_Create_Failure | 0.3182 ns | 0 B |
| CSharpFunctionalExtensions_Create_Success | 2.2914 ns | 0 B |
| CSharpFunctionalExtensions_Create_Failure | 0.0544 ns | 0 B |
| ErrorOr_Create_Success | 0.1295 ns | 0 B |
| ErrorOr_Create_Failure | 11.6516 ns | **88 B** |
| FluentResults_Create_Success | 18.7471 ns | **112 B** |
| FluentResults_Create_Failure | 40.6202 ns | **240 B** |

**Verdict:** every row below 1 ns is a tie by the noise-floor rule — that includes CSharpFunctionalExtensions' `0.0544 ns` failure, which is not a loss we can measure. The meaningful result is `Create_Failure`: **0.0192 ns and 0 B** against ErrorOr's `11.65 ns` + **88 B** and FluentResults' `40.62 ns` + **240 B**, i.e. **607x** and **2116x** faster with no allocation while they allocate an error object. ZeroResult's `0.3182 ns` is also zero-allocation; the gap is real in direction but is not the story — the story is that every competitor except ZeroResult must allocate to report a failure.

### Result matching — measured 2026-10-29

| Method | Mean | Allocated |
| --- | ---: | ---: |
| **ResulTus_Match_Success** | 0.9235 ns | **0 B** |
| **ResulTus_Match_Failure** | 1.3834 ns | **0 B** |
| ErrorOr_Match_Success | **0.7175 ns** | 0 B |
| ErrorOr_Match_Failure | **0.5482 ns** | 0 B |
| ZeroResult_Match_Success | 0.9391 ns | 0 B |
| ZeroResult_Match_Failure | **0.7818 ns** | 0 B |
| CSharpFunctionalExtensions_Match_Success | 1.9599 ns | 0 B |
| CSharpFunctionalExtensions_Match_Failure | 1.8911 ns | 0 B |
| FluentResults_Match_Success | 21.4166 ns | **96 B** |
| FluentResults_Match_Failure | 19.0831 ns | **88 B** |

**Verdict:** the settled wins are structural, not nanosecond-sized.

- **CSharpFunctionalExtensions** — **2.1x / 1.4x** faster (success / failure).
- **FluentResults** — **23x / 14x** faster, and it allocates 96 B / 88 B while we allocate nothing.
- **ZeroResult — tie on success, loss on failure.** `0.9235 vs 0.9391` success is a tie. `1.3834 vs 0.7818` failure is **1.8x slower**, and it is **not reproducible**: the 2026-10-27 run measured our own failure path at `0.9095 ns`, a 52% swing across runs on the same machine, SDK and package set. §4.1, Finding 4 applies: this hardware cannot resolve it, and one run is an anecdote. Do not publish a failure-path verdict until a repeat agrees.
- **ErrorOr — still contested.** `0.7175` versus our `0.9235` on success and `0.5482` versus `1.3834` on failure. ErrorOr has now been faster in every run, which is more consistent than the 2026-10-27 picture, but the magnitude swung from 33% to 250% on the failure path, so the number is unstable. Treat it as *unresolved with ErrorOr ahead*, not as a documented loss.

### 4.1 Spike — struct size and delegate dispatch (2026-10-27)

Self-contained experiment replicating the layouts, plus a real `ErrorOr` and `ZeroResult` baseline in the same process.

| Variant | Mean | Error | Struct |
| --- | ---: | ---: | ---: |
| Rich `Error` + delegate (success) | 1.2014 ns | ±0.0623 | **40 B** |
| Rich `Error` + delegate (failure) | 1.1310 ns | ±0.0604 | 40 B |
| Tiny `Error` + delegate (success) | 1.0064 ns | ±0.0458 | **16 B** |
| Tiny `Error` + delegate (failure) | 1.0271 ns | ±0.0211 | 16 B |
| Rich `Error` + static dispatch (success) | **0.0000 ns** | 0 | 40 B |
| Rich `Error` + static dispatch (failure) | 0.0079 ns | ±0.0073 | 40 B |
| Rich `Error` + `TryGetValue` branch (success) | **0.0000 ns** | 0 | 40 B |
| ZeroResult (success) | 1.0167 ns | ±0.0513 | 16 B |
| ZeroResult (failure) | 1.0798 ns | ±0.0431 | 16 B |
| ErrorOr (success) | 1.0743 ns | ±0.0346 | 16 B |
| ErrorOr (failure) | 0.8432 ns | ±0.0526 | 16 B |

**Finding 1 — `[StructLayout(Pack = 1)]` is a no-op on `Error`.**

Measured managed sizes: `Error` = **32 B**, `Result<int>` = **40 B**. Not 25 and 30.

`Error` holds three reference fields (`_code`, `_message`, `_metadata`). The GC requires object references to stay pointer-aligned, so the runtime relaxes `Pack = 1` and realigns. Measured proof: a rich-`Error` result is **40 B both with `Pack = 1` and with natural alignment**. The attribute does nothing here. The earlier "25 B / 30 B" figures were layout arithmetic, never the actual managed size.

**Finding 2 — struct size costs ~0.2 ns, at the edge of measurability.**

40 B → 16 B takes success from `1.2014` to `1.0064` ns. CIs do not overlap, so the effect is real in direction, but ~0.2 ns is the same order as the run-to-run variance below.

**Finding 3 — the delegate *is* the entire measurable cost of `Match`.**

Both delegate-free variants collapsed to `0.0000 ns` with `StdDev = 0`. Without a `Func<>` barrier the JIT proves the result constant and hoists the whole operation out of the loop. This proves the indirect call is what `Match` costs — but it also means the delegate-free variants **cannot be measured**; the number is an artifact, not a speedup. Option C (a static-dispatch `Match` API) is therefore unjustifiable on performance grounds.

**Finding 4 — the Match numbers do not reproduce across runs.**

| Row | Run B (21:15) | Spike (22:19) | Delta |
| --- | ---: | ---: | ---: |
| `ErrorOr_Match_Success` | 0.6694 ns | 1.0743 ns | **+60%** |
| `ResulTus_Match_Success` | 0.9879 ns | 1.2014 ns | +22% |

Same machine, same SDK 10.0.403, same `DefaultJob`, same `ErrorOr` package. The spread between runs (0.2–0.4 ns) **exceeds the effect being measured** (0.2 ns). This hardware — an i7-6700, 4 physical cores, `StdDev` of 0.16 ns on a 1.2 ns mean — cannot resolve fractional-nanosecond differences in `Match`. Any Match verdict at this scale is noise until proven otherwise.

**Consequence:** the `Match` column is a **tie across all 16-byte competitors** on this hardware. The reproducible facts are the allocation results and the struct sizes.

### 4.2 Struct size is a design problem, not a `Match` problem — resolved 2026-10-28

`Result<int>` was **40 B** because of `Error`'s 32-byte inline payload. Decision 001 closed it.

The owner chose **zero heap allocation on both paths as the hard contract promise**, which mathematically rules out the 16-byte layout: reaching 16 B requires the error behind a single reference, and building that reference allocates on the failure path. Zero allocation requires the error stored inline, which floors the type at 32 B. The two goals cannot coexist in one type, and the contract picked allocation.

The chosen option was **B1**: remove the untyped `Metadata` dictionary from `Error` (32 B → **24 B**), taking `Result<int>` from 40 B to **32 B** with zero allocation on both paths preserved. 32 B puts exactly two results in a 64-byte cache line with no straddling.

The 16-byte-and-zero-allocation combination lives in the **generic tier**, which measures **13 B** with an 8-byte struct error — tighter than every competitor's 16 B. **No competitor combines 16 B with an inline error:** `ZeroResult` reaches 16 B by putting a *class* behind a reference, `ErrorOr` by putting a `List<Error>` behind one, and `CSharpFunctionalExtensions.Result<int>` by putting its error behind one. Measured 2026-10-29:

| Element type | Kind | `SizeOf` | Array stride |
| --- | --- | ---: | ---: |
| `ResulTus.Result<int>` | struct | 32 | 32 |
| `ResulTus.Error` | struct | 24 | 24 |
| `ResulTus.Result<int, EightByteError>` | struct | **13** | 13 |
| `ErrorOr.ErrorOr<int>` | struct | 16 | 16 |
| `ErrorOr.Error` | struct | 32 | 32 |
| `ZeroResult.Result<int, SingleError>` | struct | 16 | 16 |
| `ZeroResult.Errors.SingleError` | **class** | 8 | 8 |
| `FluentResults.Result<int>` | **class** | 8 | 8 |
| `CSharpFunctionalExtensions.Result<int>` | struct | 16 | 16 |

That table is the honest form of the size claim: we are the only one that keeps a **rich error inline**, and the price is 32 B against their 16.

### 4.3 Array traversal — 32 B versus 40 B, measured 2026-10-28

Self-contained spike, throwaway replicas, BenchmarkDotNet 0.15.8, `Release`, Intel i7-6700, .NET 10.0.14.

| N | Array size 32 B / 40 B | 32 B ns/elem | 40 B ns/elem | Ratio 40/32 | Established? |
| ---: | --- | ---: | ---: | ---: | --- |
| 5 000 | 156 KiB / 195 KiB — both fit L2 | 0.7251–0.7376 | 0.7242–0.7598 | 0.982 / 1.048 | **No** — straddles 1.0 |
| 7 000 | 219 KiB / **273 KiB** — 40 B crosses 256 KiB L2 | 0.7113–0.7479 | 0.7420–0.7707 | **1.023–1.043, mean 1.035** | **Yes, marginally** — 5/5 suites same sign |
| 10 000 | 312 KiB / 391 KiB — both exceed L2 | 0.7779–0.8409 | 0.8218–0.8356 | 1.056 / 0.994 | **No** — bimodal |

**The 25% cache-line prediction was correct as traffic and wrong as time.** 40 B does fetch 1.25x more lines (1.6 elements per line versus 2.0, half of them straddling). But at ~0.74 ns/element the loop is issue-limited on the load–compare–add chain, and the hardware prefetcher absorbs the extra lines ahead of demand. The measured effect is **~3.5%, at the L2 boundary only** — roughly 1.5σ in a single run, trustworthy only because five independent suites agreed in sign.

**Verdict: the 8 bytes are a design win, not a performance win.** Do not quote 25%, and do not sell 32 B as a performance feature. The case for B1 rests on removing an `object?`-based dictionary from a library whose charter is zero allocation — that dictionary boxes value types, which is an allocation hole on the failure path.

### 4.4 Array traversal against competitors — measured 2026-10-29

`ArrayTraversalBenchmark`, `DefaultJob`, one fully populated array per row, checksum verified in `Setup` so every library does the same useful work. Success path, ns per whole array:

| N | ResulTus 32 B | ErrorOr 16 B | ZeroResult 16 B | CSharpFunctionalExtensions 16 B | FluentResults 8 B (class) |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 000 | 804.9 | **644.4** | **551.6** | **546.7** | 20,806.2 — 96 KB allocated |
| 7 000 | 5,711.2 | **4,239.4** | **4,506.9** | **4,221.4** | 159,103.7 — 672 KB allocated |
| 10 000 | 8,886.7 | **6,526.1** | **6,178.2** | **6,338.7** | 250,669.9 — 960 KB allocated |

Failure path (ResulTus counts failures, the others likewise; FluentResults has no equivalent row):

| N | ResulTus 32 B | ErrorOr 16 B | ZeroResult 16 B |
| ---: | ---: | ---: | ---: |
| 1 000 | 629.0 | **585.0** | **547.9** |
| 7 000 | 4,574.8 | **3,983.0** | **3,979.4** |
| 10 000 | 6,848.1 | **5,968.0** | **5,441.7** |

**Verdict: a documented structural loss, not a mystery.** On the success path we are **1.25x–1.47x slower per element** than every 16-byte competitor, and the ordering is stable across all three sizes. The cause is the layout, and §4.3 already proved the mechanism: at 32 B only two elements fit a 64-byte cache line, against four at 16 B, so the loop issues roughly twice as many line requests for the same array. That is the deliberate price of keeping a rich `Error` inline — the same 32 B that buys 0 B on both paths.

Two things this run does **not** say. It does not contradict §4.3: the 32-vs-40 comparison measured the effect of 8 bytes at the L2 boundary, and it stays true. And it does not make the failure path equally bad: there we are only **1.08x–1.26x** slower, because the failure loop touches one flag per element and never reads the error payload.

FluentResults is the exception that proves the point: at 8 B per element it should be the fastest, and it is **25x–28x slower**, because each element is a heap object it also allocates by the kilobyte.

> Run note: these rows come from the 2026-10-29 run. The four classes were measured by two sequential processes (traversal finished 12:52, creation/match/allocation 13:04–13:17); no benchmark process overlapped another. A benchmark host from an earlier attempt was found hung on a closed output pipe and terminated before this measurement — it never ran concurrently with a recorded run, but it is why the Release binaries had to be released before rebuilding.

### 4.5 Generic tier — packed 13 B versus aligned 16 B, measured 2026-10-29

`GenericTierTraversalBenchmark` measures one build, so the same code was built twice: with `[StructLayout(Pack = 1)]` on `Result<TSuccess, TError>` (13 B stride) and without it (16 B aligned). Two full runs per layout, `DefaultJob`, machine idle, layout confirmed by `Unsafe.SizeOf` in each run (`13/13` versus `16/16`).

| Method | N | Packed 13 B | Aligned 16 B | Aligned vs packed |
| --- | ---: | ---: | ---: | ---: |
| Success | 1 000 | 616.4 / 618.3 ns | 601.2 / 602.4 ns | **−2.5%** |
| Failure | 1 000 | 578.4 / 586.5 ns | 594.2 / 588.5 ns | +1.5% |
| Success | 7 000 | 4 946.1 / 4 902.5 ns | 5 079.5 / 5 265.1 ns | +5.0% |
| Failure | 7 000 | 3 993.1 / 4 088.4 ns | 4 104.8 / 4 183.0 ns | +2.6% |

**Verdict: no resolvable difference; the smaller stride wins.** The aligned build wins one cell out of four and loses the rest. The cell it wins hardest contains a within-layout spread of 3.6% (5 079.5 versus 5 265.1 on the same build), larger than the between-layout effect. The two cells that separate cleanly are both failure paths, and both favour 13 B. Nothing here is a performance argument for 13 B — it is the evidence that 16 B is not worth paying for, and 13 B keeps the only sub-16-byte result type in the comparison set. **The pack stays** (Decision 001).

### Allocation — measured 2026-10-29

| Method | Mean | Allocated |
| --- | ---: | ---: |
| Error_NotFound_Allocation | 0.0031 ns | 0 B |
| **ResulTus_Success_Allocation** | 0.0187 ns | **0 B** |
| **ResulTus_Failure_Allocation** | 0.0000 ns | **0 B** |
| ErrorOr_Success_Allocation | 0.0109 ns | 0 B |
| ZeroResult_Success_Allocation | 0.0000 ns | 0 B |
| ZeroResult_Failure_Allocation | 0.2633 ns | 0 B |
| FluentResults_Success_Allocation | 16.8744 ns | **112 B** |

**Verdict:** every row except FluentResults is below 1 ns and several collapsed to `0.0000 ns`, meaning the JIT eliminated the work — which is exactly the result we want, and exactly why the nanosecond column is not a ranking. All are **ties** by the noise-floor rule.

The allocation rule itself holds: **ResulTus allocates 0 B on success and on failure.** Do not quote a speed ratio against `ZeroResult_Failure_Allocation` from this run: it measured `0.0000` for us, so there is no difference left to divide. The 2026-10-27 figure of `6.5x` is retired.

### Failure path from a unique message — measured 2026-11-06

Every row above reuses one pre-built error, so all of them answer only "build a result from an error that already exists". None of them can show what building the error itself costs, which is the whole question for a library whose competitor stores its error as a class. This section answers it: `FailurePathAllocationBenchmark` constructs the failure from a **distinct message on every invocation**, cycling a 1024-message pool materialized in `GlobalSetup`. The pool is built once, so the message string is paid identically by every row and excluded from the comparison, and no library can serve the call from a cache.

Each result is stored in a per-library instance field and read back in `GlobalCleanup`. Without that store the JIT proves the error never escapes and stack-allocates it: the first version of this benchmark reported `0 B` for `ZeroResult`, whose `SingleError` is a class, while a standalone probe measured 32 B per call in both Debug and Release. The sink is what makes the row honest, and it is why this table's nanoseconds are **not** comparable to the creation rows above — every row here pays a field store those rows do not.

Measured 2026-11-06, Intel Core i7-6700, .NET SDK 10.0.403, BenchmarkDotNet 0.15.8, `DefaultJob`, two consecutive runs:

| Method | Mean run A | Mean run B | Allocated | Ratio |
| --- | ---: | ---: | ---: | ---: |
| **ResulTus_Failure_Unique_Message** | 2.807 ns | 2.830 ns | **0 B** | 1.00 |
| ErrorOr_Failure_Unique_Message | 15.159 ns | 14.955 ns | **88 B** | 5.3x |
| ZeroResult_Failure_Unique_Message | 8.859 ns | 8.268 ns | **32 B** | 3.0x |
| FluentResults_Failure_Unique_Message | 50.266 ns | 51.690 ns | **272 B** | 18x |
| CSharpFunctionalExtensions_Failure_Unique_Message | 3.033 ns | 3.053 ns | **0 B** | 1.08x |

**Verdict: ResulTus wins on allocation against three of four and ties the fourth.** `ZeroResult` allocates a 32 B `SingleError` class, `ErrorOr` an 88 B `List<Error>`, and `FluentResults` 272 B spread across its result object, its reason list and its error. **`CSharpFunctionalExtensions` allocates nothing**, because its error is a plain `string` — the message the caller had already built. That is a real tie on this axis, not a measurement artifact, and it corrects the pre-2026-11-06 assumption in §7 that every competitor pays one object per failure.

The time column separates from `CSharpFunctionalExtensions` by 8% (2.81–2.83 ns against 3.03–3.05 ns), which is only 0.22 ns. By the §2 rule it counts — the confidence intervals do not overlap in either run and the sign reproduces — but the margin is small enough that it should be read as "we are not behind", not as a selling point. Against the other three the win is 3.0x–18x and stable across both runs.

**Fair-comparison note (§3).** `CSharpFunctionalExtensions.Result<T>` can hold a message and nothing else: no code, no category. ResulTus carries `Code`, `Message` and `Type` inline for the same 0 B, so this is not a comparison of equal payloads — it is the as-used reading, and ResulTus wins it on payload at equal allocation.

**Two measurement traps, recorded so they are not repeated.**

- **A consumer can allocate on a competitor's behalf.** `FluentResults.Result.IsFailed` is not a field: it derives from `Errors`, which runs `Reasons.OfType<IError>().ToList()` on every access. Consuming the result through `IsFailed` added 88 B to this row and `Errors` added 176 B — 448 B total in the first version, all of it the benchmark's cost rather than the library's. The row now reads `Reasons.Count`, which is a stored list, and lands on the 272 B that a standalone probe measures for `Result.Fail<int>(message)` alone.
- **A `0 B` row for a class-typed error is not evidence of anything until the sink exists.** See above: `ZeroResult` reported `0 B` with no sink and 32 B with one, on the same hardware and SDK.

### Collections — array versus list at N = 1,000 — measured 2026-11-06

`CollectionOperationBenchmark` measures six operations over fully successful inputs, using the same 1,000 elements for each row. Each benchmark stores its result in an instance sink that `GlobalCleanup` reads. BenchmarkDotNet runs each parameter case in its own process, so cleanup tolerates the other five sinks remaining at their default values.

BenchmarkDotNet 0.15.8, `DefaultJob`, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14. One complete run:

| Method | Source | Mean | Allocated |
| --- | --- | ---: | ---: |
| Sequence | Array | 4.121 μs | 4,056 B |
| Traverse | Array | 2.060 μs | 4,024 B |
| Fold | Array | 1.128 μs | 0 B |
| FirstFailure | Array | 863.2 ns | 0 B |
| Partition | Array | 3.095 μs | 12,480 B |
| Combine (unit `Result`) | Array | 845.5 ns | 0 B |
| Sequence | List | 7.800 μs | 4,088 B |
| Traverse | List | 3.574 μs | 4,064 B |
| Fold | List | 1.566 μs | 0 B |
| FirstFailure | List | 1.155 μs | 0 B |
| Partition | List | 3.326 μs | 12,480 B |
| Combine (unit `Result`) | List | 1.221 μs | 0 B |

**Readout:** `Fold`, `FirstFailure` and unit `Combine` allocate nothing for either source. `List<T>` is slower than `T[]` in all six rows; this is consistent with the interface-enumerator overhead, but the single run is descriptive rather than a claim about a precise ratio. `Partition` allocates the two output arrays plus its growing lists. `Combine` in this table is the element-wise unit-result operation measured during P0. The binary value-bearing overload was added later, in P2/T1, and is not a row here: it returns a struct, so its zero-allocation floor on the success and both failure paths is pinned by tests in `tests/ResulTus.Extensions.Tests/CombineContractTests.cs` rather than by a benchmark.

**Scope:** one size only, as requested. The per-call allocations are not a scaling experiment; deterministic allocation tests in P1 cover the collection-size question.

#### P1/T3 — array and read-only-list overloads — remeasured 2026-11-06

The new indexed overloads were measured with the same BenchmarkDotNet 0.15.8 `DefaultJob`, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14. One complete run produced 14 rows, including an explicit `TraverseEnumerable` control that forces the unchanged `IEnumerable<T>` path.

| Method | Source | P0 mean / allocated | P1/T3 mean / allocated |
| --- | --- | ---: | ---: |
| Sequence | Array | 4.121 μs / 4,056 B | 1.289 μs / 4,024 B |
| Traverse | Array | 2.060 μs / 4,024 B | 7.447 μs / 4,024 B |
| Fold | Array | 1.128 μs / 0 B | 833.8 ns / 0 B |
| FirstFailure | Array | 863.2 ns / 0 B | 668.9 ns / 0 B |
| Partition | Array | 3.095 μs / 12,480 B | 2.858 μs / 12,480 B |
| Combine (unit `Result`) | Array | 845.5 ns / 0 B | 867.5 ns / 0 B |
| Sequence | List | 7.800 μs / 4,088 B | 1.824 μs / 4,024 B |
| Traverse | List | 3.574 μs / 4,064 B | 1.342 μs / 4,024 B |
| Fold | List | 1.566 μs / 0 B | 1.527 μs / 0 B |
| FirstFailure | List | 1.155 μs / 0 B | 1.184 μs / 0 B |
| Partition | List | 3.326 μs / 12,480 B | 3.150 μs / 12,480 B |
| Combine (unit `Result`) | List | 1.221 μs / 0 B | 1.161 μs / 0 B |
| TraverseEnumerable control | Array | — | 3.055 μs / 4,056 B |
| TraverseEnumerable control | List | — | 3.726 μs / 4,064 B |

**Deferred performance follow-up:** the array-specific `Traverse` overload is 7.447 μs in this run versus 3.055 μs for the `IEnumerable` control in the same run, while saving 32 B. The direct overload meets the output-array-only allocation test but loses on time. The owner explicitly asked to leave this investigation for another occasion and continue the remaining P1 tasks. Do not claim a speedup; keep this discrepancy visible in §7. The list overload improves both time and allocation. All non-materializing array/list operations measured 0 B; `Partition` remains at 12,480 B until P1/T5.

#### P1/T4 — unit-result array and read-only-list overloads — remeasured 2026-11-07

T4 adds the indexed overloads for `Result[]` and `IReadOnlyList<Result>`, so only the unit-row `Combine` changes behavior. The benchmark also re-ran the other six methods; their means stayed inside run-to-run variation and are not restated here. Same BenchmarkDotNet 0.15.8 `DefaultJob`, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14, one complete run:

| Method | Source | P1/T3 mean / allocated | P1/T4 mean / allocated |
| --- | --- | ---: | ---: |
| Combine (unit `Result`) | Array | 867.5 ns / 0 B | 682.0 ns / 0 B |
| Combine (unit `Result`) | List | 1.161 μs / 0 B | 1,208.2 ns / 0 B |

**Readout:** both sources stay at 0 B, which is the point of the change. The array row moved 21% faster and the list row 4% slower; both are single-run comparisons against a different run, and §2 requires a non-overlapping confidence interval plus a reproducing second run before a difference counts as a fact, so no speed claim is made here. The `IEnumerable<T>` overloads are untouched and remain the control.

#### P1/T5 — `Partition` pooled buffers — remeasured 2026-11-07

`Partition` now rents both buffers and copies into exactly sized arrays instead of growing two `List<T>`. Two post-change samples were taken with the same BenchmarkDotNet 0.15.8 `DefaultJob`, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14: one full 14-row collection run and one filtered run containing only these two rows.

| Method | Source | Pre-change (two runs) | Post-change (two runs) |
| --- | --- | ---: | ---: |
| Partition | Array | 2.858 μs / 12,480 B · 3.930 μs / 12,480 B | 3.756 μs / 4,048 B · 4.131 μs / 3.95 KB |
| Partition | List | 3.150 μs / 12,480 B · 3.409 μs / 12,480 B | 4.456 μs / 4,048 B · 4.557 μs / 3.95 KB |

**Readout — allocation wins, list time loses.** Allocation drops 67% on both sources, from 12,480 B to the two exactly sized output arrays, and stays at that level across both samples. Time does not follow:

- **Array: no resolvable difference.** The two post-change means (3.756, 4.131 μs) bracket the second pre-change mean (3.930 μs) and their intervals overlap it. No claim either way.
- **List: a loss, reproduced.** Both post-change means (4.456 ± 0.021, 4.557 ± 0.061 μs) sit outside the intervals of both pre-change means (3.150, 3.409 ± 0.068 μs), so by §2 this is a fact, not noise: roughly 30% slower.

**Cause.** The `IEnumerable<T>` overload is the one that lost. `List<T>` growth is amortized and `ToArray()` copies once from a contiguous block whose count is already known; the pooled version adds a rent, a capacity test per element, a copy per output array and a return, and saves only the intermediate arrays. The indexed array and read-only-list overloads read the count up front, so they pay the same rent/copy/return without the growth they were never paying before.

**Why it is tracked rather than reverted.** §2 makes allocation a hard rule with no tolerance and speed a comparative question. The operation now allocates the minimum possible (its two output arrays, which it must return), which is what P1 exists to achieve. The ~1.1 ns per element on a 1,000-element list is accepted as the price of that floor, the same class of trade as the array `Traverse` row above.


### Serialization — P0 baseline and P1 remeasurements — measured 2026-11-06

`SerializationOperationBenchmark` measures the factory-based `AddResulTusConverters()` path with `DefaultJob`. The `Result<int>[]` rows use 1,000 successful values and expose the per-element converter lookup. Input JSON and serializer options are prepared in `GlobalSetup`; benchmark outputs escape through instance sinks read in `GlobalCleanup`.

P0's pre-change baseline and the P1 remeasurements all used BenchmarkDotNet 0.15.8, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14, `DefaultJob`. The initial run produced all ten rows. P1/T1 updated the three rows affected by `ErrorType` parsing; P1/T2's repeated cache remeasurements are shown separately below.

| Method | Mean | Allocated |
| --- | ---: | ---: |
| Serialize `Result<int>` success | 177.8 ns | 80 B |
| Serialize `Result<int>` failure | 310.9 ns | 224 B |
| Deserialize `Result<int>` success | 212.3 ns | 0 B |
| Deserialize `Result<int>` failure | 500.20 ns | 112 B |
| Serialize `Result<int>[1000]` | 85.444 μs | 61,808 B |
| Deserialize `Result<int>[1000]` | 159.317 μs | 97,680 B |
| Serialize `Error` | 246.1 ns | 176 B |
| Deserialize `Error` | 343.33 ns | 120 B |
| Serialize `ErrorType` | 110.9 ns | 48 B |
| Deserialize `ErrorType` | 81.57 ns | 0 B |

**P1/T1 remeasurement:** unescaped `ErrorType` deserialization now allocates 0 B instead of 40 B. Deserializing a failed `Result<int>` and an `Error` each allocate 40 B less than P0 because their nested `ErrorType` name no longer becomes a string. Their observed means were 500.20 ns (P0 513.9 ns), 343.33 ns (P0 367.2 ns), and 81.57 ns (P0 115.3 ns) for `Result<int>` failure, `Error`, and `ErrorType`, respectively; single-run time deltas are descriptive, not a ranking. P1/T2 will remeasure the result converter rows and update them in its own commit.

**Readout:** this is a serialization baseline, not a comparison against other libraries. `Result<int>` success deserialization allocates 0 B. Array rows include the JSON string or materialized result array and all per-element work; they are not an isolated allocation measure of converter lookup.

#### P1/T2 — cache the payload converter per options instance

The pre-cache column is the P1/T1 serialization run, immediately before T2. The two post-cache columns are independent full runs with the same `DefaultJob`. All four affected operations allocate the same bytes as before; these rows isolate the repeated converter-lookup time from the output/result-array allocations.

| Operation | Before cache | P1/T2 run A | P1/T2 run B | Allocated after cache |
| --- | ---: | ---: | ---: | ---: |
| Serialize `Result<int>` success | 175.21 ns | 164.39 ns | 165.38 ns | 80 B |
| Deserialize `Result<int>` success | 217.81 ns | 196.34 ns | 193.25 ns | 0 B |
| Serialize `Result<int>[1000]` | 90.827 μs | 69.322 μs | 73.011 μs | 61,808 B |
| Deserialize `Result<int>[1000]` | 166.339 μs | 149.720 μs | 144.984 μs | 97,680 B |

Both post-cache runs are faster than the pre-cache run for all four rows, while allocations are unchanged. This is consistent with avoiding repeated `JsonSerializerOptions.GetConverter` calls; the measurements remain specific to this machine and are not a cross-library ranking. Each run produced ten rows and no benchmark issues.

### Logging — enabled versus disabled failure logging — measured 2026-11-06

`LoggingOperationBenchmark` logs one prebuilt failed `Result<int>` through a capturing `ILogger`. The logger captures the structured state reference and event count but does not format a message, so the enabled row measures the logging state and dispatch rather than a formatted string. `Enabled` is varied between false and true.

BenchmarkDotNet 0.15.8, `DefaultJob`, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14. One complete run:

| Method | Enabled | Mean | Allocated |
| --- | --- | ---: | ---: |
| LogFailure | false | 5.057 ns | 0 B |
| LogFailure | true | 26.054 ns | 64 B |

**Readout:** the disabled-level path allocates 0 B. The enabled path allocates the 64 B structured logging state; no rendered-message allocation is included because the capture logger keeps the state structured.

### Validation conversions — 1,000 issues — measured 2026-11-06

`ValidationOperationBenchmark` measures `ValidationErrors.ToDictionary()` and `ToError()` over 1,000 preconstructed issues across ten fields. The issue list is created in `GlobalSetup`, outside the measured methods.

BenchmarkDotNet 0.15.8, `DefaultJob`, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14. One complete run:

| Method | Mean | Allocated |
| --- | ---: | ---: |
| ToDictionary | 21.385 μs | 31,624 B |
| ToError | 33.04 ns | 80 B |

**Readout:** `ToDictionary` materializes the grouped dictionary and per-field message arrays. `ToError` creates the summary message string and a value-type `Error`.

## 5. Reproduction

```bash
dotnet run -c Release --project tests/ResulTus.Benchmarks
```

Rules for a valid run:

- Never run two benchmark processes concurrently. CPU contention invalidates every number.
- Always `Release`. Debug measurements are meaningless.
- Use the pinned `DefaultJob` — no shortcuts, no `InProcess`.
- Compare only runs produced on the same machine and SDK.
- **Run a suspect row at least twice.** On this hardware the run-to-run spread is 0.2–0.4 ns, larger than most of the effects being measured. A single run is an anecdote, not a measurement.
- **Never call a sub-nanosecond difference a win or a loss without a repeat.** See §4.1, Finding 4.

## 6. Current standing

Measured 2026-10-29, Intel Core i7-6700, .NET SDK 10.0.403, post-B1 (`Error` 24 B, `Result<int>` 32 B). The failure-path row was measured 2026-11-06 on the same machine and SDK.

| Benchmark area | Standing | Margin |
| --- | --- | --- |
| Creation — success | Tie (noise floor) | all rows < 1 ns |
| Creation — failure | **Win** | 607x vs ErrorOr (88 B allocated), 2116x vs FluentResults (240 B), 0 B |
| Match — vs FluentResults | **Win** | 23x / 14x, 0 B vs 96 B / 88 B |
| Match — vs CSharpFunctionalExtensions | **Win** | 2.1x / 1.4x |
| Match — vs ZeroResult | **Tie on success, unresolved loss on failure** | 0.9235 vs 0.9391; failure 1.3834 vs 0.7818 did not reproduce (§4.1, Finding 4) |
| Match — vs ErrorOr | **Unresolved, ErrorOr ahead** | ahead in every run, magnitude swung 33% → 250% |
| Allocation — success | Tie (noise floor) | 0 B, ZeroMeasurement |
| Allocation — failure | **0 B** | the speed column is eliminated code, not a ranking |
| Failure path from a unique message | **Win on allocation vs 3 of 4, tie vs CSharpFunctionalExtensions** | 0 B vs 32 B / 88 B / 272 B / 0 B; 3.0x–18x faster, 1.08x ahead of the 0 B competitor (§4.6) |
| Struct size — generic tier | **Win** | `Result<int, 8-byte error>` = **13 B**, measured against the aligned 16 B alternative with no resolvable performance gain (§4.5); every competitor is 16 B |
| Struct size — default tier | **Documented loss** | `Result<int>` 32 B vs 16 B; deliberate trade for zero allocation on both paths |
| Array traversal — vs 16 B competitors | **Documented loss** | 1.25x–1.47x slower (success), 1.08x–1.26x (failure); the 32 B stride is the cause (§4.4) |
| Array traversal — 32 B vs 40 B | **Marginal win for 40 B → 32 B** | ~3.5% at the 256 KiB L2 boundary, unresolved elsewhere (§4.3) |
| Array traversal — vs FluentResults | **Win** | 25x–28x faster, and it allocates 96 KB–960 KB per traversal |
| Collections — P1/T3 | **Measured, not compared** | Indexed array/list overloads: non-materializers 0 B; Sequence/Traverse output-only 4,024 B. Array Traverse time follow-up deferred; Partition remains 12,480 B (§4.7) |
| Serialization — satellite baseline | **Measured, not compared** | `Result<int>` success deserialization allocates 0 B; 1,000-result array deserialization allocates 97,680 B (§4.8) |
| Logging — satellite baseline | **Measured, not compared** | Disabled failure logging allocates 0 B; enabled logging allocates 64 B (§4.9) |
| Validation — satellite baseline | **Measured, not compared** | `ToDictionary` allocates 31,624 B over 1,000 issues; `ToError` allocates 80 B (§4.10) |

**The north star is met on allocation, met on the generic tier's size, and not met on the default tier's size — that last one is now a written decision, not an open issue.** The allocation result now has its head-to-head: §4.6 measures the failure path from a unique, uncached message, and the answer is ResulTus **0 B**, `ZeroResult` **32 B**, `ErrorOr` **88 B**, `FluentResults` **272 B**, and `CSharpFunctionalExtensions` **0 B**. The pre-2026-11-06 claim that every competitor allocates one object per failure was **wrong for `CSharpFunctionalExtensions`**: its error is a plain `string`, so it allocates nothing once the message exists. ResulTus ties it on allocation and differentiates on payload — `Code`, `Message` and `Type` inline for the same 0 B. The default `Result<T>` is 2x the competitors' size on purpose: it is the ergonomic tier, and it is the only one of the five that keeps a rich error stored inline — and §4.4 is the price of that decision stated as a number.

## 7. Known open gaps

| Area | Status |
| --- | --- |
| `Match` vs ErrorOr | **Unresolved, ErrorOr ahead.** Ahead in the 2026-10-27 baseline and again on 2026-10-29 (`0.7175` vs `0.9235` success, `0.5482` vs `1.3834` failure), but the failure-path margin swung from 33% to 250%, so the magnitude is not trustworthy. Direction is consistent; size is not. |
| `Match` vs ZeroResult on failure | **Unresolved.** 2026-10-27 measured us at `0.9095` against `0.8301`; 2026-10-29 measured `1.3834` against `0.7818`. Our own row moved 52% between runs, so no verdict is available on this hardware. |
| `Match` outcome overall | **Noise-floor tie** against every 16-byte competitor except the failure-path question above. The delegate is the entire measurable cost; removing it makes the JIT eliminate the operation, so it cannot be measured. |
| `Result<int>` was 40 B instead of 16 B | **Fixed 2026-10-28.** `Error` lost its untyped `Metadata` dictionary (32 → 24 B) and `Result<int>` is now **32 B**, zero allocation on both paths. The 16 B target was rejected: incompatible with the allocation contract. See **[Decision 001](decisions/001-result-struct-size.md)**. |
| `[StructLayout(Pack = 1)]` | **Fixed 2026-10-29.** Removed from `Error` (runtime realigns: 40 B either way) and from `Result<T>` (its inline `Error` holds references, so the same realignment applies: 32 B either way). Kept only on `Result<TSuccess, TError>`, where it is honored when the error type holds no references and yields the measured 13 B. The attribute now exists only where it is measured to do something. |
| Alignment of the generic tier | **Closed 2026-10-29 — no resolvable difference, 13 B kept.** Measured in §4.5: the aligned 16 B stride was 2.5% faster in one cell and 1.5%–5.0% slower in three, with a within-layout spread larger than the effect. |
| **Failure-path allocation vs competitors** | **Fixed 2026-11-06.** Measured in §4.6 with a unique, uncached message per invocation: ResulTus **0 B**, `ZeroResult` 32 B, `ErrorOr` 88 B, `FluentResults` 272 B, `CSharpFunctionalExtensions` **0 B**. The original claim "one object for each of the other four" was wrong for `CSharpFunctionalExtensions`, whose error is a plain `string`; ResulTus ties it on allocation and wins on payload. |
| **Array traversal vs competitors** | **Fixed 2026-10-29 — it is a loss.** Measured in §4.4: 1.25x–1.47x slower than every 16-byte competitor on the success path. The 32 B inline error is the cause and the trade is deliberate. Kept here as a standing fact, not an open question. |
| **Array-specific collection Traverse vs its IEnumerable path** | **Performance follow-up deferred by the owner 2026-11-06.** P1/T3 measured 7.447 μs / 4,024 B for the indexed array overload and 3.055 μs / 4,056 B for the IEnumerable control in one run. The indexed overload saves 32 B but is slower; do not claim a speed win. |
| **Indexed materializer allocation on the failure path** | **Accepted by the owner 2026-11-07 as a documented structural difference.** The array and read-only-list `Sequence`/`Traverse` overloads allocate their output array before the first failure is known: a 1,000-element source that fails at index 0 costs **4,024 B**, against **32 B** for the `IEnumerable<T>` path. The advantage of the enumerable path exists only for early failures — a failure halfway through has already grown that path's `List<T>` and `ToArray()` to the same order of allocation. Accepted because a materializer cannot return its result without allocating it, and both alternatives buy that early-failure saving with cost on the success path, which is the path that always runs. No behavior change; allocation on success is the output array itself. |
| **Indexed `Partition` time on lists** | **Documented structural difference 2026-11-07.** P1/T5 cut `Partition` allocation from 12,480 B to 4,048 B on both sources and made the list overload ~30% slower (4.456 and 4.557 µs against 3.150 and 3.409 µs, reproduced, intervals not overlapping). The allocation floor is the mandatory §2 rule; the time cost is the accepted price, measured in §4.7. |

Every gap in this table must eventually resolve to one of: **fixed**, **noise-floor tie**, or **documented structural difference**. "We are close enough" is not a resolution.

## 8. Enforcement

- A result type change that regresses any row outside the noise floor does not merge.
- A new benchmark row must be added to this document in the same commit that adds the benchmark.
- **A benchmark run measures only the rows the change affects.** Filter to the specific benchmarks (`--filter "*Class.Method*"`) and run the whole suite only when the owner explicitly asks for a full run. An unfiltered run is minutes of measurement that answers nothing about the change under review.
- This charter is the authority. If a benchmark contradicts it, the charter wins and the benchmark is suspect.
