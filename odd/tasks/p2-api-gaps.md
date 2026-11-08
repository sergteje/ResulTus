# P2 — API gaps authorized after P1

Objective: implement the API gaps the owner named after P1 closed, keeping every existing contract (32 B `Result<T>`, 0 B failure path, AOT, overload resolution) intact. Branch: `feature-1`.

Test runner: `dotnet test ResulTus.slnx`; targeted projects run alone first. Delivery strategy: `auto-chain`. Push, PR creation and merge remain the owner's decisions.

Risk: **T1 carried item 3** (contracts others consume) because it added a public `Combine` overload next to the existing `IEnumerable<Result>` unit overload; it was independently verified with no blockers. The four rejected items carry no risk because no code was written for them; the multi-error item would have carried items 1 and 3 had the core `Error` layout changed, which is part of why it was rejected.

## Specs

- **S0 — authorization.** The owner authorized starting P2 as soon as P1 closed. Verbatim: "vale cuando acabes p1 comienzas p2". This document was created at that point.

- **S1 — binary `Combine` for value-bearing results.** Verbatim from `odd/tasks/api-completion.md` S4: "Add a binary `Combine` for `Result<TFirst>` and `Result<TSecond>` that returns `Result<(TFirst First, TSecond Second)>`, evaluates the first failure before the second, and preserves the first failure in argument order. It complements, and does not change, the existing `IEnumerable<Result>` unit overload." Acceptance: both success returns the tuple in argument order; a failure in either argument returns that error; when both fail, the first argument's error wins; each failure short-circuits the other argument's evaluation path that S1 defines. Full XML documentation on the new member. This spec supersedes `api-completion.md` T3/S4 — it is implemented exactly once, here.

- **S2 — `FailIf`. REJECTED by the owner 2026-11-07 (L6).** `Ensure(predicate, error)` already fails when the predicate does not hold, so a `FailIf` that fails when the predicate holds is `Ensure(value => !predicate(value), error)` and nothing more. Adding it would publish a second way to write the same guard, force a choice at every validation site, and add no capability. No member is added; the name stays out of the API.

- **S3 — query syntax. REJECTED by the owner 2026-11-07 (L7).** Interpreting the name as C# query-expression support over `Result<T>` (`Select`/`SelectMany`/`Where`), the owner chose not to add it. `Bind` and `Map` already compose the same pipelines and make the short-circuit point explicit, while query syntax would read like LINQ-to-objects over a container that holds at most one value, and `Where` cannot exist without an `Error` to produce when the filter rejects. The repository has no LINQ surface today and keeps none.

- **S4 — multi-error inside `Error`. REJECTED by the owner 2026-11-07 (L8).** Aggregation already exists outside `Error`: `ValidationErrors` holds N `ValidationIssue` values and `ToError()` collapses them into one `Error`, and the typed tier already carries any error type, so `Result<TSuccess, ValidationErrors>` expresses multiple failures without losing any. Putting a collection inside `Error` would require a third reference field, growing `Error` from 24 B to 32 B and `Result<int>` from 32 B to 40 B, which reverses decision 001 — the decision that removed the `Metadata` dictionary for exactly this reason — and would reintroduce an allocation on the failure path that P1 measured at 0 B. No member is added and `Error` keeps its layout.

- **S5 — `StackResult`. REJECTED by the owner 2026-11-07 (L9).** Each plausible reading is either already true or already closed: as a stack-only `ref struct` it would add restrictions (no class field, no `async`, no boxing, no lambda capture) for a 0 B guarantee the existing struct already meets by measurement; as a captured stack trace it needs a field in `Error`, which S4 just rejected, and S3 already defines `Error.FromException` without a trace; as a stack of nested errors it is S4 again. No type is added.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S0 | inline | done | this commit |
| T2 | S2 | — | closed — rejected by the owner | — |
| T3 | S3 | — | closed — rejected by the owner | — |
| T4 | S4 | — | closed — rejected by the owner | — |
| T5 | S5 | — | closed — rejected by the owner | — |

## Log

### L1 — the owner named the P2 items (verbatim)

> Y los temas de P2 -FailIf, query syntax, multi-error dentro de Error, StackResult- siguen siendo una decisión de producto que no tomaste. No los meto en la bolsa.

### L2 — start authorization (verbatim)

> vale cuando acabes p1 comienzas p2

### L3 — state at start

P1 is closed: `docs/performance.md` records the allocation work and its two tracked differences, and the suite is 476/476 with a 0-warning build. `api-completion.md` already delegated its `Combine` task (T3/S4) to P2 so the member exists exactly once; that spec is the only fully defined item in this document. The other four names are scope, not specification: each needs its semantics decided by the owner before a spec, a task, or a line of code exists.

### L4 — T1 binary `Combine` completed

`ResultExtensions.Combine<TFirst, TSecond>(this Result<TFirst> first, Result<TSecond> second)` returns `Result<(TFirst First, TSecond Second)>`. It checks the first result before touching the second, so a first failure short-circuits, and a second failure propagates only when the first succeeded. The tuple preserves argument order and the failure keeps the first error in argument order.

**RED → GREEN:** the tests were written first and the project failed to compile with four `CS1061: "Result<int>" no contiene una definición para "Combine"` errors, which is the RED evidence for a new API. After the implementation, `tests\ResulTus.Extensions.Tests` is 84/84 (80 pre-existing + 4 new). Full solution build is 0 warnings and 0 errors, and the suite is 480/480.

