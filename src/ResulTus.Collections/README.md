# ResulTus.Collections — result operations over sequences

[![NuGet](https://img.shields.io/nuget/v/ResulTus.Collections?label=nuget)](https://www.nuget.org/packages/ResulTus.Collections)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.Collections?label=downloads)](https://www.nuget.org/packages/ResulTus.Collections)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package lifts the [ResulTus](https://github.com/sergteje/ResulTus) operators from a single value
to sequences, arrays and read-only lists of results. Everything lives in the `ResulTus.Collections`
namespace and short-circuits on the first failure: enumeration stops there, so the projection, action
or fold after the failure never runs. The materializing operations build exactly sized arrays.

## Install

```bash
dotnet add package ResulTus.Collections
```

## Dependencies

[`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
types and `Error` these operations consume and produce. It adds no other package reference.

## What you get

| Tier | Operations |
| --- | --- |
| Value `Result<T>` | `Sequence`, `Traverse`, `Fold`, `FirstFailure`, `FirstSuccess`, `Partition`, `Flatten`, `ForEach`, `AllSucceeded`, `AnySucceeded`, `NoneSucceeded` |
| Unit `Result` | `Combine`, `Fold`, `FirstFailure`, `ForEach`, `AllSucceeded`, `AnySucceeded`, `NoneSucceeded` |
| Typed `Result<TSuccess, TError>` | `Sequence`, `Traverse`, `Fold` |
| Async | `WhenAllAsync`, `WhenAnyAsync` |

Every operation except `Flatten` also has `T[]` and `IReadOnlyList<T>` overloads that read the count
up front and index directly.

The unit tier has no `Sequence`: a unit result carries no value to collect, so `Combine` is the whole
operation. The typed tier has exactly three operations; the rest assume the shared `Error` of the
unit and value tiers.

### Value tier — `Result<T>`

```csharp
using ResulTus;
using ResulTus.Collections;

Result<int>[] fetched = new[]
{
    Result<int>.Success(10),
    Result<int>.Success(20),
};

Result<IReadOnlyList<int>> sequenced = fetched.Sequence();                     // [10, 20]
Result<IReadOnlyList<int>> mapped = new[] { 1, 2, 3 }
    .Traverse(id => Result<int>.Success(id * 10));                             // [10, 20, 30]
Result<int> total = fetched.Fold(0, static (sum, value) => sum + value);       // success: 30
```

`Sequence` collects the success values of an already-built sequence; `Traverse` maps each source
element through a fallible selector and collects the successes. Both fail with the first error.
`Fold` reduces the success values into one accumulator.

```csharp
using ResulTus;
using ResulTus.Collections;

Result<int>[] batch = new[]
{
    Result<int>.Success(10),
    Result<int>.Failure(Error.Validation("BAD_ROW")),
};

(int[] successes, Error[] failures) = batch.Partition();   // [10] and [BAD_ROW]
bool allSucceeded = batch.AllSucceeded();                  // false
Error? firstFailure = batch.FirstFailure();                // "BAD_ROW"
IEnumerable<int> values = batch.Flatten();                 // lazily yields [10]
```

`FirstSuccess` returns the first success, else the first failure, else an
`Error.Unexpected("empty", ...)` for an empty sequence. `Flatten` is lazy and skips failures.
`ForEach` runs a side effect per success and reduces to a single `Result`. `AllSucceeded` is `true`
and `NoneSucceeded` is `true` for an empty sequence; `AnySucceeded` is `false`.

### Unit tier — `Result`

```csharp
using ResulTus;
using ResulTus.Collections;

Result[] saves = new[] { Result.Success(), Result.Success() };

Result combined = saves.Combine();                          // success
Result<int> steps = saves.Fold(0, static acc => acc + 1);   // success: 2
```

The unit `Fold` takes `Func<TAcc, TAcc>`: there is no per-element value, so the accumulator is
stepped once per successful element.

### Typed tier — `Result<TSuccess, TError>`

```csharp
using ResulTus;
using ResulTus.Collections;

Result<IReadOnlyList<int>, Error> rows = new[] { 1, 2, 3 }
    .Traverse(id => Result<int, Error>.Success(id * 10));   // [10, 20, 30]
```

The failure returned is the exact `TError` of the first failed element in source order, never an
aggregate of several errors.

### Async collections

`WhenAllAsync` awaits every task and reports the first failure in enumeration order;
`WhenAnyAsync` returns the first task to complete, whatever its outcome. Both surface cancellation
as `OperationCanceledException`, and `WhenAnyAsync` throws `ArgumentException` on an empty sequence.

```csharp
using System.Threading.Tasks;
using ResulTus;
using ResulTus.Collections;

Result<IReadOnlyList<int>> awaited = await new[]
{
    Task.FromResult(Result<int>.Success(10)),
    Task.FromResult(Result<int>.Success(20)),
}.WhenAllAsync();                                   // success: [10, 20]

Result<int> raced = await new[]
{
    Task.FromResult(Result<int>.Success(10)),
}.WhenAnyAsync();                                   // the first task to complete
```

### `Combine` has two meanings

`Combine` exists in two packages. The collection `Combine` here reduces a whole sequence of unit
results into one unit `Result`. The binary combiner
[`ResultExtensions.Combine`](https://github.com/sergteje/ResulTus/blob/main/docs/functional-operations.md)
in `ResulTus.Extensions` merges two value results into a tuple. They do not overlap.

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — required
  dependency: the core result types.
- [`ResulTus.Extensions`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Extensions/README.md)
  — the per-value fluent operators these sequence operations mirror.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [collections](https://github.com/sergteje/ResulTus/blob/main/docs/collections.md),
  [functional operations](https://github.com/sergteje/ResulTus/blob/main/docs/functional-operations.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md),
  [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
