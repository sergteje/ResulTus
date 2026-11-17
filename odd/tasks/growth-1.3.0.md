# Growth for 1.3.0: five additions that complete existing packages

Objective: close the five highest-value gaps found while auditing the shipped surface, without adding a fourteenth package and without relaxing the zero-allocation floor, the AOT posture or the error model. Branch: one per feature, merged to `main` one at a time. Test runner: `dotnet test ResulTus.slnx -c Release`. Delivery strategy: `single-pr` per feature, one release `1.3.0` after the fifth. Push, PR creation and merge are the owner's decisions.

## Specs

- **S0 - authorization.** The owner authorized the five items and the release shape. Verbatim: "Haremos 1 2 3 4 y 5 cada uno subirá el 1.X.Y subira la Y", then "A" selecting one release with five pull requests. The patch-bump reading was challenged against `odd/specs/versioning.md:35` ("Adding a member to a public type... a `MINOR` bump") and the owner chose the minor bump: every item adds public surface, so the release is `1.3.0`, not `1.2.1` through `1.2.5`.

- **S1 - serialization of the typed tier.** `ResulTus.Serialization` converts `Result`, `Result<T>`, `Error` and `ErrorType`, but not `Result<TSuccess, TError>`, which is the tier `ResulTus.Validation` and `ResulTus.HttpClient` produce and the one an API puts in a contract. Add a converter for it through the existing `AddResulTusConverters()` entry point, with the same strict wire format (`isSuccess` plus `value` or `error`), the same failure behavior on malformed documents, an AOT-safe path that does not rely on reflection over the error type, XML documentation, tests for every branch, and the package README and `docs/serialization.md` updated.

- **S2 - metrics next to traces.** `ResulTus.Diagnostics` exposes `Activity` and `DiagnosticListener` only; there is no `System.Diagnostics.Metrics` surface, so a team that wants a dashboard writes its own counters. Add an opt-in `Meter` with an outcome counter and a duration histogram, tagged with `ErrorType` and a bounded outcome name, and never with the free-form `Error.Code`, whose cardinality is unbounded. Document the cardinality rule. Measuring must not allocate on the hot path: build the tag list once per error type, not per record.

- **S3 - Minimal API ergonomics.** `ResulTus.AspNetCore` converts a result through `ToHttpResult()`/`ToActionResult()`, which covers controllers and handlers that call the extension themselves. Add an `IEndpointFilter` so a Minimal API handler that returns `Result` or `Result<T>` is converted without boilerplate, mapping the failure exactly as `ToHttpResult()` already does. Tests cover success, failure and a handler that returns something else.

- **S4 - the streaming half of the collection tier.** `ResulTus.Collections` has `WhenAllAsync`/`WhenAnyAsync` over `Task` and no `IAsyncEnumerable` support at all. Add the asynchronous mirror of the synchronous operations (`SequenceAsync`, `TraverseAsync`, `PartitionAsync`) over `IAsyncEnumerable<Result<T>>`, with the same names, order guarantees, first-failure rule and cancellation behavior, and with the pooled-buffer discipline the synchronous `Partition` already follows. Tests cover success, failure, empty stream and cancellation.

- **S5 - the analyzers for the bug the async tier opened.** `ResulTus.Analyzers` ships `RESULT001`-`RESULT006`. Add `RESULT007` for a discarded `Task<Result<...>>` or `ValueTask<Result<...>>` (a fire-and-forget that loses the failure) and `RESULT008` for an `await` used as a whole expression statement whose value is a `Result` (the failure dropped after the work ran). The original wording asked for a `ValueTask` consumed twice; that shape needs control-flow precision the syntax model cannot give without false positives on branches and loops, so it is replaced by the awaited-discard rule, which is precise. `RESULT001` stops reporting awaited statements so one line carries one warning.

- **S6 - versioning and delivery.** Each item ships as its own pull request with tests, docs and a `CHANGELOG.md` bullet under `## [Unreleased]`; the fifth and last pull request promotes `## [Unreleased]` to `## [1.3.0] - <date>` and moves `Version` in `Directory.Build.props` to `1.3.0`. `PackageValidationBaselineVersion` stays at `1.2.0` until 1.3.0 is published, then moves, exactly as the versioning spec requires. The API snapshot is regenerated in every pull request and only the affected assembly's file may change.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 | delegated | done | this commit |
| T2 | S2 | delegated | done | this commit |
| T3 | S3 | delegated | done | this commit |
| T4 | S4 | delegated | done | this commit |
| T5 | S5 | delegated | done | this commit |
| T6 | S6 | inline | pending | - |

