# Proposal: Phase 3 — Collections

## Intent

Add collection-level operations on `Result<T>`: Combine, Aggregate, WhenAll, WhenAny, Partition, Sequence, Traverse, and ForEach — compatible with IEnumerable, IReadOnlyCollection, List, and Arrays.

## Scope

### In Scope
- Combine, Aggregate, WhenAll, WhenAny, FirstFailure, FirstSuccess
- Merge, Partition, Flatten, Sequence, Traverse
- ForEach, All, Any, None
- `ArrayPool<T>` usage for allocation-sensitive aggregation
- Project: `src/ResulTus.Collections` targeting `net8.0`
- Tests: `test/ResulTus.Collections.Tests` targeting `net10.0`

### Out of Scope
- Logging (Phase 4)
- ASP.NET Core (Phase 5)

## Capabilities

### New Capabilities
- `result-collections`: Collection operations on `IEnumerable<Result<T>>`

### Modified Capabilities
None — new project.

## Approach

Extension methods on `IEnumerable<Result<T>>` and related. WhenAll/WhenAny handle sync collections + async Task/IAsyncEnumerable. Use `ArrayPool<T>` for large collections to minimize GC pressure.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ResulTus.Collections/` | New | Collections library project |
| `test/ResulTus.Collections.Tests/` | New | Tests for all collection operations |
| `ResulTus.sln` | Modified | Add new projects |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| IAsyncEnumerable adds complexity | Med | Separate sync/async overloads clearly |
| WhenAll error semantics (first vs all) | Low | Document: return first error; add `WhenAllAggregate` for all errors |

## Rollback Plan

Delete `src/ResulTus.Collections/` and tests. Remove from solution.

## Dependencies

- Phase 1 core (`ResulTus`) + Phase 2 extensions (`ResulTus.Extensions`)

## Success Criteria

- [ ] All collection methods compile and pass tests
- [ ] WhenAll/WhenAny handle empty, partial-failure, all-failure cases
- [ ] `ArrayPool<T>` buffers returned after use (verified in tests)
- [ ] Code coverage >= 95% (measured by coverlet or similar)
