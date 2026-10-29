# Proposal: Phase 10 — Source Generators

## Intent

Create incremental source generators that auto-generate Error factory methods from `[ResultErrors]` attribute on partial classes — reducing boilerplate for domain error definitions.

## Scope

### In Scope
- `[ResultErrors]` attribute for partial class/struct/record
- Auto-generates static Error factory methods from method signatures
- Generates ErrorType values
- Incremental generator pipeline (IIncrementalGenerator)
- Project: `src/ResulTus.SourceGenerators` targeting `netstandard2.0`
- Tests: `test/ResulTus.SourceGenerators.Tests` targeting `net10.0`

### Out of Scope
- Code-only analyzers (Phase 9)
- Validation source generators (separate feature)

## Capabilities

### New Capabilities
- `result-sourcegenerators`: Incremental source generators for Error factories

### Modified Capabilities
None — new project.

## Approach

Implement `IIncrementalGenerator`. Scan for `[ResultErrors]` on partial types. Generate: `public static partial Error NotFound(Guid id)` → creates Error with Code, interpolated Message, and optional Metadata.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ResulTus.SourceGenerators/` | New | Source generator project (netstandard2.0) |
| `src/ResulTus.SourceGenerators/ResultErrorsAttribute.cs` | New | Attribute definition |
| `test/ResulTus.SourceGenerators.Tests/` | New | Generator output verification tests |
| `ResulTus.sln` | Modified | Add new projects |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Incremental pipeline must be efficient | Med | Use cached pipeline; register output nodes |
| Handle all C# type kinds (struct, class, record) | Med | Test each kind; use type-agnostic generation |

## Rollback Plan

Delete `src/ResulTus.SourceGenerators/` and tests. Remove from solution.

## Dependencies

- Phase 1 core (`ResulTus`) — Error type reference
- NuGet: `Microsoft.CodeAnalysis.CSharp` (generator), `Microsoft.CodeAnalysis.Testing` (test)

## Success Criteria

- [ ] `[ResultErrors]` on partial class generates correct factory methods
- [ ] Generated Error includes Code, Message with interpolation, Metadata
- [ ] Works on struct, class, and record partial types
- [ ] Code coverage >= 95% (measured by coverlet or similar)
