# Testing

`ResulTus.Testing` provides framework-agnostic assertions for the three result shapes. It
references no test framework and no assertion library, so the same helpers work under xUnit,
NUnit, MSTest, or plain console code.

```csharp
using ResulTus;
using ResulTus.Testing;
```

## `ShouldSucceed` and `ShouldFail`

Each helper asserts one state and returns the payload the test usually wants next, so a test
can continue without a cast or a second call. An unexpected state throws
`ResultAssertionException` — a plain `Exception` subclass, which every test framework reports
as a failure — with a message naming the actual error.

```csharp
Result<int> ok = Result<int>.Success(42);
int value = ok.ShouldSucceed();        // 42

Result<int> fail = Result<int>.Failure(Error.Validation("NOT_A_NUMBER"));
Error error = fail.ShouldFail();       // Error.Validation("NOT_A_NUMBER")
```

The unit `Result` follows the same shape, returning the receiver on success and the `Error`
on failure:

```csharp
Result saved = Result.Success();
saved.ShouldSucceed();

Result failed = Result.Failure(Error.Conflict("LOCKED"));
Error conflict = failed.ShouldFail();
```

The typed tier `Result<TSuccess, TError>` returns the success value or the typed error:

```csharp
public enum DomainError { NotFound, Invalid }

Result<int, DomainError> found = Result<int, DomainError>.Success(1);
int n = found.ShouldSucceed();

Result<int, DomainError> missing = Result<int, DomainError>.Failure(DomainError.NotFound);
DomainError typedError = missing.ShouldFail();
```

The failure message for `Result<T>` and the unit tier names the error's type and code; for
the typed tier it prints the error value's `ToString()`. A success value that failed the
assertion is never included in the message — it is data the test did not choose to print.

## Contributing and test conventions

These helpers are the assertion surface, not a testing methodology. The repository's
conventions live in [`CONTRIBUTING.md`](../CONTRIBUTING.md); the ones that matter most when
writing result tests:

- **Public-interface tests, one per rule the specification names**, written as
  Given/When/Then.
- **Exercise the observable effect** — output, exit code, persisted data — and reuse
  existing helpers instead of re-implementing shared state.
- **Hand-written fakes** (including a fake `HttpMessageHandler` for HTTP), no mocking
  frameworks.
- **Deterministic**: no real network and no wall-clock dependence — use `TimeProvider`.
- **Keep test scratch state outside the worktree.**

For error equality, compare `Error` by value (`error.Should().Be(expected)`) — `Error` is a
record struct with value equality and no reference identity.
