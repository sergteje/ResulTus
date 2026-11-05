# Phase 9 — Analyzers

Objective: ship `ResulTus.Analyzers`, six Roslyn diagnostics that catch `Result` misuse at compile time. Branch: `feature-1`.

Test runner: `dotnet test tests/ResulTus.Analyzers.Tests/ResulTus.Analyzers.Tests.csproj`.

Delivery strategy: `auto-chain` forecast — ~1100 authored changed lines, so work-unit commits on `feature-1`; PR/merge stays the user's call.

## What changed from the proposal, and why

Three of the six rules in `openspec/changes/phase-9-analyzers/proposal.md` do not describe this library. Writing them as specified would produce analyzers that never fire, or that fire on the wrong thing. The user authorised reinterpreting them against the real API rather than inventing detectors or changing the core.

| ID | Proposal says | What the code actually allows |
| --- | --- | --- |
| `RESULT002` | "Unguarded `Value` access (no `IsSuccess` check)" | **`Result<T>` has no `.Value`.** No read throws. Reinterpreted as unguarded `GetValueOrDefault()`, which returns `default` in silence. |
| `RESULT003` | "Incomplete `Match` (not handling both branches)" | `Match` takes two required delegates, so the compiler already forbids an incomplete match. Reinterpreted as a deconstruction that discards the error. |
| `RESULT005` | "Unnecessary conversion" | Too vague as written. Reinterpreted as `ToResult()` on an expression that is already a `Result`, which silently builds `Result<Result<T>>`. |

`RESULT001`, `RESULT004` and `RESULT006` map cleanly and are implemented as proposed.

## The six rules

| ID | Title | Fires on | Default |
| --- | --- | --- | --- |
| `RESULT001` | Result is ignored | an expression statement whose expression is a `Result`, `Result<T>` or `Result<TSuccess,TError>` | `Warning` |
| `RESULT002` | Outcome is not checked | `GetValueOrDefault()` with no argument, when the enclosing member never consults `IsSuccess`, `IsFailure`, `TryGetValue`, `Match`, `GetValueOr` or `Deconstruct` on the receiver | `Warning` |
| `RESULT003` | Deconstruction discards the error | a deconstruction of a result type whose last element is `_` | `Warning` |
| `RESULT004` | `Bind` is a `Map` | `Bind(x => Result<T>.Success(...))` | `Info` |
| `RESULT005` | Result nested in a Result | `ToResult()` on an expression already of a result type | `Warning` |
| `RESULT006` | `Ensure` predicate is constant | `Ensure(x => true, error)` or `Ensure(x => false, error)` | `Warning` |

`RESULT003` uses one uniform rule — *the last element of the deconstruction is discarded* — because every result type puts the error last: `Result.Deconstruct(out bool, out Error)`, `Result<T>.Deconstruct(out bool, out T?, out Error)`, `Result<TSuccess,TError>.Deconstruct(out bool, out TSuccess?, out TError?)`.

`RESULT002` is deliberately coarse: it scans the enclosing member for a guard on the *same identifier*, not a full dataflow. A guard in a different method, or a result known to be successful from construction, is not modelled. That is a documented limitation, not an oversight — a control-flow analysis would be a much larger machine for the same finding.

## Severity configuration

The proposal asks for `Rules.props`. An MSBuild props file cannot set analyzer severity: Roslyn reads severity from `.editorconfig` / `.globalconfig`, keyed `dotnet_diagnostic.RESULT00N.severity`. So the project ships `ResulTus.Analyzers.globalconfig` with the six defaults, and the doc says how to override them. This is the mechanism that actually works; a props file would have been inert.

## Specs

