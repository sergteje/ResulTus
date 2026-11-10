# Changelog

All notable changes to ResulTus are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project
follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). A `0.x` version, as SemVer
defines it, means the public API may still change between minor releases.

## [Unreleased]

## [0.0.3] - 2026-11-10

### Changed

- **Multi-targeting net8.0 / net9.0 / net10.0** — all eleven production libraries now compile and
  package for the three runtimes (the analyzers and the source generator keep `netstandard2.0`);
  `Directory.Build.props` no longer pins a single `TargetFramework`. Both README editions state
  the targets, and the release workflow also mirrors the packages to GitHub Packages
  (`GITHUB_TOKEN`, `.nupkg` only) so they appear on the repository's Packages tab.

### Added

- **CI/CD** — GitHub Actions (`ci.yml` build+suite on push/PR, `release.yml` NuGet publish on
  tags, `aot-smoke` Native AOT publish-and-run) with the packaging identity (icon, readme, tags)
  and the owner's brand suite v2.0 recorded in the same body.
- **Incremental source generator pipeline** — `ErrorFactoryGenerator` no longer groups every
  candidate with `Collect()`: each `[ErrorFactory]` declaration flows on its own into the report
  and emit steps, so editing one declaration re-runs only that candidate's stage. The caching
  contract (candidates and diagnostics compare by content; `Location` decomposed into file, span
  and line positions) is pinned by `IncrementalCachingContractTests`.
- **Analyzer false-positive battery** — six curated silent-expectation sources (lookalike `Result`
  types in user namespaces, `Nullable<T>.GetValueOrDefault`, ordinary tuple discards, third-party
  identity binds, nested lookalikes, stateful `Ensure` predicates) pin the analyzers' symbol
  gates: code that a name-based search would mistake for misuse must never reach a complaint.
- **Benchmark charter generation columns** — the creation and matching comparison tables publish
  the `Gen0` column quoted from the stored run artifacts, with the rule stated that
  `Gen1`/`Gen2` were zero in every measured run and their column absence is the record.

### Changed (collection probes)

- The Collections allocation probes measure warmup and window on a dedicated thread, so residue
  from the test host's scheduling queues can no longer masquerade as library allocation under
  `-c Release`.

### Added

- **Incremental source generator pipeline** — `ErrorFactoryGenerator` no longer groups every
  candidate with `Collect()`: each `[ErrorFactory]` declaration flows on its own into the report
  and emit steps, so editing one declaration re-runs only that candidate's stage. The
  caching contract (candidates and diagnostics compare by content; `Location` decomposed into
  file, span and line positions) is pinned by `IncrementalCachingContractTests`; all generated
  behavior is covered by the existing 50 generator tests.

### Changed

- **Benchmark charter generation columns** — the creation and matching comparison tables in
  `docs/performance.md` now publish the `Gen0` column quoted from the stored run artifacts, with
  the rule stated that `Gen1`/`Gen2` were zero in every measured run and their column absence is
  the record.
- **Collections allocation probes** measure warmup and window on a dedicated thread, so residue
  from the test host's scheduling queues can no longer masquerade as library allocation under
  `-c Release`. (Changes no library behavior; the probes were the only readers of their own
  measurements.)

### Added (analyzers)

- **Analyzer false-positive battery** — six curated silent-expectation sources (lookalike `Result`
  types in user namespaces, `Nullable<T>.GetValueOrDefault`, ordinary tuple discards, third-party
  identity binds, nested lookalikes, stateful `Ensure` predicates) pin the analyzers' symbol gates:
  code that a name-based search would mistake for misuse must never reach a complaint.

## [0.0.2] - 2026-11-10

### Added

- **CI/CD** — GitHub Actions: a `ci.yml` job builds the solution and runs the full test suite on
  every push to `main` and on every pull request, a `release.yml` job builds, tests and publishes
  to NuGet on every new `v*` tag, and a Native AOT job publishes and runs the AOT smoke
  application both trimmed and natively on `windows-latest` (CI run `42653755212`, both jobs
  green) — the publish-and-run proof the 0.0.1 notes could not make. `samples/AotSmoke` is the
  checked-in guard: its only serializer surface compiles through the AOT-safe
  `ResultJsonConverter<T>(JsonTypeInfo<T>)` route, and the repo's own analyzers reject anything
  else with `IL2026`/`IL3050`.- **Async parity across the three tiers** — the typed tier (`Result<TSuccess, TError>`) gained
  its own `Task`/`ValueTask` pipeline operators (`Map`, `Bind`, `MapError`, `Ensure`, `Recover`,
  `Tap`/`OnSuccess`, `OnFailure`, `Switch`, `Match`, `Finally`), and the unit and value tiers
  gained the missing error-side terminals (`MapErrorAsync`, `OnFailureAsync`, `SwitchAsync`) plus
  the `Ensure` error-factory shapes. Cancellation-aware delegates match the existing shapes; a
  cancelled token surfaces as `OperationCanceledException`, never as a failure. The typed tier
  keeps one `Ensure` shape, documented in the API surface and in both README editions. Every
  synchronization twin's semantics are pinned by `CrossTierSemanticsTests` (Task/ValueTask/sync
  parity on success and failure paths, plus null and cancelled batteries) — the battery also
  caught and fixed a divergence in the first `OnFailureAsync` implementation.
