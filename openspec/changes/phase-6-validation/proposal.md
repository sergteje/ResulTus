# Proposal: Phase 6 — Validation

## Intent

Add validation support: adapters for FluentValidation and DataAnnotations that convert validation errors into structured Result errors.

## Scope

### In Scope
- `Validate(this T instance, IValidator<T> validator)` → `Result<T>`
- `ValidateAsync` with CancellationToken
- DataAnnotations validator adapter
- FluentValidation adapter
- Project: `src/ResulTus.Validation` targeting `net8.0`
- Tests: `test/ResulTus.Validation.Tests` targeting `net10.0`

### Out of Scope
- Source generators (Phase 10)
- Other validation libraries

## Capabilities

### New Capabilities
- `result-validation`: FluentValidation + DataAnnotations adapters

### Modified Capabilities
None — new project.

## Approach

Extension methods invoking the validator and converting errors to Result with `Error.Validation()` and details in Metadata. FluentValidation is optional — user must install separately.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ResulTus.Validation/` | New | Validation library project |
| `test/ResulTus.Validation.Tests/` | New | Tests for both adapters |
| `ResulTus.sln` | Modified | Add new projects |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| FluentValidation as optional dependency | Low | Separate target; compile-time conditional |
| DataAnnotations requires reflection | Low | Acceptable for validation; cache reflection results |

## Rollback Plan

Delete `src/ResulTus.Validation/` and tests. Remove from solution.

## Dependencies

- Phase 1 core (`ResulTus`)
- NuGet (optional): `FluentValidation`
- Framework: `System.ComponentModel.DataAnnotations` (in-box)

## Success Criteria

- [ ] FluentValidation `Validate` returns Result with mapped errors
- [ ] DataAnnotations `Validate` returns Result with mapped errors
- [ ] Async overloads accept and forward CancellationToken
- [ ] Code coverage >= 95% (measured by coverlet or similar)
