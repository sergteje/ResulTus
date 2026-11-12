# Functional operations

The fluent operators live in the `ResulTus.Extensions` package, namespace `ResulTus.Extensions`. They come in three
tiers that mirror the three result types, and each tier has a synchronous class and an asynchronous class:

| Tier | Type | Synchronous extensions | Asynchronous extensions |
| --- | --- | --- | --- |
| Unit | `Result` | `UnitResultExtensions` | `UnitResultAsyncExtensions` |
| Value | `Result<T>` | `ResultExtensions` | `ResultAsyncExtensions` |
| Typed | `Result<TSuccess, TError>` | `TypedResultExtensions` | `TypedResultAsyncExtensions` |

Every operator shares one contract: **a failure short-circuits the pipeline untouched**, so a delegate that does
not apply to the outcome is never invoked, and **nothing allocates on the heap on either path** — the only
allocation is the delegate the caller passes. `Match` is the exception to the "extension method" rule: it is an
instance method on all three result types, not an extension.

The examples below share one setup; the usings and helpers in it are implied by every statement-level block that
follows.

```csharp
using ResulTus;
using ResulTus.Extensions;

static Result<int> Parse(string text) =>
    int.TryParse(text, out var n)
        ? Result<int>.Success(n)
        : Result<int>.Failure(Error.Validation("NOT_A_NUMBER"));

static Result<int, DomainError> Find(string key) =>
    key == "ok"
        ? Result<int, DomainError>.Success(1)
        : Result<int, DomainError>.Failure(DomainError.NotFound);

static Result SaveRecord(int id) =>
    id <= 0 ? Result.Failure(Error.Validation("BAD_ID")) : Result.Success();

public enum DomainError { NotFound, Invalid }
```

## The value tier — `Result<T>`

The complete synchronous surface of `ResultExtensions`, with the exact signature and return type:

| Operator | Signature | Returns |
| --- | --- | --- |
| `Map` | `Map<T, TOut>(this Result<T>, Func<T, TOut>)` | `Result<TOut>` |
| `Bind` | `Bind<T, TOut>(this Result<T>, Func<T, Result<TOut>>)` | `Result<TOut>` |
| `Bind` (unit) | `Bind<T>(this Result<T>, Func<T, Result>)` | `Result` |
| `Combine` | `Combine<TFirst, TSecond>(this Result<TFirst>, Result<TSecond>)` | `Result<(TFirst First, TSecond Second)>` |
| `MapError` | `MapError<T>(this Result<T>, Func<Error, Error>)` | `Result<T>` |
| `Tap` | `Tap<T>(this Result<T>, Action<T>)` | `Result<T>` |
| `OnSuccess` | `OnSuccess<T>(this Result<T>, Action<T>)` — alias of `Tap` | `Result<T>` |
| `OnFailure` | `OnFailure<T>(this Result<T>, Action<Error>)` | `Result<T>` |
| `Switch` | `Switch<T>(this Result<T>, Action<T>, Action<Error>)` | `void` |
| `Ensure` | `Ensure<T>(this Result<T>, Func<T, bool>, Error)` | `Result<T>` |
| `Ensure` | `Ensure<T>(this Result<T>, Func<T, bool>, Func<Error>)` | `Result<T>` |
| `Ensure` | `Ensure<T>(this Result<T>, Func<T, bool>, Func<T, Error>)` | `Result<T>` |
| `Recover` | `Recover<T>(this Result<T>, Func<Error, T>)` | `Result<T>` |
| `Recover` | `Recover<T>(this Result<T>, Func<Error, Result<T>>)` | `Result<T>` |
| `Finally` | `Finally<T>(this Result<T>, Action<T, Error>)` | `Result<T>` |

