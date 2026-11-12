# Tooling packaging fixes — analyzers/generator route + httpclient guide

Objective: fix two pre-existing defects surfaced during `package-readmes`: (1) the Roslyn
components ship under `lib/`, so installing `ResulTus.Analyzers` or `ResulTus.SourceGenerators`
alone activates nothing; (2) `docs/httpclient.md` carries a snippet that does not compile and a
stale target-framework claim. Branch: `fix/tooling-packaging` off `main`. Delivery strategy:
`single-pr`; the CHANGELOG fix folds into the unreleased `[1.1.1]` section.
Test runner: `dotnet test ResulTus.slnx`; packaging proof is a consumer smoke test against the
packed `.nupkg` in a scratch project outside the worktree.

## Specs

- **S1 — the analyzers activate on install.** Quoting the finding verbatim:
  "`ResulTus.Analyzers` currently packs its assembly under `lib/netstandard2.0/` … so a consumer
  installing it will not yet get the rules as an analyzer". After the fix, the packed
  `ResulTus.Analyzers.nupkg` carries `analyzers/dotnet/roslyn<version>/cs/ResulTus.Analyzers.dll`,
  no `lib/` assembly, and a scratch consumer project referencing the packed package reports
  RESULT001–RESULT006 diagnostics when it compiles violating code.
- **S2 — the generator generates on install.** Same finding shape for the source generator: the
  packed `ResulTus.SourceGenerators.nupkg` carries the assembly under `analyzers/dotnet/...` and a
  scratch consumer project with `[ErrorFactory]` gets the generated factory members.
- **S3 — the guide snippet compiles.** `docs/httpclient.md` line 176 uses `(int)error.Status!`,
  which does not compile because `Status` is `HttpStatusCode?`. The guide follows the repository
  rule: a snippet that does not build is a documentation defect. Fix compiles.
- **S4 — the guide states the shipped target frameworks.** `docs/httpclient.md` line 5 claims the
  adapter "targets `net8.0`"; the csproj multi-targets `net8.0;net9.0;net10.0;net11.0` (verified in
  the package-readmes batch). The guide states the shipped matrix.
- **S5 — CHANGELOG**: the two fixes are recorded in the unreleased `[1.1.1]` section. No publish;
  push and release remain the owner's decisions.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S3, S4 (docs/httpclient.md fixes) | inline | done | — |
| T2 | S1, S2 (analyzers + generator pack routing, consumer smoke) | inline | done | — |
| T3 | S5 (CHANGELOG), suite, assess | inline | pending | — |

## Log

### L1 — origin (2026-11-12)

Surfaced during the `package-readmes` compile gate and writer audits, quoted verbatim there:
"`docs/httpclient.md` (guide, not my file) contains `(int)error.Status!` … which does not compile"
and "`ResulTus.Analyzers` currently packs its assembly under `lib/netstandard2.0/` … installing the
package alone does not activate the rules". Verified before acting: both `.nupkg`s pack the Roslyn
component under `lib/netstandard2.0/` (observed directly); `IsRoslynComponent=true` is set in both
csproj files but the SDK pack ignores it for routing; `Microsoft.CodeAnalysis.CSharp` is pinned to
4.14.0 and the generator is an `IIncrementalGenerator`, so the analyzers route is
`analyzers/dotnet/roslyn4.14/cs`. The owner said "Continua" against the offer to take these as a
separate feature.

### L2 — T1/T2 executed and verified (2026-11-12)

- **T1 guide fixes**: `docs/httpclient.md` line 5 states the shipped
  `net8.0`–`net11.0` matrix; the transport-failure `switch` case now unwraps the nullable with
  `(int)(error.Status ?? default)`. Verification: the guide's ten statement blocks were compiled
  per-block (top-level program + the shared `Declared.cs` built from its setup blocks) against the
  real `ResulTus` + `ResulTus.HttpClient` assemblies — **9/9 statement blocks compile** (blocks
  2–10; block 0/1 form the shared setup).
- **T2 pack routing**: both Roslyn component csproj files gained
  `IncludeBuildOutput=false`, `IncludeSymbols=false`, `NoPackageAnalysis=true` and a `None`
  include routing `$(OutputPath)$(AssemblyName).dll` to `analyzers/dotnet/roslyn4.14/cs/` (the
  folder follows the pinned `Microsoft.CodeAnalysis.CSharp` 4.14.0, the minimum the components
  are built against). Pack inspection: both `.nupkg` now carry the component under `analyzers/`
  and no `lib/` dll; clean pack with no warnings.
- **Consumer smoke proof (S1, S2)** against the packed packages from a local NuGet source:
  `ResulTus.Analyzers` 1.1.1 consumer compiles a discarded `Result<int>.Success(42);` statement and
  gets **`warning RESULT001: The result of this operation is discarded`** — the rules activate on
  install; `ResulTus.SourceGenerators` 1.1.1 consumer declares
  `[ResultErrors]`/`[ErrorFactory]` `UserErrors.NotFound(Guid)` with `[JsonSerializable]`-free
  plain code, **compiles, and the program runs printing `USER_NOT_FOUND`** — the generator
  generates on install.
- **T3**: fixes recorded in the unreleased `[1.1.1]` `### Fixed` section; suite run pending in the
  commit step; push and release remain the owner's decisions.

### L3 — closed (2026-11-12)

- **Suite**: `dotnet test ResulTus.slnx` — everything passes except the same 3 pre-existing
  `AsyncAllocationAuditTests` failures already verified at baseline during `package-readmes`.
- **Commits**: 7cef626 (guide fixes), 43c47bf (pack routing), 100b55d (CHANGELOG), c20e04d
  (feature document). Branch `fix/tooling-packaging` sits on top of `feat/package-readmes` because
  the `[1.1.1]` CHANGELOG section is shared; both features land in the same unreleased release.
- **Native assessment** of the slice 52ddbd5..c20e04d: risk `medium` (executable change:
  `ResulTus.Analyzers.csproj`), `review_due: false`, reason `under_budget`.
