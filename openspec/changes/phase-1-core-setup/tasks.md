# Tasks: Phase 1 — Core Setup (ResulTus)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~755 |
| 400-line budget risk | Medium (D2 = 800-line budget) |
| Chained PRs recommended | Yes (C3 force-chained) |
| Suggested split | PR 1: Foundation → PR 2: Core Types → PR 3: Tests |
| Delivery strategy | auto-chain (C3) |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Foundation: config + solution + empty csproj | PR 1 | Base: feature/tracker branch or main. All build config, no type code. |
| 2 | Core Types: ErrorType, Error, Result, Result\<T\>, Result\<TSuccess,TError\> | PR 2 | Depends on PR 1. Adds all 5 struct/enum files. |
| 3 | Tests + Verify: all test files, build + test run | PR 3 | Depends on PR 2. Full solution must build and all tests pass. |

## Phase 1: Foundation (Config + Solution)

- [x] 1.1 Create `.editorconfig` — strict .NET conventions, nullable warn, XML docs required, UTF-8 BOM
- [x] 1.2 Create `Directory.Build.props` — shared MSBuild: net8.0, Nullable enable, IsTrimmable, IsAotCompatible, LangVersion latest
- [x] 1.3 Create `Directory.Packages.props` — centralized versions: xUnit 2.9.3, FluentAssertions 7.1.0, Microsoft.NET.Test.Sdk 17.13.0
- [x] 1.4 Create `global.json` — pin SDK 10.0.301
- [x] 1.5 Create `src/ResulTus/ResulTus.csproj` — net8.0, IsTrimmable, IsAotCompatible
- [x] 1.6 Create `src/ResulTus/AssemblyInfo.cs` — InternalsVisibleTo("ResulTus.Tests"), CLSCompliant(true)
- [x] 1.7 Create `ResulTus.slnx` — add src/ResulTus and test/ResulTus.Tests projects

## Phase 2: Core Types (5 Source Files)

- [x] 2.1 Create `src/ResulTus/ErrorType.cs` — byte enum with 10 values from None to Unexpected
- [x] 2.2 Create `src/ResulTus/Error.cs` — readonly record struct, StructLayout, 9 factory methods, None singleton, Code/Message/Type/Metadata
- [x] 2.3 Create `src/ResulTus/Result.cs` — void variant: byte _isSuccess, implicit Error→Result, Match, Deconstruct, TryGetError
- [x] 2.4 Create `src/ResulTus/Result{T}.cs` — value variant: implicit T/Error, TryGetValue (2 overloads), GetValueOrDefault, GetValueOr, Match, Deconstruct
- [x] 2.5 Create `src/ResulTus/Result{TSuccess,TError}.cs` — dual generic: implicit both ways, Match, Deconstruct

## Phase 3: Tests (xUnit + FluentAssertions)

- [x] 3.1 Create `test/ResulTus.Tests/ResulTus.Tests.csproj` — net10.0, project ref to src/ResulTus, xUnit + FluentAssertions
- [x] 3.2 Create `test/ResulTus.Tests/Usings.cs` — global using FluentAssertions, Xunit, ResulTus
- [x] 3.3 Create `test/ResulTus.Tests/ErrorTypeTests.cs` — default = None, all values distinct
- [x] 3.4 Create `test/ResulTus.Tests/ErrorTests.cs` — factories set Code+Type, None singleton, equality, metadata
- [x] 3.5 Create `test/ResulTus.Tests/ResultTests.cs` — success/failure, implicit Error, Match both branches, Deconstruct, TryGetError, default struct
- [x] 3.6 Create `test/ResulTus.Tests/ResultTTests.cs` — implicit T/Error, TryGetValue overloads, GetValueOrDefault, GetValueOr, Match, Deconstruct, default
- [x] 3.7 Create `test/ResulTus.Tests/ResultTErrorTests.cs` — implicit TSuccess/TError, Match, Deconstruct, default struct

## Phase 4: Verify

- [x] 4.1 Run `dotnet build` — full solution compiles clean
- [x] 4.2 Run `dotnet test` — all 5 test classes pass
