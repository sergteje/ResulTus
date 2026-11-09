# Brand kit — official ResulTus identity

Objective: adopt the owner's proposed visual identity v1.0 as the official ResulTus brand — palette, typography, icon and logo variants — and wire the icon into every NuGet package plus both READMEs. One identity for the whole ecosystem; no per-package logos. Branch: `main` (post-merge; direct commits are owner-authorized). Delivery strategy: single commit series on `main`, CI guards the suite.

Test runner: `dotnet test ResulTus.slnx`. Packaging is the observable gate: the packed `.nupkg` must contain `icon.png` and `README.md`.

Risk: item 3 (contracts others consume — packaging metadata) and item 5 (identity that persists in a published artifact). An icon baked into a published 0.0.x cannot be changed without a new version; nothing is published by this work.

## Specs

- **S1 — the palette is the four brand colors**, quoted from the owner verbatim: Navy `#0B1F44` (identidad principal), Blue `#0078FF` (acciones y elementos destacados), Cyan `#00D4FF` (check y acentos), Ice `#F4F7FF` (fondos claros).
- **S2 — the typography is Inter**, from the owner: "Inter: textos y documentación. Inter SemiBold: títulos. Inter ExtraBold: logotipo tipográfico."
- **S3 — four official variants** (owner verbatim): "ResulTus-Icon.png — NuGet y avatar GitHub. ResulTus-Logo-Dark.png — documentación sobre fondo oscuro. ResulTus-Logo-Light.png — documentación sobre fondo claro. ResulTus-Monochrome.png — impresión y usos monocromáticos." Stored as `assets/branding/{icon,logo-dark,logo-light,logo-mono}.png` per the owner's structure.
- **S4 — the packaged icon and README:** the owner's NuGet config, quoted verbatim:
  ```xml
  <PropertyGroup>
      <PackageIcon>icon.png</PackageIcon>
      <PackageReadmeFile>README.md</PackageReadmeFile>
      <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>
  ```
  with the `None`/`Pack` includes for the icon and the README. `Directory.Build.props` holds it once; the thirteen test projects already set `IsPackable=false`, so it reaches only the packable packages.
- **S5 — one identity for the ecosystem** (owner verbatim): "Todos compartirían el mismo icono y marca. Mi recomendación es no crear logotipos diferentes por paquete. ResulTus debe convertirse en una marca reconocible dentro del ecosistema .NET." Claim: "Make every result count."
- **S6 — `assets/branding/brand-guidelines.md` documents the kit** (palette with hex and usage, typography, variants, the one-mark decision) so the identity survives its authors.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1-S3, S5 | inline | done | see L2 |
| T2 | S4, S6 | inline | done | see L2 |
| T3 | S4 | inline | done | see L2 |
| T4 | all | inline | done | see L2 |

## Log

### L1 — the owner names the official identity (verbatim)

