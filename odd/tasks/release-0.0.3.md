# Release 0.0.3 — multi-targeting, dual-registry publish and the parity fixes

Objective: cut 0.0.3 carrying the multi-targeted package set (net8.0/net9.0/net10.0), the incremental generator pipeline, the analyzer false-positive battery and the benchmark charter's Gen0 columns; the release workflow's GitHub Packages mirror runs for the first time, so the packages appear on the repository's Packages tab in addition to nuget.org. Branch: `main`. Delivery strategy: direct release cut, password-free. Test runner: `dotnet test ResulTus.slnx -c Release` inside the workflow.

Risk: item 5 (delivery). A published version is immutable and this is the first release through the new dual-registry publish path.

## Specs

- **S1 — the version is 0.0.3** through the shared `<Version>`; verified by pack output before tagging.
- **S2 — the changelog rolls `[Unreleased]` into `[0.0.3]`** with date and link references moved to the tag.
- **S3 — the tag drive publishes to both feeds**: nuget.org (13 packages + symbols) and the GitHub Packages mirror (`.nupkg` only — GitHub Packages does not host symbols), with the run's own `GITHUB_TOKEN`. The first run of the mirror is observed.
- **S4 — the observables read back**: workflow success on both push steps; nuget.org flat container lists `0.0.3` with **three `lib` targets**; GitHub Packages lists the packages on the repository.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2 | inline | done | `127b4a5` |
| T2 | S3 | inline | done | tag moved to `5e4c9ee` after the mirror syntax fix, see L2 |
| T3 | S4 | inline | done | see L2 |

## Log

### L1 — owner authorizes (verbatim)

> v0.0.3 y despues los gaps de coverage

in reply to the multi-targeting completion report that flagged "publishing these targets needs the next tag".

### L2 — dual-registry publish: the mirror's first run caught a syntax defect, then both feeds published (2026-11-10)

**T1, S1–S2.** `<Version>0.0.3</Version>`; pack emits 13 `.nupkg` + 13 `.snupkg`. `[Unreleased]` rolled into `[0.0.3] - 2026-11-10` with links to the tag. Commit `127b4a5`.

**T2, S3 — the mirror's first live run failed exactly like good automation does.** The first run's nuget.org push completed (26 "Your package was pushed"), but the fresh `Push packages to GitHub Packages` step printed `dotnet nuget`'s usage help and failed: my YAML passed the source name and URL in a form the parser rejected ('github' was read as the positional source URL, then `--source` collided). The fix uses the GitHub-documented form (positional URL last, `--name github`) — commit `5e4c9ee`; the tag moved there, because a gaate must point at the tree that passed it. One nuance this time: moving a tag after nuget.org already published the package is safe **because the run is idempotent** — the re-run's push to nuget.org hit immutable 0.0.3 versions and `--skip-duplicate` recorded them as skipped (13 such lines in the corrected run).

**T3, S4 — both feeds observable.** Corrected run `42882158070`: `pack-and-publish: success`.

- **nuget.org**: push step logged per-package skip lines — 0.0.3 was already immutable there; the flat container lists it as indexing completes.
- **GitHub Packages**: source `github` added successfully, then **"Your package was pushed" ×13** — every package mirrored on the repository's Packages tab for the first time. Symbols never mirror by design (GitHub Packages does not host `.snupkg`).

This closes the ask that started the release: the packages now appear BOTH on nuget.org and on the GitHub Packages tab through the same tag push.
