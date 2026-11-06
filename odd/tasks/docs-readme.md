# Documentation P3 — README

Objective: give ResulTus a front door. `README.md` currently contains a heading and nothing else, so the library's contract, its measured standing and its eleven packages are invisible to anyone who finds the repository. Branch: `feature-1`.

Test runner: none. This phase changes Markdown only; the verification is the build and the suite staying green, plus the snippet compile check in S5.

Delivery strategy: `single-pr`. Forecast ~250 authored changed lines, all in `README.md`. PR and merge stay the user's call.

Risk: none. No code and no public API change.

## Specs

- **S1 — the README states what the library is and what it promises.** It opens with one sentence of purpose and then the contract, taken from `docs/performance.md` and not invented: zero heap allocation on the success path **and** on the failure path, `Error` as a 24-byte inline `readonly record struct`, `Result<int>` at 32 bytes, and the generic tier `Result<TSuccess, TError>` at 13 bytes. Every number carries a link to `docs/performance.md`.
- **S2 — quick start with compiling examples.** At minimum: creating a success and a failure, `Match`, `TryGetValue`, one extension pipeline (`Map` → `Bind` → `Ensure` → `Recover`), the generic tier with a custom error type, and one `System.Text.Json` round trip using the converters from `ResulTus.Serialization`.
- **S3 — the package table.** One row per project, each with a single line of purpose: `ResulTus`, `ResulTus.Extensions`, `ResulTus.Collections`, `ResulTus.Logging`, `ResulTus.Serialization`, `ResulTus.Diagnostics`, `ResulTus.AspNetCore`, `ResulTus.Validation`, `ResulTus.Validation.FluentValidation`, `ResulTus.Analyzers`, `ResulTus.SourceGenerators`.
- **S4 — differentiators are stated as verifiable facts.** The analyzer rule ids (`RESULT001`–`RESULT006`), the `[ErrorFactory]` source generator (`RESULTGEN001`), the structured logging state, the `Activity`/`DiagnosticSource` diagnostics, and the strict JSON that rejects numeric and unknown `ErrorType` names. Each claim must be traceable to a file in `src/` or to a row in `docs/performance.md`. No superlative that is not backed by a measurement.
- **S5 — every code block compiles.** Assumption, stated as one: the snippets are compiled against the built assemblies in a scratch project under `%TEMP%`, never committed to the repository, so a reader cannot copy a snippet that does not build. This is not a requested requirement; it is the acceptance bar for "the README is correct".
- **S6 — artifact rules.** English, Conventional Commit message, no `Co-Authored-By` and no AI attribution. The comparison table against ErrorOr, ZeroResult, FluentResults and CSharpFunctionalExtensions is either omitted or carries the verification note from the audit: ErrorOr and ZeroResult were checked against their published READMEs on 2026-11-06, the other two are stated as public-API knowledge.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2 | inline | pending | — |
| T2 | S3, S4 | inline | pending | — |
| T3 | S5, S6 | inline | pending | — |

## Log

### L1 — original request (verbatim)

> La fase 11 la vamos a postponer. Antes quiero que veamos todo el tema de rendimiento gc, heap, allocations de toda la libreia completa y tambien si hay cosas que podamos mejorar Span<T>,ConcurrentDictionary, etc etc ver que tienen otras librerias que nosotros no tengamos, en cual destaca la nuestra sobre otra, que tienen otras que podamos implementar y de paso mejorar aqui

### L2 — this document was requested (verbatim)

> Documenta los P0, p1 y p3 para que no se olvide para hacerlo por ODD

### L3 — starting evidence

- `README.md` contains exactly `# ResulTus` and two blank lines. There is no `CONTRIBUTING.md` and no `.github/` directory.
- `Directory.Build.props` already carries the package metadata that a README has to agree with: `Description`, `PackageLicenseExpression` MIT, and the repository URL.
- The numbers S1 must quote are in `docs/performance.md` §4.2 and §4.5, measured 2026-10-29 on an Intel Core i7-6700 with .NET SDK 10.0.403.
- The analyzer ids are `RESULT001`–`RESULT006` in `src/ResulTus.Analyzers/DiagnosticDescriptors.cs`; the generator id is `RESULTGEN001` in `src/ResulTus.SourceGenerators`.

### L4 — test-first exception

This phase changes no behavior, so there is no RED/GREEN. The verification is: every snippet compiles against the built assemblies in a throwaway project, `dotnet build ResulTus.slnx` stays at 0 warnings, and `dotnet test ResulTus.slnx` stays green. A snippet that does not compile is a defect in the README, not an exception.

### L5 — scope boundary

The README does not document P2. The feature gaps found in the audit (`FailIf`, `Combine` for `Result<T>`, query syntax, multi-error, `StackResult`) are a product decision the owner has not taken, and advertising a feature that does not exist is worse than omitting it.
