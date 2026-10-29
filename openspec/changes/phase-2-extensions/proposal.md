# Proposal: Phase 2 — Extensions

## Intent

Add functional extension methods (Map, Bind, Match, Tap, Ensure, Recover, Finally) and all async variants to Result and Result\<T\>, turning them into a fluent pipeline tool for .NET.

## Scope

### In Scope
- Sync: Map, Bind, Ensure, Match, Tap, Recover, Finally, MapError, OnSuccess, OnFailure, Switch
- Async: MapAsync, BindAsync, EnsureAsync, MatchAsync, TapAsync, RecoverAsync, FinallyAsync (ValueTask overloads)
- Conversions: ToResult, ToNullable, ToOption, ToMaybe, ToException
- Project: `src/ResulTus.Extensions` targeting `net8.0`
- Tests: `test/ResulTus.Extensions.Tests` targeting `net10.0`

### Out of Scope
- Collections methods (Phase 3)
- Logging integration (Phase 4)

## Capabilities

### New Capabilities
- `result-extensions`: Map, Bind, Match, Tap, Ensure, Recover, Finally + async + conversion methods

### Modified Capabilities
None — new project.

## Approach

All methods as `readonly record struct` extension methods with `[MethodImpl(AggressiveInlining)]`. Async overloads accept `CancellationToken`. ValueTask overloads for sync-hot paths to minimize allocations.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ResulTus.Extensions/` | New | Extensions library project |
| `test/ResulTus.Extensions.Tests/` | New | Tests covering all methods + async |
| `ResulTus.sln` | Modified | Add new projects |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Overload ambiguity (many methods) | Med | Careful naming — use distinct parameter types |
| Async method explosion | Med | Template-based generation; ValueTask for hot paths |

## Rollback Plan

Delete `src/ResulTus.Extensions/` and `test/ResulTus.Extensions.Tests/`. Remove from solution. Revert any solution changes.

## Dependencies

- Phase 1 core types (`ResulTus` project)

## Success Criteria

- [ ] All sync/async extension methods compile and pass tests
- [ ] No heap allocation on sync hot path (ValueTask where appropriate)
- [ ] `dotnet build` succeeds with no warnings
- [ ] Code coverage >= 95% (measured by coverlet or similar)
