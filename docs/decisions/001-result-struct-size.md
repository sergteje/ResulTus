# Decision 001 — Shrink `Result<T>` from 40 bytes to 16

**Status:** **closed 2026-10-28 — Option B1.** `Error` lost its untyped `Metadata` dictionary (32 B → 24 B) and `Result<int>` is now **32 B**, with zero heap allocation on both paths. Option A (16 B) was rejected as incompatible with the allocation contract; Option C (40 B) was rejected as paying an allocation hole for nothing.
**Date opened:** 2026-10-27
**Blocks:** Phase 2 (Extensions) — every `Map`/`Bind`/`Tap` signature returns `Result<T>`, so its size and shape must be settled first.

`Result<int>` currently occupies **40 bytes**. Every competitor occupies **16**. This is not a microbenchmark artifact — it is a memory cost paid on every array, every copy, and every pass-by-value, and it grows with the size of `T`. The fix is not in `Match`; it is in what `Result<T>` stores inline.

## The decision in one table

| | Before B1 | Option A — error behind one reference | Option B — shrink `Error` itself | Option C — accept 40 B |
| --- | ---: | ---: | ---: | ---: |
| `Result<int>` size | 40 B | **16 B** | 16–24 B | 40 B |
| Zero-alloc on **success** (contract) | ✅ | ✅ | ✅ | ✅ |
| Zero-alloc on **failure** (bonus, not promised) | ✅ | ❌ 1 object | ✅ | ✅ |
| `Error` keeps code + message + type + metadata | ✅ | ✅ | ❌ loses fields | ✅ |
| API break for consumers | — | None | None | — |
| Work required | — | Medium: `Error` becomes reference-backed | High: redesign `Error` shape | None (document only) |

## Quick path

1. Read **What we actually promise** below — it decides whether Option A is even legal.
2. Read **Where the 40 bytes actually cost you** — it decides whether this matters at all.
3. Run the measurement spike described in **Open measurements**.
4. Fill in **Decision** at the bottom.

## What we actually promise

The core spec is explicit and narrower than the README marketing might suggest:

| Spec ID | Requirement |
| --- | --- |
| RV-10 | MUST have zero heap allocations on **success** path |
| RT-13 | Zero heap allocations on **success** path |
| RG-7 | Zero heap allocations on **success** path |
| ER-6 | `Metadata` MUST be `null` when empty (zero allocation) |

**Nowhere does the contract promise an allocation-free failure path.** We happen to have one today (`AllocationBenchmark.ResulTus_Failure_Allocation` = 0.0381 ns, 0 B), but it is a bonus, not a commitment.

**This is the hinge of the whole decision.** If failure-path allocation is cargo-culted from "we're zero-alloc", Option A is off the table for emotional reasons. If it is read from the spec, Option A is legal and cheap.

## Where the 40 bytes actually cost you

| `T` | Current | Option A | Delta per element |
| --- | ---: | ---: | ---: |
| `int` (4 B) | 40 B | 16 B | −24 B |
| `long` (8 B) | 48 B | 24 B | −24 B |
| `Guid` (16 B) | 56 B | 32 B | −24 B |

A flat `Result<int>[]` of 10 000 elements: **400 KB vs 160 KB**. A `List<Result<Guid>>` of 100 000: **5.6 MB vs 3.2 MB**.

Costs that scale with struct size:

- **Cache pressure.** 40 B straddles cache lines more often than 16 B. It matters in array scans, LINQ over results, and any batch validation pipeline.
- **Copy cost on every pass-by-value.** A result returned, assigned, or passed through `Map`/`Bind` is copied. Bigger struct, more `mov` instructions.
- **Collection growth.** `List<T>` capacities, `Dictionary` values, `Memory<T>` slices.

Costs that **do not** scale, and where this decision is invisible:

- A single `Result` on the stack in a hot loop. That is exactly what every existing benchmark measures, which is why the benchmarks never surfaced this.

## Evidence