- **Packaging** — the package icon and the canonical README are carried inside every `nupkg`
  (`PackageIcon`, `PackageReadmeFile`), and search tags were added to the shared metadata.
- **Brand identity** — the official mark, the dark/light/print variants and the usage guidelines
  live in `assets/branding/`, supplied as owner artwork (brand suite v2.0), packaged as the icon
  and consumed by both README editions.

### Verified

- **Trimming and Native AOT** are run-verified in CI (`aot-smoke`, run `42653755212`, green): a
  console application published both trimmed and natively executes the core result types plus the
  AOT-safe System.Text.Json surface, passing every assertion. Both README editions carry the
  runtime-compatibility section; the 0.0.1 notes' "not proven by a publish-and-run result" is
  superseded by this release.

## [0.0.1] - 2026-11-08

First versioned release. Thirteen packages, one result type shared by all of them.

### Added

- **`ResulTus`** — `Result`, `Result<T>`, `Result<TSuccess, TError>`, `Error` and `ErrorType` as
  inline `readonly record struct` types. Zero heap allocation on the success path and on the
  failure path, including a failure built from a unique, uncached message. `Error` is 24 bytes,
  `Result<int>` is 32 bytes and the generic tier is 13 bytes with a reference-free error; the
  32-byte default tier is the documented price of the inline error
  ([decision 001](docs/decisions/001-result-struct-size.md)).
- **`ResulTus.Extensions`** — `Map`, `Bind`, `Ensure`, `Recover`, `MapError`, `Tap`, `Finally` and
  `Switch` for the unit, `Result<T>` and generic tiers; `Task`/`ValueTask` overloads with
  cancellation for the unit and `Result<T>` pipeline operators at this tag (the typed tier's async
  operators land in 0.0.2); and the value, nullable and exception conversions.
- **`ResulTus.Collections`** — `Traverse`, `Sequence`, `Fold`, `Partition`, `FirstFailure`,
  `Combine` and the inspection queries over arrays, lists, read-only lists and `IEnumerable<T>`,
  plus the asynchronous `WhenAll` and `WhenAny`.
- **`ResulTus.Logging`** — `ILogger` extensions that hand the provider named fields
  (`ResultType`, `IsSuccess`, `ErrorType`, `ErrorCode`, `ErrorMessage`) instead of a rendered
  string, with no allocation while the log level is disabled.
- **`ResulTus.Serialization`** — `System.Text.Json` converters for `Result<T>`, `Error` and
  `ErrorType` over a fixed wire format (`isSuccess`, `value`, `error`) that ignores naming
  policies, plus strict reading that rejects numeric tokens and unknown `ErrorType` names.
- **`ResulTus.Diagnostics`** — opt-in timing and tracing through one `ActivitySource` and one
  `DiagnosticListener`, silent and allocation-free when nothing listens.
- **`ResulTus.AspNetCore`** — `ToActionResult`, `ToHttpResult`, `ErrorType`-to-status mapping and
  RFC 9457 ProblemDetails.
- **`ResulTus.HttpClient`** — `SendResultAsync` returning `Result<TSuccess, HttpClientError>`, with
  typed failure kinds and caller cancellation kept as control flow.
- **`ResulTus.Validation`** and **`ResulTus.Validation.FluentValidation`** — structured
  `ValidationErrors`/`ValidationIssue`, a DataAnnotations adapter and an `IValidator<T>` adapter.
- **`ResulTus.Analyzers`** — `RESULT001` through `RESULT006`, treating result misuse as build
  diagnostics.
- **`ResulTus.SourceGenerators`** — `[ResultErrors]`/`[ErrorFactory]` error factories, with
  `RESULTGEN001` for declarations the generator cannot emit.
- **`ResulTus.Testing`** — framework-independent `ShouldSucceed`/`ShouldFail` assertions.
- **Documentation** — `README.md` and `README.es.md` at parity, `docs/performance.md` as the
  measured charter every number in the README links to, `docs/httpclient.md`, and the
  [`result-core` specification](odd/specs/result-core.md).

### Notes

- No CI yet: the repository has no workflow, so build and test run locally
  (`dotnet build ResulTus.slnx`, `dotnet test ResulTus.slnx`).
- Trimming and Native AOT compatibility are designed and annotated, not proven by a published
  publish-and-run result.
- Benchmarks are reproducible through `tests/ResulTus.Benchmarks/`; every recorded number and its
  measurement stamp live in [`docs/performance.md`](docs/performance.md).

[Unreleased]: https://github.com/sergteje/ResulTus/compare/v0.0.3...HEAD
[0.0.3]: https://github.com/sergteje/ResulTus/releases/tag/v0.0.3
[0.0.2]: https://github.com/sergteje/ResulTus/releases/tag/v0.0.2
[0.0.1]: https://github.com/sergteje/ResulTus/releases/tag/v0.0.1
