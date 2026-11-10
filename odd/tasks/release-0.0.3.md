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
| T1 | S1, S2 | inline | pending | version + changelog |
| T2 | S3 | inline | pending | tag push |
| T3 | S4 | inline | pending | verification |

## Log

### L1 — owner authorizes (verbatim)

> v0.0.3 y despues los gaps de coverage

in reply to the multi-targeting completion report that flagged "publishing these targets needs the next tag".