All measured 2026-10-27, .NET SDK 10.0.403, `Release`, `DefaultJob`, Intel i7-6700. Full detail in [performance.md](../performance.md) §4.1–4.2.

| Fact | Value | How it was established |
| --- | --- | --- |
| Actual `Error` managed size | **32 B** | `Unsafe.SizeOf<Error>()` in a replica spike |
| Actual `Result<int>` managed size | **40 B** | `Unsafe.SizeOf`, matches the real type |
| `ZeroResult<int, SingleError>` | 16 B | `Unsafe.SizeOf` |
| `ErrorOr<int>` | 16 B | `Unsafe.SizeOf` |
| `[StructLayout(Pack = 1)]` effect on `Error` | **none** | 40 B with and without it |
| `Error` payload removed (8 B error) | collapses to 16 B | same result type, only the error changed |

**Why `Pack = 1` is dead weight.** `Error` holds three reference fields. The GC requires object references to stay pointer-aligned, so the runtime relaxes the pack and realigns. Measured proof: identical 40 B with and without the attribute. The earlier "25 B / 30 B" figures in the project history were layout arithmetic, never the actual managed size. **Do not add `Pack = 1` to new types expecting a size win.**

**What this proves.** The 32-byte `Error` inlined in `Result<T>` is the entire cause. Nothing else contributes: not the `byte` flag, not the generic `T`, not the alignment attribute.

## Options in detail

### Option A — error behind a single reference

`Result<T>` stores one reference to error data instead of the `Error` struct inline.

```
Result<int> = 4 (T) + 8 (reference) + 1 (flag) + padding = 16 B
```

**Gains:** 16 B, matching every competitor. Scales cleanly for large `T`. Success path still allocation-free — the reference is `null`.

**Costs:** the failure path allocates one object unless the error is a cached singleton. Mitigations that keep the cost near zero in practice:

- Cache the common factory outputs (`Error.NotFound`, `Error.Unauthorized`, …) as singletons — the spec already treats `Error.None` as a singleton.
- Intern by `(code, type)` so repeated domain errors share one instance.
- Reuse through a pooled/`readonly` error catalog registered at startup.

**Risk:** if consumers build errors per-failure with unique messages, failure paths allocate. That is a real regression against today's behaviour, even though it does not violate the contract.

**Verdict:** the only option that reaches 16 B. Legal per spec. Requires the most design thought.

### Option B — shrink `Error` itself

Reduce `Error`'s inline footprint so `Result<T>` lands at 16–24 B without an indirection.

| Sub-option | `Error` size | `Result<int>` | Sacrifice |
| --- | ---: | ---: | --- |
| B1 — drop `Metadata` from the struct | 24 B | 32 B | `Metadata` needs a different home |
| B2 — single ref (code+message interned) + type byte | 16 B | 24 B | one allocation per new `(code,message)` pair |
| B3 — pure single ref, `ErrorType` inside the target | 8 B | 16 B | `Error` becomes a reference wrapper in all but name |

**Gains:** keeps the failure path allocation-free for B1; partial for B2.

**Costs:** `Metadata` is genuinely useful (structured validation errors, correlation IDs). Dropping it removes a feature the spec already defines (ER-6 implies it exists). B3 converges on Option A anyway — it just hides the reference inside `Error`.

**Risk:** a spec amendment. `ER` requirements define `Code`, `Message`, `Type`, `Metadata`; changing the field set changes the public contract.

**Verdict:** B1 is a cheap partial win (40 → 32 B) with a real feature cost. B2 is a compromise that pays an allocation *and* still loses 8 bytes. B3 is Option A with extra steps.

### Option C — accept 40 bytes

Keep everything inline. Document the size as the price of a rich, allocation-free `Error`.

**Gains:** zero work, zero risk, zero API change, keeps allocation-free failure.

**Costs:** 2.5x the memory of every competitor in every collection. A reviewer comparing libraries will see the number. `Result<T>` is the library's core type — this is the one place where "it's fine" is hardest to defend.

**Verdict:** defensible only with a written rationale. The performance charter forbids "we are close enough" as a resolution; this option needs a real argument, not a shrug.