## Log

### L1 - the owner named the scope and the delivery shape (verbatim)

> Haremos 1 2 3 4 y 5 cada uno subirá el 1.X.Y subira la Y

Then, after the patch-versus-minor correction was raised against the versioning spec:

> A

Option A was "one `1.3.0` release with the five items, five pull requests, one tag and one publication".

### L2 - state at start

`main` is at `ec22396`, CI green, `1.2.0` published on nuget.org with its baseline in place. The five items come from the audit of the shipped surface: `ResulTus.Serialization` has no `Result<TSuccess,TError>` converter (`git grep "Result<TSuccess, TError>" src/ResulTus.Serialization` returns nothing), `ResulTus.Diagnostics` has zero `System.Diagnostics.Metrics` references, `ResulTus.AspNetCore` has no `IEndpointFilter`, `ResulTus.Collections` has zero `IAsyncEnumerable` references, and `ResulTus.Analyzers` stops at `RESULT006`.

### L3 - T1: the typed tier serializes

`ResulTus.Serialization` gained `ResultJsonConverter<TSuccess, TError>` and the factory now closes it for `Result<,>`, so `AddResulTusConverters()` covers the tier without a second call. The wire format is the sibling's verbatim (`isSuccess` plus `value` or `error`), the error goes through the converter registered for `TError`, and the `JsonTypeInfo` constructor gives Native AOT a path that never reflects over the payload.

Evidence: 98 of 98 serialization tests on net8.0, net10.0 and net11.0 (baseline 86), ApiCompat 11 of 11 with exactly five snapshot lines added to `ResulTus.Serialization.txt`, solution build 0 warnings and 0 errors, and the documentation snippet gate at 24 documents and 130 blocks (four new fences, all compiling). Malformed documents throw `JsonException` with the sibling's messages: a success without `value`, a failure without `error`, a missing `isSuccess`, and a null document each name what is wrong.

One behaviour change is recorded rather than hidden: the factory now claims `Result<TSuccess, TError>`, so a consumer registering its own converter for that type after `AddResulTusConverters()` gets the built-in one. Registering before the call, or not calling the extension, keeps the custom converter.
### L4 - T2: metrics next to the traces

`ResulTus.Diagnostics` now publishes a `Meter` named `ResulTus` with two instruments: `resul_tus.result.outcomes` (`Counter<long>`) and `resul_tus.result.duration` (`Histogram<double>`, seconds per OpenTelemetry). `ResultMetrics.Record` is the public entry point, and the existing `Measure`/`MeasureAsync` path records both instruments from the same call that already produced the `Activity` and the `DiagnosticListener` event, so one call feeds spans, events and metrics.

Tags are bounded by construction: `outcome` always, `error_type` on failure only. `Error.Code` is never a tag, and the test asserts it by using a distinctive code and checking that no measurement carries it. The tag sets are built once per process in a static table indexed by `ErrorType`, and the listener transcript shows one record per measurement with no per-call construction.

Evidence: 37 of 37 diagnostics tests on net8.0, net10.0 and net11.0 (baseline 29), ApiCompat 11 of 11 with twelve snapshot lines added to `ResulTus.Diagnostics.txt`, build 0 warnings, snippet gate at 24 documents and 134 blocks (four new fences), and the existing `Activity` tests untouched, so the span tags and event payloads are unchanged.

One runtime detail worth keeping: on .NET 8 a disposed `MeterListener` keeps receiving measurements and `Instrument.Enabled` stays true, so the test double stops recording on dispose instead of relying on the runtime. The library does not depend on `Enabled`; `Add` and `Record` are no-ops with no listener and allocate nothing, which the existing zero-allocation test still proves.
### L5 - T3: Minimal API filters without reflection

`ResulTus.AspNetCore` gained two filters, `ResultEndpointFilter` for the unit tier and `ResultEndpointFilter<TSuccess>` for `Result<TSuccess>`, plus `WithResultMapping()` and `WithResultMapping<TSuccess>()` on `RouteHandlerBuilder`, so a handler reads `app.MapGet("/x", () => useCase.Handle()).WithResultMapping();`.

