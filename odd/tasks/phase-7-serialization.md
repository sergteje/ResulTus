# Phase 7 — Serialization

Objective: ship `ResulTus.Serialization` with System.Text.Json converters for `Result`, `Result<T>`, `Error` and `ErrorType`, plus a one-call registration extension. Branch: `feature-1`. Test runner: `dotnet test tests/ResulTus.Serialization.Tests/ResulTus.Serialization.Tests.csproj`.

Delivery strategy: `auto-chain` forecast — ~1 200 authored changed lines, so work-unit commits on `feature-1`; PR/merge stays the user's call.

## Why this phase matters more than the proposal suggests

`Result<T>` exposes only `IsSuccess` and `IsFailure` as public properties. Without a converter, `JsonSerializer.Serialize(Result<int>.Success(42))` produces `{"isSuccess":true,"isFailure":false}` — **the value is silently lost**, and so is the whole `Error` on the failure path. Any user who puts a `Result<T>` in a DTO, a cache or a log loses data with no warning. The roadmap puts Phase 5 (ASP.NET Core) before this one; that ordering builds an API surface on top of a broken wire format, so Phase 7 runs first.

## Wire format (the contract)

Fixed camelCase property names, independent of `JsonSerializerOptions.PropertyNamingPolicy`. Rationale: a wire format is a contract, and letting ambient options change it means the same `Result` serializes differently depending on who serializes it. Predictable beats configurable here.

### `ErrorType` — the enum name as a string

```json
"NotFound"
```

Written as the canonical enum name. Read case-insensitively. Raw numbers are **rejected** with `JsonException`: a number silently maps to a value and breaks the moment the enum is reordered.

### `Error`

```json
{"code":"missing","message":"not there","type":"NotFound"}
```

- `code` required, may be the empty string.
- `message` omitted when `null`.
- `type` required.

`Error` has a **private constructor** and only static factories, so deserialization maps `ErrorType` to its factory (`Error.NotFound(...)`, `Error.Timeout(...)`, …). Every one of the ten enum values has a factory today; an unmapped value throws `JsonException` instead of degrading silently.

### `Result` (unit)

```json
{"isSuccess":true}
{"isSuccess":false,"error":{"code":"dup","message":null,"type":"Conflict"}}
```

- `isSuccess` required.
- On failure, `error` required.
- `isFailure` is never written: it is derivable from `isSuccess`, and two fields that can disagree are a liability.

### `Result<T>`

```json
{"isSuccess":true,"value":42}
{"isSuccess":false,"error":{"code":"missing","type":"NotFound"}}
```

- `isSuccess` required.
- On success, `value` required (a `null` value is legal when `T` is nullable; the property must still be present).
- On failure, `error` required.
- Property order is not significant on read; unknown properties are skipped.

### Registration

`JsonSerializerOptions.AddResulTusConverters()` registers all four and returns the same instance for chaining.

## Specs

- **S1 — the project exists and is wired.** New `src/ResulTus.Serialization/ResulTus.Serialization.csproj` targeting `net8.0`, `RootNamespace`/`AssemblyName` = `ResulTus.Serialization`, referenced from `ResulTus.slnx`. No new package: `System.Text.Json` is in-box for `net8.0`. It MUST build under `TreatWarningsAsErrors` and `CS1591 = error`.
- **S2 — `ErrorType` round-trips as a name.** `ErrorType.NotFound` serializes to `"NotFound"` and reads back. Reading accepts any casing. Reading a number, an unknown name, or `null` throws `JsonException`.
- **S3 — `Error` round-trips.** `{"code","message","type"}` as specified above. `message` is omitted when `null`. A missing `code` or `type` throws `JsonException`.
- **S4 — the unit `Result` round-trips.** `{"isSuccess":true}` and `{"isSuccess":false,"error":{...}}`. A failure without `error` throws `JsonException`; a missing `isSuccess` throws `JsonException`.
- **S5 — `Result<T>` round-trips.** `{"isSuccess":true,"value":<T>}` and `{"isSuccess":false,"error":{...}}`. A success without `value` throws `JsonException`. Works for a value type, a reference type, a nullable value type and a nested `Result<Result<int>>`.
- **S6 — registration.** `AddResulTusConverters()` makes `JsonSerializer.Serialize`/`Deserialize` use the converters without further configuration, and returns the same options instance.
- **S7 — no silent data loss.** Round-tripping any of the four types preserves `IsSuccess`, the success value and the full `Error` (code, message, type). This is the regression the phase exists to prevent.
- **S8 — AOT and trimming stay clean where the framework allows it.** The project inherits `IsAotCompatible` and `IsTrimmable` and MUST build with 0 warnings under `TreatWarningsAsErrors`. An open-generic converter cannot be AOT-clean by design: both `JsonSerializerOptions.GetConverter(Type)` and `Type.MakeGenericType(Type[])` carry `RequiresDynamicCode`/`RequiresUnreferencedCode`, and there is no public alternative for reaching the converter of an arbitrary `T` from inside a `JsonConverter<T>`. The two suppressions are therefore `[UnconditionalSuppressMessage]`, narrowly scoped to the three members that need them, each carrying a justification that names the real limitation and points Native AOT callers at `JsonSerializerContext`. Silencing the analyzer project-wide, or claiming AOT support the code cannot deliver, is not acceptable.
- **S9 — tests cover both paths of every converter.** New `tests/ResulTus.Serialization.Tests` targeting `net10.0` with xUnit v3 + FluentAssertions, one RED observed per behavior group before its implementation.
- **S10 — no regression.** `dotnet build ResulTus.slnx` with 0 warnings and the whole suite green, including the existing 47 core, 80 extensions and 38 collections tests.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2, S3 | inline | done — RED `CS1061`, GREEN 9/9 | see git log |
| T2 | S4, S5, S8 | inline | done — RED 15 failed / 9 passed, GREEN 24/24 | see git log |
| T3 | S6 | inline | done — GREEN 42/42 | see git log |
| T4 | S7, S9, S10 | inline | done — GREEN 59/59, 96.1% lines | see git log |