## Recommendation — superseded

**This section is the original 2026-10-27 recommendation and is kept for the reasoning trail. It was overruled.** The owner chose zero allocation on both paths as the hard contract promise, which makes Option A illegal. The final decision is **B1**; see `## Decision`.

**Option A**, conditional on the spike below confirming that cached errors keep failure allocations at zero in realistic usage.

Reasoning:

1. The contract only promises success-path zero allocation, so A is legal.
2. A is the only option that reaches parity at 16 B — the number every competitor already hits.
3. The allocation cost lands on the failure path, which is the cold path by definition.
4. A cached error catalog makes the cost disappear for the dominant pattern (fixed domain error codes).
5. B trades a feature (metadata) for a partial win, and B3 collapses into A.

**If the spike shows cached errors do not cover realistic failure usage**, fall back to **B1 + Option C documentation**: take the free 8 bytes from `Metadata`, accept 32 B, and write down why.

## Open measurements — completed 2026-10-28

All five were run. Results: sizes pinned by `tests/ResulTus.Tests/LayoutTests.cs`; the traversal result is in [performance.md](../performance.md) §4.3. The cached-versus-unique failure-allocation measurements became moot when the contract adopted zero allocation on both paths — there is no indirection to test.

| Measurement | Why it decides something |
| --- | --- |
| `Unsafe.SizeOf` for each option × `T` ∈ {`int`, `long`, `Guid`} | Confirms the size table is not arithmetic wishful thinking |
| `Allocated` on the failure path with a **cached** error | Does A actually allocate in a realistic scenario? |
| `Allocated` on the failure path with a **unique-message** error | Confirms the worst case and how bad it is |
| Array traversal over 10 000 results, all options | **The only benchmark where 40 B shows up.** Match did not reveal it; do not repeat that mistake |
| `Result<Guid>` pass-by-value through `Map` | Confirms the cost scales with `T` |

**Do not use a `Match` microbenchmark for this decision.** Match measured ~1 ns with a run-to-run spread of 0.2–0.4 ns on this hardware; the size effect is smaller than the noise. Traverse an array instead.

## Contract — what `Result<T>` promises

Settled 2026-10-28, before any layout work. This is the yardstick every competitor comparison is judged against. Anything not listed under **Promises** is not part of the contract and must not be claimed in the README.

### Promises

| # | Promise | Source |
| --- | --- | --- |
| **P1** | **Zero heap allocations on the success path**, for every type parameter. | RV-10, RT-13, RG-7; `AllocationBenchmark` |
| **P2** | `sizeof(Result<T>)` = `round8(sizeof(T) + sizeof(Error) + 1)`. Measured after B1: `Result<int>` = **32 B**, `Result<long>` = **40 B**, `Result<Guid>` = **48 B**; `sizeof(Error)` = 24 B. Pinned by `tests/ResulTus.Tests/LayoutTests.cs`. | `Unsafe.SizeOf` |
| **P3** | **Zero heap allocations on the failure path, always** — including errors built with a unique message. | `AllocationBenchmark` |
| **P4** | `default(Result<T>)` is a failure carrying `Error.None`, with zero allocation. | unit test + `Unsafe.SizeOf` |
| **P5** | No read API allocates: `IsSuccess`, `IsFailure`, `Match`, `Deconstruct`, `TryGetValue`, `GetValueOrDefault`, `GetValueOr`, `Error`. | `AllocationBenchmark` |

### Explicit non-promises

- **16 B parity with competitors on the default tier.** Zero allocation on both paths requires `Error` stored inline, which floors `Result<T>` at 32 B. Reaching 16 B requires the error behind a reference, which allocates on failure. The two goals are mutually exclusive by layout arithmetic; this contract picks zero allocation.
- **Constant size.** Size grows with `T`; the fixed overhead is `sizeof(Error)` + 1 byte plus padding.
- **`Match` wall-clock parity with any competitor.** Run-to-run spread (0.2–0.4 ns) exceeds the measured effect (0.2 ns) on this hardware. Any claim here is unverifiable and must not be published.
- **Any allocation guarantee for `Result<TSuccess, TError>` when `TError` is a reference type.**

