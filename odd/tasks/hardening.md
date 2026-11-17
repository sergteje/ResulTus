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
| T13 | S13 | inline | done | this commit |
| T14 | S14 | inline | done | this commit |
| T15 | S15 | delegated | done | this commit |
| T16 | S16 | inline | done | this commit |
| T17 | S17 | inline | done | this commit |
| T18 | S18 | inline | done | this commit |
| T19 | S19 | inline | done | this commit |

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

### L7 - typed async contract coverage (T13)

**T13 / S13 - the audit premise was partly stale; the real gaps are closed.** `CrossTierSemanticsTests` already proved Task-versus-ValueTask agreement against the synchronous outcome for eleven of the typed operators (`MapAsync`, `BindAsync`, `TapAsync`, `MatchAsync`, `RecoverAsync` in both shapes, `FinallyAsync`, `MapErrorAsync`, `OnFailureAsync`, `SwitchAsync`, `EnsureAsync`), and its `Typed_AsyncMethods_NullAndCancelledShouldFollowTheContract` battery covered null delegates and cancellation for four of them. The typed file measured 95.83% lines and 100% branches before this task.

**Gaps that were real.** `OnSuccessAsync` had no contract test at all even though it is public surface on both sources, and it is a pure alias of `TapAsync`, so nothing pinned that the alias never drifts. The null `Task` source guard (`ArgumentNullException.ThrowIfNull(result)`, present in every `Task`-source wrapper) was untested for the whole tier. The cancelled-token contract existed for four of the twelve call shapes, so the "cancellation is control flow, never a captured failure" rule was demonstrable only for a sample.

**Change.** New `tests/ResulTus.Extensions.Tests/TypedResultAsyncContractTests.cs` with five facts: `OnSuccessAsync` through each source runs the effect once with the success value, never on failure, and returns the original result unchanged; a cancelled token makes it throw `OperationCanceledException` without running the effect; a null `Task` source throws `ArgumentNullException` for all twelve call shapes of the tier; and a cancelled token throws `OperationCanceledException` before awaiting the source or invoking the delegate for the eight operators that had no such test, asserting the delegates were never invoked. The four already covered are named in the file's remarks instead of duplicated.

**Evidence.** Extensions in Release: 206/206. Debug on net8.0 and net11.0: 203/206, the three failures being the owner-disposed `AsyncAllocationAuditTests` that only fail outside Release. No production code changed, so no RED exists for these tests; they pin shipped behavior.

**Risk:** none of items 1-6. Test-only change.

### L8 - asynchronous exception capture (T14)

**T14 / S14 - `Result.TryAsync` and `Result.TryValueTaskAsync`.** Four new overloads on `Result`: value-producing and unit delegates for the `Task` and the `ValueTask` source. Each takes the `CancellationToken` the delegate receives, checks it before the delegate starts, captures any non-cancellation exception through `Error.FromException`, and rethrows `OperationCanceledException`, so the synchronous rule ("cancellation is control flow, never an error") now holds across the asynchronous boundary.

**Scope decision: no `Func<Exception, Error>` overloads.** The synchronous tier has two mapping overloads per shape; the asynchronous tier deliberately ships none, because S14 names the capture helpers and the mapping need is not part of it, and adding them later is additive and non-breaking. Recorded here so the asymmetry is a decision, not an omission.

**Route: inline.** One coherent behavior across production code, tests, docs and snapshot; no independent unit could run in parallel and no writer reason held.

**Test-first evidence.** `tests/ResulTus.Tests/TryAsyncContractTests.cs` was written first and failed to compile with `CS0117: 'Result' no contiene una definición para 'TryAsync'` (the methods did not exist). After the implementation: 12/12 pass on net10.0, and the suite reports 98 tests in ResulTus.Tests on all four target frameworks.

**Public surface and versioning.** `dotnet run --project scripts/ApiSnapshot -- --write` added exactly four lines, all in `PublicApi/ResulTus.txt`; no other snapshot changed, so nothing else drifted. Adding members is a minor bump, which is the `1.2.0` slice this feature forecast for the B1-B4 group, and `CHANGELOG.md` records it under `## [Unreleased] / ### Added`.

