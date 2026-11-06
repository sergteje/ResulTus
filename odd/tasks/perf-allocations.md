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
| T1 | S1 | inline | pending | — |
| T2 | S2 | inline | pending | — |
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

- **T1** — a test asserting that `"Not\u0046ound"` reads as `ErrorType.NotFound` and that `"notfound"` reads as `ErrorType.NotFound` passes today and must keep passing after the change; a test asserting zero string allocation on the unescaped path fails today.
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