> ¡Esta sería la identidad visual oficial que propondría para ResulTus!
>
> [Brand Kit — ResulTus, propuesta de identidad v1.0, palette (Navy #0B1F44, Blue #0078FF, Cyan #00D4FF, Ice #F4F7FF), Inter (SemiBold títulos, ExtraBold logotipo), variants ResulTus-Icon/Logo-Dark/Logo-Light/Monochrome, assets/branding/ structure, NuGet config with PackageIcon + PackageReadmeFile, and the one-identity-for-the-ecosystem decision]

Layout deviations, stated openly: the owner's sketch lists `benchmarks/` and `ResulTus.sln`; the repo's live layout is `tests/ResulTus.Benchmarks/` and `ResulTus.slnx`. The brand work changes neither — adopting a template's hypothetical paths over the repo's real ones would break the solution.

The `PackageReadmeFile` addition is the owner's own config and is safe to adopt: the packaged README is the canonical README, with a known limitation that its relative links (the performance charter, the Spanish edition) do not resolve inside nuget.org's renderer. That is accepted as-is; the file remains correct on GitHub, which is where the links matter. `PackageTags` was earlier noted as missing and remains out of the owner's snippet — it is added only if the owner names tag values.

### L2 — assets rendered locally, metadata wired, pack verified (2026-11-09)

**How the images exist.** The AI image generator produced no output: all four requests failed with *"Insufficient account funds"* — a provider-side failure, not a repository decision. The failure is recorded, not retried. The assets were therefore **rendered locally** with the real brand system: the official `Inter-4.1.zip` (rsms/inter, SIL OFL) provided `Inter-ExtraBold.ttf`, and the geometry — navy rounded square, blue orbit arc, cyan check stroke — was composed with GDI+ at 512×512 (icon) and 1081×320 (logos, transparent except mono). Per S2 the wordmark provably uses Inter ExtraBold, not a substitute: it renders from the shipped TTF. If the generator gains funds, these files can be replaced with AI variants; until then they are the official assets.

**T1, T2, T3 — files in place.**
- `assets/branding/icon.png` (12,003 B), `logo-dark.png` (16,918 B, transparent), `logo-light.png` (16,836 B, transparent), `logo-mono.png` (11,842 B, transparent, white knocked-out check for print), `brand-guidelines.md` (palette with hex and usage, Inter roles, the four variants, the one-mark-for-the-ecosystem rule S5, clear-space and 56 px minimum). All far below NuGet's 1 MB icon limit.
- `Directory.Build.props` gained exactly the owner's snippet (S4): `PackageIcon` + `PackageReadmeFile` remain a single solution-wide block; the `None` includes use `$(MSBuildThisFileDirectory)` so the icon and README resolve from the repo root for every project regardless of depth.
- Both READMEs open with a `<picture>` swapping `logo-dark.png` / `logo-light.png` by color-scheme preference, identical markup in both editions.

**T4 — verification, all observed.**
- `dotnet pack src/ResulTus -c Release` → the `.nupkg` contains **`icon.png` (12,003 B) and `README.md` (25,141 B)**; the `.nuspec` declares `<icon>icon.png</icon>`, `<readme>README.md</readme>`, `<license type="expression">MIT</license>`. Temp output deleted.
- `dotnet build ResulTus.slnx` → 0 warnings / 0 errors. `dotnet test --no-build` → **670 passed, 0 failed, 0 skipped**.
- README parity re-measured symmetrically: **25/25 headings, 405/405 numeric tokens** across the two editions.
- The CI workflow runs the same build+test on push; its result is the third, independent gate.

### L3 — owner-supplied brand suite v2.0 replaces the rendered assets (2026-11-09)

**Owner, with the two files (`ResultusIconSuite.jpeg`, `ResultusIcon.jpeg`):**

> mira he hecho unos logos un una suite completa de logos

The owner's own artwork supersedes the locally rendered set — the owner defines the identity. What changed in `assets/branding/`:

- `icon.png` — the owner's 1254×1254 master, downsampled to 512×512 PNG (192,964 B, under NuGet's 1 MB limit). JPEG→PNG conversion and bicubic resample only; no redesign.
- `logo-dark.png`, `logo-light.png`, `logo-mono.png` — the three lockup cards cropped from the owner's suite sheet (1536×1024), visually verified after cutting: navy card for dark, white card for light, solid black for print.
- `brand-guidelines.md` — the lockup set is the owner's fixed raster art, no longer re-typeset from Inter; the Inter roles remain for material that needs new text. The owner's brand values (Reliable, Performant, Flexible, Developer First) are recorded.

The README `<picture>` markup kept the same filenames, so both editions gained the new art without a markup change. Pack verified: the `nupkg` carries the owner's icon (structure otherwise unchanged, 0 warnings / 0 errors pack). README parity stays symmetric (26/26 headings, 408/408 numeric tokens).

### L4 — the README AOT claim the owner approved (2026-11-09)

**Owner, verbatim:**

> tambien dale a esto: el AOT tiene prueba observada. Publicar "Native AOT compatible" en el README es una decisión de wording tuya

Both README editions gained `## Runtime compatibility` (identical structure, translated) stating that trimming and Native AOT are **verified by an observed publish-and-run** in CI, citing the `aot-smoke` job and run `42653755212`, and scoping the claim to what the smoke actually exercises: the core result types plus the AOT-safe System.Text.Json surface. The factory-based registration stays recommended for non-AOT apps. `CHANGELOG.md`'s `[Unreleased]` gained a **Verified** section with the same run id. The 0.0.1 `Notes` line "not proven by a published publish-and-run result" stays untouched — it describes the tag truthfully and is superseded in `[Unreleased]`, exactly per the S3 rule.
