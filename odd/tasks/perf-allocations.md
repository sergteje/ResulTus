# Performance P1 — Allocation fixes

Objective: remove the four allocation and CPU costs the audit found by reading code, and make the charter's "0 B on both paths" claim true for the satellite packages. Branch: `feature-1`.

Test runner: `dotnet test ResulTus.slnx`; benchmarks with `dotnet run -c Release --project tests/ResulTus.Benchmarks`.

Delivery strategy: `auto-chain`. Forecast ~450 authored changed lines (overloads, tests and the charter rows). PR and merge stay the user's call.

Risk: item 3 (contracts others consume) for T3 and T4 only — see L6. T1 and T2 are internal and carry no high-risk item.

## Specs

- **S1 — reading an `ErrorType` allocates no string, and the documented contract survives.** `ErrorTypeJsonConverter.ReadValue` must stop calling `reader.GetString()` on the common path. The replacement compares the UTF-8 bytes with the ten member names and returns the matching `ErrorType`. Two documented behaviors are preserved and both are covered by a test:
  - **Case-insensitivity.** `ErrorTypeJsonConverter.cs:16` documents that the name is matched case-insensitively, and the current map is `StringComparer.OrdinalIgnoreCase`. `Utf8JsonReader.ValueTextEquals` is case-**sensitive** and therefore cannot be used; the comparison must be case-insensitive, e.g. `Ascii.EqualsIgnoreCase` over `reader.ValueSpan`, or over `reader.ValueSequence` when `reader.HasValueSequence` is true.
  - **Escaped input.** When `reader.ValueIsEscaped` is true the raw span holds escape sequences, so the byte comparison cannot succeed. That path falls back to `reader.GetString()` and the existing dictionary. A JSON document containing `"Not\u0046ound"` must still read as `ErrorType.NotFound`.
  - An unknown or non-string value still throws `JsonException` with the same messages that exist today.
- **S2 — the `T` converter is resolved once per converter instance.** `ResultJsonConverter<T>` calls `options.GetConverter(typeof(T))` on every element in both `Write` (`ResultJsonConverterFactory.cs:103`) and `Read` (`:170`). It must resolve once, store the result in a private field, and reuse it. A benign race is acceptable: two threads may resolve the same instance, and the field assignment is idempotent. No observable behavior changes.
  - The cache is keyed by the **options instance**, not by the converter alone. `ResultJsonConverter<T>` is built by `Activator.CreateInstance` in `CreateConverter` (`:58`) with no access to the options, so it cannot capture them at construction, and `GetConverter` is an instance method on a specific `JsonSerializerOptions`. System.Text.Json caches converters per options instance today, so one field would hold; storing the options reference beside the converter and re-resolving when it differs makes the invariant hold by construction, for one reference comparison on the hit path.
  - `Write` only reaches `GetConverter` when the value is non-null (`:97` guards it), so the write-side cost is paid on the value path and never on a failure. The read side has no such guard: `Result<int>[]` pays one resolution per element.
- **S3 — collection operations stop allocating an enumerator.** Every method that today `foreach`es over `IEnumerable<T>` gains an index-based overload for `T[]` and for `IReadOnlyList<T>`:
  - `ResultCollectionExtensions`: `Sequence`, `Traverse`, `Fold`, `FirstFailure`, `FirstSuccess`, `Partition`, `ForEach`, `AllSucceeded`, `AnySucceeded`, `NoneSucceeded`.
  - `UnitResultCollectionExtensions`: `Combine`, `Fold`, `FirstFailure`, `ForEach`, `AllSucceeded`, `AnySucceeded`, `NoneSucceeded`.
  The `IEnumerable<T>` overloads stay, unchanged. The new overloads preserve every observable behavior: `ArgumentNullException` on a null source, short-circuit order, enumeration order, and the exact result. Verified by a deterministic allocation test using `GC.GetAllocatedBytesForCurrentThread()`: the non-materializing methods (`Fold`, `FirstFailure`, `FirstSuccess`, `ForEach`, `AllSucceeded`, `AnySucceeded`, `NoneSucceeded`, `Combine`) must measure exactly **0 B** across repeated calls; the materializing ones (`Sequence`, `Traverse`, `Partition`) must measure the same bytes as a hand-written `for` loop over the same array that allocates only the output array.