**Documentation.** `docs/error-handling.md` extends the "From an exception" section: what the helpers do, the snippet with both sources, and the limit that they do not replace `try/catch` around code you cannot change. The snippet was extracted into a scratch console project outside the worktree referencing `src/ResulTus` and built with 0 warnings, so it compiles today and not only once the snippet gate (T15) exists.

**Evidence.** Solution build: 0 warnings, 0 errors across four target frameworks. Full suite: everything green except the three owner-disposed `AsyncAllocationAuditTests` that fail only outside Release. ApiCompat: 11/11 on net8.0, net9.0, net10.0 and net11.0.

**Risk:** none of items 1-6. The change is additive public API with no altered signature, no behavior change to existing members, no stored data, and no concurrency change.

### L9 - repository hygiene (T18)

**T18 / S18 - the missing hygiene files, and the line-ending decision that goes with them.** `.gitattributes` now defines line endings instead of leaving them to each contributor's `core.autocrlf`: `* text=auto`, explicit text types, `*.ps1` as CRLF, `*.sh` as LF, and the four branding PNGs plus signing artifacts as binary. `SECURITY.md` states the supported lines, the private reporting channel, what a useful report contains and what is out of scope. `.github/CODEOWNERS` names the owner for every path. `.github/PULL_REQUEST_TEMPLATE.md` turns the repository's own rules into a checklist, including the risk line AGENTS.md requires. `.github/ISSUE_TEMPLATE/` carries a bug form and a feature form that asks for the real use, which is the rule that keeps opinionated machinery out.

**The one-time renormalization is not part of this change.** `git add --renormalize .` would rewrite the line endings of 308 tracked files in a single commit. That is content-preserving but it touches every file, and doing it while `feature/hardening` is in flight would conflict with this branch on every merge. `.gitattributes` carries the note, and the decision stays with the owner, best taken from `main` with no branch open.

**Owner action, not performed here.** Private vulnerability reporting has to be enabled in the repository settings for the `SECURITY.md` channel and the contact link to resolve. Changing repository settings is a remote policy mutation, so it is reported, not executed.

**Risk:** none of items 1-6. Repository metadata and documentation only; no code, no build, no workflow change.

### L10 - CI cost and duplicated work (T16)

**T16 / S16 - one build instead of two, and a restore cache.** `coverage-gate` was a second job that checked out, installed four SDKs, restored and rebuilt the solution, then ran the same test projects the previous job had already built. Its single step now runs at the end of `build-and-test`, which reuses that job's Release build: `scripts/coverage-gate.ps1` already passes `--framework net10.0 --no-build`, so nothing in the measurement changed. The job count drops from five to four and the duplicated checkout, SDK install, restore and build disappear.

**Caching.** Each of the four remaining jobs restores `~/.nuget/packages` with `actions/cache@v4`, keyed on `Directory.Packages.props`, every `*.csproj` and `global.json`, plus `.config/dotnet-tools.json` for the docs job, with an operating-system restore key. A dependency bump invalidates the cache; nothing else does. The key does not include source files, because restore does not depend on them.

**No gate weakened.** Same script, same `-Threshold 0.90`, same failure semantics: the job that builds and tests is now also the job that fails on a package below the line. Locally the script reports all eleven packages at or above 90% lines.

**Job-name safety.** Removing the `coverage-gate` job cannot strand a required status check: `main` reports "Branch not protected", so no check is required (recorded in T19). Enabling protection is an owner decision about repository settings and is not performed here.

**Validation.** The workflow was parsed with a real YAML parser (`YamlDotNet` in a scratch project outside the worktree) which read back four jobs and their step counts, so the file is not merely indented plausibly.

**Risk:** item 5 (delivery) - CI job configuration. No gate threshold, script, or coverage measurement changed.

### L11 - branch protection, read-only (T19)

**T19 / S19 - nothing blocks a merge today.** Read-only queries on 2026-11-17: `GET /branches/main/protection` answers `Branch not protected` (HTTP 404) and `GET /rulesets` answers `[]`. No required status check exists, so a red CI run does not prevent a merge; the merge decision is entirely manual. This is why T16 could remove the `coverage-gate` job name without stranding a check.

