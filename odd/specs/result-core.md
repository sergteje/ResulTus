# result-core Specification

## Purpose

Defines the 4 fundamental types of ResulTus — `Error`, `Result` (void), `Result<T>`, and `Result<TSuccess, TError>` — as `readonly record struct` types with zero heap allocations on both the success and the failure path, targeting net8.0+.

## Requirements

### ErrorType

| ID | Requirement |
|----|-------------|
| ET-1 | MUST be a `byte`-based enum |
| ET-2 | Values: `None`, `Failure`, `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`, `Timeout`, `Cancelled`, `Unexpected` |

#### Scenario: Default value is None
- GIVEN `default(ErrorType)`
- WHEN evaluating
- THEN it MUST equal `ErrorType.None`

#### Scenario: All values are distinct
- GIVEN every `ErrorType` value
- WHEN compared by underlying integer
- THEN each MUST be unique

### Error

| ID | Requirement |
|----|-------------|
| ER-1 | MUST be a `readonly record struct` |
| ER-2 | Properties: `Code` (`string`, non-null), `Message` (`string?`), `Type` (`ErrorType`) |
| ER-3 | `Error.None` singleton: `Code == ""`, `Type == ErrorType.None` |
| ER-4 | Static factories: `Failure()`, `Validation()`, `NotFound()`, `Conflict()`, `Unauthorized()`, `Forbidden()`, `Timeout()`, `Cancelled()`, `Unexpected()` |
| ER-5 | Immutable — all properties `{ get; }` or `{ get; init; }` |
| ER-6 | `Error` MUST NOT carry untyped payload data (no `object`-based dictionaries); structured extras belong to a typed `TError` in `Result<TSuccess, TError>`, where they cost no boxing and no allocation |
| ER-7 | Equality MUST be defined over the `Code` contract (null-coalesced to `string.Empty`, ordinal) and not over the raw backing field: `default(Error)`, whose field is `null`, MUST equal `Error.None` under `==`, `Equals` and `GetHashCode` |

#### Scenario: Factory sets correct ErrorType and Code
- GIVEN `Error.NotFound("user.missing")`
- THEN `Type` MUST be `ErrorType.NotFound` and `Code` MUST be `"user.missing"`

#### Scenario: Error.None has empty code and no type category
- GIVEN `Error.None`
- THEN `Code` MUST be `string.Empty` and `Type` MUST be `ErrorType.None`

#### Scenario: Structural equality
- GIVEN two errors with identical `Code`, `Message`, `Type`
- WHEN comparing with `==`
- THEN they MUST be equal

#### Scenario: The default value is "no error"
- GIVEN `default(Error)` and `Error.None`
- WHEN comparing with `==`, `Equals` or `GetHashCode`
- THEN they MUST be indistinguishable, because the default struct never runs the constructor and its code field is `null`
- AND a `null` `Message` MUST stay distinct from `string.Empty`, since only `Code` carries the null-coalescing contract

#### Scenario: Different errors are not equal
- GIVEN `Error.Failure("A")` and `Error.Failure("B")`
- WHEN comparing
- THEN they MUST NOT be equal

#### Scenario: Error carries no untyped payload
- GIVEN the public surface of `Error`
- THEN it MUST expose only `Code`, `Message` and `Type`
- AND it MUST NOT expose a dictionary or other `object`-based payload

### Result (void)

| ID | Requirement |
|----|-------------|
| RV-1 | MUST be a `readonly record struct` |
| RV-2 | `IsSuccess` MUST be a `byte` field (1 = success, 0 = failure) |
| RV-3 | `IsFailure` MUST return `!IsSuccess` |
| RV-4 | Implicit conversion from `Error` → `Result` |
| RV-5 | Static factories: `Result.Success()`, `Result.Failure(Error)` |
| RV-6 | `Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure)` |
| RV-7 | `Deconstruct(out bool isSuccess, out Error error)` |
| RV-8 | `TryGetError(out Error error)` — returns `false` on success |
| RV-9 | MUST NOT have a `Value` property |
| RV-10 | MUST have zero heap allocations on success path |

#### Scenario: Success has IsSuccess true
- GIVEN `Result.Success()`
- THEN `IsSuccess` MUST be `true` and `IsFailure` MUST be `false`

#### Scenario: Failure wraps error
- GIVEN `var err = Error.NotFound("x")` and `Result.Failure(err)`
- THEN `IsSuccess` MUST be `false` and `TryGetError(out var e)` MUST yield `e == err`

#### Scenario: Implicit conversion from Error to Result
- GIVEN `Error err = Error.Timeout("t")`
- WHEN `Result result = err`
- THEN `result.IsFailure` MUST be `true`

