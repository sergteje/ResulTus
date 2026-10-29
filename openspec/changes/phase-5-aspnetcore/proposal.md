# Proposal: Phase 5 — ASP.NET Core

## Intent

Bridge Result to ASP.NET Core: `ToHttpResult()` for Minimal APIs, `ToActionResult()` for MVC, automatic error-to-HTTP status mapping with ProblemDetails support.

## Scope

### In Scope
- `ToActionResult()` for MVC controllers
- `ToHttpResult()` for Minimal APIs (IResult)
- Error → HTTP Status: Validation→400, NotFound→404, Unauthorized→401, Forbidden→403, Conflict→409, Failure→500
- Customizable error mapping delegate
- ProblemDetails support
- Project: `src/ResulTus.AspNetCore` targeting `net8.0`
- Tests: `test/ResulTus.AspNetCore.Tests` targeting `net10.0`

### Out of Scope
- Validation adapters (Phase 6)
- OpenAPI integration

## Capabilities

### New Capabilities
- `result-aspnetcore`: ASP.NET Core integration with HTTP mapping

### Modified Capabilities
None — new project.

## Approach

Extension methods on Result/Result\<T\>. Minimal APIs → `Results.Ok`/`Results.BadRequest`/etc. MVC → `ObjectResult` with ProblemDetails. Custom mapper delegate for user-defined status mapping.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ResulTus.AspNetCore/` | New | ASP.NET Core integration project |
| `test/ResulTus.AspNetCore.Tests/` | New | Tests for MVC + Minimal APIs |
| `ResulTus.sln` | Modified | Add new projects |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| FrameworkReference required | Low | Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` |
| MVC vs Minimal API divergence | Med | Keep API surface consistent; separate internal paths |

## Rollback Plan

Delete `src/ResulTus.AspNetCore/` and tests. Remove from solution.

## Dependencies

- Phase 1 core (`ResulTus`)
- Framework: `Microsoft.AspNetCore.App` (ref-only)

## Success Criteria

- [ ] `ToHttpResult()` returns correct IResult for each error type
- [ ] `ToActionResult()` returns correct ObjectResult with ProblemDetails
- [ ] Custom mapper delegate overrides default mapping
- [ ] Code coverage >= 95% (measured by coverlet or similar)
