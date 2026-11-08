# Release 0.0.1 — documentation hygiene and delivery

Objective: close the repository's documentation debt and cut the first versioned deliverable, **0.0.1**. Two groups of work: the dead references left by retiring the SDD store, and the release plumbing that turns a 75-commit branch into a version someone else can install. Branch: `feature-1`. Delivery strategy: single PR from `feature-1` to `main`; publishing stays manual because CI/CD (phase 11) is parked.

Test runner: `dotnet test ResulTus.slnx`. The version change is packaging metadata only, so the gate is a green build and a green suite, not new tests.

Risk: item 5 (delivery/release). The version string reaches consumers through the package identity and cannot be reused once published, so it is set once, deliberately, and never published from here.

## Specs

- **S1 — no document points at a path that no longer exists.** The **13 `openspec/` citations across 10 `odd/tasks/*.md` records** must resolve or state where the original lives. Two files are excluded because they already explain the retirement: `docs-readme.md` (its log entries) and `phase-11-ci-cd.md` (its own header). Historical wording stays untouched: the record said what it said, and rewriting history to look tidy is worse than annotating it. Quote from the owner's instruction: "Documentation y entrega está sera la version 0.0.1".
- **S2 — the deliverable carries version 0.0.1.** `Directory.Build.props` sets it once for all thirteen packages; no project overrides it.
- **S3 — delivery is one reviewable pull request** from `feature-1` to `main`, opened by the owner, never by an agent.
- **S4 — nothing is published from this feature.** No NuGet push, no tag without an explicit owner instruction; the release workflow does not exist yet.
- **S5 — 0.0.1 ships a changelog.** `CHANGELOG.md` in Keep a Changelog form, written from what actually shipped and not from assumptions; a released version documents itself in the same commit that ships it. Quote from the owner's instruction: "Hara falta además de un changelog me imagino haz T3".

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 | inline | done | see L2 |
| T2 | S2 | inline | done | see L2 |
| T3 | S3 | owner | blocked | — |
| T4 | S4 | owner | pending | — |
| T5 | S5 | inline | done | see L3 |

## Log

### L1 — owner scopes the release (verbatim)

> Documentation y entrega está sera la version 0.0.1

### L2 — documentation and version (2026-11-08)

**T1, S1.** The citation count was corrected before acting: `git grep -o "openspec/" -- odd/tasks` returns **23 occurrences across 12 files**, not the 19 first reported. Two files need no change — `docs-readme.md` explains the retirement in its own log, and `phase-11-ci-cd.md` states it in its header — so the work was **13 citations across 10 records**.

The fix annotates instead of rewriting. Each of the ten records gained one closing line: *"The `openspec/` paths cited above belong to the retired SDD store. The originals are recoverable with `git show 75b0962:<path>`; this record quotes the scoped parts inline."* Editing the historical sentences was rejected: a task record states what was believed and verified at the time, and back-editing it to look current destroys exactly the evidence a reader comes for. The annotation gives the pointer without touching the claim.

**T2, S2.** `<Version>0.0.1</Version>` was added to the shared packaging group in `Directory.Build.props`, so all thirteen projects inherit it and none overrides it. Verified by observable effect rather than by echoing a property: `dotnet pack src/ResulTus/ResulTus.csproj -c Release` emits `ResulTus.0.0.1.nupkg` (13,106 B) and `ResulTus.0.0.1.snupkg` (10,318 B). `Version` and `PackageVersion` both resolve to `0.0.1`.

**Gate after both changes.** `dotnet build ResulTus.slnx` → 0 warnings / 0 errors. `dotnet test ResulTus.slnx --no-build` → **670 passed, 0 failed, 0 skipped**. The pack output went to a temp directory outside the worktree and was deleted; no artifact entered the repository.

**Still open.** T3 (the pull request) and T4 (tag or publish) are the owner's, per `AGENTS.md`: an agent never opens a pull request or publishes without an explicit instruction. Nothing was tagged and nothing was pushed to NuGet.

### L3 — changelog written, T3 blocked on authentication (2026-11-08)

**T5, S5.** `CHANGELOG.md` was added in Keep a Changelog form with a `0.0.1` section written from what the repository actually ships: the five core types and their measured sizes, the nine extension surfaces, and the documentation set. It does not claim anything the repository cannot show — the comparison links point at the performance charter, not at prose.

Two deliberate choices inside it:

- **A `Notes` subsection exists** even though Keep a Changelog does not define one, because a first release has facts an installer needs and no other section fits: there is no CI, trimming/AOT compatibility is annotated rather than proven by a publish-and-run result, and every benchmark number is reproducible from the repository.
- **The `Unreleased` and `0.0.1` link references point at `v0.0.1`**, a tag that does not exist yet. That is the Keep a Changelog convention and it resolves the moment T4 tags the release; until then those two links 404 on GitHub and nothing else in the file depends on them.

`AGENTS.md` gained the matching rule so the file does not rot: `CHANGELOG.md` records every released version, and a change that ships updates it in the same commit.

**T3 attempted, blocked.** `gh --version` reports 2.95.0, but `gh auth status` reports *"You are not logged into any GitHub hosts"* and no `GH_TOKEN` is present in the environment. Opening a pull request needs credentials that belong to the owner, and fabricating or borrowing an ambient one is out of bounds, so the command was not run against the API.

The repository state is ready for it: `git rev-list --count HEAD..origin/main` is **0**, so `feature-1` is a clean fast-forward from `main` and the pull request will show no conflict. The one-command path, once the owner authenticates:

```bash
gh auth login
gh pr create --base main --head feature-1 --title "feat: add ResulTus 0.0.1, a zero-allocation Result library for .NET" --body-file .git/PR_BODY.md
```

The body was drafted here rather than in the repository, because a PR description is not a tracked artifact. `--fill` also works if the owner prefers the commit-derived body.
