# Phase 10 — Source generators

Objective: ship `ResulTus.SourceGenerators`, an incremental generator that implements the `static partial Error` factories declared on a `[ResultErrors]` type. Branch: `feature-1`.

Test runner: `dotnet test tests/ResulTus.SourceGenerators.Tests/ResulTus.SourceGenerators.Tests.csproj`.

Delivery strategy: `auto-chain`. The forecast was ~900 authored changed lines; the work landed at 2369, in one work-unit commit on `feature-1`, the same shape as Phase 8 (1261) and Phase 9 (2247). PR and merge stay the user's call.

## What changed from the proposal, and why

| Proposal says | What the code actually allows |
| --- | --- |
| Generated `Error` carries `Metadata` | **`Error` has no `Metadata`.** It is a three-field `readonly record struct` (`Code`, `Message?`, `Type`) built to live inline in every result with no allocation. A metadata bag would undo that and is a core API change this phase does not own. Dropped. |
| "Generates `ErrorType` values" | `ErrorType` already exists with its ten members. There is nothing to generate; the generator consumes the value, it does not produce it. |
| `src/ResulTus.SourceGenerators/ResultErrorsAttribute.cs` | The attribute cannot live in the generator assembly. A generator is referenced with `ReferenceOutputAssembly="false"`, so a consumer could not write `[ResultErrors]` at all. Both attributes ship in `ResulTus` core. |
| `test/ResulTus.SourceGenerators.Tests` | This repository uses `tests/`. |
| "Auto-generates static Error factory methods from method signatures" | A signature alone carries no code, no category and no text. The user asked for one attribute per method carrying all three. See L2. |

## The contract

```csharp
[ResultErrors]
public static partial class UserErrors
{
    [ErrorFactory("USER_NOT_FOUND", ErrorType.NotFound, "User {id} not found")]
    public static partial Error NotFound(Guid id);
}
```

generates

```csharp
public static partial global::ResulTus.Error NotFound(global::System.Guid id)
    => global::ResulTus.Error.NotFound("USER_NOT_FOUND", $"User {id} not found");
```

- `[ResultErrors]` is the opt-in marker. A type without it is never scanned, even if it carries `[ErrorFactory]`.
- `[ErrorFactory(code, type, message)]` carries the three values. Nothing is inferred from the method name.
- The message template is emitted verbatim as the content of an interpolated string, so a placeholder is a parameter name. Literal braces are written `{{` and `}}`, exactly as in an interpolated string.
- `ErrorType.None` is rejected: a factory that produces "no error" is not a factory.
- Every `Error` static factory is named after its `ErrorType` member, so `Error.{Type}(code, message)` is uniform across all nine usable values.

## Specs

