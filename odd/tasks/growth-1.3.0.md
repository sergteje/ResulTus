# Growth for 1.3.0: five additions that complete existing packages

Objective: close the five highest-value gaps found while auditing the shipped surface, without adding a fourteenth package and without relaxing the zero-allocation floor, the AOT posture or the error model. Branch: one per feature, merged to `main` one at a time. Test runner: `dotnet test ResulTus.slnx -c Release`. Delivery strategy: `single-pr` per feature, one release `1.3.0` after the fifth. Push, PR creation and merge are the owner's decisions.

## Specs

- **S0 - authorization.** The owner authorized the five items and the release shape. Verbatim: "Haremos 1 2 3 4 y 5 cada uno subirá el 1.X.Y subira la Y", then "A" selecting one release with five pull requests. The patch-bump reading was challenged against `odd/specs/versioning.md:35` ("Adding a member to a public type... a `MINOR` bump") and the owner chose the minor bump: every item adds public surface, so the release is `1.3.0`, not `1.2.1` through `1.2.5`.

- **S1 - serialization of the typed tier.** `ResulTus.Serialization` converts `Result`, `Result<T>`, `Error` and `ErrorType`, but not `Result<TSuccess, TError>`, which is the tier `ResulTus.Validation` and `ResulTus.HttpClient` produce and the one an API puts in a contract. Add a converter for it through the existing `AddResulTusConverters()` entry point, with the same strict wire format (`isSuccess` plus `value` or `error`), the same failure behavior on malformed documents, an AOT-safe path that does not rely on reflection over the error type, XML documentation, tests for every branch, and the package README and `docs/serialization.md` updated.

- **S2 - metrics next to traces.** `ResulTus.Diagnostics` exposes `Activity` and `DiagnosticListener` only; there is no `System.Diagnostics.Metrics` surface, so a team that wants a dashboard writes its own counters. Add an opt-in `Meter` with an outcome counter and a duration histogram, tagged with `ErrorType` and a bounded outcome name, and never with the free-form `Error.Code`, whose cardinality is unbounded. Document the cardinality rule. Measuring must not allocate on the hot path: build the tag list once per error type, not per record.

- **S3 - Minimal API ergonomics.** `ResulTus.AspNetCore` converts a result through `ToHttpResult()`/`ToActionResult()`, which covers controllers and handlers that call the extension themselves. Add an `IEndpointFilter` so a Minimal API handler that returns `Result` or `Result<T>` is converted without boilerplate, mapping the failure exactly as `ToHttpResult()` already does. Tests cover success, failure and a handler that returns something else.

- **S4 - the streaming half of the collection tier.** `ResulTus.Collections` has `WhenAllAsync`/`WhenAnyAsync` over `Task` and no `IAsyncEnumerable` support at all. Add the asynchronous mirror of the synchronous operations (`SequenceAsync`, `TraverseAsync`, `PartitionAsync`) over `IAsyncEnumerable<Result<T>>`, with the same names, order guarantees, first-failure rule and cancellation behavior, and with the pooled-buffer discipline the synchronous `Partition` already follows. Tests cover success, failure, empty stream and cancellation.

- **S5 - the analyzer for the bug the async tier opened.** `ResulTus.Analyzers` ships `RESULT001`-`RESULT006`. Add `RESULT007` for a discarded `Task<Result<...>>` or `ValueTask<Result<...>>` (a fire-and-forget that loses the failure) and `RESULT008` for a `ValueTask<Result<...>>` consumed twice, both with the same documentation, severity and test discipline as the existing analyzers.

- **S6 - versioning and delivery.** Each item ships as its own pull request with tests, docs and a `CHANGELOG.md` bullet under `## [Unreleased]`; the fifth and last pull request promotes `## [Unreleased]` to `## [1.3.0] - <date>` and moves `Version` in `Directory.Build.props` to `1.3.0`. `PackageValidationBaselineVersion` stays at `1.2.0` until 1.3.0 is published, then moves, exactly as the versioning spec requires. The API snapshot is regenerated in every pull request and only the affected assembly's file may change.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 | delegated | done | this commit |
| T2 | S2 | delegated | pending | - |
| T3 | S3 | inline | pending | - |
| T4 | S4 | delegated | pending | - |
| T5 | S5 | delegated | pending | - |
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