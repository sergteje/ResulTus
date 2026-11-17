# Hardening and consumer gaps after 1.1.1

Objective: harden ResulTus across the audit roadmap (sections A, B, C, D) while keeping every existing contract intact - the zero-allocation floor on both paths, the 24 B `Error` / 32 B `Result<T>` / 13 B generic-tier layouts, the AOT and trimming posture, and the public API snapshots. Section E is excluded by the owner. Branch: `feature/hardening`. Test runner: `dotnet test ResulTus.slnx` (targeted projects first). Delivery strategy: `auto-chain`, sliced as `1.1.2` (patch) then `1.2.0` (minor). Push, PR creation and merge remain the owner's decisions.

## Specs

- **S0 - authorization.** The owner approved the whole roadmap except section E. Verbatim: "Todo menos el E". Excluded and therefore out of scope: `FailIf`, query syntax, multi-error inside `Error`, `StackResult`, `in`/`ref readonly` on `Result<T>`, GC configuration, a 16 B layout, and a coverage badge without a service behind it.

- **S1 - `Partition` pool leak (A1).** All three `Partition` overloads (`IEnumerable<Result<T>>`, `Result<T>[]`, `IReadOnlyList<Result<T>>` in `src/ResulTus.Collections/ResultCollectionExtensions.cs`) rent both pooled buffers before the guarded region, so a throw from the second rent leaks the first buffer. Each overload must return the success buffer on every exit path, including a failure of the second rent. Evidence today: lines 207-208, 643-644, 698-699. Acceptance: both rents are reachable by the `finally` that returns them, or the first is returned when the second throws; the public behavior and the measured 0 B non-materializer path are unchanged. Test-first exception: no deterministic test can force the second `ArrayPool` rent to throw without process-wide allocation failure, so this is fixed by inspection and recorded here.

- **S2 - `ValidationErrors` is immutable evidence (A2).** `ValidationErrors` currently stores the caller's `IReadOnlyList<ValidationIssue>` by reference, so a caller that retains a mutable list can change an already-created failure (`src/ResulTus.Validation/ValidationErrors.cs:25-40`). The constructor must snapshot the issues so that mutating the source collection after construction cannot change `Issues` or `HasErrors`. Acceptance: a test constructs with a mutable `List<ValidationIssue>`, mutates the list, and asserts `Issues` and `HasErrors` are unchanged; the existing `ToDictionary` behavior is unaffected.

- **S3 - `Match` callback contract (A3).** Both `Match` overloads (`src/ResulTus/Result.cs:73-75`, `src/ResulTus/Result{TSuccess,TError}.cs:111-112`) invoke callbacks with no null guard, so a null callback yields a branch-dependent `NullReferenceException` or goes unnoticed on the unexecuted branch - unlike every pipeline operator, which guards delegates (`src/ResulTus.Extensions/ResultExtensions.cs:69-72`). Both overloads must throw `ArgumentNullException` for a null `onSuccess` or `onFailure`, independent of the branch taken. Acceptance: a test proves `ArgumentNullException` for a null callback on both a success and a failure result; the success-path allocation stays at 0 B.

- **S4 - CHANGELOG honesty (C1).** `CHANGELOG.md` has no `## [Unreleased]` section and its `[Unreleased]` link reference compares from `v1.0.0`, skipping `v1.1.0`/`v1.1.1`. Add an `## [Unreleased]` section at the top of the versions and fix the reference to `compare/v1.1.1...HEAD`, keeping Keep a Changelog form.

- **S5 - versioning spec corruption (C2).** `odd/specs/versioning.md:43` contains the corrupted token `диагности-free` where the sentence requires `diagnostic-free`. Replace it, preserving the meaning: code that compiled and was diagnostic-free before cannot start being flagged without a major bump.

- **S6 - AGENTS.md AOT claim (C3).** `AGENTS.md:75-76` says AOT and trimming are "a design claim, not a published publish-and-run result", but `.github/workflows/ci.yml` publishes and runs a trimmed sample and a Native AOT sample on Windows (`aot-smoke`) and Native AOT on Linux (`aot-smoke-linux`). Replace the false claim with the observed proof, without overclaiming beyond what the jobs prove.

- **S7 - stale ODD status (C4).** `odd/tasks/phase-11-ci-cd.md` still says "Status: **not started**" and that the repository has no `.github/` directory, although its task rows are done and the workflows exist. `odd/tasks/audit-first-pass.md` T6 leaves multi-targeting pending while its own note says the study is moot. Reconcile both against shipped evidence; do not invent outcomes (T6 remains an owner decision if it still is one).