**Same query, adjacent settings.** The repository is public, Dependabot security updates are enabled, secret scanning is disabled (with push protection, non-provider patterns and validity checks all disabled), Actions allow every action, and SHA pinning is not required.

**What an owner would decide, not done here.** Enabling a branch ruleset on `main` (require the `build-and-test` status, forbid force pushes and deletion), turning on secret scanning with push protection, and requiring SHA pinning for third-party actions are all repository-settings mutations. Each needs an explicit go, so this task reports them and changes nothing.

**Risk:** none of items 1-6. Read-only API queries and a documentation entry.

### L12 - package validation, provenance and security scanning (T17)

**T17 / S17 - a pack-time baseline, signed provenance and two free scanners.** `Directory.Build.props` turns on `EnablePackageValidation` with `PackageValidationBaselineVersion 1.1.1`, which is the existing package-validation baseline the audit asked for: `dotnet pack` now compares the packed surface against the last released package and fails on a break. `release.yml` attests build provenance for the exact nupkg and snupkg bytes before anything is pushed, with the `id-token` and `attestations` permissions it needs. Two new workflows cover the scanning gap: `codeql.yml` analyzes the C# surface on push, pull request and a weekly schedule, and `dependency-review.yml` fails a pull request that adds a high or critical advisory.

**The baseline is not a no-op, and that was measured.** Packing at 1.1.1 against 1.1.1 passes; packing at a simulated 1.2.0 against 1.1.1 also passes, which is the add-only change this branch ships; and packing with a baseline of 9.9.9 fails with `NU1102`, proving the check engages instead of silently succeeding. The versioning spec records the rule that the baseline moves forward when a new minor or major ships.

**`dotnet nuget verify` is deliberately absent.** Measured: `dotnet nuget verify` on the package this workflow produces answers `NU3004: the package is not signed`, because the project ships without author signing, which S17 puts out of scope. Adding the step would fail every release, so the release workflow documents why the pack-time package validation is the content gate instead. Signature verification becomes meaningful only if author signing is adopted.

**CodeQL needs a manual build.** Autobuild runs one `dotnet build` with one SDK, which cannot compile a solution whose test projects multi-target net8.0-net11.0 and whose xUnit v3 projects need an app host per framework, so the workflow installs the same four SDKs as `build-and-test` and builds the solution itself.

**Observed gap, owner's decision.** Secret scanning and push protection are disabled on the repository, and enabling them is a repository-settings change, recorded in T19 rather than performed.

**Validation.** All five workflows were parsed with `YamlDotNet` in a scratch project outside the worktree; `release.yml` reads back ten steps and each new workflow one job. Action semantics themselves can only be proven by a real run, which happens on the next push and pull request.

**Risk:** item 5 (delivery and release) - a new deploy-time gate and two new workflows. The library's public surface and behavior are untouched.

### L13 - documentation snippet gate (T15)