- **S4 — `Partition` stops churning `List<T>`.** `ResultCollectionExtensions.Partition` rents both buffers from `ArrayPool`, grows them by doubling exactly as `Sequence` does, copies to exactly sized arrays, and returns both buffers in a `finally` with `RuntimeHelpers.IsReferenceOrContainsReferences<T>()` as the clear flag. The returned tuple is byte-for-byte the same value the current implementation produces, and no rented buffer is ever returned to the caller.
- **S5 — the charter records the change.** The rows that `odd/tasks/perf-instrumentation.md` created are re-measured and updated in `docs/performance.md` in the same commit that changes the behavior, per §8.
- **S6 — no behavior regression.** The whole suite stays green, and new tests cover each new overload with: an array source, a `List<T>` source, an empty source, a null source, a first-element failure and a last-element failure.
- **S7 — documentation and build.** Every new public member carries full XML documentation (`CS1591`/`CS1573`/`CS1734` are errors here), and `dotnet build ResulTus.slnx` reports 0 warnings and 0 errors.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 | inline | done | this commit |
| T2 | S2 | inline | done | this commit |
| T3 | S3 (non-unit collections) | inline | pending | — |
| T4 | S3 (unit collections) | inline | pending | — |
| T5 | S4 | inline | pending | — |
| T6 | S5, S6, S7 | inline | pending | — |

## Log

### L1 — original request (verbatim)

> La fase 11 la vamos a postponer. Antes quiero que veamos todo el tema de rendimiento gc, heap, allocations de toda la libreia completa y tambien si hay cosas que podamos mejorar Span<T>,ConcurrentDictionary, etc etc ver que tienen otras librerias que nosotros no tengamos, en cual destaca la nuestra sobre otra, que tienen otras que podamos implementar y de paso mejorar aqui

### L2 — this document was requested (verbatim)

> Documenta los P0, p1 y p3 para que no se olvide para hacerlo por ODD

### L3 — starting evidence

- `ErrorTypeJsonConverter.ReadValue` (`src/ResulTus.Serialization/ErrorTypeJsonConverter.cs:77`) calls `reader.GetString()` and then looks the string up in a `Dictionary<string, ErrorType>` built with `StringComparer.OrdinalIgnoreCase` (`:101`). One string is allocated per error deserialized.
- `ResultJsonConverter<T>.Write` (`src/ResulTus.Serialization/ResultJsonConverterFactory.cs:103`) and `.Read` (`:170`) call `options.GetConverter(typeof(T))` inside the element loop.
- `ResultCollectionExtensions` `foreach`es over `IEnumerable<T>` at lines 37, 85, 129, 153, 177, 206, 247, 271, 293 and 315. `UnitResultCollectionExtensions` does the same at 25, 49, 77, 99, 123, 144 and 165. With an array source the runtime returns `SZGenericArrayEnumerator<T>`, a class; with a `List<T>` source the struct enumerator is boxed. Either way it is one heap allocation per call.
- `ResultCollectionExtensions.Partition` (`:203-218`) allocates two `List<T>` and calls `ToArray()` on each, while `Sequence` (`:32`) and `Traverse` (`:80`) already rent from `ArrayPool<T>`.
- `docs/performance.md` §8: "A new benchmark row must be added to this document in the same commit that adds the benchmark."

### L4 — test-first evidence

Each task observes RED before its implementation:

- **T1** — existing lowercase input and the escaped `"Not\u0046ound"` case must keep reading as `ErrorType.NotFound`; the unescaped `ValueSpan` allocation test fails before the fix, and segmented `ValueSequence` input is tested on the same zero-allocation path.
- **T2** — a test that serializes a `Result<int>[]` and asserts the converter instance is resolved once per converter. This is not directly observable from the public API, so the RED evidence is the benchmark row from P0 plus a test that a shared `JsonSerializerOptions` produces identical output across repeated calls. Stated here rather than dressed up as a failing behavior test.
- **T3/T4** — the allocation test described in S3 fails today with a non-zero delta and passes after the overloads exist.
- **T5** — the same allocation test for `Partition` fails today.
- **T6** — not a behavior task; see L7.

### L5 — decisions the audit already forced

- **`Utf8JsonReader.ValueTextEquals` cannot be used for S1.** It is case-sensitive and the converter documents case-insensitive matching. Using it would silently narrow a published contract. The span comparison must be `Ascii.EqualsIgnoreCase`.
- **`Span<T>` is not added to the core.** `Error` compares with `string.Equals(..., Ordinal)` and `Result<T>` has no heap payload, so a span API there buys nothing. The only span work in this phase is the UTF-8 comparison in S1 and the index-based iteration in S3.
- **No `ConcurrentDictionary`.** All caches in the library are immutable static `Dictionary` built once in a static constructor; a read-only `Dictionary` beats `ConcurrentDictionary` on reads. The one mutable cache this phase introduces (S2) is a single field, where a concurrent dictionary would be a regression.

### L6 — risk