- **S1 — the projects exist and are wired.** `src/ResulTus.SourceGenerators` targets `netstandard2.0`, sets `IsRoslynComponent`, `EnforceExtendedAnalyzerRules`, and overrides `IsTrimmable`/`IsAotCompatible` to `false`; it references `Microsoft.CodeAnalysis.CSharp` with `PrivateAssets=all`. `tests/ResulTus.SourceGenerators.Tests` targets `net10.0`. Both in `ResulTus.slnx`. 0 warnings under `TreatWarningsAsErrors`.
- **S2 — the attributes are the contract.** `ResultErrorsAttribute` is valid on a class or a struct and on nothing else; `ErrorFactoryAttribute` is valid on a method and on nothing else; both are `Inherited = false`, `AllowMultiple = false`; `ErrorFactoryAttribute` exposes `Code`, `Type` and `Message` as readable properties, rejects a `null` code, and treats a `null` message as "this error carries no message" rather than as an empty one.
- **S3 — the generated body is correct.** The generated method is `partial`, has the declared accessibility and name, returns `Error`, and its body is exactly `Error.{Type}(code, $"template")`. Verified by executing the generated assembly: `Code`, `Message` and `Type` of the returned value match the attribute, and the interpolated placeholders are filled from the arguments.
- **S4 — every type kind works.** `class`, `struct`, `record class` and `record struct`, each `partial`, top-level and nested, generic and non-generic. The generated part repeats the declaring modifiers (accessibility, `static`, `readonly`, `ref`, `unsafe`) and the type parameter list, so it merges with the user's part instead of conflicting with it.
- **S5 — parameters are reproduced faithfully.** Parameter types are emitted fully qualified, nullable annotations are preserved, and `ref`, `in`, `ref readonly` and `params` are reproduced, so the implementation part matches the declaration part and the compiler reports no signature mismatch. `out` is **not** reproduced: it is diagnosed under S6 instead, because a generated body that returns an `Error` cannot assign the caller's variable. See L6.
- **S6 — an unusable declaration is diagnosed, not silently dropped.** `RESULTGEN001` is reported when the containing type is not marked `[ResultErrors]`, is not `partial`, or is `file`-scoped; or when the method is not `partial`, already has a body, is not `static`, does not return `Error`, has an `out` parameter, or uses `ErrorType.None`. The message names the offending member.
- **S7 — the pipeline is incremental and the output is deterministic.** Only types carrying `[ResultErrors]` produce output; a compilation that uses neither attribute produces zero generated trees; running the generator twice over the same compilation produces the same hint names and the same text.
- **S8 — no regression.** `dotnet build ResulTus.slnx` with 0 warnings, the whole suite green including the 399 existing tests, and coverage on `ResulTus.SourceGenerators` >= 95% lines and branches.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2 | inline | done | `this commit` |
| T2 | S3, S4 | inline | done | `this commit` |
| T3 | S5 | inline | done | `this commit` |
| T4 | S6, S7 | inline | done | `this commit` |
| T5 | S8 | inline | done | `this commit` |

## Log

### L1 — original request (verbatim)

> continuamos

Phase 10 (Source Generators) from `openspec/changes/phase-10-sourcegenerators/proposal.md`, continuing the roadmap after Phase 9.

### L2 — the design decision (verbatim)

> Un atributo por método (Recomendado)

Chosen over a type-level prefix with name-based conventions, and over pure convention. The method name does not become a contract, nothing falls back to a silent default, and every factory states its code, its category and its text in one place. `[ResultErrors]` stays as the opt-in marker because the proposal names it.

### L3 — starting evidence

- `Error` is `public readonly record struct Error` with `Code` (never null), `Message?` and `Type`. No `Metadata`. Private constructor; all construction goes through the static factories.
- The static factories are `Failure`, `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`, `Timeout`, `Cancelled`, `Unexpected`, each `(string code, string? message = null)` and each setting the matching `ErrorType`. `None` is a property, not a factory.
- `ErrorType` is `enum ErrorType : byte` with `None = 0` and the nine categories above.
- `Directory.Build.props` applies `TargetFramework=net8.0`, `Nullable=enable`, `IsTrimmable=true`, `IsAotCompatible=true`, `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true` to every project.
- `Directory.Packages.props` already pins `Microsoft.CodeAnalysis.CSharp` 4.14.0, which exposes `ForAttributeWithMetadataName`.
- `src/ResulTus.Analyzers` is the working precedent for a netstandard2.0 Roslyn component in this repository: same framework overrides, same `PrivateAssets=all`, same `EnforceExtendedAnalyzerRules`.
- The proposal's `[ResultErrors]` example carries no code, no category and no message, so the generator has nothing to read from a bare signature. That is what L2 resolves.

### L4 — test infrastructure

Same decision as Phase 9, for the same reason: `Microsoft.CodeAnalysis.Testing` ships verifiers for xunit v2, NUnit and MSTest, and this repository is on xunit.v3. The test project drives the generator directly — `CSharpGeneratorDriver.Create(...).RunGeneratorsAndUpdateCompilation(...)` — and asserts three things per case: the generated text, that the updated compilation has zero errors, and the behaviour of the emitted assembly. The third one is what makes the test about the library rather than about a string.

### L5 — RED/GREEN evidence

Every task observed a failing test before its implementation, except where noted.