### The 16 B + zero-allocation combination lives in the generic tier

| Type | Size | Success allocs | Failure allocs | Status |
| --- | ---: | ---: | ---: | --- |
| `Result<int>`, `Error` inline without `Metadata` | **32 B** | 0 | 0 | **chosen (B1)** |
| `Result<int>`, `Error` inline with `Metadata` | 40 B | 0 | 0 | rejected (C) — pays an `object?` boxing hole for nothing |
| `Result<int>`, error behind a reference | 16 B | 0 | 0 cached / 1 unique | rejected (A) — violates P3 |
| `Result<int, ErrorCode>`, `ErrorCode` an 8-byte struct | **13 B** | **0** | **0** | **already shipped** |

Every competitor's 16 B is reached the same way — by putting the error **behind a reference**, which is exactly the shape Option A was rejected for:

| Element type | Kind | `SizeOf` | What the 16 B holds |
| --- | --- | ---: | --- |
| `ResulTus.Result<int>` | struct | 32 | a 24-byte `Error` inline |
| `ResulTus.Result<int, EightByteError>` | struct | **13** | an 8-byte struct error inline |
| `ErrorOr.ErrorOr<int>` | struct | 16 | a `List<Error>` reference (`ErrorOr.Error` alone is 32 B) |
| `ZeroResult.Result<int, SingleError>` | struct | 16 | a reference — **`SingleError` is a class** |
| `CSharpFunctionalExtensions.Result<int>` | struct | 16 | a reference |
| `FluentResults.Result<int>` | **class** | 8 | the whole result is a reference |

So no competitor combines 16 B with an inline error, and none of them is allocation-free while building a *new* error. ResulTus is the only one whose error is a value type stored inline, and the only one whose generic tier goes below 16 B. Any "we beat every competitor" claim must be made in `Result<TSuccess, TError>`, not in `Result<T>`. The default tier is the ergonomic one: it competes on features and on allocation behaviour, not on bytes.

### 13 B packed versus 16 B aligned — measured 2026-10-29, the pack stays

The open question was whether dropping `Pack = 1` and taking an aligned 16 B stride (four elements per 64-byte cache line, payload always word-aligned) beats the packed 13 B stride (denser, but one in five elements straddles a line and the inline payload starts at a different alignment in each element).

`GenericTierTraversalBenchmark` measures exactly one build, so the same code was run against both layouts — two full runs per layout, `DefaultJob`, nothing else running, machine idle. The layout table printed by each run confirms which layout was built: `SizeOf 13 / stride 13` for the packed build, `16 / 16` for the aligned one.

| Method | N | Packed 13 B (run 1 / run 2) | Aligned 16 B (run 1 / run 2) | Aligned vs packed |
| --- | ---: | ---: | ---: | ---: |
| Success | 1 000 | 616.4 / 618.3 ns | 601.2 / 602.4 ns | **−2.5% (aligned faster)** |
| Failure | 1 000 | 578.4 / 586.5 ns | 594.2 / 588.5 ns | +1.5% (packed faster) |
| Success | 7 000 | 4 946.1 / 4 902.5 ns | 5 079.5 / 5 265.1 ns | +5.0% (packed faster) |
| Failure | 7 000 | 3 993.1 / 4 088.4 ns | 4 104.8 / 4 183.0 ns | +2.6% (packed faster) |

**Verdict: no resolvable difference, so the smaller stride wins.** The aligned build wins one of four cells and loses the other three. The one cell it wins is success at 1 000 elements; the cell where it loses hardest contains a run-to-run spread inside the *same* layout of **3.6%** (5 079.5 versus 5 265.1), which is larger than the between-layout effect being measured. This is exactly the §4.1, Finding 4 situation: on this hardware a difference of this size is not attributable.

