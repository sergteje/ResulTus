# Phase 11 — CI/CD and release

Objective: give the repository the infrastructure it still does not have — a GitHub Actions pipeline that restores, builds, tests, packs and publishes, plus the release and packaging metadata around it. Status: **not started**. The repository has no `.github/` directory today, so nothing builds or tests on push or pull request.

Branch: none created. Delivery strategy: undecided. Test runner: `dotnet test ResulTus.slnx` once a workflow exists to call it.

Recovered from `openspec/changes/phase-11-ci-cd/proposal.md` after that store was retired. The proposal itself is quoted verbatim in `L1` below; nothing in it has been executed.

## Specs

Each line quotes the retired proposal's success criteria verbatim; they have not been reviewed or renegotiated.

- **S1** — "CI workflow runs on push/PR: builds + tests all projects".
- **S2** — "Release workflow publishes NuGet packages on `v*` tag".
- **S3** — "BenchmarkDotNet suite runs and reports comparative results".
- **S4** — "DocFX site builds without errors".
- **S5** — "Code coverage >= 95% (measured by coverlet or similar)". *(Renegotiated by the
  owner: the gate is fixed at 90% lines per package — commit `a2fe88a`, README badges at that
  target; see L3 postscript and L4.)*

Scope carried over, item by item, with what is already true today:

| Proposal item | State |
| --- | --- |
| `.github/workflows/ci.yml` | absent |
| `.github/workflows/release.yml` | absent |
| `tests/ResulTus.Benchmarks/` | **exists** — the proposal line is obsolete |
| `README.md` | **exists**, canonical in two editions — obsolete |
| `CONTRIBUTING.md` | absent |
| NuGet metadata in `Directory.Build.props` | partial: `Description` and `RepositoryUrl` present, `PackageTags`/`PackageIcon` absent |
| DocFX site under `docs/` | absent; `docs/` holds guides and the performance charter |

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 | inline | done | `265c144` (workflow added), `d70133e` (explicit permissions) |
| T2 | S2 | inline | done | `265c144`, `5e4c9ee`, `d70133e`; observed runs 42867362862 (0.0.2), 0.0.3, 1.0.0 |
| T3 | S3 | delegated writer (2 sessions: surface gap on `Program.cs`, then run + document) | done | `79846e1` — see L6 |
| T4 | S4 | delegated writer (1 session) | done | `786425f` — see L6; independent verifier in progress |
| T5 | S5 | inline (verification) | done | gaps closed `6492691`/`0b4c05f`/`109400e`; gate `a2fe88a`; local rerun green — see L5 |

## Log

### L1 — the retired proposal (verbatim)

> # Proposal: Phase 11 — CI/CD and Release
>
> ## Intent
>
> Set up GitHub Actions CI/CD, BenchmarkDotNet suite, NuGet publishing, DocFX documentation, and project-wide metadata files.
>
> ## Scope
>
> ### In Scope
> - GitHub Actions: Restore → Build → Test → Pack → Publish
> - NuGet publish via tag creation (v*)
> - BenchmarkDotNet suite comparing with FluentResults, ErrorOr, CSharpFunctionalExtensions
> - DocFX documentation site in `docs/`
> - README, CONTRIBUTING, LICENSE updates
> - NuGet package metadata (icon, description, tags)
>
> ### Out of Scope
> - Enterprise features
> - Private NuGet feed
>
> ## Capabilities
>
> ### New Capabilities
> None — infrastructure and release only.
>
> ### Modified Capabilities
> None.
>
> ## Approach
>
> Standard .NET GitHub Actions workflow. NuGet push only on `v*` tags. BenchmarkDotNet project in `tests/`. DocFX in `docs/`. All packages share metadata via `Directory.Build.props`.
>
> ## Affected Areas
>
> | Area | Impact | Description |
> |------|--------|-------------|
> | `.github/workflows/ci.yml` | New | CI build + test |
> | `.github/workflows/release.yml` | New | NuGet publish on tag |
> | `docs/` | New | DocFX documentation site |
> | `tests/ResulTus.Benchmarks/` | New | BenchmarkDotNet suite |
> | `README.md` | Modified | Full project documentation |
> | `CONTRIBUTING.md` | New | Contribution guidelines |
>
> ## Risks
>
> | Risk | Likelihood | Mitigation |
> |------|------------|------------|
> | NuGet API key management | Low | GitHub secrets; scoped API key |
> | Benchmark interpretation needs baseline | Low | Include FluentResults/ErrorOr as baselines |
>
> ## Rollback Plan
>
> Remove `.github/workflows/`. Delete `docs/` and `tests/ResulTus.Benchmarks/`. Revert README/CONTRIBUTING to previous state.
>
> ## Dependencies
>
> - All phases 1-10 complete
> - NuGet.org account and API key
> - GitHub repo with Actions enabled
>
> ## Success Criteria
>
> - [ ] CI workflow runs on push/PR: builds + tests all projects
> - [ ] Release workflow publishes NuGet packages on `v*` tag
> - [ ] BenchmarkDotNet suite runs and reports comparative results
> - [ ] DocFX site builds without errors
> - [ ] Code coverage >= 95% (measured by coverlet or similar)