**Risk:** item 3 (contracts others consume). The member is additive and nothing else in the library is named `Combine` for `Result<T>`, but a new extension can still change overload resolution at a call site that today binds elsewhere, so this unit goes to independent verification.

### L5 — independent verification of T1, and the promoted probes

A separate verifier reviewed `git diff fab4e08..9e1c66c` in a scratch tree outside the workspace, built and tested both trees there, and reproduced the RED by adding the new test file to the baseline: four `CS1061` errors, exactly the evidence L4 records. Verdicts: **S1 pass, build requirement pass, no blockers**. Beyond the spec it confirmed the zero-allocation floor on the success path and on both failure paths, that the named tuple elements survive as metadata (`First`/`Second` usable at the call site), that every existing `Combine` call site still binds to the same member on `IEnumerable<Result>`, `Result[]` and `IReadOnlyList<Result>`, and that the default `Result<T>` (a failure carrying `Error.None`) combines without throwing.

Three non-blocking advisories came out of it:

1. **The charter had gone stale.** `docs/performance.md` still said "no `Result<T>` overload exists" next to the P0 `CollectionOperationBenchmark` table. That was true when the table was measured and false as soon as T1 landed. The row is the element-wise unit operation, so the readout now says so and points at the allocation tests that pin the binary overload instead.
2. **System.Text.Json cannot see `ValueTuple` fields.** A `Result<(int, string)>` serializes its success payload as `{}`, because the default contract ignores fields and a tuple has no properties. This is not caused by T1 — any `Result<T>` whose `T` is a tuple already behaved that way — but `Combine` makes that shape easy to produce. Left as an advisory, recorded here, and it belongs in the P3 README's serialization caveats rather than in a behavior change to the serializer.
3. **`Combine` now exists twice under the same name in two packages.** The binary overload lives in `ResulTus.Extensions` and the element-wise unit overloads in `ResulTus.Collections`. Arity keeps ordinary call sites unambiguous, but a caller that imports both namespaces sees two `Combine` families. Unavoidable given the spec, and the overload-resolution guards now pin it.

Nine of the verifier's contract probes and four overload-resolution guards were promoted to permanent tests: `tests/ResulTus.Extensions.Tests/CombineContractTests.cs` (tuple shape and named elements, argument order with distinct types, both-fail precedence, second-failure-only-when-first-succeeded, default results, null success value, and the three allocation paths) and `tests/ResulTus.Collections.Tests/CombineOverloadResolutionTests.cs` (sequence, array, read-only-list and binary binding). The Collections test project gained a reference to `ResulTus.Extensions` so both families are in scope for the binding guards. After promotion the suite is **493/493** (Extensions 93, Collections 64) with a 0-warning, 0-error build.

### L6 — `FailIf` rejected by the owner

The owner was asked how to define `FailIf`, given that `Ensure(predicate, error)` already fails when the predicate does not hold, so a `FailIf` failing when it does hold is `Ensure(value => !predicate(value), error)` and nothing more. The answer, verbatim:

> No agregarlo (Recomendado)

So S2 is closed without a member. This is not a deferral: no `FailIf` exists in the API, and `Ensure` is the only guard operator. Anything that reads as "fail if" is written as the negated predicate, which keeps one spelling per behavior.

Three P2 items remain undefined — `query syntax`, multi-error inside `Error`, and `StackResult`. Each still needs an owner decision before a spec exists.

### L7 — query syntax rejected by the owner

Asked how to interpret the name, with the two-plausible-readings flagged (`from`/`select` query expressions versus a fluent query builder), and with the observation that the repository has no `Select`, `SelectMany` or `Where` anywhere, the owner answered verbatim:

> No agregarlo (Recomendado)

So S3 is closed without a member and the library keeps zero LINQ surface. `Bind` and `Map` remain the only composition operators, and they say explicitly where the failure short-circuits instead of hiding it behind query-form translation.

Two P2 items remain undefined — multi-error inside `Error` and `StackResult`.

### L8 — multi-error inside `Error` rejected by the owner

The owner was shown the measured constraint rather than a preference: `Error` is a `readonly record struct` holding `string _code`, `string? _message` and `ErrorType _type`, for 24 B, and `Result<int>` is 32 B because decision 001 removed the untyped `Metadata` dictionary for exactly this reason. Holding several failures inside `Error` needs a third reference field, which takes `Error` to 32 B and `Result<T>` to 40 B and puts a real allocation back on the failure path that P1 closed at 0 B. The owner answered verbatim:

> No agregarlo (Recomendado)

S4 is closed. Aggregation stays where it already works: `ValidationErrors` for validation issues, and `Result<TSuccess, TError>` with any error type the caller chooses, `ValidationErrors` included, when several failures must survive together. `Error` keeps its layout, decision 001 stands unchanged, and P1's measurements remain valid.

One P2 item remains undefined — `StackResult`.

### L9 — `StackResult` rejected by the owner, and P2 closed

The owner was shown the three readings the name admits and what each one costs. Answered verbatim:

> No agregarlo (Recomendado)

S5 is closed with no type. P2 is now complete: one implementable item, the binary `Combine`, shipped and verified; four names that were scope rather than specification (`FailIf`, query syntax, multi-error inside `Error`, `StackResult`), each rejected by the owner with a recorded reason and none of them added to the API. No P2 item remains open, so the gate S8 of `api-completion.md` placed in front of that feature's remaining tasks is released.


