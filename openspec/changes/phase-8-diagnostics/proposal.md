# Proposal: Phase 8 — Diagnostics

## Intent

Add diagnostic support: Stopwatch-based timing, DiagnosticSource integration, Activity tracing for Result operations — enabling OpenTelemetry compatibility.

## Scope

### In Scope
- Stopwatch-based timing for Result operations
- DiagnosticSource integration
- Activity tracing via `System.Diagnostics.Activity`
- Opt-in wrappers for extensions, collections, validation
- Project: `src/ResulTus.Diagnostics` targeting `net8.0`
- Tests: `test/ResulTus.Diagnostics.Tests` targeting `net10.0`

### Out of Scope
- OpenTelemetry exporters (compatible via DiagnosticSource/Activity)
- Automatic instrumentation (manual opt-in)

## Capabilities

### New Capabilities
- `result-diagnostics`: Performance tracking, tracing, Activity support

### Modified Capabilities
None — new project.

## Approach

Extension methods wrapping Result operations with diagnostic instrumentation. Use `DiagnosticSource` and `Activity` for OpenTelemetry compatibility. All instrumentation opt-in by explicit call — no ambient overhead.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ResulTus.Diagnostics/` | New | Diagnostics library project |
| `test/ResulTus.Diagnostics.Tests/` | New | Tests verifying Activity emission |
| `ResulTus.sln` | Modified | Add new projects |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Performance overhead from instrumentation | Med | Opt-in only; measure overhead in benchmarks |
| Activity disposal required | Low | Use `using` pattern; verify in tests |

## Rollback Plan

Delete `src/ResulTus.Diagnostics/` and tests. Remove from solution.

## Dependencies

- Phase 1 core (`ResulTus`)
- Phase 2 extensions (`ResulTus.Extensions`)
- Framework: `System.Diagnostics.DiagnosticSource` (in-box)

## Success Criteria

- [ ] DiagnosticSource emits start/stop events for wrapped operations
- [ ] Activity tracing creates proper parent-child relationships
- [ ] Zero allocation when diagnostics not in use (opt-in only)
- [ ] Code coverage >= 95% (measured by coverlet or similar)
