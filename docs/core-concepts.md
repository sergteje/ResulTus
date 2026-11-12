# Core concepts

ResulTus models success and failure as inline `readonly record struct` values. The library has one
error type and three result tiers: the unit `Result`, the value-carrying `Result<T>`, and the typed
`Result<TSuccess, TError>`. This guide covers `Error`, the three tiers, the default-state and
null-success contracts, and the conversions between tiers.

The snippets below are statement-level; the shared setup is implied by all of them:

```csharp
using ResulTus;
using ResulTus.Extensions;

public enum DomainError { NotFound, Invalid }
```

## Error

`Error` is a 24-byte `readonly record struct` holding a code, an optional message and a category. It
has three members and no untyped payload:

```csharp
Error notFound = Error.NotFound("USER_NOT_FOUND", "No user with that id");

Console.WriteLine(notFound.Code);      // "USER_NOT_FOUND"
Console.WriteLine(notFound.Message);   // "No user with that id"
Console.WriteLine(notFound.Type);      // NotFound
```

| Member | Type | Meaning |
| --- | --- | --- |
| `Code` | `string` | The machine-readable code. Never `null` (a `null` code coalesces to `string.Empty`). |
| `Message` | `string?` | An optional human-readable message. |
| `Type` | `ErrorType` | The error category. |

`Error` is a `record struct` with value equality: two errors are equal when `Code`, `Message` and
`Type` all match.

### `Error.None`

`Error.None` is the no-error singleton — an empty `Code` and `ErrorType.None`. The default struct
compares equal to it:

```csharp
Error none = Error.None;

Console.WriteLine(none.Code == string.Empty);     // True
Console.WriteLine(none.Type);                     // None
Console.WriteLine(default(Error) == Error.None);  // True
```

### The built-in factories

Every factory has the shape `Error.X(code, string? message = null)` and sets the matching
`ErrorType`:

| Factory | `ErrorType` |
| --- | --- |
| `Error.Failure(code, message?)` | `Failure` |
| `Error.Validation(code, message?)` | `Validation` |
| `Error.NotFound(code, message?)` | `NotFound` |
| `Error.Conflict(code, message?)` | `Conflict` |
| `Error.Unauthorized(code, message?)` | `Unauthorized` |
| `Error.Forbidden(code, message?)` | `Forbidden` |
| `Error.Timeout(code, message?)` | `Timeout` |
| `Error.Cancelled(code, message?)` | `Cancelled` |
| `Error.Unexpected(code, message?)` | `Unexpected` |
| `Error.FromException(exception)` | `Unexpected` |

`ErrorType` is a `byte` enum with the ten values `None`, `Failure`, `Validation`, `NotFound`,
`Conflict`, `Unauthorized`, `Forbidden`, `Timeout`, `Cancelled` and `Unexpected`. The values carry a
conceptual HTTP mapping: `Validation` → 400, `Unauthorized` → 401, `Forbidden` → 403, `NotFound` →
404, `Conflict` → 409, and `Failure`/`Unexpected` → 500.

`Error.FromException(exception)` captures the exception's full type name as the `Code` and its
message as the `Message`; it captures no stack trace, which keeps the error's two-reference layout
(and, in turn, the 32-byte `Result<T>`).

## `Result` — the unit tier

`Result` is the void tier for commands that return nothing. Create it with `Success()` or
`Failure(error)`, and read it through `IsSuccess`/`IsFailure`, `Match`, `Deconstruct` or
`TryGetError`:

```csharp
Result saved = Result.Success();

Console.WriteLine(saved.IsSuccess);   // True

Result failed = Result.Failure(Error.Validation("BAD_ID"));
// An Error converts implicitly into a failed Result:
Result alsoFailed = Error.Validation("BAD_ID");

Console.WriteLine(failed.Match(
    onSuccess: () => "saved",
    onFailure: error => error.Code));   // "BAD_ID"
```

```csharp
failed.Deconstruct(out var isSuccess, out var error);
Console.WriteLine($"{isSuccess} / {error.Code}");   // False / BAD_ID

if (failed.TryGetError(out var err))
{
    Console.WriteLine(err.Code);   // BAD_ID
}
```

## `Result<T>` — the value tier

`Result<T>` carries a `T` on success or an `Error` on failure. The value is never a naked property:
`TryGetValue`, `GetValueOrDefault`, `GetValueOr` and `Match` are the public interface, and the
analyzer `RESULT002` warns when `GetValueOrDefault` is read without checking the outcome first.

```csharp
Result<int> ok = Result<int>.Success(42);
Result<int> notFound = Result<int>.Failure(Error.NotFound("USER_NOT_FOUND"));

if (ok.TryGetValue(out var value, out var error))
{
    Console.WriteLine(value);   // 42
}
else
{
    Console.WriteLine(error.Code);
}

Console.WriteLine(ok.GetValueOrDefault());          // 42
Console.WriteLine(notFound.GetValueOrDefault(-1));  // -1
Console.WriteLine(notFound.GetValueOr(e => 0));     // 0

Console.WriteLine(ok.Match(v => $"value {v}", e => e.Code));   // "value 42"

ok.Deconstruct(out var isSuccess, out var v, out var err);
Console.WriteLine($"{isSuccess} / {v} / '{err.Code}'");   // True / 42 / ''
```

