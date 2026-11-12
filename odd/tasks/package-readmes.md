# Package READMEs — one focused README per NuGet package

Objective: give each of the 13 NuGet packages its own focused README (packed into its `.nupkg`)
instead of shipping the root canonical README everywhere. Branch: `feat/package-readmes` off
`main` (b25c9e5). Delivery strategy: `single-pr` (forecast exceeds 400 authored lines; one
feature branch, work-unit commits, the pull request is the owner's decision).
Test runner: `dotnet test ResulTus.slnx` plus `dotnet pack` verification; snippets compile in a
scratch project outside the worktree.

## Specs

- **S1 — one focused README per package**, quoting the owner's request verbatim:
  "los readme de cada nuget deberian de ser diferentes". Each of the 13 packable projects under
  `src/` (`ResulTus`, `ResulTus.Extensions`, `ResulTus.Collections`, `ResulTus.Logging`,
  `ResulTus.Serialization`, `ResulTus.Diagnostics`, `ResulTus.AspNetCore`, `ResulTus.HttpClient`,
  `ResulTus.Validation`, `ResulTus.Validation.FluentValidation`, `ResulTus.Analyzers`,
  `ResulTus.SourceGenerators`, `ResulTus.Testing`) gets `src/<Package>/README.md` with: a compact
  branding header (one picture block, same filenames as the root), what the package adds, its
  dependencies, install command, focused examples of **its** API, and links back to the root docs
  guides. The root `README.md` stays the canonical GitHub entry point and is no longer packed into
  any package.
- **S2 — links must resolve on nuget.org**: the known limitation recorded in
  `odd/tasks/brand-identity.md` (relative links to `docs/` and `README.es.md` do not resolve in
  nuget.org's renderer) is fixed for the per-package readmes by using absolute GitHub URLs
  (`https://github.com/sergteje/ResulTus/blob/main/...`) for every link that leaves the package
  README. No broken relative links inside the packed readmes.
- **S3 — packaging wiring**: `Directory.Build.props` keeps `PackageReadmeFile=README.md` and the
  icon include but drops the root-README `None` include; each package csproj adds
  `<None Include="README.md" Pack="true" PackagePath="" />`. Verification: `dotnet pack` of
  representative packages — each `.nupkg` contains exactly its own README (and `icon.png`), and
  the root README content is absent from package readmes.
- **S4 — every code block in a per-package README compiles**, per the repository rule, against
  the real source projects in a scratch project outside the repository. A snippet that does not
  build is a documentation defect.
- **S5 — document only shipped API** (assumption recorded: per-package readmes are English-only
  artifacts; the Spanish parity rule applies to the root `README.md`/`README.es.md` editions, not
  to the packed readmes). No unshipped feature, no invented benchmark number; package readmes
  carry no performance tables — the charter numbers stay in the root README and docs.
- **S6 — artifact rules**: English, Conventional Commits, no AI attribution, work-unit commits on
  the feature branch; the 13 tooling test projects stay `IsPackable=false`.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2, S5 (13 README files; core `ResulTus` inline as template, 12 delegated in 3 parallel batches) | inline + delegated writers | done | f6b63a9 |
| T2 | S3 (props + 13 csproj wiring, pack verification) | inline | done | 7250a22 |
| T3 | S4, S5 (snippet compile gate, pack sweep, root README cross-link) | inline + verify | done | 9d4ecc5 (cross-link) + f6b63a9 (AspNetCore fix) |
| T4 | Release 1.1.1: bump `Version` in `Directory.Build.props` + CHANGELOG entry | inline | done | ac15c7b |

## Log

### L1 — owner request (2026-11-12)

> Hola los readme de cada nuget deberian de ser diferentes, creo que eso estaba pendiente no?

Verified state first: it was **not** pending — `odd/tasks/brand-identity.md` recorded the opposite
decision (one canonical README packed into every package, with the known nuget.org link
limitation). The owner then chose "Sí, hacerlos": per-package readmes. Assumptions recorded in S5
(English-only packed readmes; no perf tables per package; root README stays canonical).

### L2 — delivery is version 1.1.1 (owner, 2026-11-12)

> Cuando acabes con eso sera la version 1.1.1

Recorded verbatim. T4 added: once T1–T3 close, `Directory.Build.props` `Version` goes 1.1.0 → 1.1.1
and `CHANGELOG.md` gains the released-version section describing the per-package readmes. No
publish without explicit instruction.

### L3 — T1 executed: 13 package readmes written (2026-11-12)

Core `ResulTus/README.md` written inline as the template (S1 structure: badges, picture block,
intro, Install, What you get, Related packages, Learn more, MIT footer). The remaining 12 were
written by three parallel workers with disjoint edit surfaces: batch A (Extensions, Collections,
Serialization, Logging), batch B (Diagnostics, AspNetCore, HttpClient), batch C (Validation,
Validation.FluentValidation, Testing, Analyzers, SourceGenerators). Each worker cross-checked every
documented member against the package source and the csproj dependency graph, and audited: no
relative links, correct package id in badges, template heading structure. S# covered: S1, S2, S5.
Known base-only follow-ups surfaced by the workers (not caused by this feature): `docs/httpclient.md`
carries `(int)error.Status!`, which does not compile, and says the package targets net8.0 only;
`ResulTus.Analyzers` packs its assembly under `lib/` instead of the analyzer location, so installing
the package alone does not activate the rules.

### L4 — T2/T3/T4 executed and verified (2026-11-12)

- **T2 wiring**: `Directory.Build.props` keeps `PackageReadmeFile=README.md` + the icon include and
  drops the root-README `None` include; the 13 package csproj files each add
  `<None Include="README.md" Pack="true" PackagePath="" />`. Pack sweep: **13/13 `.nupkg` carry
  their own focused README (3.7–11.7 KB) plus `icon.png`**; the root canonical README is packed
  into no package. Evidence in the pack sweep run against `src/*` Release output in the session
  temp dir.
- **T3 compile gate**: all **40 `csharp` blocks** across the 13 package readmes extracted into
  per-block scratch projects outside the repository (net10.0, block verbatim, top-level-program
  form; declaration-only blocks as `Library`; cross-block dependencies modeled by sharing the
  declaring block, the same session model the canonical README gate used) — **40/40 compile**.
  The gate caught **one real defect, fixed in f6b63a9**: `src/ResulTus.AspNetCore/README.md`
  assigned `IActionResult mapped = conflict.ToActionResult(_ => 422)`, which no longer compiles
  because **.NET 10 changed `ActionResult<TValue>` to implement `IConvertToActionResult` instead
  of `IActionResult` directly** (verified by reflection over the net10.0 framework). The block now
  types the variable as `ActionResult<int>` and carries a note about the change; the unit-tier
  `Result.ToActionResult` overloads still return `IActionResult` directly.
- **Root README cross-link (9d4ecc5)**: the packages table links each package name to its
  per-package README, and both editions state that every package ships its own focused readme.
- **T4 release (ac15c7b)**: `Version` 1.1.0 → 1.1.1 in `Directory.Build.props`; CHANGELOG gains
  the `[1.1.1]` section describing the per-package readmes; both README editions updated the
  "shipped at" line to 1.1.1 and the packaged-readme note at parity.
- **Suite**: `dotnet test ResulTus.slnx` — everything passes except 3
  `AsyncAllocationAuditTests` (Extensions) failures on net10 and net11. **Verified pre-existing**:
  they reproduce identically at baseline b25c9e5 in a clean worktree; this feature touches no
  source code. Reported to the owner as a base-only follow-up, not a defect of this change.
- No publish, push or pull request was performed — those remain the owner's decisions.