### L3 — S1 and S2 shipped days ago; S5's coverage has a measured baseline (2026-11-10)

- **S1 done**: `ci.yml` runs build + the full suite on every push to `main` and every pull request (first observed run 42631408235, green).
- **S2 done**: `release.yml` published 0.0.2 to nuget.org on the `v0.0.2` tag (run 42867362862, 26 push confirmations).
- **S3 (benchmarks reporting) is the remaining open item; S4 (DocFX) stays parked** until a documentation site is a product want.
- **S5 measured, not installed**: the coverlet collector ran per test project under `-c Release`, reading each module's own `<package>` element from the cobertura output:

| Package | Lines | Branches |
| --- | ---: | ---: |
| ResulTus | 92.62% | 95.00% |
| ResulTus.Extensions | 98.63% | 96.23% |
| ResulTus.Collections | 83.88% | 77.73% |
| ResulTus.Serialization | 96.92% | 92.19% |
| ResulTus.AspNetCore | 71.87% | 83.33% |
| ResulTus.HttpClient | 83.02% | 61.22% |
| ResulTus.Logging | 100.00% | 95.45% |
| ResulTus.Diagnostics | 100.00% | 100.00% |
| ResulTus.Validation | 100.00% | 100.00% |
| ResulTus.Testing | 87.87% | 83.33% |

The proposal's 95% target reads against the repo aggregate of these numbers, and five packages sit under it. Installing the gate today would fail the build by design and hide nothing — these are the measured numbers, not estimates. Closing the gaps (AspNetCore, Collections, Testing, HttpClient first) is test work, not configuration. The gate becomes a step in `ci.yml` the day the owner fixes the definition (repo-aggregate lines or per-package minimum) and tickets the current gaps. The analyzers and the source generator are deliberately excluded: Roslyn projects earn coverage in their contract suites, not line percentages.

### L3 postscript — per-module measurement corrected the dilution, and the gaps closed (2026-11-10)

The original numbers above came out diluted: each test project's cobertura file also traces the
modules of the libraries it references, and a reference like `ResulTus.AspNetCore → ResulTus →
ResulTus.Validation` registered the transitively referenced production code at zero within the
aspnetcore run — classes that ARE covered, in runs belonging to other test projects. Per-package
coverage therefore asks for the merge analysis.

