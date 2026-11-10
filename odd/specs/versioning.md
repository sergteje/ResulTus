# The ResulTus versioning strategy

This document is a durable specification: it tells every contributor and consumer what the
version numbers mean now and what they promise from 1.0 on.

## Versioning scheme

ResulTus follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html) with
[Keep a Changelog](https://keepachangelog.com/) for documentation. A version is always
`MAJOR.MINOR.PATCH` and any prerelease is published with a SemVer prerelease suffix
(`-rc.1`) and never released from the same version that already shipped.

## The 0.x contract — while the API is still informing itself

- 0.x releases are for early adopters and their feedback; **every minor update can carry a
  breaking change, including removals**.
- The `CHANGELOG.md` body marks breaking entries in a `### Breaking` subsection so an upgrade
  never requires reading prose with a hunting eye.
- The public surface is recorded by the API-compatibility snapshots under
  `tests/ResulTus.ApiCompat.Tests/PublicApi` — one text file per shipped assembly. During 0.x,
  a change to any of these files **must be deliberate** and appear in the same commit as the
  change itself. That is not yet the promise of equality across versions — it is the promise
  that what changed is visible, reviewable and named.
- The analyzers' diagnostic ids (`RESULT001`–`RESULT006`) and the source generator's
  (`RESULTGEN001`) are part of the contract: their fires must not change meaning without a
  version bump and a changelog entry. This is the rule that keeps them useful as build-time
  dashboards.

## The 1.0 promise — the surface freezes

When ResulTus ships `1.0.0`, every `MAJOR` version change is a breaking change and nothing else
is. The exact definition of *breaking* from that day is mechanical, checked by the
API-compatibility tests from the checked-in snapshots:

- **Adding** a member to a public type, a whole new type, or a new package — a `MINOR` bump.
- **Changing** a member's signature, its parameter types, its return type or its accessibility —
  a `MAJOR` bump.
- **Removing** a member, deleting a public type or removing an entire package — a `MAJOR` bump.
- **Changing behavior** while the shape stays — judged per case; contract guarantees live in the
  docstrings and the specs, which are part of what the tests and CI enforce, so the mutation of
  a contract also counts as a break even when it is invisible to the compiler.
- The analyzers' and source generator's diagnostic *ids and meanings* fall under the same rule:
  code that compiled and диагности-free before cannot start being flagged without a major bump.

Between `MAJOR` versions: `MINOR` for added surface, `PATCH` for fixes only. Every use of the
snapshot tests gates this.

## The deprecation policy

The same rule that keeps behavior stable keeps its removals loud:

1. A member that will be removed is marked `[Obsolete]` (with the replacement and the version
   where the removal happens) at least one minor release before deletion, so the compiler can
   warn every affected caller.
2. The same version's `CHANGELOG.md` names the removal under a `### Deprecated` subsection.
3. The removal itself happens only in a `MAJOR` release — gauged as a break, never as a fix —
   and reuses the version bump where it marries with any other breaking change that version
   carries.

## Publishing flow

A release is a `v*` tag; the release workflow publishes the packages to nuget.org, mirrors them
to GitHub Packages, and creates the GitHub Release object with notes taken verbatim from that
version's changelog section. A published version is immutable. A defect in a version on
nuget.org never re-publishes the same version — the fix ships under the next version, including,
if needed, under an immediate patch.

## Package identity and cross-package compatibility

Every ResulTus package carries the same version and is released from the same tag. A consumer
mixing versions of two ResulTus packages is expected to follow the last one — the packages are
built from one tree, and cross-version mixing is untested territory, not a supported shape.
Every ResulTus package carries the same target frameworks, so a package in one version can be
resolved by the other packages built from the same tree.