On failure, `TryGetValue` and `Deconstruct` hand back `default(T)` for the value and `Error.None`
for the error, never garbage. A value converts implicitly into a success and an `Error` into a
failure, so `Result<int> x = 42;` and `Result<int> y = Error.NotFound("X");` both work.

## `Result<TSuccess, TError>` — the typed tier

The typed tier carries a custom error type on failure instead of the shared `Error`. It is
intentionally narrow: `IsSuccess`/`IsFailure`, `Match` and `Deconstruct`, plus the static factories
and the implicit conversions from each side.

```csharp
Result<int, DomainError> found = Result<int, DomainError>.Success(42);
Result<int, DomainError> missing = Result<int, DomainError>.Failure(DomainError.NotFound);

Console.WriteLine(found.Match(
    onSuccess: value => $"value {value}",
    onFailure: error => $"failed {error}"));   // "value 42"

missing.Deconstruct(out var isSuccess, out var value, out var error);
Console.WriteLine($"{isSuccess} / {error}");   // False / NotFound
```

The fluent operators on this tier live in `ResulTus.Extensions`, not on the type itself — see
[Functional operations](functional-operations.md).

## The default-state contract

The default value of every tier is a *failure*, never a zero-value success:

```csharp
Result<int> defaultResult = default;

Console.WriteLine(defaultResult.IsSuccess);   // False
Console.WriteLine(defaultResult.IsFailure);   // True

Result<int> explicitFailure = Result<int>.Failure(Error.None);
Console.WriteLine(defaultResult == explicitFailure);   // True
```

`default(Result<T>)` carries `Error.None`, so `default(Result<T>) == Result<T>.Failure(Error.None)`
holds. The unit tier behaves the same way (`default(Result)` is a failure), and the typed tier's
default is a failure whose error value is `default(TError)`. Combined with the `Error` value
equality above, `default(Error) == Error.None` by design — the flag byte reads `0` across all three
tiers.

## The null-success contract

`Success(T)` is one erased signature shared by `T` and `T?`, so a success can carry a null value for
any `T`. The nullability annotation declares the *intent*; it does not change the runtime:

```csharp
Result<string?> nullSuccess = Result<string?>.Success(null);

Console.WriteLine(nullSuccess.IsSuccess);   // True

if (nullSuccess.TryGetValue(out var value, out var error))
{
    Console.WriteLine(value is null);   // True
}
```

`Result<string?>` says "a null success is expected here"; `Result<string>` says "the success should
not be null", though the runtime cannot enforce it. Consumers therefore read the state through
`TryGetValue` or `Match` and never assume a success value is non-null. This contract is pinned by
`DefaultAndNullStateAuditTests`.

## Converting between tiers

`ResulTus.Extensions` provides `ResultConversions`, the extension methods that move a result between
the tiers (and a few other shapes):

| Method | From | To | Notes |
| --- | --- | --- | --- |
| `ToResult<T>()` | `T` | `Result<T>` | Wraps a plain value as a success. |
| `ToNullable<T>()` | `Result<T>` (struct `T`) | `T?` | Success → the value; failure → `null`. |
| `ToUnitResult<T>()` | `Result<T>` | `Result` | Drops the value; the error is preserved. |
| `ToGenericResult<T, TErr>(map)` | `Result<T>` | `Result<T, TErr>` | Lifts into the typed tier, mapping `Error` → `TErr`. |
| `ToErrorResult<T, TErr>(map)` | `Result<T, TErr>` | `Result<T>` | Maps `TErr` → `Error`. |
| `ToErrorResult<T>()` | `Result<T, Error>` | `Result<T>` | No map needed: the error is already an `Error`. |
| `ToException()` | `Error` | `Exception` | `Message` = the message or the code; `Data["Code"]` = the code. |

```csharp
Result<int> ok = Result<int>.Success(42);
Result<int> failed = Result<int>.Failure(Error.NotFound("MISSING"));

Result okUnit = ok.ToUnitResult();
Result failedUnit = failed.ToUnitResult();   // failure, same error

int? n = ok.ToNullable();        // 42
int? m = failed.ToNullable();    // null

Result<int> wrapped = 7.ToResult();

// Lift into the typed tier, mapping the Error to its Code.
Result<int, string> typed = failed.ToGenericResult(e => e.Code);
Console.WriteLine(typed.Match(v => $"value {v}", e => $"code {e}"));   // code MISSING

// Drop the typed error back to the shared Error.
Result<int> back = typed.ToErrorResult(code => Error.Validation(code));
Console.WriteLine(back.Match(v => $"value {v}", e => e.Code));   // MISSING

// When the typed error is already an Error, no map is required.
Result<int, Error> typedError = Result<int, Error>.Failure(Error.Conflict("DUP"));
Result<int> dropped = typedError.ToErrorResult();
Console.WriteLine(dropped.Match(v => $"value {v}", e => e.Code));   // DUP

Exception ex = Error.Validation("BAD_INPUT").ToException();
Console.WriteLine($"{ex.Message} / {ex.Data["Code"]}");   // BAD_INPUT / BAD_INPUT
```

`ToNullable<T>` is constrained to structs on purpose: for reference types,
`GetValueOrDefault()` already returns `null` on failure.
