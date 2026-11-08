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
- **S5** — "Code coverage >= 95% (measured by coverlet or similar)".

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
| T1 | S1 | undecided | pending | — |
| T2 | S2 | undecided | pending | — |
| T3 | S3 | undecided | pending | — |
| T4 | S4 | undecided | pending | — |
| T5 | S5 | undecided | pending | — |

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
