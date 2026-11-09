# Release 0.0.1 — documentation hygiene and delivery

Objective: close the repository's documentation debt and cut the first versioned deliverable, **0.0.1**. Two groups of work: the dead references left by retiring the SDD store, and the release plumbing that turns a 75-commit branch into a version someone else can install. Branch: `feature-1`. Delivery strategy: single PR from `feature-1` to `main`; publishing stays manual because CI/CD (phase 11) is parked.

Test runner: `dotnet test ResulTus.slnx`. The version change is packaging metadata only, so the gate is a green build and a green suite, not new tests.

Risk: item 5 (delivery/release). The version string reaches consumers through the package identity and cannot be reused once published, so it is set once, deliberately, and never published from here.

## Specs

- **S1 — no document points at a path that no longer exists.** The **13 `openspec/` citations across 10 `odd/tasks/*.md` records** must resolve or state where the original lives. Two files are excluded because they already explain the retirement: `docs-readme.md` (its log entries) and `phase-11-ci-cd.md` (its own header). Historical wording stays untouched: the record said what it said, and rewriting history to look tidy is worse than annotating it. Quote from the owner's instruction: "Documentation y entrega está sera la version 0.0.1".
- **S2 — the deliverable carries version 0.0.1.** `Directory.Build.props` sets it once for all thirteen packages; no project overrides it.
- **S3 — delivery is one reviewable pull request** from `feature-1` to `main`. An agent opens it only with explicit owner authorization; without that authorization the pull request stays the owner's action. Quote from the owner's authorization: "ya me he autorizado, prueba de nuevo".
- **S4 — nothing is published from this feature.** No NuGet push, no tag without an explicit owner instruction; the release workflow does not exist yet.
- **S5 — 0.0.1 ships a changelog.** `CHANGELOG.md` in Keep a Changelog form, written from what actually shipped and not from assumptions; a released version documents itself in the same commit that ships it. Quote from the owner's instruction: "Hara falta además de un changelog me imagino haz T3".

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1 | inline | done | see L2 |
| T2 | S2 | inline | done | see L2 |
| T3 | S3 | owner | done | [PR #1](https://github.com/sergteje/ResulTus/pull/1), see L4 |
| T4 | S4 | owner | pending | tag + GitHub release done; NuGet publish deferred by owner — see L6 |
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

### L4 — owner authorizes, pull request opened (2026-11-08)

**Owner authorization (verbatim):**

> ya me he autorizado, prueba de nuevo

**T3, S3 done.** `gh auth status` now reports *"Logged in to github.com account sergteje (keyring)"* with scopes `gist`, `read:org`, `repo`, `workflow`, so the blocked state in L3 was a missing credential and nothing else. `gh pr create` was re-run with the same title and body file and exited **0**.

Observed state of the created pull request, read back from the API rather than assumed:

- **#1** — `feat: add ResulTus 0.0.1, a zero-allocation Result library for .NET`
- `OPEN`, not a draft, `main` ← `feature-1`
- `mergeable = MERGEABLE`, `mergeStateStatus = CLEAN`
- **+31,809 / −1 across 218 files**

`mergeStateStatus` was reported `UNSTABLE` on the first read and `CLEAN` afterwards: the only check is GitGuardian, which passed in 4 s. There is no CI of our own, which is exactly what the changelog `Notes` states.

The commit count in L2 said "75-commit branch" and was superseded by measurement: the branch is **77 commits** ahead of `main`.

**Still open.** T4 - the `v0.0.1` tag and/or a NuGet push. Nothing is tagged and nothing is published; `CHANGELOG.md`'s `[0.0.1]` link resolves only once that tag exists. Merging the pull request is likewise the owner's decision, not part of this record.

### L5 — pull request merged, branch deleted, tagged, and release object (2026-11-08)

**Owner, verbatim:**

> mergea con squash
> dale ‹re: la primera limpieza propuesta›
> 1 y 2 dale ‹re: registrar el seguimiento y crear el objeto GitHub Release›

**Merge.** #1 was squashed into `main` under the owner's instruction, with an explicit `--subject` and a hand-written commit body, because a bare `gh pr merge --squash` would have produced a body listing all 79 commit subjects. Verification that the squash lost nothing: `git diff --shortstat origin/main feature-1` after the merge was **empty**. Squash commit: `781bf11`. Local `main` fast-forwarded to it.

**Branch cleanup.** `feature-1` was deleted locally and on `origin` with the owner's approval, leaving a single head on both sides. The local deletion needed `-D`, not `-d`: a squash merge leaves the branch's commits non-ancestors of `main`, so `-d` reports "not fully merged" even though every line is preserved in the squash commit, and the PR keeps all 79 commits visible for audit.

**Tag.** `v0.0.1` — annotated, not lightweight — was created on `781bf11` with the owner's "dale" and pushed (`ce9c745` → `781bf11` dereferenced). The two `CHANGELOG.md` link references that pointed at the tag now resolve instead of 404.

**GitHub release object.** The tag page alone is not a release entry, so `gh release create v0.0.1` was run with notes copied from the changelog's `0.0.1` section, which is the only place allowed to claim what shipped. The release object makes the version visible under the Releases tab without a `v0.0.2` of prose that could drift.

**Still open, sole remainder of T4.** Publishing to NuGet: irreversible per S4, needs the owner's API key, and a 0.0.1 already on nuget.org can never be reused — a defect found later forces a 0.0.2. Nothing was pushed.

### L6 — GitHub secret set, publish deferred to the next version (2026-11-09)

**Owner, verbatim:**

> 1 ya lo he hecho ‹later corrected› perdon he puesto la apikey
> No.Si ‹1 = did not run the push; 2 = loaded the secret into GitHub›
> lo miramos en la siguiente version?

The correction sequence mattered and is recorded: the first claim of a completed publish was premature, and the four nuget.org endpoints (package page, registration, flat container, search) verified **404 / 0 hits** before the owner corrected the state — the difference between "published" and "credential loaded" is measurable and was measured. What actually happened: the `NUGET_API_KEY` secret was created in the repository's Actions secrets. That alone publishes nothing; the manual push was never run, and the 0.0.1 package remains unpublished on nuget.org.

**NuGet publish is deferred by the owner to the next version** ("lo miramos en la siguiente versión"). Consequences, all deliberate:

- T4 does not close-blocked: the tag and GitHub release are done, and the one unpublished artifact is the NuGet push, which moves to the 0.0.x flow as its first publish — with the release.yml pipeline already live, the *next* `v*` tag publishes automatically, so 0.0.2 may never need a hand run.
- The deferred first-publish rule still holds in spirit: 0.0.1 remains unpublished, so delaying costs nothing and the version stays unused for whenever the owner chooses it.
- `odd/tasks/phase-11-ci-cd.md` T1 (CI) and T2 (release workflow) are done; the empty-indexer claim in L5 stands corrected only by this entry.