The two cells that do separate cleanly are both failure paths, and both favour the packed stride: the failure loop reads one flag per element, which is the case where fewer bytes per element buys the most. Nothing here is a performance argument *for* 13 B — it is an argument that **16 B costs nothing to skip**, and skipping it keeps the only headline the generic tier has: a sub-16-byte result type with an inline error, with zero allocation on both paths.

**Decision: `Pack = 1` stays on `Result<TSuccess, TError>`.** The follow-up is closed, not deferred.


### Migration notes

- **`Error` stays a `readonly record struct`** (spec line 5). Converting it into a class would make `default(Error)` null and break every `Deconstruct(out bool, out T, out Error)` caller. This is the second reason Option A is rejected.
- **`Metadata` was a spec feature**, not an implementation detail: `ER-2`, `ER-3` and `ER-6` defined it. B1 was therefore a **spec amendment** as well as a code change; all three requirements and the scenarios that asserted it were amended in [`odd/specs/result-core.md`](../../odd/specs/result-core.md).
- **`[StructLayout(Pack = 1)]` now lives only where it does measurable work.** Removed from `Error` (runtime forced realignment: 40 B with and without it) and from `Result<T>` (its inline `Error` holds references, so the same realignment applies and the attribute is inert — `Result<int>` measures 32 B either way). It stays on `Result<TSuccess, TError>`, where it is honored when the error type holds no references and produces the measured 13 B generic tier instead of 16 B. See the section above.
- **Nothing about `Error.None` needed to change.** It stays a property that builds a struct, which costs no allocation. Option A would have forced it into a cached singleton.
- **`AllocationBenchmark.ResulTus_Failure_Allocation` stays at `0 B` and is now the assertion target for P3**, not an accidental bonus. `LayoutTests.FailurePath_ShouldNotAllocate` pins it in the unit suite.
- **`default(Error) == Error.None` is now true — fixed 2026-10-29.** The record struct's synthesized `Equals` compared the raw `_code` field (`null` versus `string.Empty`), so the two representations of "no error" disagreed. `Error` now declares `Equals(Error)` and `GetHashCode()` over the `Code` contract (null-coalesced, ordinal), `Message` (ordinal, `null` stays distinct from `string.Empty`) and `Type`; the synthesized `==` routes through it. Spec requirement **ER-7** and a scenario pin it; `ErrorTests.DefaultStruct_ShouldEqualNone` proves it.

## Decision

> **Chosen option:** **B1** — remove the untyped `Metadata` dictionary from `Error`. `Error` goes 32 B → 24 B, `Result<int>` goes 40 B → **32 B**, zero heap allocation preserved on both paths.
>
> **Rationale:** Zero allocation on the failure path (P3) requires `Error` inline, so 16 B is unreachable — that rules out Option A. Between 40 B and 32 B, `Metadata` was an `IReadOnlyDictionary<string, object?>`: an `object?` bag that boxes every value type stored in it, which is a live allocation hole on the very path the contract promises is allocation-free. A zero-allocation library cannot carry an untyped payload in its core type. Removing it is the only lever that shrinks the type, and it also makes `Result<int>` pack exactly two per 64-byte cache line with no straddling.
>
> **What the measurement says:** the array-traversal spike measured 32 B against 40 B at ~**3.5%**, and only at the 7 000-element point where the two arrays land on opposite sides of the 256 KiB L2 (5/5 suites agreed in sign; within noise at 5 000 and 10 000). The predicted 25% cache-line traffic reduction is real as traffic but does not become time: the loop is issue-limited and the prefetcher absorbs the extra lines. **The 8 bytes are a design win, not a performance win, and must not be sold as one.** See performance.md §4.3.
>
> **Accepted cost:** `ER-6` is replaced and `Metadata` leaves the default tier. Callers needing structured extras use `Result<TSuccess, TError>` with their own typed error — no boxing, no allocation, and 13 B instead of 32 B. `Result<int>` remains 2x every competitor's size; that is the deliberate price of a rich error type with zero allocation on both paths.
>
> **Decided by / date:** owner, 2026-10-28, extended 2026-10-29. Contract (P1–P5), Option B1, spec amendment, and measurement all closed. The `Pack = 1` follow-up on `Result<TSuccess, TError>` was measured on 2026-10-29 and **closed: the packed 13 B stays**, because the aligned 16 B alternative showed no resolvable advantage and the smaller stride preserves the only sub-16-byte claim in the comparison set. `default(Error) == Error.None` was also closed the same day (ER-7).

