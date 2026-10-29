# Proposal: Phase 7 — Serialization

## Intent

Add System.Text.Json serialization support for Result, Result\<T\>, Error, and ErrorType with custom JsonConverters.

## Scope

### In Scope
- JsonConverter for Error
- JsonConverter for Result (void)
- JsonConverter for Result\<T\>
- JsonConverter for ErrorType
- Configurable serialization format options
- Project: `src/ResulTus.Serialization` targeting `net8.0`
- Tests: `test/ResulTus.Serialization.Tests` targeting `net10.0`

### Out of Scope
- Newtonsoft.Json support
- MessagePack, Protobuf

## Capabilities

### New Capabilities
- `result-serialization`: System.Text.Json converters for Result types

### Modified Capabilities
None — new project.

## Approach

Implement `JsonConverter<T>` for each type. Support both success/failure shapes in deserialization. Add `JsonSerializerOptions` extension methods for easy converter registration.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ResulTus.Serialization/` | New | Serialization library project |
| `test/ResulTus.Serialization.Tests/` | New | Round-trip tests for all converters |
| `ResulTus.sln` | Modified | Add new projects |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Success/failure shape differs | Med | Use discriminated JSON with a type discriminator |
| Round-trip fidelity | Med | Extensive round-trip tests in test suite |

## Rollback Plan

Delete `src/ResulTus.Serialization/` and tests. Remove from solution.

## Dependencies

- Phase 1 core (`ResulTus`)
- Framework: `System.Text.Json` (in-box)

## Success Criteria

- [ ] All 4 converters serialize/deserialize with full fidelity
- [ ] Discriminated JSON handles success vs failure correctly
- [ ] `JsonSerializerOptions` extension registers all converters at once
- [ ] Code coverage >= 95% (measured by coverlet or similar)
