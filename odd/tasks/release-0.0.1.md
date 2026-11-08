# Release 0.0.1 — documentation hygiene and delivery

Objective: close the repository's documentation debt and cut the first versioned deliverable, **0.0.1**. Two groups of work: the dead references left by retiring the SDD store, and the release plumbing that turns a 75-commit branch into a version someone else can install. Branch: `feature-1`. Delivery strategy: single PR from `feature-1` to `main`; publishing stays manual because CI/CD (phase 11) is parked.

Test runner: `dotnet test ResulTus.slnx`. The version change is packaging metadata only, so the gate is a green build and a green suite, not new tests.

Risk: item 5 (delivery/release). The version string reaches consumers through the package identity and cannot be reused once published, so it is set once, deliberately, and never published from here.

## Specs

- **S1 — no document points at a path that no longer exists.** The **13 `openspec/` citations across 10 `odd/tasks/*.md` records** must resolve or state where the original lives. Two files are excluded because they already explain the retirement: `docs-readme.md` (its log entries) and `phase-11-ci-cd.md` (its own header). Historical wording stays untouched: the record said what it said, and rewriting history to look tidy is worse than annotating it. Quote from the owner's instruction: "Documentation y entrega está sera la version 0.0.1".
- **S2 — the deliverable carries version 0.0.1.** `Directory.Build.props` sets it once for all thirteen packages; no project overrides it.
- **S3 — delivery is one reviewable pull request** from `feature-1` to `main`, opened by the owner, never by an agent.
- **S4 — nothing is published from this feature.** No NuGet push, no tag without an explicit owner instruction; the release workflow does not exist yet.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 | inline | done | see L2 |
| T2 | S2 | inline | done | see L2 |
| T3 | S3 | owner | pending | — |
| T4 | S4 | owner | pending | — |

## Log

### L1 — owner scopes the release (verbatim)

> Documentation y entrega está sera la version 0.0.1

### L2 — documentation and version (2026-11-08)

**T1, S1.** The citation count was corrected before acting: `git grep -o "openspec/" -- odd/tasks` returns **23 occurrences across 12 files**, not the 19 first reported. Two files need no change — `docs-readme.md` explains the retirement in its own log, and `phase-11-ci-cd.md` states it in its header — so the work was **13 citations across 10 records**.

The fix annotates instead of rewriting. Each of the ten records gained one closing line: *"The `openspec/` paths cited above belong to the retired SDD store. The originals are recoverable with `git show 75b0962:<path>`; this record quotes the scoped parts inline."* Editing the historical sentences was rejected: a task record states what was believed and verified at the time, and back-editing it to look current destroys exactly the evidence a reader comes for. The annotation gives the pointer without touching the claim.

**T2, S2.** `<Version>0.0.1</Version>` was added to the shared packaging group in `Directory.Build.props`, so all thirteen projects inherit it and none overrides it. Verified by observable effect rather than by echoing a property: `dotnet pack src/ResulTus/ResulTus.csproj -c Release` emits `ResulTus.0.0.1.nupkg` (13,106 B) and `ResulTus.0.0.1.snupkg` (10,318 B). `Version` and `PackageVersion` both resolve to `0.0.1`.

**Gate after both changes.** `dotnet build ResulTus.slnx` → 0 warnings / 0 errors. `dotnet test ResulTus.slnx --no-build` → **670 passed, 0 failed, 0 skipped**. The pack output went to a temp directory outside the worktree and was deleted; no artifact entered the repository.

**Still open.** T3 (the pull request) and T4 (tag or publish) are the owner's, per `AGENTS.md`: an agent never opens a pull request or publishes without an explicit instruction. Nothing was tagged and nothing was pushed to NuGet.
