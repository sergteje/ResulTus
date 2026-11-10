# Release 1.0.0 — the stable contract

Objective: declare 1.0.0. The public surface is no longer an apology — it is a checked-in
snapshot enforced by CI; the versioning spec activates the 1.0 promise: from this version, any
surface change requires a MAJOR bump. Branch: `main`. Delivery strategy: the tag-driven pipeline
performs the entire publish — nuget.org, the GitHub Packages mirror, and the GitHub Release
object with notes verbatim from this changelog section.

Risk: item 5 (delivery). A published 1.0.0 is the loudest promise a library makes — immutable on
nuget.org, and the version number every consumer treats as a stability statement.

## Specs

- **S1 — the version is 1.0.0** through the shared `<Version>`; verified by pack output before
  anything is tagged.
- **S2 — the changelog carries the `[1.0.0]` section** with the covenant stated at its top and
  link references moved to the tag; `[Unreleased]` starts fresh.
- **S3 — the tag-driven pipeline performs the whole publish**, including the GitHub Release
  object with notes extracted from this changelog section (the covenant automated at
  `d70133e`), and the analyzer mirror to GitHub Packages.
- **S4 — the observables read back after publication**, per the 0.0.2/0.0.3 playbook.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2 | inline | done | see L1 |
| T2 | S3 | inline | pending | tag push |
| T3 | S4 | inline | pending | verification |

## Log

### L1 — owner declares 1.0.0 (verbatim)

> para que ha servido esto de apicompat?? si y declarar 1.0.0

The owner's question answered what the api-compat contract served: declaring 1.0.0 without it
would be a verbal promise; with it, the surface per assembly lives in eleven committed text
files, and the 1.0 promise ("a surface change without a MAJOR bump fails CI") is mechanical, not
moral. The declaration is therefore gated by tooling that already exists and already runs — the
sequence the owner ordered (multi-targeting → coverage gaps → gate → api-compat) ends with
exactly the gate that makes this version safe to declare.