- **S1 — the projects exist and are wired.** `src/ResulTus.Analyzers` targets `netstandard2.0` and overrides `IsTrimmable=false`, `IsAotCompatible=false` from `Directory.Build.props`; it references `Microsoft.CodeAnalysis.CSharp` 4.14.0 with `PrivateAssets=all` so the compiler API does not flow to consumers. `tests/ResulTus.Analyzers.Tests` targets `net10.0`. Both in `ResulTus.slnx`. 0 warnings under `TreatWarningsAsErrors`.
- **S2 — `RESULT001`** fires on a bare expression statement producing any of the three result types, and does not fire on an assignment, a `return`, an argument, or `_ = ...` (the explicit-discard idiom).
- **S3 — `RESULT002`** fires on `GetValueOrDefault()` with no argument when no guard on the receiver appears in the member, and does not fire when `IsSuccess`, `TryGetValue`, `Match`, `GetValueOr` or `Deconstruct` is consulted, nor when the call passes a fallback argument.
- **S4 — `RESULT003`** fires when the last deconstruction element is `_`, for all three result types, in both the `var (...)` declaration form and the `(...) = ...` assignment form. It does not fire when the error is captured.
- **S5 — `RESULT004`** fires on `Bind` whose lambda body is `Result<T>.Success(...)` and does not fire on a `Bind` whose lambda returns anything else, nor on a legitimate `Map`.
- **S6 — `RESULT005`** fires on `ToResult()` where the receiver is already a result type, and does not fire on `ToResult()` over a plain value.
- **S7 — `RESULT006`** fires when the `Ensure` predicate ignores its parameter and returns a constant, and does not fire on a predicate that uses the value.
- **S8 — no false positives.** Every rule has at least one "does not fire" test on valid code, and the harness compiles that code with zero compiler errors so a broken test cannot pass by failing to compile.
- **S9 — the descriptors are the contract.** Each descriptor declares an ID, title, message format, category and default severity; a test asserts the six IDs are exactly `RESULT001`–`RESULT006` with the severities in the table above, so a typo cannot ship silently.
- **S10 — no regression.** `dotnet build ResulTus.slnx` with 0 warnings, the whole suite green including the 358 existing tests, and coverage on `ResulTus.Analyzers` >= 95% lines and branches.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S8 | inline | done | `this commit` |
| T2 | S2, S5, S7 | inline | done | `this commit` |
| T3 | S3, S4, S6 | inline | done | `this commit` |
| T4 | S9, S10 | inline | done | `this commit` |

## Log

### L1 — original request (verbatim)

> 9 va

Phase 9 (Analyzers) from `openspec/changes/phase-9-analyzers/proposal.md`, after the user was told three of the six rules do not map to the real API.

### L2 — the decision (verbatim)

> reinterpretacion va

Authorises reinterpreting `RESULT002`, `RESULT003` and `RESULT005` against the real API instead of inventing detectors, dropping them, or adding a throwing `Value` to the core in a phase that does not own that API.

### L3 — starting evidence

- `Result<T>` exposes `IsSuccess`, `IsFailure`, `Success`, `Failure`, implicit operators from `T` and from `Error`, `Match`, `Deconstruct`, `TryGetValue` (two overloads), `GetValueOrDefault`, `GetValueOr`. No `Value`, no throwing read.
- `Result` and `Result<TSuccess,TError>` have the same shape minus the value accessors.
- `Deconstruct` arities: `Result` is `(bool, Error)`; `Result<T>` is `(bool, T?, Error)`; `Result<TSuccess,TError>` is `(bool, TSuccess?, TError?)`. The error is last in all three.
- `ResultConversions.ToResult<T>(this T value)` is `=> Result<T>.Success(value)`, so calling it on a `Result<int>` yields `Result<Result<int>>`.
- `ResultExtensions.Bind<T,TOut>(this Result<T>, Func<T,Result<TOut>>)` and `Map<T,TOut>(this Result<T>, Func<T,TOut>)` are both in `ResulTus.Extensions`.
- `ResultExtensions.Ensure<T>(this Result<T>, Func<T,bool> predicate, Error error)`.
- `Directory.Build.props` applies `TargetFramework=net8.0`, `IsTrimmable=true`, `IsAotCompatible=true` and `TreatWarningsAsErrors=true` to every project, so the analyzer project must override the first three.
- `Directory.Packages.props` is centrally managed; `Microsoft.CodeAnalysis.CSharp` is not yet listed. NuGet has 4.14.0 as the last 4.x and 5.9.0 as the newest 5.x; 4.14.0 is chosen so consumers are not forced onto Roslyn 5.

### L4 — test infrastructure decision

`Microsoft.CodeAnalysis.Testing` ships its verifiers for xunit v2, NUnit and MSTest; this repo is on xunit.v3 3.2.2. Rather than take a compatibility shim, the test project drives Roslyn directly: build a `CSharpCompilation` from source text with references taken from the running runtime plus `ResulTus.dll`, then read `CompilationWithAnalyzers.GetAnalyzerDiagnosticsAsync`. That is the layer the verifier packages wrap anyway, it has no xunit-version coupling, and it lets each test assert the compiler produced no errors before it asserts anything about the analyzer.

### L5 — RED/GREEN evidence

**T1 — projects and harness.**

- RED: `dotnet build tests/ResulTus.Analyzers.Tests` failed with `CS0246` on `Microsoft.CodeAnalysis.CSharp` and on every type the harness uses, because the package and the two projects did not exist.
- GREEN: build succeeds; the solution now has 9 test assemblies instead of 8.

**T2 — the three syntactic rules (`RESULT001`, `RESULT004`, `RESULT006`).**