#### Scenario: Match invokes correct branch
- GIVEN `Result.Success()`
- WHEN `Match(() => "ok", _ => "fail")`
- THEN the result MUST be `"ok"`
- BUT given `Result.Failure(Error.None)`
- WHEN `Match(() => "ok", _ => "fail")`
- THEN the result MUST be `"fail"`

#### Scenario: Deconstruct on success
- GIVEN `Result.Success()`
- WHEN `var (success, error) = result`
- THEN `success` MUST be `true` and `error` MUST be `Error.None`

#### Scenario: Default struct is failure
- GIVEN `default(Result)`
- THEN `IsSuccess` MUST be `false`

### Result\<T\>

| ID | Requirement |
|----|-------------|
| RT-1 | MUST be a `readonly record struct` |
| RT-2 | `IsSuccess` as a `byte` field |
| RT-3 | Implicit conversion from `T` → `Result<T>` |
| RT-4 | Implicit conversion from `Error` → `Result<T>` |
| RT-5 | Static factories: `Success(T)`, `Failure(Error)` |
| RT-6 | `Match<TResult>(Func<T, TResult>, Func<Error, TResult>)` |
| RT-7 | `Deconstruct(out bool, out T?, out Error)` |
| RT-8 | `TryGetValue(out T?, out Error)` — safe access to both |
| RT-9 | `TryGetValue(out T?)` — returns value only |
| RT-10 | `GetValueOrDefault(T defaultValue = default)` |
| RT-11 | `GetValueOr(Func<Error, T>)` — recover from error |
| RT-12 | MUST NOT expose a direct `Value` property |
| RT-13 | Zero heap allocations on success path |

#### Scenario: Implicit conversion from T
- GIVEN `int value = 42`
- WHEN `Result<int> r = value`
- THEN `r.IsSuccess` MUST be `true` and `TryGetValue(out var v)` MUST yield `v == 42`

#### Scenario: Implicit conversion from Error
- GIVEN `Error err = Error.Conflict("dup")`
- WHEN `Result<int> r = err`
- THEN `r.IsFailure` MUST be `true`

#### Scenario: TryGetValue returns false on failure
- GIVEN `Result<int>.Failure(Error.Unexpected(""))`
- WHEN calling `TryGetValue(out var v, out var e)`
- THEN return MUST be `false`, `v` MUST be `default`, and `e` MUST be the error

#### Scenario: GetValueOrDefault returns fallback on failure
- GIVEN a failed `Result<int>`
- WHEN calling `GetValueOrDefault(99)`
- THEN result MUST be `99`

#### Scenario: GetValueOr recovers from error
- GIVEN a failed `Result<int>`
- WHEN calling `GetValueOr(_ => -1)`
- THEN result MUST be `-1`

#### Scenario: Deconstruct on success reveals value
- GIVEN `Result<int>.Success(7)`
- WHEN `var (ok, val, err) = r`
- THEN `ok` MUST be `true`, `val` MUST be `7`, and `err` MUST be `Error.None`

#### Scenario: Default struct is failure with default value
- GIVEN `default(Result<int>)`
- THEN `IsSuccess` MUST be `false` and `TryGetValue(out var v)` MUST yield `v == 0`

### Result\<TSuccess, TError\>

| ID | Requirement |
|----|-------------|
| RG-1 | MUST be a `readonly record struct` |
| RG-2 | Properties: `IsSuccess` (byte), `IsFailure` |
| RG-3 | Implicit conversion from `TSuccess` and from `TError` |
| RG-4 | Static factories: `Success(TSuccess)`, `Failure(TError)` |
| RG-5 | `Match<TResult>(Func<TSuccess, TResult>, Func<TError, TResult>)` |
| RG-6 | `Deconstruct(out bool, out TSuccess?, out TError?)` |
| RG-7 | Zero heap allocations on success path |

#### Scenario: Implicit conversion from success value
- GIVEN `Result<int, string>.Success(10)`
- THEN `IsSuccess` MUST be `true` and `Match(v => v, e => -1)` MUST be `10`

#### Scenario: Implicit conversion from error value
- GIVEN `Result<int, string> r = "fail"`
- THEN `IsFailure` MUST be `true`

#### Scenario: Deconstruct on failure
- GIVEN a failed `Result<int, string>`
- WHEN `var (ok, val, err) = r`
- THEN `ok` MUST be `false` and `err` MUST NOT be `null`

#### Scenario: Default struct is failure with default values
- GIVEN `default(Result<int, string>)`
- THEN `IsSuccess` MUST be `false`

### ErrorType → HTTP Mapping (informative)

| ErrorType | HTTP Status | Notes |
|-----------|-------------|-------|
| Validation | 400 | Bad Request |
| NotFound | 404 | Not Found |
| Conflict | 409 | Conflict |
| Unauthorized | 401 | Unauthorized |
| Forbidden | 403 | Forbidden |
| Failure | 500 | Internal Server Error |
| Unexpected | 500 | Internal Server Error |