`Map` projects the success value; `Bind` chains another result-producing step; `MapError` transforms only the
error; `Tap`/`OnSuccess`/`OnFailure` run a side effect and return the receiver unchanged; `Switch` runs exactly one
of the two branches; `Ensure` keeps the success only while a predicate holds; `Recover` replaces a failure with a
value or a result; `Finally` runs one action on either path. `Combine` is the binary value combiner: it returns both
success values as a named tuple, or the first failure.

```csharp
Result<string> label = Parse("42")
    .Map(v => v * 2)
    .Map(v => $"doubled to {v}");            // success: "doubled to 84"

Result<int> bound = Parse("21")
    .Bind(v => v < 0
        ? Result<int>.Failure(Error.Validation("NEGATIVE"))
        : Result<int>.Success(v + 1));       // success: 22

Result<int> adult = Parse("18")
    .Ensure(v => v >= 18, Error.Forbidden("UNDERAGE"));          // success: 18
Result<int> guarded = Parse("7")
    .Ensure(v => v >= 18, () => Error.Forbidden("UNDERAGE"));    // failure: "UNDERAGE"

Result<int> recovered = Parse("not-a-number")
    .Recover(error => -1);                   // success: -1
Result<int> fallback = Parse("not-a-number")
    .Recover(error => Parse("0"));           // success: 0

Result<int> mapped = Parse("x")
    .MapError(error => Error.Validation("INVALID", error.Message));  // failure: "INVALID"

Result<int> tapped = Parse("42")
    .Tap(v => Console.WriteLine($"tap saw: {v}"))     // "tap saw: 42"
    .OnFailure(e => Console.WriteLine(e.Code));       // not invoked (success)

Parse("42").Switch(
    onSuccess: v => Console.WriteLine($"switch success: {v}"),   // "switch success: 42"
    onFailure: e => Console.WriteLine($"switch failure: {e.Code}"));

Result<int> finalized = Parse("x")
    .Finally((value, error) => Console.WriteLine($"finally: value={value} code={error.Code}"));
// on failure the first argument is default(T): "finally: value=0 code=NOT_A_NUMBER"

string verdict = Parse("42").Match(
    onSuccess: v => $"ok: {v}",
    onFailure: e => $"failed: {e.Code}");    // "ok: 42"

Result<(int First, int Second)> combined = Parse("1").Combine(Parse("2"));
// success: (First: 1, Second: 2)
```

`Ensure` and `Recover` each have multiple overloads, and overload resolution is by delegate shape. The three
`Ensure` forms take an eager `Error`, a zero-parameter `Func<Error>` factory, or a `Func<T, Error>` factory that
receives the rejected value — the value-aware form is the only one that can name the value in the message. The two
`Recover` forms take either a `Func<Error, T>` (produce a value) or a `Func<Error, Result<T>>` (produce another
result); a lambda returning `T` binds the value overload because the identity conversion beats the user-defined
conversion into `Result<T>`, and a lambda returning the result struct binds the other. A bare `default` literal is
the one ambiguous case in `Ensure`, because it converts to both `Error` and `Func<Error>` — write `default(Error)`
when you mean the eager overload.

## The unit tier — `Result`

`UnitResultExtensions` mirrors the value tier for the value-less `Result`. A unit success carries nothing, so the
delegates take no value: `Map` produces a `Result<T>`, `Bind` chains either a `Result<T>` or another `Result`, and
the side-effect delegates take no argument.

| Operator | Signature | Returns |
| --- | --- | --- |
| `Map` | `Map<T>(this Result, Func<T>)` | `Result<T>` |
| `Bind` | `Bind<T>(this Result, Func<Result<T>>)` | `Result<T>` |
| `Bind` | `Bind(this Result, Func<Result>)` | `Result` |
| `MapError` | `MapError(this Result, Func<Error, Error>)` | `Result` |
| `Tap` | `Tap(this Result, Action)` | `Result` |
| `OnSuccess` | `OnSuccess(this Result, Action)` — alias of `Tap` | `Result` |
| `OnFailure` | `OnFailure(this Result, Action<Error>)` | `Result` |
| `Switch` | `Switch(this Result, Action, Action<Error>)` | `void` |
| `Ensure` | `Ensure(this Result, Func<bool>, Error)` | `Result` |
| `Ensure` | `Ensure(this Result, Func<bool>, Func<Error>)` | `Result` |
| `Recover` | `Recover(this Result, Func<Error, Result>)` | `Result` |
| `Finally` | `Finally(this Result, Action<Error>)` | `Result` |