- RED: `dotnet build` failed with `CS0103: DiagnosticDescriptors` in the three analyzers and `CS0246` on the three analyzer types in the three test files.
- GREEN: `9/9` tests. One test failure along the way was the test's fault, not the rule's: `IgnoredResultTests.HandledResults_ShouldNotReport` used `MapError` without `using ResulTus.Extensions`, and the harness refused to analyse code that does not compile — exactly what S8 asks of it.

**T3 — the three semantic rules (`RESULT002`, `RESULT003`, `RESULT005`).**

- RED: `CS0246` on `UnguardedOutcomeAnalyzer`, `DiscardedErrorAnalyzer` and `NestedResultAnalyzer` in the new test files.
- GREEN: `15/15`. Two real defects surfaced here and both were fixed, not papered over:
  - `DiscardedErrorAnalyzer` only fired on the assignment form. A throwaway probe test showed why: `var (a, b, _) = x;` and `(a, b, _) = x;` are both `IDeconstructionAssignmentOperation`, but the declaration form's `Target` is a `DeclarationExpressionOperation` wrapping the tuple while the assignment form's `Target` *is* the tuple. `FindTargetTuple` now looks through `ChildOperations` instead of assuming a shape.
  - `NestedResultAnalyzer` missed the static form. A second probe showed that for an extension method called as `ResultConversions.ToResult(result)` Roslyn sets `ReceiverType` to the *declaring class*, not `null`, so the fallback never ran. It now branches on `ReducedFrom is null` and reads `Parameters[0].Type` in the static case.

**T4 — descriptors, coverage and no regression.**

- RED: none to observe; this task is measurement, not behaviour.
- GREEN: `41/41` analyzer tests, `dotnet build ResulTus.slnx` with 0 warnings, whole solution green at 399 tests (358 pre-existing + 41 new).
- Coverage on `ResulTus.Analyzers`: **97.8% lines (269/275), 96.8% branches (120/124)** — above the S10 bar without lowering it.

### L6 — decisions taken while implementing

- **`EnforceExtendedAnalyzerRules=true` on the analyzer project.** The build refused to compile without it (`RS1036`). It is the switch that turns on the rules catching the mistakes that crash an analyzer inside a consumer's build — a missing concurrent-execution opt-in, an unreleased diagnostic id, an unsupported API — so it belongs on, not suppressed.
- **No `ArgumentNullException` guard in `Initialize`.** Six analyzers each carried `if (context is null) throw`. Roslyn is the only caller and never passes null, so the guard was dead code that only inflated the uncovered-branch count. Removed rather than tested.
- **`LambdaBody` reads block bodies.** A lambda written as `value => { return x; }` says exactly what `value => x` says. Ignoring blocks was a real false negative for `RESULT004` and `RESULT006`; it now unwraps a single-`return` block.
- **`RedundantBindAnalyzer` handles the static form.** `ResultExtensions.Bind(result, lambda)` was a second real false negative. The rule now takes the *last* argument as the lambda, which is the only argument in the reduced form and the second one in the static form.
- **`FindEnclosingMember` is one line.** `GlobalStatementSyntax` derives from `MemberDeclarationSyntax`, so top-level statements do get a scope. The six-case `switch` that preceded it was an enumeration of the same thing. The test that assumed otherwise was wrong and was corrected: a top-level `GetValueOrDefault()` *does* report, because its scope checks nothing.
- **Four branches stay uncovered, by construction.** They are guards against states the Roslyn API cannot produce, and each is documented in the code:
  - `DiscardedErrorAnalyzer` — `operation.Target is null`. A deconstruction assignment always has a target.
  - `RedundantBindAnalyzer` — the lambda body invocation does not resolve to a method. Unreachable in code that compiles: the lambda's type participates in the outer `Bind` overload resolution, so if the body does not resolve the outer call does not either, and the rule exits earlier.
  - `UnguardedOutcomeAnalyzer` — `member is null`. Every invocation sits inside a `MemberDeclarationSyntax`, top-level statements included.
  - `UnguardedOutcomeAnalyzer` — the static `GuardNames` initialiser, a compiler-generated type-initialiser guard that runs once per process.
- Three null-conditional chains that could never be null were rewritten to say so (`ContainingNamespace!`, `ContainingType!`, `Parameters[0]`), because a named type always has a namespace and a method symbol always has a containing type. The `NestedResultAnalyzer` ternary's `Parameters.Length > 0 ? … : null` arm was removed for the same reason: `ToResult` always declares its receiver parameter.

### L7 — closing state

- `src/ResulTus.Analyzers`: 9 source files plus the project and the `.globalconfig`, `netstandard2.0`, 0 warnings.
- `tests/ResulTus.Analyzers.Tests`: 12 source files plus the project, 41 tests, `net10.0`.
- Severities ship in `ResulTus.Analyzers.globalconfig`, packed to `build/`.
- Next phase: 10 (source generator). Delivery — commit chain, PR, merge — stays the user's call.
