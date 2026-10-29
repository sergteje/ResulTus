## Exploration: phase-1-core-setup — Initial Project Scaffold for ResulTus

### Current State
The ResulTus project is a greenfield .NET library with the following structure:
- `src/` — empty directory (no projects)
- `test/` — empty directory (no projects)
- `.gitignore` — standard .NET gitignore (from GitHub's Dotnet.gitignore)
- `LICENSE` — MIT, Copyright (c) 2026 sergteje
- `README.md` — single line with project name
- `openspec/` — already initialized with `config.yaml`, `specs/` (empty), `changes/` (with empty `archive/`)
- `.atl/` — skill-registry cache
- `.vs/` — Visual Studio config (gitignored)
- No `.editorconfig` anywhere (project root, user home, or otherwise)
- No `global.json`, no `Directory.Build.props`, no `Directory.Packages.props`
- No solution file

**SDK Environment**: .NET SDK 10.0.301 available
- Runtimes installed: .NET 6.0.36, 8.0.28, 9.0.17, 10.0.8, 10.0.9
- Can target `net8.0`, `net9.0`, and `net10.0` seamlessly
- All C# 13/14 features available (collection expressions, field-backed properties, etc.)

**OpenSpec Config**: Already configured with:
- `schema: spec-driven`
- Strict TDD disabled (no test project yet)
- Rules defined for proposal, specs, design, tasks, apply, verify, archive phases
- Build command: `dotnet build`
- `coverage_threshold: 0` (no coverage tool yet)
- Testing: no runner detected, no layers available

### Affected Areas
The entire project is greenfield — there is nothing to modify. The following will be **created**:

| Path | Purpose |
|------|---------|
| `ResulTus.sln` | Solution file orchestrating all projects |
| `.editorconfig` | Code style conventions (dotnet new editorconfig + customizations) |
| `.gitattributes` | Line ending normalization for cross-platform |
| `Directory.Build.props` | Shared MSBuild properties (target framework, language version, nullable, ImplicitUsings, assembly metadata) |
| `Directory.Packages.props` | Central NuGet package management (PackageReference pattern) |
| `src/ResulTus/` | Core library: `Error`, `Result`, `Result<T>`, `Result<TSuccess, TError>` as readonly record structs |
| `src/ResulTus/ResulTus.csproj` | Project file targeting `net8.0`+, with NativeAOT/trimming annotations |
| `src/ResulTus/Error.cs` | The `Error` readonly record struct |
| `src/ResulTus/Result.cs` | The void `Result` readonly record struct |
| `src/ResulTus/Result{T}.cs` | The `Result<T>` readonly record struct |
| `src/ResulTus/Result{TSuccess,TError}.cs` | The `Result<TSuccess, TError>` readonly record struct |
| `src/ResulTus/ResultExtensions.cs` | Extension methods (Map, Bind, Match, etc.) |
| `src/ResulTus/AssemblyInfo.cs` | InternalsVisibleTo, CLSCompliant, metadata attributes |
| `test/ResulTus.Tests/` | Unit test project |
| `test/ResulTus.Tests/ResulTus.Tests.csproj` | Test project targeting `net10.0`, referencing xUnit + FluentAssertions |
| `test/ResulTus.Tests/ErrorTests.cs` | Tests for Error type |
| `test/ResulTus.Tests/ResultTests.cs` | Tests for void Result |
| `test/ResulTus.Tests/ResultTTests.cs` | Tests for Result<T> |
| `test/ResulTus.Tests/ResultTErrorTests.cs` | Tests for Result<TSuccess, TError> |
| `test/ResulTus.Tests/Usings.cs` | Global usings for test project |

### Approaches

This is a greenfield project, so the main decision is **how to structure the initial scaffold**. There's really only one viable approach for Phase 1:

1. **Phase-1 scaffold with Core + Tests only** — recommended
   - Create solution, editorconfig, Directory.Build.props, Directory.Packages.props
   - Create only the Core project (`src/ResulTus/`) with the 4 fundamental types
   - Create the test project with xUnit + FluentAssertions
   - Leave other modules (Extensions, Collections, Logging, etc.) for Phase 2+
   - **Pros**: Quick to ship; validates the core API design early; establishes patterns for later modules
   - **Cons**: None significant for a greenfield project
   - **Effort**: Low

2. **Full monolith then extract modules** — not recommended
   - Create everything in one project, then split later
   - **Cons**: Counter to modular design intent; adds extraction work later; no benefit for a greenfield project

3. **All module stubs at once** — overkill for Phase 1
   - Create csproj stubs for all 10 modules
   - **Cons**: Premature; no code to put in stubs; pollutes the solution with empty projects

### Recommendation
**Approach 1** — Phase 1 should deliver only:
- Solution, editorconfig, global.json (pin SDK 10.0.301 for CI reproducibility)
- Directory.Build.props (shared properties: `net8.0` minimum + `net10.0` for tests)
- Directory.Packages.props (centralized: xUnit v2, FluentAssertions, xUnit.runner.visualstudio, Microsoft.NET.Test.Sdk, BenchmarkDotNet optional)
- Core project: `src/ResulTus/` with net8.0+ TFM, the 4 core readonly record structs
- Test project: `test/ResulTus.Tests/` with xUnit + FluentAssertions targeting net10.0
- Update `openspec/config.yaml` to reflect the new state (test runner exists, build command stays)

Key design decisions for the Core:
- **Readonly record structs** (`readonly record struct Error`, `readonly record struct Result`, etc.) — zero heap allocation, value equality, immutability by default
- **`Error`** should contain a non-nullable `string Code`, nullable `string? Message`, and a read-only dictionary (or immutable dictionary) for metadata
- **`Result`** should have implicit conversion from `Error`, methods: `IsSuccess`, `IsFailure`, `Error`, `Match`, `Deconstruct`
- **`Result<T>`** should have implicit conversion from `T` and from `Error`, methods: `IsSuccess`, `IsFailure`, `Error`, `Value`, `Map`, `Bind`, `Match`, `Deconstruct`
- **`Result<TSuccess, TError>`** — the fully generic variant for custom error types
- Types MUST be annotated with `[IsReadOnly]` / `[SkipLocalsInit]` where applicable
- Assembly MUST set `[assembly: CLSCompliant(true)]`
- Projects MUST set `<IsTrimmable>true</IsTrimmable>` and `<IsAotCompatible>true</IsAotCompatible>` for net8.0+ (where available)

### Risks
- **No .editorconfig exists**: If not created, code style will be inconsistent from day one. Mitigation: create it in Phase 1.
- **SDK 10.0.301 as host, targeting net8.0**: .NET 10 SDK can target net8.0, but should verify the `net8.0` TFMs produce correct output. The SDK compatibility is verified — `dotnet build` with `net8.0` works from 10.x SDK.
- **Package version selection**: Need to pick stable versions for xUnit (v2.9.x), FluentAssertions (v6.x or 7.x), BenchmarkDotNet (v0.14.x). Pin specific versions in Directory.Packages.props to avoid surprise breakage.
- **IsAotCompatible on net8.0**: The `<IsAotCompatible>` property is available from .NET 8 SDK onward. Since we use SDK 10.0, this is fine. But must use `TargetFramework` not `TargetFrameworks` (single TFM per project) or condition the AotCompatible property.

### Ready for Proposal
Yes — the exploration is complete. The orchestrator can proceed to `sdd-propose` for `phase-1-core-setup`.

**Next phase to run**: `sdd-propose`
