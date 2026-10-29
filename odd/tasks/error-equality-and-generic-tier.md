# Error equality + generic tier alignment

Objective: close the two known debts (`default(Error) != Error.None` under `==`, and the unmeasured 13 B packed generic tier) and refresh `docs/performance.md` §4 with the post-B1 numbers. Branch: `feature-1`. Test runner: `dotnet test tests/ResulTus.Tests/ResulTus.Tests.csproj`.

## Specs

- **S1 — `default(Error)` equals `Error.None` under `==`.** The user's exact words: "Deuda conocida menor: default(Error) != Error.None bajo ==, y el tier genérico 13 B desalineado." `default(Error) == Error.None` MUST be `true`, `default(Error).Equals(Error.None)` MUST be `true`, and their `GetHashCode()` MUST be equal. Because `default(Error)._code` is `null` while `Error.None._code` is `string.Empty`, equality MUST compare the `Code` contract (null-coalesced to `string.Empty`, ordinal) instead of the raw field.
- **S2 — no regression in structural equality.** Two errors are equal if and only if `Code`, `Message` and `Type` are equal. `Error.Failure("A") != Error.Failure("B")`, `Error.None` MUST NOT equal a `Failure` with an empty code, and a `null` versus empty `Message` MUST remain distinct (only `Code` carries the null-coalescing contract).
- **S3 — the generic tier layout is decided by measurement, not by arithmetic.** The 13 B packed stride and the 16 B aligned stride MUST be compared by an array-traversal benchmark on this machine, both success and failure paths. The winner is kept, `LayoutTests` pins it, and `docs/decisions/001-result-struct-size.md` records the measurement and the reason. If the difference is inside the noise floor, the smaller stride wins and the decision document says so explicitly.
- **S4 — `docs/performance.md` §4 carries no pre-B1 ResulTus row.** Every ResulTus baseline row MUST come from the run of 2026-10-29 (`BenchmarkDotNet.Artifacts/results/*`, Creation 13:04, Match 13:13, Allocation 13:17, ArrayTraversal 12:52), and §4 must state the post-B1 `Result<int>` size and the honest verdicts, including the ones that are losses.
- **S5 — the change is clean.** `dotnet build` with 0 warnings, the full test suite green, no scratch paths left in the worktree.
- **S6 — `.codegraph/` is ignored.** The agent index is a local artifact and MUST NOT be committable.

## Tasks

| ID | Spec | Route | Status |
| --- | --- | --- | --- |
| T1 | S1, S2 | inline | done — RED on `DefaultStruct_ShouldEqualNone`, then 47/47 green |
| T2 | S3 | inline (4 benchmark runs in background) | done — packed 13 B kept, two samples per layout |
| T3 | S4 | inline | done — §4, §4.4, §4.5, §6, §7 rewritten from the 2026-10-29 run |
| T4 | S5, S6 | inline | done — solution build 0 warnings, 47/47 green, `.codegraph/` ignored |

## Log

### L1 — original request (verbatim)

> Arranca y arregla esto:Deuda conocida menor: default(Error) != Error.None bajo ==, y el tier genérico 13 B desalineado.

Preceded by the user's answer to "¿Arranco por §4?" — so the §4 refresh is authorized in the same pass (S4).

### L2 — starting evidence

- `Error` is a `readonly record struct` with fields `_code`, `_message`, `_type`; the compiler-synthesized `Equals` compares raw fields, so `default` (`_code == null`) never equals `None` (`_code == ""`). `Code` already null-coalesces in its getter, so the contract is documented but not enforced in equality.
- `Result<TSuccess, TError>` still carries `[StructLayout(LayoutKind.Sequential, Pack = 1)]` and measures 13 B for `Result<int, EightByteError>`; without the pack it is expected to measure 16 B.
- The 2026-10-29 benchmark run finished; `docs/performance.md` §4 still publishes the 2026-10-27 (pre-B1 opt-in) numbers.
- Working tree pre-existing edits: `src/ResulTus/Error.cs`, `tests/ResulTus.Tests/ErrorTests.cs`, `tests/ResulTus.Benchmarks/Program.cs` (modified), plus new `LayoutTests.cs`, `ArrayTraversalBenchmark.cs`, `docs/`.

### L3 — T1 evidence

- RED: `DefaultStruct_ShouldEqualNone` failed (`Con error: 1, Superado: 20`) — exactly the new test, while the two no-regression tests (empty-code failure, `null` vs empty message) already passed.
- Fix: explicit `Equals(Error)` / `GetHashCode()` comparing `Code` (null-coalesced, ordinal), `Message` (ordinal, `null != ""`), `_type`. The record struct's synthesized `==` routes through the declared `Equals`, which the green test confirms.
- GREEN: 47/47 (44 before, +3 tests). Build 0 warnings.

### L4 — T2 measurement, packed 13 B (run A)

Layout printed by the run: `Result<int, EightByteError>` → `SizeOf 13`, `stride 13`.

| Method | N | Mean |
| --- | ---: | ---: |
| ResulTus_Generic_Traverse_Success | 1 000 | 616.4 ns |
| ResulTus_Generic_Traverse_Failure | 1 000 | 578.4 ns |
| ResulTus_Generic_Traverse_Success | 7 000 | 4,946.1 ns |
| ResulTus_Generic_Traverse_Failure | 7 000 | 3,993.1 ns |

### L5 — blocked resources found

A `ResulTus.Benchmarks` host (PID 1296, started 12:53) was hung on a closed output pipe since the previous session, holding the Release DLLs and blocking every rebuild. Terminated after confirming it had burned 4.7 s of CPU in half an hour, i.e. it was not benchmarked-running. No recorded run overlapped another. Also: `dotnet run -- --filter "*X*"` must be passed as two separate tokens, otherwise BDN rejects the option and `Program.cs` line 13 throws a `NullReferenceException` on the null parsed config instead of printing a usable error.

### L6 — T2 result: the pack stays

Layout confirmed per build by `Unsafe.SizeOf` printed at run time: `13/13` packed, `16/16` aligned. Two runs per layout.

| Method | N | Packed 13 B | Aligned 16 B | Aligned vs packed |
| --- | ---: | ---: | ---: | ---: |
| Success | 1 000 | 616.4 / 618.3 ns | 601.2 / 602.4 ns | −2.5% |
| Failure | 1 000 | 578.4 / 586.5 ns | 594.2 / 588.5 ns | +1.5% |
| Success | 7 000 | 4 946.1 / 4 902.5 ns | 5 079.5 / 5 265.1 ns | +5.0% |
| Failure | 7 000 | 3 993.1 / 4 088.4 ns | 4 104.8 / 4 183.0 ns | +2.6% |

Aligned wins 1 of 4 cells. The aligned success/7000 pair itself spreads 3.6% run to run, wider than the between-layout effect. Both cleanly-separated cells (the failure paths) favour packed. Per the pre-registered rule in S3 — inside the noise floor means the smaller stride wins — `Pack = 1` stays on `Result<TSuccess, TError>`.

### L7 — consequences of keeping 13 B

- No layout change to ship; the debt was the *unmeasured* ambiguity, and it is now a measured decision.
- `Pack = 1` was also removed from `Result<T>`, where it is an inert attribute (its inline `Error` holds references, so the GC realigns and `Result<int>` is 32 B either way). The attribute now exists only where it is measured to save bytes. `LayoutTests` still pins 32 B.
- Decision 001 gained the measurement section, closed its `Pack` follow-up, and records the `default(Error)` fix (ER-7).
- performance.md gained §4.5 for the generic-tier spike and §4.4 for competitor traversal; §6/§7 reclassified both as resolved.


