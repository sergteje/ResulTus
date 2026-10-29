# Proposal: Phase 4 — Logging

## Intent

Integrate Result with `Microsoft.Extensions.Logging.ILogger`: structured logging for success/failure outcomes with configurable log levels.

## Scope

### In Scope
- LogSuccess, LogFailure, LogAll extension methods
- LogError, LogWarning, LogInformation for specific error types
- Async overloads for `Task<Result<T>>`
- Project: `src/ResulTus.Logging` targeting `net8.0`
- Tests: `test/ResulTus.Logging.Tests` targeting `net10.0`
- Dependency: `Microsoft.Extensions.Logging.Abstractions`

### Out of Scope
- Serilog/NLog adapters (extensible via ILogger)
- ASP.NET Core middleware (Phase 5)

## Capabilities

### New Capabilities
- `result-logging`: ILogger integration for Result

### Modified Capabilities
None — new project.

## Approach

Use `ILogger.Log` with structured logging templates. Default level mapping: Success → LogLevel.Debug, Failure → LogLevel.Error. Configurable via optional delegate parameter.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ResulTus.Logging/` | New | Logging library project |
| `test/ResulTus.Logging.Tests/` | New | Tests verifying log output |
| `ResulTus.sln` | Modified | Add new projects |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| None significant | Low | Standard ILogger pattern |

## Rollback Plan

Delete `src/ResulTus.Logging/` and tests. Remove from solution.

## Dependencies

- Phase 1 core (`ResulTus`)
- NuGet: `Microsoft.Extensions.Logging.Abstractions`

## Success Criteria

- [ ] All log methods emit correct structured log entries
- [ ] Log level configurable per call site
- [ ] Async overloads log correctly for `Task<Result<T>>`
- [ ] Code coverage >= 95% (measured by coverlet or similar)