- **S8 - Roslyn minimum disclosed (B5).** The analyzers and source generator packages ship under `analyzers/dotnet/roslyn4.14/cs` and set Roslyn 4.14 as the minimum in their project comments, but their package READMEs only say the Roslyn dependency is private. Add the minimum compiler version to `src/ResulTus.Analyzers/README.md` and `src/ResulTus.SourceGenerators/README.md` so a consumer on an older compiler is told why the package does nothing.

- **S9 - `HttpClientError` factories documented (B6).** `Transport`, `Timeout`, `StatusError` and `InvalidResponse` exist in the public API snapshot but no user-facing document names them. Document the construction helpers in `src/ResulTus.HttpClient/README.md` against the same error model table.

- **S10 - Spanish package table parity (B7).** `README.es.md` lists package names as plain text while `README.md` links each one to its focused `src/<Package>/README.md`. Restore the links so both editions are at parity in sections, numbers and code blocks.

- **S11 - execute tests on net8/net9 (B1).** Libraries target net8.0-net11.0; every test project runs only net10/net11, so net8/net9 consumers have zero executed coverage. Add net8/net9 to the test matrix (or the smallest project that can run them) so a framework-specific defect is caught in CI, not by a bug report.

- **S12 - FluentValidation behavior coverage (B2).** The adapter has API-snapshot coverage only. Add behavioral tests for the sync and async validation paths and the projection into `ValidationErrors`.

- **S13 - typed async contract coverage (B3).** The typed async tier has allocation coverage for two chains but no branch or cancellation contract tests. Add contract tests for `Task` and `ValueTask` paths, including that cancellation rethrows.

- **S14 - async exception capture (B4).** `Try` is synchronous only, so an async consumer writes its own `try/catch` and loses the "cancellation is control flow" rule. Add `TryAsync`/`TryValueTaskAsync` that capture a non-cancellation exception as a failure and rethrow `OperationCanceledException`. This is new public surface: a minor bump and an API-snapshot update.

- **S15 - documentation snippet compile gate (D1).** The rule "every code block compiles" is enforced manually. Add an automated gate that extracts and builds the documentation snippets so drift is a failing check, not a manual review.

- **S16 - CI cost and gating (D2).** `coverage-gate` rebuilds what `build-and-test` already built and `setup-dotnet` has no cache. Remove the duplicated build and enable dependency caching without weakening any gate.

- **S17 - package verification and security scanning (D3).** Release packs and pushes without a checked-in package-validation baseline, `dotnet nuget verify`, or a provenance step, and the workflow inventory has no CodeQL or dependency review. Add what is free on a public repository; author signing (which needs a paid certificate) is explicitly out of scope.

- **S18 - repository hygiene (D4).** There is no `SECURITY.md`, CODEOWNERS, or issue/PR template, and no `.gitattributes` despite a LF/CRLF mix. Add the hygiene files so line endings and reporting paths are defined.

- **S19 - branch protection check (D5).** Whether a failing job blocks a merge cannot be established from the repository. Check read-only and report; changing GitHub repository settings is a remote policy mutation that requires an explicit go and is not performed here.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 | inline | done | bbe5bd3 |
| T2 | S2 | inline | done | bbe5bd3 |
| T3 | S3 | inline | done | bbe5bd3 |
| T4 | S4 | inline | done | d0a2032 |
| T5 | S5 | inline | done | d0a2032 |
| T6 | S6 | inline | done | d0a2032 |
| T7 | S7 | inline | done | d0a2032 |
| T8 | S8 | inline | done | d0a2032 |
| T9 | S9 | inline | done | d0a2032 |
| T10 | S10 | inline | done | d0a2032 |
| T11 | S11 | inline | done | this commit |
| T12 | S12 | inline | done | this commit |
| T13 | S13 | inline | pending | - |
| T14 | S14 | delegated | pending | - |
| T15 | S15 | inline | pending | - |
| T16 | S16 | inline | pending | - |
| T17 | S17 | inline | pending | - |
| T18 | S18 | inline | pending | - |
| T19 | S19 | inline | pending | - |

## Log

### L1 - the owner authorized the whole roadmap except section E (verbatim)

> Todo menos el E

