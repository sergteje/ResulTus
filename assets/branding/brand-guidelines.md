# ResulTus brand guidelines — v1.0

> Claim: **Make every result count.**

These guidelines define the ResulTus visual identity. The identity lives at
[`assets/branding/`][assets]; if a new asset deviates from this document, the
document wins until the owner redefines it.

## Palette

| Color | HEX | Usage |
| --- | --- | --- |
| Navy | `#0B1F44` | Primary identity |
| Blue | `#0078FF` | Actions and standout elements |
| Cyan | `#00D4FF` | Check and accents |
| Ice | `#F4F7FF` | Light backgrounds |

Navy is the brand's ground color — every surface the mark sits on is either
navy, ice, or transparent. Cyan is reserved for the check stroke and small
accents; it is never a background. Blue appears once per composition, as the
orbit arc around the check.

## Typography

ResulTus uses [Inter][inter] (SIL Open Font License).

- **Inter** — body text and documentation.
- **Inter SemiBold** — titles.
- **Inter ExtraBold** — the logotype. The wordmark is always the exact string
  `ResulTus` with that casing: capital R, capital T, no space.

The logo PNGs in `assets/branding/` were rendered from the official
`Inter-ExtraBold.ttf`, so no font installation is needed to reproduce them;
the rendering script parameters live in this repository's history.

## Official variants

| File | Use |
| --- | --- |
| [`icon.png`][icon] | NuGet package icon and GitHub avatar |
| [`logo-dark.png`][dark] | Documentation on dark backgrounds |
| [`logo-light.png`][light] | Documentation on light backgrounds |
| [`logo-mono.png`][mono] | Print and monochrome uses (solid black, check knocked out in white) |

The READMEs pick dark/light automatically with a `<picture>` element. Print
uses the monochrome variant only — never reinterpret the palette in CMYK.

## One mark for the whole ecosystem

Every package in the ResulTus ecosystem — `ResulTus`, `ResulTus.AspNetCore`,
`ResulTus.Validation`, `ResulTus.Collections`, `ResulTus.Logging`,
`ResulTus.SourceGenerators`, and those that follow — shares the same icon and
brand. No per-package logos. The icon is wired once in
`Directory.Build.props` via `PackageIcon`, so every packable project carries
the same identity without per-project configuration.

Do not redraw, recolor, rotate, or add effects to the mark. Clear space
around the logo equals the check stroke width. Minimum reproduction size for
the icon is 56×56 px — below that, use the icon alone rather than the full
logo.

[assets]: ./assets/branding/
[inter]: https://rsms.me/inter/
[icon]: ./assets/branding/icon.png
[dark]: ./assets/branding/logo-dark.png
[light]: ./assets/branding/logo-light.png
[mono]: ./assets/branding/logo-mono.png