```csharp
Result<int> fromUnit = SaveRecord(42).Map(() => 1);                  // success: 1
Result<int> unitBound = SaveRecord(42).Bind(() => Result<int>.Success(5));  // success: 5
Result unitChain = SaveRecord(42).Bind(() => Result.Success());      // success

Result unitGuarded = SaveRecord(42).Ensure(() => true, Error.Validation("GUARD"));  // success
Result unitRecovered = SaveRecord(-1).Recover(error => Result.Success());           // success
Result unitMapped = SaveRecord(-1).MapError(e => Error.Validation("RENAMED"));      // failure: "RENAMED"

SaveRecord(42).Tap(() => Console.WriteLine("saved"));                 // "saved"

SaveRecord(42).Switch(
    onSuccess: () => Console.WriteLine("unit switch success"),        // "unit switch success"
    onFailure: e => Console.WriteLine($"unit switch failure {e.Code}"));

SaveRecord(-1).Finally(e => Console.WriteLine($"unit finally: {e.Code}"));  // "unit finally: BAD_ID"
```

## The typed tier — `Result<TSuccess, TError>`

`TypedResultExtensions` carries a custom error type instead of the shared `Error`, and never drops it: every
failure path keeps the receiver's `TError` unchanged. `MapError` is the one operator that changes the error type,
returning `Result<TSuccess, TNewError>`. The typed tier deliberately keeps a single `Ensure` shape — an
already-held `TError` value — rather than factory shapes, because a typed error is an arbitrary value the caller
already has.

| Operator | Signature | Returns |
| --- | --- | --- |
| `Map` | `Map<TSuccess, TError, TOut>(this Result<TSuccess, TError>, Func<TSuccess, TOut>)` | `Result<TOut, TError>` |
| `Bind` | `Bind<TSuccess, TError, TOut>(this Result<TSuccess, TError>, Func<TSuccess, Result<TOut, TError>>)` | `Result<TOut, TError>` |
| `MapError` | `MapError<TSuccess, TError, TNewError>(this Result<TSuccess, TError>, Func<TError, TNewError>)` | `Result<TSuccess, TNewError>` |
| `Tap` | `Tap<TSuccess, TError>(this Result<TSuccess, TError>, Action<TSuccess>)` | `Result<TSuccess, TError>` |
| `OnSuccess` | `OnSuccess<TSuccess, TError>(this Result<TSuccess, TError>, Action<TSuccess>)` | `Result<TSuccess, TError>` |
| `OnFailure` | `OnFailure<TSuccess, TError>(this Result<TSuccess, TError>, Action<TError>)` | `Result<TSuccess, TError>` |
| `Switch` | `Switch<TSuccess, TError>(this Result<TSuccess, TError>, Action<TSuccess>, Action<TError>)` | `void` |
| `Ensure` | `Ensure<TSuccess, TError>(this Result<TSuccess, TError>, Func<TSuccess, bool>, TError)` | `Result<TSuccess, TError>` |
| `Recover` | `Recover<TSuccess, TError>(this Result<TSuccess, TError>, Func<TError, TSuccess>)` | `Result<TSuccess, TError>` |
| `Recover` | `Recover<TSuccess, TError>(this Result<TSuccess, TError>, Func<TError, Result<TSuccess, TError>>)` | `Result<TSuccess, TError>` |
| `Finally` | `Finally<TSuccess, TError>(this Result<TSuccess, TError>, Action<TSuccess, TError>)` | `Result<TSuccess, TError>` |