This follows the ranked audit (three defects A1-A3, seven consumer gaps B1-B7, four lying documents C1-C4, five engineering items D1-D5) presented at the end of the 2026-11-17 session. Section E is the do-not-do list and stays untouched.

### L2 - state at start

`main` and `origin/main` are equal at `4b9ca71`, working tree clean. The three `Partition` `ArrayPool<Error>.Shared.Rent` sites are confirmed at lines 208, 644 and 699 of `src/ResulTus.Collections/ResultCollectionExtensions.cs`. `ValidationErrors` aliases the caller's list at lines 25-40. `Match` has no null guard at `Result.cs:73-75` and `Result{TSuccess,TError}.cs:111-112`. The first round targets the `1.1.2` patch slice: T1-T10.

### L3 - defects A1-A3 fixed (T1-T3)

**T1 / A1 - `Partition` pool leak.** In all three overloads the second `ArrayPool<Error>` rent moved inside the `try`, and the `finally` now returns the failure buffer only when it was actually rented. A throw from the second rent therefore returns the first buffer instead of leaking it. No deterministic test can force that allocation to fail without process-wide OOM, so S1's test-first exception applies; this is fixed by inspection and the three `Partition` suites stay green (Collections 111/111 on net10 and net11).

**T2 / A2 - `ValidationErrors` snapshot.** The constructor now copies the issues (`Issues = [.. issues]`) instead of aliasing the caller's list, and the XML doc states the copy.

**T3 / A3 - `Match` guards.** A third overload was found beyond the two the audit named: `Result<T>.Match` at `Result{T}.cs:111`, in addition to `Result.Match` and `Result<TSuccess,TError>.Match`. All three now throw `ArgumentNullException` for a null `onSuccess` or `onFailure` before any branch runs, and each documents the exception. The single-generic overload had been missed in the initial pass and was caught by grepping every `public TResult Match` in `src/ResulTus/`.

**RED -> GREEN evidence.** Tests were written first (`tests/ResulTus.Tests/MatchContractTests.cs` for all three overloads across both branches; `Constructor_ShouldSnapshotTheSourceCollection` in `tests/ResulTus.Validation.Tests/ValidationErrorsTests.cs`). With the four source files stashed, the tests failed: 3/3 `MatchContractTests` FAIL and `Constructor_ShouldSnapshotTheSourceCollection` FAIL. After restoring the fixes: ResulTus.Tests 3/3, Validation 28/28 on both TFMs, Collections 111/111 on both TFMs. Full solution build is 0 warnings, 0 errors.

**Risk:** none of items 1-6 - no stored data, security, public contract shape, concurrency, delivery, or untested behavior changes. `Match` keeps its signature and 0 B success path; `ValidationErrors` gains one copy allocation on a non-hot construction path; the `Partition` edit is on an OOM-only exit path.
### L4 - documents and consumer gaps closed (T4-T10)

**T4 / C1 - CHANGELOG.** Added an `## [Unreleased]` section listing the three fixes, moved the `[Unreleased]` comparison link to `v1.1.1...HEAD`, and added the missing `[1.1.1]` and `[1.1.0]` link references so every version header resolves.

**T5 / C2 - versioning spec.** Replaced the corrupted token in `odd/specs/versioning.md:43` with `diagnostic-free`.

**T6 / C3 - AGENTS.md.** Replaced the false "not a published publish-and-run result" claim with the observed CI proof: `aot-smoke` publishes and runs trimmed plus Native AOT on Windows, `aot-smoke-linux` does Native AOT on Linux.

**T7 / C4 - stale ODD status.** `phase-11-ci-cd.md` now reports `done`, its scope table matches the shipped workflows, metadata and DocFX site, and an `L8` records the reconciliation. `audit-first-pass.md` S8 notes the multi-targeting premise is obsolete; T6 stays owner-routed but reads `moot - awaits owner close`, because closing it is an owner decision, not an engineering one.

**T8 / B5 - Roslyn minimum.** Both `ResulTus.Analyzers/README.md` and `ResulTus.SourceGenerators/README.md` now state the Roslyn 4.14.0 minimum and that an older compiler does not load the assembly.

**T9 / B6 - `HttpClientError` factories.** Added a construction-helpers table to `ResulTus.HttpClient/README.md` with the four factory signatures and their arguments.

**T10 / B7 - Spanish table.** Restored the package links in `README.es.md`; all 13 rows now match the English edition.

