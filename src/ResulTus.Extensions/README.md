# ResulTus.Extensions — the fluent operators

[![NuGet](https://img.shields.io/nuget/v/ResulTus.Extensions?label=nuget)](https://www.nuget.org/packages/ResulTus.Extensions)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.Extensions?label=downloads)](https://www.nuget.org/packages/ResulTus.Extensions)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package adds the fluent operators to the [ResulTus](https://github.com/sergteje/ResulTus) types:
`Map`, `Bind`, `Ensure`, `Recover`, `MapError`, `Tap`, `Switch`, `Finally` and their `Async` twins,
for all three result tiers. Everything lives in the `ResulTus.Extensions` namespace. One contract
holds everywhere — a failure short-circuits the pipeline untouched, so a delegate that does not
apply to the outcome is never invoked.

## Install

```bash
dotnet add package ResulTus.Extensions
```

## Dependencies

[`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
types and `Error` this package extends. It adds no other package reference.

## What you get

Each tier has a synchronous and an asynchronous extension class:

| Tier | Synchronous | Asynchronous |
| --- | --- | --- |
| Unit `Result` | `UnitResultExtensions` | `UnitResultAsyncExtensions` |
| Value `Result<T>` | `ResultExtensions` | `ResultAsyncExtensions` |
| Typed `Result<TSuccess, TError>` | `TypedResultExtensions` | `TypedResultAsyncExtensions` |

The operators, per tier:

| Operator | Effect |
| --- | --- |
| `Map` | Projects the success value. |
| `Bind` | Chains another result-producing step. |
| `MapError` | Transforms the error; a success is untouched. |
| `Tap` / `OnSuccess` | Runs a side effect on a success and returns the receiver. |
| `OnFailure` | Runs a side effect on a failure and returns the receiver. |
| `Switch` | Runs exactly one of two branches; returns `void`. |
| `Ensure` | Keeps the success only while a predicate holds. |
| `Recover` | Replaces a failure with a value or with another result. |
| `Finally` | Runs one action on either path and returns the receiver. |
| `Combine` | Value tier only: merges two successes into a named tuple. |

`Match` is the exception: it is an instance method on the result types, not an extension, and it
folds both paths into one value.

### Value tier — `Result<T>`

```csharp
using ResulTus;
using ResulTus.Extensions;

Result<int> parsed = Result<int>.Success(21);

Result<string> label = parsed
    .Map(value => value * 2)
    .Map(value => $"doubled to {value}");                        // "doubled to 42"

Result<int> guarded = parsed
    .Ensure(value => value >= 18, Error.Forbidden("UNDERAGE"));  // success: 21

Result<int> recovered = Result<int>.Failure(Error.Validation("NOT_A_NUMBER"))
    .Recover(error => -1);                                       // success: -1

Result<int> fallback = Result<int>.Failure(Error.NotFound("MISSING"))
    .Recover(error => Result<int>.Success(0));                   // success: 0
```

`Ensure` takes an eager `Error`, a `Func<Error>` factory, or a `Func<T, Error>` factory that
receives the rejected value. `Recover` takes either `Func<Error, T>` or `Func<Error, Result<T>>`.

### Unit tier — `Result`

A unit success carries no value, so the delegates take no argument:

```csharp
using ResulTus;
using ResulTus.Extensions;

Result saved = Result.Success();

Result<int> fromUnit = saved.Map(() => 1);                       // success: 1
Result chained = saved.Bind(() => Result.Success());             // success
Result guarded = saved.Ensure(() => true, Error.Validation("GUARD"));
```

### Typed tier — `Result<TSuccess, TError>`

The typed tier keeps the caller's error type on every failure path; `MapError` is the one operator
that changes it. Its `Ensure` takes an already-held `TError` value.

```csharp
using ResulTus;
using ResulTus.Extensions;

Result<int, Error> typed = Result<int, Error>.Success(21);

Result<int, Error> handled = typed
    .Map(value => value * 2)
    .Ensure(value => value > 0, Error.Validation("NON_POSITIVE"))
    .Recover(error => 0);

string summary = handled.Match(
    onSuccess: value => $"handled {value}",                     // "handled 42"
    onFailure: error => $"failed {error.Code}");
```

### Async operators

Every operator except `Combine` has an `Async` twin, usable over a `ValueTask<TResult>` or a
`Task<TResult>` source. The delegates receive the value and a `CancellationToken`; a cancelled token
surfaces as `OperationCanceledException`, never as a failure.

```csharp
using System.Threading.Tasks;
using ResulTus;
using ResulTus.Extensions;

ValueTask<Result<int>> fetched = ValueTask.FromResult(Result<int>.Success(21));

Result<int> doubled = await fetched
    .MapAsync((value, cancellationToken) => ValueTask.FromResult(value * 2));   // success: 42
```

Two asymmetries are deliberate: `OnSuccessAsync` exists only on the typed tier (the other tiers use
`TapAsync`), and the unit tier's `BindAsync` is value-producing only.

### Conversions

`ResultConversions` moves between the result types, plain values and exceptions:

| Method | Converts |
| --- | --- |
| `ToResult` | A value into a successful `Result<T>`. |
| `ToNullable` | A `Result<T>` over a value type into `T?` (`null` on failure). |
| `ToUnitResult` | A `Result<T>` into the unit `Result`, dropping the value. |
| `ToException` | An `Error` into an `Exception` whose `Data["Code"]` holds the code. |
| `ToErrorResult` | A typed result into `Result<TSuccess>` by mapping its error. |
| `ToGenericResult` | A `Result<TSuccess>` into the typed tier by mapping its error. |

```csharp
using System;
using ResulTus;
using ResulTus.Extensions;

Result<int> loaded = Result<int>.Success(42);

Result<int> wrapped = 42.ToResult();          // success: 42
int? nullable = loaded.ToNullable();          // 42
Result unit = loaded.ToUnitResult();          // success, value dropped
Exception failure = Error.NotFound("USER_NOT_FOUND").ToException();
```

The full signature tables and the composition laws live in the
[functional operations guide](https://github.com/sergteje/ResulTus/blob/main/docs/functional-operations.md).

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — required
  dependency: the core types these operators extend.
- [`ResulTus.Collections`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Collections/README.md)
  — lifts the same operators to sequences of results.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [functional operations](https://github.com/sergteje/ResulTus/blob/main/docs/functional-operations.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md),
  [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md),
  [collections](https://github.com/sergteje/ResulTus/blob/main/docs/collections.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