The two closed filters are the design, not a shortcut: a filter receives `object?`, so recognizing `Result<TSuccess>` at runtime would need reflection, and the package builds with `IsAotCompatible=true` where that is a warning and therefore an error. Each filter converts only its own tier through the existing `ToHttpResult()` and returns every other value untouched.

Evidence: 62 of 62 AspNetCore tests on net8.0, net10.0 and net11.0 (baseline 45), including six end-to-end tests that build a real `WebApplication` with `CreateSlimBuilder()` and invoke the endpoint's `RequestDelegate`, comparing the mapped response with the direct `ToHttpResult()` response and with the same route mapped without the filter. ApiCompat 11 of 11 with nine snapshot lines added to `ResulTus.AspNetCore.txt`, build 0 warnings, snippet gate at 24 documents and 135 blocks.

The typed tier stays out of the filter on purpose: `ResulTus.Validation` already bridges `Result<TSuccess, ValidationErrors>` explicitly, and a generic error policy does not exist. The package README states that boundary.
### L6 - T4: the streaming half

`ResulTus.Collections` gained `SequenceAsync`, `TraverseAsync` and `PartitionAsync` over `IAsyncEnumerable`, each mirroring its synchronous counterpart: same order, same first-failure short-circuit, same empty-input result, same exactly sized arrays copied out of a rented buffer and never handed to the caller.

One deliberate divergence is documented: the asynchronous operations also check the token per element, so a source that ignores cancellation cannot quietly turn a cancelled stream into a success. That is a stronger guarantee than the synchronous operations can offer, not a different outcome.

Evidence: 133 of 133 collection tests on net8.0, net9.0, net10.0 and net11.0 (baseline 111), including three differential tests that run one input through both halves and compare outcomes, consumed prefix and mapper calls; ApiCompat 11 of 11 with three snapshot lines added to `ResulTus.Collections.txt`; build 0 warnings; snippet gate at 24 documents and 137 blocks.

The only edit to the synchronous file widens `Grow<T>` from private to internal so both halves share it, with an unchanged body, because duplicating it would be a second implementation of the same growth rule.
### L7 - T5: two analyzers for the async bug class, and one rule split

`RESULT007` reports a `Task<Result<...>>` or `ValueTask<Result<...>>` produced as an expression statement and never awaited, so the outcome is unobserved and an exception inside it becomes an unobserved task exception. `RESULT008` reports an `await` used as a whole expression statement whose value is a `Result`, so the failure is dropped after the work ran. Both are `Warning`, both match the awaitable by namespace and metadata name rather than by display string, and both stay silent on the deliberate acknowledgement: `_ = SomethingAsync();` and `_ = await SomethingAsync();`.

**Spec deviation, recorded.** S5 asked for `RESULT008` to catch a `ValueTask<Result<...>>` consumed twice. That needs control-flow precision: a textual count of awaits false-positives on mutually exclusive branches (`if (c) await vt; else await vt;`), on a loop that awaits once per iteration, and on a task returned to a helper that awaits it. Syntax plus the semantic model cannot separate those, so the awaited-discard rule shipped instead, which is precise, and S5 was rewritten to say so.

**A rule split, not a duplicate.** The worker measured that `RESULT001` already reported `await SomethingAsync();`, because the await expression's static type is the unwrapped `Result`. Shipping `RESULT008` unchanged would have put two warnings on one line, so `RESULT001` now skips awaited statements and `RESULT008` owns that shape, with a test that pins the silence and the analyzers README row stating the ownership.

**Precision is the product here.** The negative tests prove silence on `_ =` discards, non-result awaitables (`Task<int>`, `Task<string>`), a consumer's own `CustomTask<Result>`, and every shape where the value is assigned, returned, passed or tested.

**Parity sweep.** The rule range moved from `RESULT001`-`RESULT006` to `RESULT001`-`RESULT008` in both root README editions, `src/ResulTus/README.md`, `src/ResulTus.SourceGenerators/README.md` and `odd/specs/versioning.md`, and the rule tables in both READMEs and `docs/error-handling.md` gained the two rows.

**Evidence.** 53 of 53 analyzer tests on net8.0, net10.0 and net11.0 (baseline 47, plus the split test that pins `RESULT001`'s silence on an awaited statement), with the RED being `CS0246` for the two analyzers before they existed. Solution build 0 warnings. No snapshot changed, because `scripts/ApiSnapshot` excludes the analyzers by design.