- **T1 — Risk: none.** The public surface does not change; the only risk is behavioral and is pinned by the case-insensitivity and escape tests in S1.
- **T2 — Risk: none.** Internal caching with idempotent assignment.
- **T3/T4 — Risk: item 3 (contracts others consume).** Adding an overload can change which overload the compiler selects for an existing call site. A caller that passes an array literal or a `List<T>` will bind to the new overload instead of the `IEnumerable<T>` one, so the new overloads must be behaviorally identical, including the null check. This is why S6 demands the same test matrix for both sources, and why the `IEnumerable<T>` overloads are not removed or rewritten.
- **T5 — Risk: item 3 (contracts others consume).** `Partition` keeps its signature and its returned tuple; only the internal buffering changes. The risk is a rented buffer escaping to the caller or being returned twice, which the `finally` in S4 and the exact-copy prevent.

### L7 — verification

- `dotnet build ResulTus.slnx` — 0 warnings, 0 errors.
- `dotnet test ResulTus.slnx` — the whole suite green, including the 449 tests that exist today plus the new ones.
- `dotnet run -c Release --project tests/ResulTus.Benchmarks` — the P0 rows re-measured; any row outside the noise floor of `docs/performance.md` §5 is recorded, not hidden.

### L8 — S2 refined after reading the factory (2026-11-06)

Read the two P1 targets before implementing. The line numbers in S1 and S2 are correct: `GetString()` at `ErrorTypeJsonConverter.cs:77`, the case-insensitive map at `:101`, and both `GetConverter` calls at `ResultJsonConverterFactory.cs:103` and `:170`. The labels codegraph prints for `Write` and `Read` point at the declaration with its AOT attributes, not at the call site.

One thing the spec did not cover, now written into S2: `ResultJsonConverter<T>` is created by `Activator.CreateInstance` in `CreateConverter` (`:58`) and therefore never sees the `JsonSerializerOptions` at construction, while `GetConverter` is an instance method on a specific options object. Caching one converter in a plain field relies on System.Text.Json caching converters per options instance — true today, but an invariant the type does not state. The cache stores the options reference and re-resolves when it differs, which costs one comparison and makes the invariant structural.

Also recorded: `Write` only resolves when the value is non-null (`:97`), so the write-side cost never lands on a failure; `Read` has no such guard, which is why `Result<int>[]` pays per element.

No code touched. P1 waits for the P0 benchmark process to finish before the first build.

### L9 — P0 baselines completed (2026-11-06)

P0 is closed. The pre-change serialization baseline is recorded in `docs/performance.md` §4.8: deserializing `Result<int>[1000]` is 159.317 μs / 97,680 B, serializing it is 85.444 μs / 61,808 B, and `ErrorType` deserialize is 115.3 ns / 40 B. P1/T1 must remeasure and update its affected rows in the same commit as the UTF-8 comparison. P1/T2 must remeasure the `Result<int>` and array rows in its caching commit. Collections rows are in §4.7 and will be remeasured by T3–T5. The full baseline suite verified 449 tests passed across 10 projects; solution build was 0 warnings and 0 errors.

### L10 — T1 UTF-8 `ErrorType` parsing completed

The allocation test observed RED before implementation: 100 unescaped `"NotFound"` reads allocated 4,000 B through `GetString()`. The converter now compares unescaped `ValueSpan` and segmented `ValueSequence` bytes with `System.Text.Ascii.EqualsIgnoreCase`; escaped values and unknown names retain the existing `GetString()`/dictionary path and error behavior. Tests cover case-insensitive, escaped, unescaped zero-allocation, and segmented zero-allocation input.

**Verification:** the 62-test Serialization project passed. Focused ErrorType tests passed 29/29, including both allocation paths. Release benchmark build had 0 warnings and 0 errors; the Serialization benchmark run exited 0, produced all 10 rows, and reported no benchmark issues. P1/T1 remeasurement in `docs/performance.md` §4.8: `DeserializeResultFailure` 513.9 ns / 152 B → 500.20 ns / 112 B; `DeserializeError` 367.2 ns / 160 B → 343.33 ns / 120 B; `DeserializeErrorType` 115.3 ns / 40 B → 81.57 ns / 0 B. The other seven rows remain the P0 values because T1 does not affect them.

### L11 — T2 converter cache completed

The cache is one immutable holder containing the `JsonSerializerOptions` identity and `JsonConverter<T>`; a reference match returns it, while another options instance resolves and replaces it. The holder keeps the pair coherent under concurrent use. `GetConverter` suppressions now sit on the helper that contains that call, because analyzer suppressions on callers do not flow into a helper.

The output-stability test passed before the implementation and again after it. A second test reuses one result converter instance with two options instances containing different integer converters and verifies both writing and reading use the active options. The full Serialization project passed 64/64 tests. Release benchmark build had 0 warnings and 0 errors; two full Serialization benchmark runs each produced ten rows and no issues. The four affected rows and both post-cache means are in `docs/performance.md` §4.8; all four show the same direction versus the pre-cache run, with unchanged allocation counts.
