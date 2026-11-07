# P2 — API gaps authorized after P1

Objective: implement the API gaps the owner named after P1 closed, keeping every existing contract (32 B `Result<T>`, 0 B failure path, AOT, overload resolution) intact. Branch: `feature-1`.

Test runner: `dotnet test ResulTus.slnx`; targeted projects run alone first. Delivery strategy: `auto-chain`. Push, PR creation and merge remain the owner's decisions.

Risk: **T1 carries item 3** (contracts others consume) because it adds a public `Combine` overload that can change overload resolution next to the existing `IEnumerable<Result>` unit overload. Everything else is unclassified until its spec exists; the multi-error item, if it reaches the core `Error` layout, would carry item 1 and item 3 and must be assessed before implementation.

## Specs

- **S0 — authorization.** The owner authorized starting P2 as soon as P1 closed. Verbatim: "vale cuando acabes p1 comienzas p2". This document was created at that point.

- **S1 — binary `Combine` for value-bearing results.** Verbatim from `odd/tasks/api-completion.md` S4: "Add a binary `Combine` for `Result<TFirst>` and `Result<TSecond>` that returns `Result<(TFirst First, TSecond Second)>`, evaluates the first failure before the second, and preserves the first failure in argument order. It complements, and does not change, the existing `IEnumerable<Result>` unit overload." Acceptance: both success returns the tuple in argument order; a failure in either argument returns that error; when both fail, the first argument's error wins; each failure short-circuits the other argument's evaluation path that S1 defines. Full XML documentation on the new member. This spec supersedes `api-completion.md` T3/S4 — it is implemented exactly once, here.

- **S2 — `FailIf`.** **Assumption pending owner definition.** No semantics are recorded anywhere in the repository beyond the name. Open questions that must be answered before this spec can be written: the receiver (`Result`, `Result<T>`, `Result<TSuccess,TError>`, or all three); whether the condition is a predicate over the success value or a bare flag; whether the error is passed eagerly (`Error`) or lazily (`Func<Error>`), where the lazy form would match the accepted `Ensure` direction; and whether the method returns the receiver or a new result. Do not implement from the name alone.

- **S3 — query syntax.** **Assumption pending owner definition.** No semantics recorded beyond the name. Open questions: whether this means C# LINQ query-expression support (`Select`/`SelectMany`/`Where`/`Join`, with `Result<T>` as a monad) or a fluent query builder; which tier it covers (`Result<T>` only, or also `Result<TSuccess,TError>`); and whether it introduces a new package. This is the largest item and may be rejected in favour of plain fluent pipelines.

- **S4 — multi-error inside `Error`.** **Assumption pending owner definition, and the highest-risk item.** No semantics recorded beyond the name. Open questions: whether an error aggregates several failures or only one; the rendering and equality rules for an aggregate; whether the aggregate lives in `Error` itself (which currently holds two references and is the reason `Result<T>` is 32 B, see `docs/decisions/001-result-struct-size.md`) or in a separate type that does not change the struct layout. Any change to the `Error` layout is a break of the size decision and the 0 B failure-path claim, and must be measured and re-decided, not assumed.

- **S5 — `StackResult`.** **Assumption pending owner definition.** No semantics recorded beyond the name. Open questions: whether it is a stack-only result (a `ref struct` with `ISpanFormattable`-style zero-allocation behavior), a result that carries a captured stack trace, or a result type usable as a stack frame for nested error propagation. Each of those is a different feature with different constraints; the name alone does not select one.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S0 | inline | pending | — |
| T2 | S2 | — | blocked — spec undefined | — |
| T3 | S3 | — | blocked — spec undefined | — |
| T4 | S4 | — | blocked — spec undefined and risk assessment required | — |
| T5 | S5 | — | blocked — spec undefined | — |

## Log

### L1 — the owner named the P2 items (verbatim)

> Y los temas de P2 -FailIf, query syntax, multi-error dentro de Error, StackResult- siguen siendo una decisión de producto que no tomaste. No los meto en la bolsa.

### L2 — start authorization (verbatim)

> vale cuando acabes p1 comienzas p2

### L3 — state at start

P1 is closed: `docs/performance.md` records the allocation work and its two tracked differences, and the suite is 476/476 with a 0-warning build. `api-completion.md` already delegated its `Combine` task (T3/S4) to P2 so the member exists exactly once; that spec is the only fully defined item in this document. The other four names are scope, not specification: each needs its semantics decided by the owner before a spec, a task, or a line of code exists.
