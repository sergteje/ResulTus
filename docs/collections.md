# Collections

`ResulTus.Collections` lifts the result operators from a single value to sequences, arrays and read-only lists of
results. Every method lives in the `ResulTus.Collections` namespace and short-circuits on the first failure: it
stops enumerating there, so the projection, action or fold after the failure never runs. The materializing
operations (`Sequence`, `Traverse`, `Partition`) return an exactly sized array, and the internal buffering used to
build it is a rented `ArrayPool<T>` buffer that is always returned.

There is one naming collision worth knowing up front: **`Combine` appears twice, in two packages, with different
meanings.** The binary value combiner `ResultExtensions.Combine` (in `ResulTus.Extensions`, documented in
[Functional operations](functional-operations.md)) combines two `Result<T>` into a tuple. The collection `Combine`
here reduces a whole sequence of unit `Result` into a single unit `Result`. They do not overlap: one takes two
value results, the other takes a sequence of unit results.

The examples share one setup:

```csharp
using ResulTus;
using ResulTus.Collections;

static Result<int> FetchRow(int id) =>
    id > 0 ? Result<int>.Success(id * 10) : Result<int>.Failure(Error.Validation("BAD_ROW"));

static Result SaveRecord(int id) =>
    id <= 0 ? Result.Failure(Error.Validation("BAD_ID")) : Result.Success();

public enum DomainError { NotFound, Invalid }
```

## The value tier — `Result<T>`

`ResultCollectionExtensions` provides the full set. Every operation that has an `IEnumerable<T>` overload also has
`T[]` and `IReadOnlyList<T>` overloads that read the count up front and index directly:

| Operation | Signature | Returns |
| --- | --- | --- |
| `Sequence` | `Sequence<T>(this IEnumerable<Result<T>>)` | `Result<IReadOnlyList<T>>` |
| `Traverse` | `Traverse<TSource, TOut>(this IEnumerable<TSource>, Func<TSource, Result<TOut>>)` | `Result<IReadOnlyList<TOut>>` |
| `Fold` | `Fold<T, TAcc>(this IEnumerable<Result<T>>, TAcc, Func<TAcc, T, TAcc>)` | `Result<TAcc>` |
| `FirstFailure` | `FirstFailure<T>(this IEnumerable<Result<T>>)` | `Error?` |
| `FirstSuccess` | `FirstSuccess<T>(this IEnumerable<Result<T>>)` | `Result<T>` |
| `Partition` | `Partition<T>(this IEnumerable<Result<T>>)` | `(T[] Successes, Error[] Failures)` |
| `Flatten` | `Flatten<T>(this IEnumerable<Result<T>>)` | `IEnumerable<T>` (lazy) |
| `ForEach` | `ForEach<T>(this IEnumerable<Result<T>>, Action<T>)` | `Result` |
| `AllSucceeded` | `AllSucceeded<T>(this IEnumerable<Result<T>>)` | `bool` |
| `AnySucceeded` | `AnySucceeded<T>(this IEnumerable<Result<T>>)` | `bool` |
| `NoneSucceeded` | `NoneSucceeded<T>(this IEnumerable<Result<T>>)` | `bool` |

`Sequence` collects the success values of an already-built sequence of results; `Traverse` maps each source
element through a fallible selector and collects the successes. Both fail with the first error. `Fold` reduces the
success values into one accumulator. `FirstFailure` returns the first error or `null`; `FirstSuccess` returns the
first success, else the first failure, else `Error.Unexpected("empty", "The sequence contains no results.")` for
an empty sequence. `Partition` splits a mixed batch so neither side is lost. `Flatten` lazily yields the success
values and skips failures. `ForEach` runs a side effect per success and reduces to a single `Result`.