```csharp
Result<int, DomainError> handled = Find("ok")
    .Map(value => value * 2)
    .Bind(value => value < 100
        ? Result<int, DomainError>.Success(value)
        : Result<int, DomainError>.Failure(DomainError.Invalid))
    .Ensure(value => value > 0, DomainError.Invalid)
    .Recover(_ => 0)
    .Tap(value => Console.WriteLine($"typed tap: {value}"));   // "typed tap: 2"

string summary = handled.Match(
    onSuccess: value => $"handled {value}",
    onFailure: error => $"failed {error}");                    // "handled 2"

Result<int, DomainError> renamed = Find("nope")
    .MapError(error => error == DomainError.NotFound ? DomainError.Invalid : error);
// failure: Invalid (the error type stays DomainError, only the value changes)
```

The typed `Finally` has no `Error.None` sentinel, so on a success the second argument is `default` and on a
failure the first argument is `default` — read the result's own state to tell the two paths apart:

```csharp
Result<int, DomainError> finalized = Find("nope")
    .Finally((value, error) => Console.WriteLine($"typed finally: value={value} error={error}"));
// "typed finally: value=0 error=NotFound"
```

## Async operators

Every synchronous operator has an `Async` twin, available over both a `ValueTask<TResult>` and a
`Task<TResult>` source. The delegates are cancellation-aware — each takes a `CancellationToken` as its last
parameter — and an optional token is observed before the awaited work runs. A cancelled token surfaces as
`OperationCanceledException`, never as a failure. The `Task` overloads wrap the source into a `ValueTask` and
delegate to the same core, so a sync-completing path allocates nothing (see
[Zero allocation](#zero-allocation)).

```csharp
static ValueTask<Result<int>> FetchAsync(int id) =>
    ValueTask.FromResult(id > 0 ? Result<int>.Success(id) : Result<int>.Failure(Error.Validation("BAD_ID")));
```

```csharp
Result<int> fetched = await FetchAsync(21)
    .MapAsync((n, token) => ValueTask.FromResult(n * 2))
    .EnsureAsync((n, token) => ValueTask.FromResult(n >= 0), Error.Validation("NEGATIVE"))
    .RecoverAsync((error, token) => ValueTask.FromResult(0));
// success: 42

Result<int> viaTask = await Task.FromResult(Result<int>.Success(7))
    .MapAsync((n, token) => ValueTask.FromResult(n + 1));     // success: 8

string asyncVerdict = await FetchAsync(21).MatchAsync(
    (n, token) => ValueTask.FromResult($"ok {n}"),
    (e, token) => ValueTask.FromResult($"fail {e.Code}"));    // "ok 21"

Result<int> asyncMapped = await FetchAsync(-1)
    .MapErrorAsync((e, token) => ValueTask.FromResult(Error.Validation("RENAMED", e.Message)));
// failure: "RENAMED"
```

The async operator set per tier:

| Tier | Operators |
| --- | --- |
| Value `Result<T>` | `MapAsync`, `BindAsync`, `TapAsync`, `OnFailureAsync`, `SwitchAsync`, `MatchAsync`, `EnsureAsync` (eager + two factory shapes), `RecoverAsync`, `FinallyAsync`, `MapErrorAsync` |
| Unit `Result` | `MapAsync`, `BindAsync` (value-producing), `TapAsync`, `OnFailureAsync`, `SwitchAsync`, `MatchAsync`, `EnsureAsync` (eager + zero-arg factory), `RecoverAsync`, `FinallyAsync`, `MapErrorAsync` |
| Typed | `MapAsync`, `BindAsync`, `TapAsync`, `OnSuccessAsync`, `OnFailureAsync`, `SwitchAsync`, `MatchAsync`, `EnsureAsync` (single shape), `RecoverAsync` (value + result), `FinallyAsync`, `MapErrorAsync` |

Two asymmetries are deliberate and worth knowing. `OnSuccessAsync` exists only on the typed tier; the value and
unit tiers use `TapAsync` for the same job (the synchronous tiers do carry `OnSuccess` as an alias). And the unit
tier's `BindAsync` is value-producing only — there is no unit-to-unit `BindAsync`, because a unit result chained
to an async step produces a value result. The typed tier keeps a single `EnsureAsync` shape, matching its
synchronous twin.

## Composition laws

The operators obey the standard functional laws, certified by 12 pinned probes in the quality audit
(`tests/ResulTus.Extensions.Tests/CompositionLawAuditTests.cs`), with the same law forms asserted identically on
all three tiers:

- **Functor identity and composition (`Map`).** `Map(v => v)` preserves the result; `Map(f).Map(g)` equals
  `Map(v => g(f(v)))`.
- **Monad associativity and short-circuit (`Bind`).** `Bind(f).Bind(g)` equals `Bind(v => f(v).Bind(g))`, and a
  failure never invokes the binder — the first failure in a chain wins and travels untouched.
- **Error-transformation identity (`MapError`).** `MapError(e => e)` preserves the result.
- **Guard and recovery idempotence (`Ensure`, `Recover`).** `Ensure` and `Recover` leave a failure untouched (the
  predicate and the recovery are never invoked on failure), and `Recover` leaves a success untouched — so a
  recovered result is a success and a second `Recover` is a no-op.
- **Side-effect operators return the original value.** `Tap`, `OnSuccess`, `OnFailure` and `Finally` return the
  receiver unchanged; `Switch` invokes exactly one branch.

One audit lesson applies when you assert these laws yourself: value equality only proves a law within one type
instantiation. A law that changes `T` (like functor composition or `Bind` associativity) asserts the preserved
state plus the carried error, not cross-type equality between `Result<int>` and `Result<string>`.

```csharp
// Short-circuit: the mapper and the binder never run on a failure.
Result<int> shortCircuit = Result<int>.Failure(Error.NotFound("NOPE"))
    .Map(v => v * 2)
    .Bind(v => Result<int>.Success(v));
// IsFailure == true, error.Code == "NOPE"
```

## Null-success state in operators

`Success(T)` is one erased signature shared by `T` and `T?`, so a success carrying `null` is representable for
every `T`. The nullability annotation carries the intent; consumers read the state through `TryGetValue` or
`Match` and never assume a success value is non-null. On a failure, `TryGetValue` hands out `default(T)` — never
garbage — so a caller that ignores the `bool` at worst observes a default. The typed tier follows the same rule
through `Deconstruct` and `Match`.

```csharp
Result<string?> maybeNull = Result<string?>.Success(null);
bool isSuccess = maybeNull.TryGetValue(out var value, out _);
// isSuccess == true, value == null
```

The operators propagate this state faithfully: `Map`/`Bind` hand the (possibly null) value to their delegate and
wrap whatever the delegate returns, `Tap`/`OnSuccess` observe the value as-is, and `Ensure`'s predicate receives
the (possibly null) value.

## Zero allocation

The sync path allocates nothing on the heap on either path: every operator returns a struct and reads the
receiver through `TryGetValue` or `Deconstruct`, so the only allocation is the delegate the caller supplies. The
async path holds the same floor on the sync-completing path, certified by the audit's allocation probes
(`tests/ResulTus.Extensions.Tests/AsyncAllocationAuditTests.cs`): a full `MapAsync` → `EnsureAsync`/`RecoverAsync`
→ `BindAsync` → `TapAsync` → `MapErrorAsync` → `MatchAsync` chain over an already-completed `ValueTask` with
synchronously-completing mappers and static delegates measures **0 B**, and the `Task` wrapper adds nothing beyond
the already-completed task. `docs/performance.md` is the authority for every number and the hard rule it states —
an allocation regression is a defect, a slower row is a discussion.