With per-module scoping (classes under the package's own directory) and the maximal-hits union
across the project files, the corrected numbers after this series:

| Package | Lines | Branches |
| --- | ---: | ---: |
| ResulTus | **100.00%** (244/244) | ≥95% |
| ResulTus.AspNetCore | **100.00%** (64/64) | — |
| ResulTus.Logging / Diagnostics / Validation / FluentValidation | **100.00%** | — |
| ResulTus.SourceGenerators | 99.14% | — |
| ResulTus.Extensions | 98.64% | — |
| ResulTus.Collections | **98.53%** | — |
| ResulTus.Serialization | 96.93% | — |
| ResulTus.Testing | **100.00%** (66/66) | — |
| ResulTus.HttpClient | **93.21%** (412/442) | — |

The remaining sub-95% gaps are inside compiler-generated async state corridors (`HttpClient`
8.86% of an async pipeline's lifted locals) and the analyzer/source-generator systems whose
coverage is contract-driven. A 25-cent measurement error poked under the release — the
05-cci.yml gate at 95% per-package lines would be RED on HttpClient's lifted-locals arithmetic
today, so the gate discussion stays open with the owner until that story is told. Suite: 753/0.

### L4 — close-out of the remaining phase-11 items (2026-11-11)

> L1 — owner request (verbatim): "venga 1,2 y 3 si tienes que delegar delega si tienes que meter
> al background mete al background"

The owner approved the three remaining items in one batch: (1) coverage gaps, (2) HttpClient
macro benchmarks, (3) the DocFX site.

- **Coverage (S5/T5):** the per-module correction already closed the gaps (L3 postscript) and
  the owner fixed the gate at 90% lines per package (`a2fe88a`, README badges included). The
  remaining work is verification only: rerun `scripts/coverage-gate.ps1` locally against a
  Release build to confirm every package passes its row. S5's renegotiated criterion is the
  90% per-package gate; the proposal's original 95% aggregate is superseded by the owner's
  decision, recorded here.
- **HttpClient macro benchmarks (S3/T3):** `tests/ResulTus.Benchmarks/` is net10.0-only today
  and references no `ResulTus.HttpClient`. The parked decision — net10-only rows vs a
  net8/9/10 runtime matrix for the macro benchmark — was asked to the owner this session;
  the writer launches once the answer lands.
- **DocFX (S4/T4):** nothing exists yet. One bounded writer creates the local-tool manifest,
  `docfx.json`, a minimal articles set, and a CI job proving the site builds without errors
  (S4's verbatim criterion). Publishing to GitHub Pages is deliberately out of scope until the
  owner asks for it.

Risk lines — DocFX/CI unit: item 5 (a new CI job is delivery-environment surface); benchmark
unit: item 6 until written, then reassessed.

### L5 — T5 closed: the gate passes locally at the owner's 90% (2026-11-11)

`dotnet build ResulTus.slnx -c Release` (0 errors) followed by
`powershell -NoProfile -File scripts/coverage-gate.ps1 -Threshold 0.90` locally — all eleven
packages PASS:

| Package | Lines | Gate |
| --- | ---: | --- |
| ResulTus | 100.00% | PASS |
| ResulTus.Extensions | 98.64% | PASS |
| ResulTus.Collections | 98.53% | PASS |
| ResulTus.Serialization | 96.93% | PASS |
| ResulTus.AspNetCore | 98.44% | PASS |
| ResulTus.HttpClient | 93.21% | PASS |
| ResulTus.Logging / Diagnostics / Validation / Testing / FluentValidation | 100.00% | PASS |

One environmental note for the record: the local machine has no `pwsh` (PowerShell 7), so
`pwsh -File scripts/coverage-gate.ps1` fails locally with CommandNotFound; the same invocation
is fine on the ubuntu CI runner. Locally, `powershell -File` runs the script unchanged. T5 is
done at the renegotiated criterion: 90% lines per package, gate green.

### L6 — T3 and T4 shipped; the three phase-11 remaining items are all realized (2026-11-11)

**T3 done (`79846e1`).** `HttpClientMacroBenchmark` — four macro methods over a fake
`HttpMessageHandler` (zero network), `[MemoryDiagnoser]`, AOT-safe `JsonTypeInfo<Widget>` route in
both camps, fresh `HttpRequestMessage` per invocation (single-use — the first run failed on this and
was corrected). Registration in `Program.cs` required an orchestrator-granted surface extension
(the fixed class list needs one `Run<T>` line; `--filter` silently skips unregistered classes).
Observed run (BDN 0.15.8, DefaultJob, i7-6700, net10.0): failure path **545.2 ns / 1.13 KB** vs raw
`EnsureSuccessStatusCode` **3,536.4 ns / 1.27 KB** — 6.5x, non-overlapping ranges; success is a tie
within noise (CIs overlap). `MultimodalDistribution` on three of four rows is recorded in
`docs/performance.md`, not hidden; the failure-path verdict does not rest on those means. Decision
002 (`docs/decisions/002-benchmarks-net10-only.md`) records the owner's net10-only choice — the
benchmark host is not multi-targeted. Risk: none — additive benchmark + observed numbers; no
production surface changed.

**T4 done (`786425f`).** DocFX site builds without errors: local tool manifest (docfx 2.81.0),
`docs/docfx.json` (metadata for the 11 production libraries pinned to net10.0; the two netstandard2.0
Roslyn components excluded — forcing net10.0 breaks their `PrivateAssets` compiler reference),
minimal `docs/index.md`, `docs/.gitignore`, and a `docs` CI job (checkout, setup-dotnet 10,
`dotnet tool restore`, `dotnet tool run docfx -- docs/docfx.json`). Writer verification: tool
restore exit 0, full docfx pipeline exit 0 ("Build succeeded. 0 warning(s) 0 error(s)", 59 HTML
pages), `git check-ignore` confirms `docs/_site`/`docs/api` ignored. Publishing to GitHub Pages
remains out of scope until the owner asks. assessed tier and outcome: 786425f high (escalated item 5; owner granted native review), 79846e1
medium/under_budget (covered by the slice review), cd209c7 passive, 6d72b95 passive-delta. The
independent verifier returned S4 MET (five probes pass, no blockers; scratch removed). The native
compact review (lenses readability + reliability, lineage `review-185a679d835b947c`) returned
**approved** with zero blockers and nine advisory findings (benchmark `_checksum` dead sink,
unanchored substring checks in `DocfxSiteConfigTests`, floating SDK in the `docs` job, duplicated
Roslyn exclusion rule) — acknowledged, authority burned; the reviewed boundary advances to
`6d72b95`. Advisory findings are follow-up work, not corrections.

**Phase-11 status:** T1–T5 all done. The proposal's five success criteria are realized: CI (S1),
tag release (S2), benchmark comparative results (S3, realized as the HttpClient macro suite the
owner chose), DocFX site (S4), coverage (S5, renegotiated by the owner to the 90% per-package
gate). Remaining open by choice: GitHub Pages publishing (S4 extension), and the bench-interpretation
baseline coverage for the macro suite grows as the owner asks for rows.
