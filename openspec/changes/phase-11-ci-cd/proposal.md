# Proposal: Phase 11 — CI/CD and Release

## Intent

Set up GitHub Actions CI/CD, BenchmarkDotNet suite, NuGet publishing, DocFX documentation, and project-wide metadata files.

## Scope

### In Scope
- GitHub Actions: Restore → Build → Test → Pack → Publish
- NuGet publish via tag creation (v*)
- BenchmarkDotNet suite comparing with FluentResults, ErrorOr, CSharpFunctionalExtensions
- DocFX documentation site in `docs/`
- README, CONTRIBUTING, LICENSE updates
- NuGet package metadata (icon, description, tags)

### Out of Scope
- Enterprise features
- Private NuGet feed

## Capabilities

### New Capabilities
None — infrastructure and release only.

### Modified Capabilities
None.

## Approach

Standard .NET GitHub Actions workflow. NuGet push only on `v*` tags. BenchmarkDotNet project in `tests/`. DocFX in `docs/`. All packages share metadata via `Directory.Build.props`.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `.github/workflows/ci.yml` | New | CI build + test |
| `.github/workflows/release.yml` | New | NuGet publish on tag |
| `docs/` | New | DocFX documentation site |
| `tests/ResulTus.Benchmarks/` | New | BenchmarkDotNet suite |
| `README.md` | Modified | Full project documentation |
| `CONTRIBUTING.md` | New | Contribution guidelines |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| NuGet API key management | Low | GitHub secrets; scoped API key |
| Benchmark interpretation needs baseline | Low | Include FluentResults/ErrorOr as baselines |

## Rollback Plan

Remove `.github/workflows/`. Delete `docs/` and `tests/ResulTus.Benchmarks/`. Revert README/CONTRIBUTING to previous state.

## Dependencies

- All phases 1-10 complete
- NuGet.org account and API key
- GitHub repo with Actions enabled

## Success Criteria

- [ ] CI workflow runs on push/PR: builds + tests all projects
- [ ] Release workflow publishes NuGet packages on `v*` tag
- [ ] BenchmarkDotNet suite runs and reports comparative results
- [ ] DocFX site builds without errors
- [ ] Code coverage >= 95% (measured by coverlet or similar)
