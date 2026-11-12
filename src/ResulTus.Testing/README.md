# ResulTus.Testing — result assertions for tests

[![NuGet](https://img.shields.io/nuget/v/ResulTus.Testing?label=nuget)](https://www.nuget.org/packages/ResulTus.Testing)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.Testing?label=downloads)](https://www.nuget.org/packages/ResulTus.Testing)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package adds assertion helpers for the three [ResulTus](https://github.com/sergteje/ResulTus)
result shapes, for use in test code. It references no test framework and no assertion library, so
the same helpers work under xUnit, NUnit, MSTest or plain console code. Each helper asserts one
state and returns the payload the test usually wants next, so a test continues without a cast or a
second call.

## Install

```bash
dotnet add package ResulTus.Testing
```

## Dependencies

[`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
types and `Error`. It adds no test-framework or assertion-library reference.

## What you get

`ResultAssertions` exposes one pair of helpers per tier, and `ResultAssertionException` is the
failure they throw:

| Tier | `ShouldSucceed()` returns | `ShouldFail()` returns |
| --- | --- | --- |
| `Result` | the same `Result` | the `Error` |
| `Result<T>` | the success value `T` | the `Error` |
| `Result<TSuccess, TError>` | the success value `TSuccess` | the typed error `TError` |

```csharp
using ResulTus;
using ResulTus.Testing;

Result<int> ok = Result<int>.Success(42);
int value = ok.ShouldSucceed();                                // 42

Result<int> failed = Result<int>.Failure(Error.Validation("NOT_A_NUMBER"));
Error error = failed.ShouldFail();                             // the NOT_A_NUMBER error

Result saved = Result.Success();
_ = saved.ShouldSucceed();                                     // successful unit result

Error conflict = Result.Failure(Error.Conflict("LOCKED")).ShouldFail();

Result<int, string> missing = Result<int, string>.Failure("NOT_FOUND");
string typedError = missing.ShouldFail();                      // "NOT_FOUND"
```

An assertion that does not hold throws `ResultAssertionException`, whose message names the actual
error; every test framework reports an unhandled exception as a failure. The exception derives from
`Exception` directly, which is what keeps the package framework-agnostic.

Two message rules are deliberate. For the unit and `Result<T>` tiers the failure message states the
error's type, code and message. For the typed tier the message prints the error value's
`ToString()`. A success value is never written into a message: it is data the test did not choose
to print.

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — required
  dependency: the result types these helpers assert on.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [testing](https://github.com/sergteje/ResulTus/blob/main/docs/testing.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md),
  [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