**T15 / S15 - the rule became a check.** `scripts/SnippetGate` is a repository tool that compiles every ```csharp fence of `README.md`, `README.es.md`, `docs/**/*.md` and `src/*/README.md` against the shipped `src/` projects and fails on any compile error. It is a delegated unit: the specification was written by the orchestrator, a bounded worker implemented it, and the orchestrator re-ran it and produced its own negative control before accepting it.

**Shape.** Per document it emits one source file into a temporary project under the system temp directory: using directives hoisted once and deduplicated, type and namespace declarations emitted verbatim at namespace level (so `[JsonSerializable]` and `[ResultErrors]` see what a reader would paste), and each statement-shaped block wrapped in its own `static void` or `static async Task` method. One generated solution and one `dotnet build` compile the whole set; compiler diagnostics are mapped back to `document:line (block #n)`.

**One compilation per document is a measured requirement, not a preference.** Documents that each declare `AppJsonContext` crash the System.Text.Json generator when compiled together: it names its output after the declared type, so two same-named contexts collide on a duplicate hint name. Per-document assemblies remove that collision and keep every document self-contained.

**The stub harness is deliberately small.** `scripts/SnippetGate/Stubs/Globals.stubs.cs` supplies only the state the prose carries across fences: `args`, `failed`, `logger`, `json`, `converter`, `options`, plus a real `[JsonSerializable(typeof(int))]` context behind the shared converter. Every document that declares one of those names still declares it, and the local declaration shadows the stub, so no stub reshapes a snippet to make it compile. `Parse`, `Find`, `FetchAsync`, `Product`, `DomainError` and the per-document contexts are declared by the documents themselves; only the cross-fence state needed help.

**No exclusion path.** A ```csharp fence with no content fails the gate, and a pseudo-code block has to be retagged to ```text so the document stops claiming it is C#. None needed retagging: all 126 blocks are real C# and every one compiles, so the documentation diff of this task is empty.

**Evidence, verified by the orchestrator.** `dotnet run --project scripts/SnippetGate -c Release` prints `snippet gate: 24 documents, 126 blocks compiled, none excluded` and exits 0 in 26 seconds. An injected `int __orchestratorProbe = "text";` in `docs/getting-started.md` makes it fail with `docs/getting-started.md:29 (block #2): CS1001`, exit 1, while keeping the generated project for inspection; reverting the document returns the gate to green and `git diff` over `docs` is empty. The worker additionally showed an injected `CS0029` and an injected rename of `Result.Success` to `Result.Succeeded` (`CS0117`) each failing with the exact document, line and block, and reported no snippet that disagreed with the shipped API.

**CI and documents.** One step, `Documentation snippet gate`, runs after the coverage gate in `build-and-test`; the workflow was parsed back with a YAML parser and reads seven steps in that job. `AGENTS.md` and `CONTRIBUTING.md` no longer describe the manual "extract the blocks and build them in a throwaway project" procedure: both point at the gate and at where the stubs live.

**Known limits, stated rather than hidden.** The generated project enables implicit usings, so a snippet that omits `using System;` still compiles although a pasted copy would not; the gate compiles the blocks, it does not execute them; and it costs about 26 seconds warm.

**Risk:** item 5 (delivery) - one new CI step. No library code, public surface, or document content changed.

### L14 - closure: a contract the roadmap broke, and the consolidated run

**A regression the roadmap caused, caught by the suite.** Merging the coverage gate into `build-and-test` (T16) broke `DocfxSiteConfigTests.CiWorkflow_ShouldBuildDocfxSiteInDocsJob`, which asserted a job literal, `coverage-gate:`. The test was right to fail: it exists so a CI job cannot disappear silently. The contract it protects is the gate, not the job that hosted it, so `432844f` re-pins it where the gate now runs, adds the documentation snippet gate to the same assertion, and keeps every other job plus the read-only top-level permission. This is the kind of defect the roadmap's own checks are supposed to catch, and they did.

**Coverage the new API cost, restored.** `Result` dropped to 97.47% lines after T14 because the unit overloads' two cancellation exits had no test. One test now covers both sources, before-start and in-flight cancellation, and asserts the delegate never ran; the package is back to 100%.

**Consolidated Release run, the CI equivalent.** `dotnet build ResulTus.slnx -c Release`: 0 warnings, 0 errors. `dotnet test ResulTus.slnx -c Release --no-build`: 52 of 52 test-project runs green (13 projects on net8.0, net9.0, net10.0 and net11.0), ApiCompat included. Per-package coverage: 11 of 11 at or above 90% lines. Documentation snippet gate: 24 documents, 126 blocks, none excluded. The three `AsyncAllocationAuditTests` do not appear because Release is the configuration CI uses.

**Roadmap state.** T1-T19 are done: 12 commits on `feature/hardening` after the two from the `1.1.2` slice. Remaining owner decisions, none of them engineering blockers: push, whether the slice ships as one pull request or as the forecast chain, the one-time line-ending renormalization, and enabling branch protection plus secret scanning in the repository settings.

### L15 - delivery as one pull request of 1.2.0 (owner, 2026-11-17)

The owner settled the four open decisions in one message: "1 PR unico como 1.2.0 / 2 olvidado / 3 adelante / 4 ok". Recorded verbatim and interpreted as follows.

**1 - one pull request, released as `1.2.0`.** The forecast chain (`1.1.2` then `1.2.0`) is dropped: the whole hardening slice ships as a single pull request whose version is `1.2.0`. `Directory.Build.props` moves `Version` to `1.2.0`, `CHANGELOG.md` turns its `[Unreleased]` section into `## [1.2.0] - 2026-11-17` with the link references shifted (`[Unreleased]` now compares from `v1.2.0`), and both README editions state that the packages ship at `1.2.0`. `PackageValidationBaselineVersion` stays at `1.1.1` on purpose: a baseline must already exist on the feed, so it moves in the change after the release; the versioning spec now says so.

**2 - the line-ending renormalization is forgotten.** The owner's word is "olvidado". `.gitattributes` stays as the forward-looking definition and the 308-file renormalization will not happen, so this closes that item for good.

**3 - the remote settings go ahead.** Branch protection on `main`, secret scanning with push protection, and the Actions hardening are applied from this session with the owner's explicit go. What was applied and what was deliberately left out are recorded in the delivery log entry that follows this decision.

**4 - the baseline bump is acknowledged.** It happens after `1.2.0` is published, as the versioning spec now states.

**A release defect found while preparing the release.** `release.yml` extracted the notes with `$0 == "## [1.2.0]"`, an equality match against a heading that carries a date, so every release so far published empty notes. It now matches the section by prefix and fails the job when the section produces no text, so a release can no longer be created with no notes.

### L16 - correction: CodeQL was already active, so no workflow was needed

**Correction to L12.** The audit claim "the workflow inventory has no CodeQL" was literally true and still wrong: `GET /repos/{owner}/{repo}/code-scanning/default-setup` shows the repository has used GitHub's default setup for `actions` and `csharp` since 2026-11-09, with a weekly schedule and the default query suite. Adding `codeql.yml` duplicated that analysis, and the first pull request showed both suites running the same commit. The workflow was removed; the repository keeps the default setup, which also covers the workflow files that a C#-only workflow would miss. `dependency-review.yml` and the package validation/provenance work stand unchanged, so the scanning gap T17 named is closed by configuration that already existed.

**Consequence for the roadmap.** T17 keeps its outcome (a pack-time baseline, provenance, dependency review, and code scanning active) with one fewer repository file, and the phase-11 scope table records CodeQL as active without a workflow. The lesson is recorded because it will recur: a repository-settings capability is not visible in the repository, so an audit of files alone cannot see it.

### L17 - delivery: pull request 15 and the repository settings

**Pull request.** https://github.com/sergteje/ResulTus/pull/15, `feature/hardening` into `main`, titled "feat: 1.2.0 hardening, async capture and delivery gates". The branch was 14 commits ahead of `main` and level with it when the pull request opened. The body fills the repository's own template, states the risk line, lists the Release-configuration evidence and records the two owner steps after merge.

**Settings applied, on the owner's "3 adelante".** Branch protection on `main`: the `build-and-test` status is required and branches must be up to date, force pushes and deletions are blocked, and administrator enforcement is off so the owner keeps an escape hatch on a repository they alone maintain. No approving-review requirement, because GitHub cannot let an author approve their own pull request and a solo maintainer would block every merge. Secret scanning and push protection are enabled; secret-scanning validity checks answer `disabled` even when requested, so that capability is not available on this repository. Private vulnerability reporting is enabled, which makes the channel `SECURITY.md` documents and the issue-template contact link real.

**Deliberately not applied.** SHA pinning for Actions is left off: every workflow pins actions by tag (`actions/checkout@v7`), and turning the setting on would fail every run until sixteen action references are rewritten to full commit SHAs and Dependabot is taught to keep them. That is a change with its own pull request, not a checkbox. Requiring the CodeQL and dependency-review checks is also deferred until they have a green run on this pull request; requiring an unproven check risks blocking every future merge.

**Correction carried in the same delivery.** `codeql.yml` was removed in `53f7904` because the repository already runs CodeQL through GitHub's default setup (L16).