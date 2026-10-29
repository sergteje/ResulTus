# Proposal: Phase 1 — Core Setup

## Intent

Greenfield scaffold for ResulTus, a high-performance Result Pattern library for .NET. Create the foundation — solution structure, build conventions, and 4 core readonly record struct types (`Error`, `Result`, `Result<T>`, `Result<TSuccess, TError>`) with zero heap allocations targeting `net8.0` minimum.

## Scope

### In Scope
1. Solution file (`ResulTus.sln`) — Core + Tests projects
2. `.editorconfig` — strict .NET conventions, nullable, warnings-as-errors, XML docs required
3. `Directory.Build.props` — shared MSBuild properties (`net8.0` base)
4. `Directory.Packages.props` — centralized xUnit + FluentAssertions
5. `global.json` — pin SDK 10.0.301
6. Core project `src/ResulTus/` targeting `net8.0` — Error, Result, Result\<T\>, Result\<TSuccess,TError\> as readonly record structs
7. Test project `test/ResulTus.Tests/` targeting `net10.0` — xUnit + FluentAssertions
8. `AssemblyInfo.cs` — `InternalsVisibleTo` for test project
9. Update `openspec/config.yaml` — reflect new state

### Out of Scope
- Extension methods (Map, Bind, Match, MapAsync, BindAsync) — Phase 2
- Collections, Logging, AspNetCore, Validation, Serialization packages
- Analyzers / Source Generators
- Benchmarks and CI/CD pipeline

## Capabilities

### New Capabilities
- `result-core`: Fundamental Result types — Error, Result, Result\<T\>, Result\<TSuccess,TError\> as readonly record structs targeting net8.0+

### Modified Capabilities
None — greenfield project.

## Approach

Core types are `readonly record struct` with `[StructLayout(LayoutKind.Sequential, Pack = 1)]` and `[MethodImpl(MethodImplOptions.AggressiveInlining)]`:

- **Error**: `string Code`, `string? Message`, `ErrorType Type`, `IReadOnlyDictionary<string, object?>? Metadata`
- **Result**: void variant, implicit conversion from `Error`, `IsSuccess`/`IsFailure`/`Error`/`Match`/`Deconstruct`
- **Result\<T\>**: value variant, implicit conversions from `T` and `Error`, plus `Value` accessor
- **Result\<TSuccess, TError\>**: fully generic variant for custom success/error types

All projects set `<IsTrimmable>true</IsTrimmable>` and `<IsAotCompatible>true</IsAotCompatible>`.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `ResulTus.sln` | New | Solution orchestrating all projects |
| `.editorconfig` | New | Strict .NET conventions |
| `Directory.Build.props` | New | Shared MSBuild properties |
| `Directory.Packages.props` | New | Centralized NuGet versions |
| `global.json` | New | Pin SDK to 10.0.301 |
| `src/ResulTus/` | New | Core library (5 files: csproj, 4 types) |
| `test/ResulTus.Tests/` | New | Unit tests (5 files: csproj, 4 test classes) |
| `openspec/config.yaml` | Updated | Test runner detection, current state |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| SDK 10 targeting `net8.0` compatibility | Low | Verified working; single TFM per project |
| Package version drift | Low | Pin exact versions in Directory.Packages.props |

## Rollback Plan

Delete `ResulTus.sln`, `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`. Remove `src/ResulTus/` and `test/ResulTus.Tests/`. Revert `openspec/config.yaml` to previous state.

## Dependencies

None — fully greenfield.

## Success Criteria

- [ ] `dotnet build` succeeds for the full solution
- [ ] All 4 core types compile as `readonly record struct`
- [ ] Tests compile and pass
- [ ] No heap allocations on happy path (verifiable via benchmarks in Phase 2+)
- [ ] Code coverage >= 95% (measured by coverlet or similar)
