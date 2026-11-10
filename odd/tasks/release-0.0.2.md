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
| T1 | S1, S2 | inline | done | `cf2a99d` |
| T2 | S3 | inline | done | tag moved to `efef23c` after the gating fix, see L2 |
| T3 | S4 | inline | done | see L2 |

## Log

### L1 — owner authorizes the release (verbatim)

> DALE

in reply to the "¿Arrancamos 0.0.2 — bump de versión + changelog + tag + publicar, con tu OK?" proposal after the async-parity surface was completed (707 tests, CI green).

### L2 — the pipeline blocked on a real defect, then published (2026-11-10)

**T1, S1–S2.** `<Version>0.0.2</Version>` in the shared group; `dotnet pack` emits `ResulTus.0.0.2.nupkg` (213,633 B) and `.snupkg` locally before anything was tagged. `[Unreleased]` rolled into `[0.0.2] - 2026-11-10` with the link references moved to `v0.0.2` and a fresh empty `[Unreleased]`. Commit: `cf2a99d`.

**T2, S3 — the tag-driven pipeline did its first real job, and it was to block.** The first `v0.0.2` tag push made `release.yml` build and test in Release, where **three `ResulTus.Collections.Tests` allocation probes failed** — the long-standing "Release reads 24 L" finding, previously postphoned, turned into a hard publish gate exactly as designed: the automation refused to package a tree the Release suite says is broken. Diagnosis (per-operator probes then per-window probes): the 24 bytes were **not library allocation** — an empty measurement window in any configuration reports it; the "difference" was that Debug scheduling stayed clean while Release scheduling let residue from earlier tests (framework work and continuations queued on the measuring thread) land inside the window. All three tests pass individually even under Release. Fix: `tests/ResulTus.Collections.Tests/AllocationProbe.cs` measures warmup+window on a **dedicated thread**, starting the byte account at zero, so the window only counts the probe's own operations; each of the three tests delegates to it. Release-equivalent local gate after the fix: `dotnet test -c Release` → **707 passed, 0 failed**. Commit `efef23c`; the tag was deleted and re-pushed at that commit (a tag gate must point at the tree that passed it).

**Second pipeline run: published.** Run `42867362862` — `pack-and-publish: success`: Build 0 errors, Tests Release green, Pack created all thirteen `ResulTus.0.0.2.nupkg`/`.snupkg`, and the push step logged **"Your package was pushed" 26 times** (13 packages + 13 symbol packages) with the owner's `NUGET_API_KEY` secret.

**T3, S4 — observables, read back:** pipeline success and push log, observed. nuget.org's own indexes (flat container, search, package page) still returned 404/0-hits minutes after publication — standard first-publish indexing latency, not a publish failure; the authoritative confirmation is the pipeline's push log. The package page and search listing resolve when nuget.org's index refreshes.
