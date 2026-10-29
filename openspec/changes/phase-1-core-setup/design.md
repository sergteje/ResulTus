# Design: Phase 1 — Core Setup

## Technical Approach

Greenfield scaffold for ResulTus: build-convention files (`.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `ResulTus.sln`) plus 4 core `readonly record struct` types (`Error`, `Result`, `Result<T>`, `Result<TSuccess, TError>`) with `[StructLayout(LayoutKind.Sequential, Pack = 1)]` and `IsSuccess` as a `byte` field. No heap allocations on success paths. Single TFM `net8.0` for the core library, `net10.0` for tests.

## Architecture Decisions

### Decision: `byte` field instead of `bool` for `IsSuccess`

| Option | Tradeoff | Decision |
|--------|----------|----------|
| `bool` field | CLR treats bool as 1 byte but can introduce padding when adjacent to ref fields | ❌ |
| `byte` field + `IsSuccess`/`IsFailure` computed booleans | Guarantees predictable layout; explicit 0/1 contract with `LayoutKind.Sequential` | ✅ |

**Rationale**: `bool` in .NET is 1 byte but the CLR may pad to align reference-type fields. Using `byte` gives deterministic packing — the `_isSuccess` field sits at offset 24 (after 3 refs) taking exactly 1 byte with 7 bytes padding to 32 total. Cheap and predictable.

### Decision: `Error? _error` nullable reference field

| Option | Tradeoff | Decision |
|--------|----------|----------|
| `Error` + `bool _isErrorSet` | 2 fields where 1 would do; need to synchronize both | ❌ |
| `Error? _error` (nullable) | `Nullable<Error>` is a struct with `hasValue` + `value`; violates `Sequential, Pack=1` simplicity. Using `Error? _error` as a *nullable reference* (not nullable value) is the intent, but `readonly record struct Error` is a value type, so `Error?` is `Nullable<Error>` — **do NOT use this**. Use `byte _isSuccess` + `Error _error` instead. | ✅ Reconsider |
| `byte _isSuccess` + `Error _error` zero-value | `default(Error)` has `Code == ""`, `Type == None` — indistinguishable from real None. Need separate flag. | ✅ |

**Corrected approach**: Use `byte _isSuccess` (1 = success, 0 = failure) and `Error _error` (non-nullable). On success, `_error` is `default` (which is `Error.None`). On failure, `_isSuccess = 0` and `_error` holds the real error. This avoids wrapping in `Nullable<Error>` and keeps the struct layout simple.

### Decision: `IReadOnlyDictionary<string, object?>?` for Metadata

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Custom `MetadataCollection` | More control but defeats zero-alloc goal (still needs a dictionary internally) | ❌ |
| `IReadOnlyDictionary<string, object?>?` | Standard BCL interface; consumers already know it; null = no metadata | ✅ |

**Rationale**: Zero-cost abstraction — null means no allocation. Consumers who need metadata create a `Dictionary<string, object?>` which already implements `IReadOnlyDictionary`.

### Decision: No direct `Value` property on `Result<T>`

| Option | Tradeoff | Decision |
|--------|----------|----------|
| `public T Value { get; }` | Throws on failure — easy to forget to check `IsSuccess` | ❌ |
| `TryGetValue`, `GetValueOrDefault`, `GetValueOr`, `Match` | Forces explicit handling; no surprise exceptions | ✅ |

**Rationale**: Design intent is to *prevent* unguarded access. Callers must explicitly decide what happens on failure. This is the same philosophy as F# `Result` and Rust `Result`.

### Decision: Single TFM `net8.0` for core

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Multi-target `net8.0`+`net9.0`+`net10.0` | More build complexity, conditional compilation, longer CI | ❌ |
| Single TFM `net8.0` | Simplest; library works on all .NET 8+ runtimes via compatibility | ✅ |

**Rationale**: `net8.0` is the current LTS. Users on .NET 9/10 can reference it directly — no compile-time features from newer TFMs are needed for these types. Test project uses `net10.0` to match the installed SDK.

### Decision: No custom equality

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Override `Equals`/`GetHashCode` | Adds maintenance; record struct already generates correct value equality | ❌ |
| Default record struct equality | Works correctly for all fields; handles `IReadOnlyDictionary` reference comparison | ✅ |

**Rationale**: `readonly record struct` auto-generates `Equals` and `==` based on all fields. For `IReadOnlyDictionary` this means reference equality, which is acceptable — callers who create different dictionary instances with same content get structurally different errors, which is the documented behavior.

## Data Flow

```
User Code
  │
  ├── Error.Success()           ──→  Result (IsSuccess=true, _error=default)
  ├── Error.Failure(err)        ──→  Result (IsSuccess=false, _error=err)
  ├── Error.NotFound("msg")     ──→  Error (Code, Type=NotFound, Message, Metadata?)
  │
  ├── T value                   ──→  Result<T> (implicit conversion)
  ├── Error err                 ──→  Result<T> (implicit conversion)
  │
  ├── TSuccess val              ──→  Result<TSuccess,TError> (implicit)
  ├── TError err                ──→  Result<TSuccess,TError> (implicit)
  │
  └── .Match(onSuccess, onFailure) ──→  unified TResult
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `ResulTus.sln` | Create | Solution with `src/ResulTus` + `test/ResulTus.Tests` |
| `.editorconfig` | Create | Strict .NET rules: nullable warn, XML docs required, file header, charset UTF-8 |
| `Directory.Build.props` | Create | Shared: `net8.0` TFM, LangVersion latest, Nullable enable, ImplicitUsings, IsTrimmable, IsAotCompatible |
| `Directory.Packages.props` | Create | Centralized: xUnit 2.9.3, FluentAssertions 7.1.0, Microsoft.NET.Test.Sdk 17.13.0 |
| `global.json` | Create | Pin SDK 10.0.301 |
| `src/ResulTus/ResulTus.csproj` | Create | `net8.0`, `<IsTrimmable>`, `<IsAotCompatible>`, reference to `src/ResulTus` |
| `src/ResulTus/ErrorType.cs` | Create | `byte`-based enum — 10 values |
| `src/ResulTus/Error.cs` | Create | `readonly record struct` with `[StructLayout]`, 9 factory methods, `Error.None` |
| `src/ResulTus/Result.cs` | Create | Void result: `byte _isSuccess`, implicit `Error`→`Result`, Match, Deconstruct, TryGetError |
| `src/ResulTus/Result{T}.cs` | Create | Generic result: `T? _value`, implicit both directions, Match, Deconstruct, TryGetValue variants, GetValueOrDefault, GetValueOr |
| `src/ResulTus/Result{TSuccess,TError}.cs` | Create | Dual-generic: `TSuccess? _value`, `TError? _error`, `byte _isSuccess`, Match, Deconstruct, implicit both directions |
| `src/ResulTus/AssemblyInfo.cs` | Create | `[InternalsVisibleTo("ResulTus.Tests")]`, `[CLSCompliant(true)]` |
| `test/ResulTus.Tests/ResulTus.Tests.csproj` | Create | `net10.0`, xUnit, FluentAssertions, project ref to `src/ResulTus` |
| `test/ResulTus.Tests/ErrorTypeTests.cs` | Create | Default value, distinct values |
| `test/ResulTus.Tests/ErrorTests.cs` | Create | Factories, equality, None singleton, metadata |
| `test/ResulTus.Tests/ResultTests.cs` | Create | Success/failure, implicit conversion, Match, Deconstruct, TryGetError, default struct |
| `test/ResulTus.Tests/ResultTTests.cs` | Create | Implicit T/Error, TryGetValue, GetValueOrDefault, GetValueOr, Match, Deconstruct, default struct |
| `test/ResulTus.Tests/ResultTErrorTests.cs` | Create | Implicit TSuccess/TError, Match, Deconstruct, default struct |
| `test/ResulTus.Tests/Usings.cs` | Create | Global usings: `FluentAssertions`, `Xunit`, `ResulTus` |

## Interfaces / Contracts

```csharp
// ErrorType — byte enum (non-exhaustive, 10 values)
public enum ErrorType : byte { None, Failure, Validation, NotFound, Conflict,
    Unauthorized, Forbidden, Timeout, Cancelled, Unexpected }

// Error — readonly record struct
public readonly record struct Error
{
    public static Error None { get; }
    public string Code { get; }
    public string? Message { get; }
    public ErrorType Type { get; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; }
    public static Error Failure(string code, string? message = null, /* metadata */);
    // ... 8 more factory methods
}

// Result — void variant
public readonly record struct Result
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public static Result Success();
    public static Result Failure(Error error);
    public static implicit operator Result(Error error);
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure);
    public void Deconstruct(out bool isSuccess, out Error error);
    public bool TryGetError(out Error error);
}

// Result<T> — value variant
public readonly record struct Result<T>
{
    public static Result<T> Success(T value);
    public static Result<T> Failure(Error error);
    public static implicit operator Result<T>(T value);
    public static implicit operator Result<T>(Error error);
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure);
    public void Deconstruct(out bool isSuccess, out T? value, out Error error);
    public bool TryGetValue(out T? value, out Error error);
    public bool TryGetValue(out T? value);
    public T GetValueOrDefault(T defaultValue = default);
    public T GetValueOr(Func<Error, T> fallback);
}

// Result<TSuccess, TError> — dual generic
public readonly record struct Result<TSuccess, TError>
{
    public static Result<TSuccess, TError> Success(TSuccess value);
    public static Result<TSuccess, TError> Failure(TError error);
    public static implicit operator Result<TSuccess, TError>(TSuccess value);
    public static implicit operator Result<TSuccess, TError>(TError error);
    public TResult Match<TResult>(Func<TSuccess, TResult> onSuccess, Func<TError, TResult> onFailure);
    public void Deconstruct(out bool isSuccess, out TSuccess? value, out TError? error);
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | ErrorType — default, distinct values | `default(ErrorType) == None`, each value unique cast to `byte` |
| Unit | Error — factories, None, equality, metadata | FluentAssertions: `Should().Be()` / `Should().NotBe()` / `Should().BeOfType<>()` |
| Unit | Result — success, failure, implicit, Match, Deconstruct, TryGetError, default struct | Every factory, every conversion, both Match branches, default value edge case |
| Unit | Result\<T\> — implicit T/Error, TryGetValue, GetValueOrDefault, GetValueOr, Match, Deconstruct, default | Both `TryGetValue` overloads, recovery via `GetValueOr`, fallback via `GetValueOrDefault` |
| Unit | Result\<TSuccess,TError\> — implicit both directions, Match, Deconstruct, default | Both generic params as reference/value types |

No integration or E2E tests — Phase 1 is pure types with no I/O or external dependencies.

## Migration / Rollout

No migration required. All files are new — no existing data or schema to migrate. Rollback: delete all created files.

## Open Questions

None.