| Task | RED | GREEN |
| --- | --- | --- |
| T1 | `dotnet build` of the test project: `CS0246` for `ResultErrorsAttribute` and `ErrorFactoryAttribute`, five times. | 5/5 — `AttributeContractTests`. |
| T2 | `CS0246` for `ErrorFactoryGenerator` in `GeneratorHarness`. | 19/19 — generation and type-kind tests. |
| T3 | 2 of 7 parameter tests failed: `params` and `ref readonly` were not reproduced (`Contain` failed). The other five already passed, because the parameter renderer was written in T2 to satisfy the basic signature; that is stated here rather than dressed up as a RED. | 28/28. |
| T4 | All 9 diagnostic tests failed (`Single` found no `RESULTGEN001`). The 4 incremental tests passed immediately: they describe behaviour the pipeline already had. | 41/41. |
| T5 | Not a behaviour task; see L7. | 50/50, coverage 99.1% lines / 98.2% branches. |

Two failures in T2 were real generator defects found by the tests, not by inspection:

- `AddSource` rejected the source text: *"El elemento SourceText con hintName ... debe tener un conjunto de codificación explícito"*. `SourceText.From` needs the encoding passed explicitly.
- The generated call was `Error.NotFound(USER_NOT_FOUND, ...)` — the code was emitted without its quotes, so every generated file failed with `CS0103`.

### L6 — decisions the implementation forced

- **`out` parameters are diagnosed, not reproduced.** A partial method's implementation must repeat `out`, and then `CS0177` demands it be assigned on every path. The generated body returns an `Error`; it has no value for the caller's variable. Assigning `default` would compile and hand the caller a value the factory never produced — a silent lie. `RESULTGEN001` says so instead. S5 was amended with this entry.
- **`params` and `ref readonly` are part of the signature.** The plan carried a note not to emit `params`; the test proved otherwise — the two parts would not merge. Both are emitted now, and `params` and a ref kind are mutually exclusive by construction.
- **`FactoryShape.Type` is a `string`, not an `ErrorType`.** The generator is `netstandard2.0` and cannot reference the `net8.0` core, so it carries the member *name* and emits `global::ResulTus.Error.{name}(...)`. That is also the honest model: a generator should not depend on the assembly it generates code for.
- **`IsExternalInit` polyfill.** `netstandard2.0` does not ship it, and the pipeline models are `record`s so their value equality lets the incremental pipeline compare finished text instead of symbols.
- **Release tracking files.** `RS2008` is an error under `EnforceExtendedAnalyzerRules`. `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` were added rather than suppressing the rule: a shipped Roslyn component should document its rule ids.
- **Dead defensive branches removed, the Roslyn boundary kept.** Guards a compiler contract makes unreachable were deleted instead of covered with artificial tests: a null `ContainingType`, an empty `Attributes` array, a null enum argument, a type with no syntax reference, and a hint name that needed sanitizing (every component is already a C# identifier). The three lines that remain unreachable are the boundary with Roslyn itself — the `TargetSymbol` cast guard and the `AttributeClass` null side — and are left in place, listed in L7.

### L7 — verification

- `dotnet build ResulTus.slnx` — 0 warnings, 0 errors.
- `dotnet test ResulTus.slnx` — 449 tests green: the 399 that existed before Phase 10 plus the 50 new ones.
- Coverage on `ResulTus.SourceGenerators`, from `--collect:"XPlat Code Coverage"`: **345/348 lines (99.1%)**, **109/111 branches (98.2%)**.
- The three uncovered lines and two uncovered branches are unreachable by construction: `context.TargetSymbol is not IMethodSymbol` (the pipeline predicate only admits method declarations), `FactoryCandidate.Ignored()` (its only caller is that guard), and the `AttributeClass` null side of the opt-in check (an attribute whose class cannot be resolved is an error symbol, not a null one).

### L8 — next

Phase 11 (CI/CD, DocFX, NuGet publish). Delivery — commit chain, PR, merge — stays the user's call.