## Log

### L1 — original request (verbatim)

> siguentes fases que hay?

Followed by the roadmap answer and:

> Vamos pues

So Phase 7 (Serialization) is the authorized next phase. Delivery (PR/merge) is still not authorized.

### L2 — starting evidence

- Phases 1–3 closed: `ResulTus` + `ResulTus.Extensions` + `ResulTus.Collections`, 165 tests green, solution build 0 warnings.
- `openspec/changes/phase-7-serialization/proposal.md` lists the four converters, "configurable serialization format options", a `JsonSerializerOptions` registration extension, and `test/ResulTus.Serialization.Tests` (this repo uses `tests/`).
- `ErrorType : byte` with the remark "Values are ordered by severity" — so the ordinal is meaningful internally but fragile on the wire, which is why the format uses names.
- `Error`'s constructor is `private`; only the static factories are public. The converter therefore maps `ErrorType` to a factory.
- `Result<T>`'s only public properties are `IsSuccess` and `IsFailure`, which is exactly the data-loss gap this phase closes.
- `Directory.Packages.props` has no `System.Text.Json` entry and none is needed: it ships in-box with `net8.0`.

### L3 — decisions taken while implementing (not user requirements)

- Fixed camelCase property names, not policy-driven. Documented on the extension method.
- `isFailure` is not emitted. Redundant with `isSuccess` and able to disagree with it.
- Numbers rejected for `ErrorType`; names only.
- `Error`'s `code` and `type` are required on read. A failure with no error is meaningless, and a lenient default would manufacture `Error.None` carrying a code.
- The core assembly is **not** modified: no `Error.Create` factory is added. The converter absorbs the factory mapping instead, keeping Phase 7 a new-project-only change as the proposal scopes it.
- `AddResulTusConverters` is the registration name.

### L4 — the AOT assumption was wrong, and the compiler proved it

Two suppressions were removed on the assumption that `JsonSerializerOptions.GetConverter(Type)` was safe, and the build failed with `IL3050`/`IL2026` on `ResultJsonConverterFactory.cs`. Then the suppression was removed from `CreateConverter` too, and `Type.MakeGenericType` failed the same way. Both APIs are annotated. There is no public route from inside a `JsonConverter<T>` to the converter of an arbitrary `T` that avoids them.

So the honest position is: this converter works normally and under trimming of application code, and Native AOT callers must declare their `Result<T>` instantiations through a `JsonSerializerContext`. That limitation is written on `CreateConverter`, `Write` and `Read`, and S8 was rewritten to say it instead of pretending the code is AOT-clean. Three narrow `[UnconditionalSuppressMessage]` attributes, each justified.

### L5 — test project opts out of AOT/trimming

`Directory.Build.props` sets `IsAotCompatible=true` and `IsTrimmable=true` for **every** project, test assemblies included. The reflection-based `JsonSerializer.Serialize<T>` calls in the tests therefore failed the build with `IL2026`/`IL3050`. A test assembly is never trimmed or AOT-compiled, so `tests/ResulTus.Serialization.Tests.csproj` sets both to `false` with a comment. The library's own AOT guarantee is still verified by building `ResulTus.Serialization` alone, which is warning-free.

### L6 — verification evidence

- T1 RED: `CS1061` — `AddResulTusConverters` did not exist. GREEN: 9/9.
- T2 RED: 15 failed, 9 passed. GREEN: 24/24.
- T3/T4 GREEN: 42/42, then 59/59 after closing coverage gaps.
- Coverage on `ResulTus.Serialization`: **96.1% lines, 88.6% branches** (target 95% lines). `ErrorTypeJsonConverter`, `ResultJsonConverterFactory` and `ResultJsonSerializerOptionsExtensions` at 100%.
- Remaining uncovered lines are defensive guards, not behavior: "unexpected token while reading", "unexpected end of JSON", and the `_ =>` arm of the `ErrorType` to factory switch. All three are unreachable while the reader validates the token stream; the `_ =>` arm is the safety net for a future `ErrorType` added without its factory mapping.
- Coverage gap found by measurement, not by guessing: `EveryErrorType_ShouldRoundTrip` round-trips the **enum**, not the `Error` factory mapping — only `None`, `NotFound` and `Timeout` were proven. Added `EveryErrorType_ShouldMapToItsFactory`, which is where the other seven mappings got covered.
- Full solution: `dotnet build ResulTus.slnx` → 0 warnings, 0 errors. `dotnet test ResulTus.slnx` → 224 passed (59 serialization, 47 core, 80 extensions, 38 collections). No regression in the pre-existing 165.

*The `openspec/` paths cited above belong to the retired SDD store. The originals are recoverable with `git show 75b0962:<path>`; this record quotes the scoped parts inline.*