```csharp
Result<IReadOnlyList<int>> rows = new[] { 1, 2, 3 }.Traverse(FetchRow);
// success: [10, 20, 30]

Result<IReadOnlyList<int>> all = new[] { FetchRow(1), FetchRow(2), FetchRow(3) }.Sequence();
// success: [10, 20, 30]

Result<int> total = new[] { FetchRow(1), FetchRow(2), FetchRow(3) }
    .Fold(0, static (sum, v) => sum + v);
// success: 60

(int[] succeeded, Error[] failed) = new[] { FetchRow(1), FetchRow(0), FetchRow(2) }.Partition();
// succeeded: [10, 20], failed: [BAD_ROW]

Error? firstFailure = new[] { FetchRow(0), FetchRow(2) }.FirstFailure();   // "BAD_ROW"
Result<int> firstSuccess = new[] { FetchRow(0), FetchRow(2) }.FirstSuccess();  // success: 20

Result visited = new[] { FetchRow(1), FetchRow(2) }.ForEach(v => Console.WriteLine(v));
// prints 10, then 20; returns success

IEnumerable<int> flattened = new[] { FetchRow(1), FetchRow(0), FetchRow(2) }.Flatten();
// lazily yields [10, 20]; the failure is skipped
```

The three aggregate predicates answer three questions about a batch, with defined empty-sequence behavior:

```csharp
bool allSucceeded = new[] { FetchRow(1), FetchRow(2) }.AllSucceeded();   // true (empty -> true)
bool anySucceeded = new[] { FetchRow(0), FetchRow(2) }.AnySucceeded();   // true (empty -> false)
bool noneSucceeded = new[] { FetchRow(0) }.NoneSucceeded();              // true (empty -> true)
```

## The unit tier — `Result`

`UnitResultCollectionExtensions` deliberately does **not** repeat `Sequence`: a unit result carries no value to
collect, so `Combine` is the whole operation. The tier offers `Combine`, `Fold`, `FirstFailure`, `ForEach` and the
three predicates.

```csharp
Result combined = new[] { SaveRecord(1), SaveRecord(2) }.Combine();
// success when every element succeeded, else the first failure

Result<int> count = new[] { SaveRecord(1), SaveRecord(2) }
    .Fold(0, static (acc) => acc + 1);   // success: 2
```

The unit `Fold` takes `Func<TAcc, TAcc>` — it has no per-element value, so the accumulator is stepped once per
successful element.

## The typed tier — `Result<TSuccess, TError>`

`TypedResultCollectionExtensions` mirrors the value tier without dropping the typed error. The failure it returns
is the **exact `TError` of the first failed element in source order** — never an aggregate of several errors. It
provides exactly three operations: `Sequence`, `Traverse` and `Fold`.

```csharp
Result<IReadOnlyList<int>, DomainError> typedRows = new[] { 1, 2, 3 }.Traverse(id => id > 0
    ? Result<int, DomainError>.Success(id * 10)
    : Result<int, DomainError>.Failure(DomainError.Invalid));
// success: [10, 20, 30]
```

The typed tier has no `Partition`, `FirstSuccess`, `FirstFailure`, `Flatten`, `ForEach` or predicates. Those
operate on the shared `Error` of the unit and value tiers; a typed error is an arbitrary domain value with no
ordering the collection layer could assume.

## Async collections

`ResultCollectionAsyncExtensions` covers sequences of result-producing tasks. `WhenAllAsync` awaits every task and
reports the first failure in enumeration order; `WhenAnyAsync` returns the first task to complete, whatever its
outcome. Both surface cancellation as `OperationCanceledException`.

```csharp
Result<IReadOnlyList<int>> awaited = await new[]
{
    Task.FromResult(FetchRow(1)),
    Task.FromResult(FetchRow(2)),
}.WhenAllAsync();
// success: [10, 20]

Result<int> raced = await new[]
{
    Task.FromResult(FetchRow(1)),
    Task.FromResult(FetchRow(2)),
}.WhenAnyAsync();
// the first task to complete, its outcome preserved (here: success 10)
```

`WhenAnyAsync` throws `ArgumentException` on an empty sequence.

## Allocation

`Fold`, `FirstFailure` and the unit `Combine` allocate nothing for either source. `Sequence` and `Traverse`
allocate their output array; `Partition` allocates its two exactly sized output arrays (measured 4,048 B after the
pooled-buffer change). `docs/performance.md` is the authority for every number, and it records one accepted
structural difference: the indexed `Sequence`/`Traverse` overloads allocate their output array before the first
failure is known, so an early-failing large source costs the output array where the `IEnumerable<T>` path would
stop cheaply — accepted because a materializer cannot return its result without allocating it.
