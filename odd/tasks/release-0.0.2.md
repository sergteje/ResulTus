# Release 0.0.2 — async parity, verified AOT and the first automated NuGet publish

Objective: cut 0.0.2 carrying everything post-0.0.1-tag that has observed verification, and let the tag-driven pipeline perform the project's first real automated NuGet publish with the loaded secret. Branch: `main`. Delivery strategy: single PR was already merged; this is a direct release cut. Test runner: `dotnet test ResulTus.slnx` via the release workflow itself.

Risk: item 5 (delivery). 0.0.2 on nuget.org is permanent — this is exactly the automations' first live run and it publishes what the suite says is good.

## Specs

- **S1 — the version is 0.0.2** for all thirteen packages through the shared `<Version>`; verified by the pack output before anything is tagged.
- **S2 — the changelog carries a `0.0.2` section** built from `[Unreleased]` with keep-a-changelog discipline, link references updating to `v0.0.2` and a fresh empty `[Unreleased]` afterwards.
- **S3 — nothing is published by hand**: the tag `v0.0.2` triggers `release.yml` (build Release → test Release → pack → push `nupkg`+`snupkg` with the `NUGET_API_KEY` secret, `--skip-duplicate`). The first automated publish is observed, not assumed.
- **S4 — the observables after publication**: pipeline run success listing both jobs; the nupkg served by the flat container and the search index; the package page online.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2 | inline | pending | version + changelog |
| T2 | S3 | inline | pending | tag push |
| T3 | S4 | inline | pending | verification |

## Log

### L1 — owner authorizes the release (verbatim)

> DALE

in reply to the "¿Arrancamos 0.0.2 — bump de versión + changelog + tag + publicar, con tu OK?" proposal after the async-parity surface was completed (707 tests, CI green).
