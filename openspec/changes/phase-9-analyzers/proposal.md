# Proposal: Phase 9 — Analyzers

## Intent

Create Roslyn diagnostic analyzers that detect Result misuse at compile time: ignored results, unguarded Value access, incomplete Match, redundant Bind, and unnecessary conversions.

## Scope

### In Scope
- RESULT001: Ignored Result (result not used)
- RESULT002: Unguarded Value access (no IsSuccess check)
- RESULT003: Incomplete Match (not handling both branches)
- RESULT004: Redundant Bind (when Map would suffice)
- RESULT005: Unnecessary conversion
- RESULT006: Unnecessary Ensure
- Project: `src/ResulTus.Analyzers` targeting `netstandard2.0`
- Tests: `test/ResulTus.Analyzers.Tests` targeting `net10.0`

### Out of Scope
- Code fixes (can be added later)
- Source Generators (Phase 10)

## Capabilities

### New Capabilities
- `result-analyzers`: Roslyn diagnostic analyzers for Result misuse

### Modified Capabilities
None — new project.

## Approach

Implement `DiagnosticAnalyzer` subclasses using `SymbolAction`, `SyntaxNodeAction`, `CodeBlockAction`. Analyzer with `language = "csharp"`. Package includes `Rules.props` for severity configuration.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ResulTus.Analyzers/` | New | Analyzer project (netstandard2.0) |
| `src/ResulTus.Analyzers/Rules/` | New | Diagnostic descriptor definitions |
| `src/ResulTus.Analyzers/rules.props` | New | Rule severity configuration |
| `test/ResulTus.Analyzers.Tests/` | New | Analyzer tests with CodeAnalysis.Testing |
| `ResulTus.sln` | Modified | Add new projects |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Must target netstandard2.0 for VS compat | Low | Single TFM; no modern API issues |
| Testing requires CodeAnalysis.Testing infra | Med | Add test infra package; use verifier pattern |

## Rollback Plan

Delete `src/ResulTus.Analyzers/` and tests. Remove from solution.

## Dependencies

- Phase 1 core (`ResulTus`) — references for symbol matching
- NuGet: `Microsoft.CodeAnalysis.CSharp` (analyzer), `Microsoft.CodeAnalysis.Testing` (test)

## Success Criteria

- [ ] Each of 6 analyzers fires on correct code patterns
- [ ] No false positives on valid Result usage
- [ ] Analyzer package loads in IDE without errors
- [ ] Code coverage >= 95% (measured by coverlet or similar)