**Risk:** none of items 1-6. Documentation and task-status text only, plus one spec typo.

### L5 - net8/net9 test matrix (T11)

**T11 / S11 - the test matrix reaches every supported TFM.** All 13 test projects now multi-target `net8.0;net9.0;net10.0;net11.0`. `ci.yml` gained `9.0.x` and `8.0.x` next to `11.0.x`/`10.0.x` in `build-and-test` and `coverage-gate`, because a test project that targets a runtime the runner does not have fails at execution time, not at build time. The AOT jobs and `docs` keep their single-SDK lists: they publish one sample and build metadata for one TFM.

**Route: inline.** Thirteen one-line `TargetFrameworks` edits and two YAML lists. No Writer trigger held: no independent behavior units, and the change is mechanical.

**No runtime pin in the repository.** An earlier attempt pinned `<RuntimeFrameworkVersion>8.0.33</RuntimeFrameworkVersion>` per test project to dodge a local `MSB3030 apphost.exe not found`. That was a machine artifact, not a repository decision: this host has the .NET 8 runtime but no .NET 8 SDK, so `packs/Microsoft.NETCore.App.Host.win-x64` has no 8.x entry while `KnownAppHostPacks` from SDK 11 asks for 8.0.34. The pin was reverted and the environment was fixed instead, by staging a local packs overlay (junctions to the installed packs plus the downloaded 8.0.34 host pack) and passing `-p:NetCoreTargetingPackRoot=<overlay>` on this machine only. CI needs no overlay because `setup-dotnet` installs the 8.0/9.0 SDKs, which bring their host packs; a requested 8.0.x apphost pack resolves up to any installed 8.0.x patch, as observed with 8.0.28 resolving to the 8.0.33 pack.

**Evidence.** `dotnet build ResulTus.slnx`: 0 warnings, 0 errors with four TFMs. `dotnet test ResulTus.slnx --no-build` on net8.0, net9.0, net10.0 and net11.0: ApiCompat 11, Analyzers 47, AspNetCore 45, Collections 111, Diagnostics 29, HttpClient 50, Logging 36, Serialization 86, SourceGenerators 53, Testing 19, Validation 28 and ResulTus.Tests 86 pass on every TFM. The only failures are the three owner-disposed `AsyncAllocationAuditTests` in Extensions (198/201 on all four TFMs), identical on every runtime.

**Cost.** One extra `dotnet test` execution per TFM in `build-and-test`; the coverage gate stays on net10.0 because it passes `--framework net10.0` explicitly, so the measurement is unchanged.

**Risk:** item 5 (delivery) - CI job configuration changes. No stored data, security surface, public contract, or concurrency change.

### L6 - FluentValidation behavior coverage (T12)

**T12 / S12 - the audit premise was stale; one real gap closed.** `tests/ResulTus.Validation.Tests/FluentValidationTests.cs` already carried ten behavioral tests when this task started: sync success, sync multi-failure with property, message and code, repeated failures not deduplicated, an async-rule validator refused by the sync overload, async success, async failure including the asynchronous rule, cancellation-token forwarding, cancellation surfacing as `OperationCanceledException`, and the null-property-name projection. S12 asked to "add behavioral tests for the sync and async validation paths and the projection into `ValidationErrors`", so the only case the suite did not pin is the async overload's null-instance guard, which S12's own wording covers because it names both paths.

**Change.** One test, `NullInstance_ValidateAsync_ShouldThrow`: a null instance through `ValidateAsync` throws `ArgumentNullException` and the asynchronous rule never observes a token, so the sync and async overloads reject the same input. It pins existing behavior, so it is a characterization test: no RED exists, the guard already shipped.

**Evidence.** Release coverage gate, whole solution: ResulTus 100%, Extensions 98.64%, Collections 98.55%, Serialization 96.93%, AspNetCore 98.44%, HttpClient 93.21%, Logging 100%, Diagnostics 100%, Validation 100%, Testing 100%, Validation.FluentValidation 100%, all at or above the 90% line threshold. Validation tests: 29/29 on net8.0 and net11.0 (28 before the added test).

**Observed, not scheduled.** The three `AsyncAllocationAuditTests` that fail in Debug pass in Release, which is why the Release coverage gate is green while a Debug suite run reports them; that is consistent with the owner's disposition and stays untouched.

**Risk:** none of items 1-6. One test that asserts already-shipped behavior; no production code changed.