## Checklist

- [x] Confirmed the contract only promises success-path zero allocation
- [x] Confirmed the contract now promises zero allocation on both paths (P3), which rules out Option A
- [x] Confirmed `Pack = 1` stays or goes for the right reason — removed from `Error` and `Result<T>` (both measured no-ops), kept on `Result<TSuccess, TError>` (honored, saves 3 bytes)
- [x] Measured the packed 13 B versus aligned 16 B generic tier — 2026-10-29, two runs per layout, no resolvable difference, packed kept
- [x] Closed `default(Error) != Error.None` — explicit `Equals`/`GetHashCode` over the `Code` contract, spec `ER-7`
- [x] Ran the array traversal benchmark — not a `Match` microbenchmark
- [x] Decided whether `Metadata` is worth 8 bytes (chose B1: it is not)
- [x] Recorded the outcome in [performance.md](../performance.md) §4.2, §4.3, §6 and §7

## References

- [performance.md](../performance.md) — full benchmark baseline, noise-floor rules, array traversal, current standing
- [Result{T}.cs](../../src/ResulTus/Result%7BT%7D.cs) — the 32-byte instantiation
- [Error.cs](../../src/ResulTus/Error.cs) — the 24-byte payload, `Pack = 1` removed
- `tests/ResulTus.Tests/LayoutTests.cs` — pins every size in P2 and the zero-allocation failure path
- [result-core.md](../../odd/specs/result-core.md) — RV-10, RT-13, RG-7 (both-path allocation), ER-2, ER-3, ER-6 (no untyped payload)

## Next

Resume state, 2026-10-28.

**Blocking nothing, but not yet done:**

1. **Nothing is committed.** `src/ResulTus/Error.cs` and `tests/ResulTus.Tests/ErrorTests.cs` are modified, `tests/ResulTus.Tests/LayoutTests.cs` and `docs/` are untracked, and `openspec/` is git-ignored (`.gitignore:61`) so the spec amendment will not be committed without a force-add. Decide the commit scope first.
2. **Competitor timing comparison is measured — 2026-10-29, and it is a loss.** The full suite ran after B1 (Creation, Match, Allocation) and `ArrayTraversalBenchmark` finally produced timings against ErrorOr, ZeroResult, FluentResults and CSharpFunctionalExtensions. Sequential traversal is **1.25x–1.47x slower** than every 16-byte competitor and only the failure path is close (1.08x–1.26x); FluentResults is 25x–28x slower on the same rows and allocates 96 KB–960 KB. All of it is recorded in [performance.md](../performance.md) §4 and §4.4, and §6/§7 classify it as a documented structural loss rather than an open question.
3. **13 B versus 16 B for the generic tier — measured and closed 2026-10-29.** Two runs per layout, same code, `GenericTierTraversalBenchmark`: the aligned build was 2.5% faster in one cell and 1.5%–5.0% slower in the other three, with the failure paths consistently favoring the packed stride and a within-layout spread (3.6%) larger than the effect. No resolvable difference, so the smaller stride stays. The table and reasoning are in the section above.
4. **`default(Error) != Error.None`** under record-struct `==` — **fixed 2026-10-29.** `Error` declares `Equals`/`GetHashCode` over the `Code` contract, the spec gained `ER-7`, and `ErrorTests.DefaultStruct_ShouldEqualNone` pins it.
5. **Phase 2 (Extensions) is unblocked.** The layout and the contract are settled, so `Map`/`Bind`/`Tap` can be designed against a 32-byte `Result<T>` and a 13-byte generic tier.
