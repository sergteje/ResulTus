# ResulTus

[![CI](https://github.com/sergteje/ResulTus/actions/workflows/ci.yml/badge.svg?branch=main&label=CI)](https://github.com/sergteje/ResulTus/actions/workflows/ci.yml)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus?label=downloads)](https://www.nuget.org/packages/ResulTus)
[![NuGet](https://img.shields.io/nuget/v/ResulTus?label=nuget)](https://www.nuget.org/packages/ResulTus)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/branding/logo-dark.png">
  <img src="assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="540">
</picture>

**English** · [Español](README.es.md)

ResulTus is a high-performance `Result` library for .NET that represents success or failure as an inline `readonly record struct` with zero heap allocation on both paths.

## The contract

Every claim below is a measured fact, not a promise. Each number links to the row in [`docs/performance.md`](docs/performance.md) that recorded it.

- **Zero heap allocation on the success path *and* on the failure path.** Building a success and building a failure both allocate [0 B](docs/performance.md#allocation--measured-2026-10-29), including when the failure is constructed from a distinct, uncached message.
- **`Error` is a 24-byte inline `readonly record struct`** holding a code, an optional message and a category — no untyped payload, no heap ([§4.2](docs/performance.md#42-struct-size-is-a-design-problem-not-a-match-problem--resolved-2026-10-28)).
- **`Result<int>` is 32 bytes**, exactly two results to a 64-byte cache line with no straddling. The inline `Error` is what makes 0 B possible; the 32 B is the deliberate price of keeping it inline rather than behind a reference ([§4.2](docs/performance.md#42-struct-size-is-a-design-problem-not-a-match-problem--resolved-2026-10-28)).
- **The generic tier `Result<TSuccess, TError>` is 13 bytes** with a reference-free error type, tighter than every competitor's 16 B ([§4.2](docs/performance.md#42-struct-size-is-a-design-problem-not-a-match-problem--resolved-2026-10-28), [§4.5](docs/performance.md#45-generic-tier--packed-13-b-versus-aligned-16-b-measured-2026-10-29)).

Those four facts are a trade, stated openly: the 32 B default tier and its array-traversal cost are shown alongside the wins in [Benchmarks](#benchmarks). Nothing here is hidden.

## Packages

| Package | Purpose |
| --- | --- |
| [`ResulTus`](src/ResulTus/README.md) | The core types: `Result`, `Result<T>`, `Result<TSuccess, TError>`, `Error`, `ErrorType`. |
| [`ResulTus.Extensions`](src/ResulTus.Extensions/README.md) | The fluent operators (`Map`, `Bind`, `Ensure`, `Recover`, `MapError`, `Tap`, `Finally`, `Switch`) and conversions, synchronous and asynchronous. |
| [`ResulTus.Collections`](src/ResulTus.Collections/README.md) | Collection operations over sequences, arrays and read-only lists of results (`Traverse`, `Sequence`, `Fold`, `Partition`). |
| [`ResulTus.Logging`](src/ResulTus.Logging/README.md) | Structured `ILogger` extensions that emit the outcome as named fields, not a rendered string. |
| [`ResulTus.Serialization`](src/ResulTus.Serialization/README.md) | `System.Text.Json` converters for `Result<T>`, `Error` and `ErrorType` with a fixed, strict wire format. |
| [`ResulTus.Diagnostics`](src/ResulTus.Diagnostics/README.md) | Opt-in `Activity`/`DiagnosticListener` timing and tracing around result-producing operations. |
| [`ResulTus.AspNetCore`](src/ResulTus.AspNetCore/README.md) | Converts results into ASP.NET Core action results and RFC 9457 ProblemDetails. |
| [`ResulTus.HttpClient`](src/ResulTus.HttpClient/README.md) | Turns HTTP responses, ProblemDetails documents and transport failures into `Result<TSuccess, HttpClientError>`. |
| [`ResulTus.Validation`](src/ResulTus.Validation/README.md) | Structured `ValidationErrors`/`ValidationIssue` and DataAnnotations validation. |
| [`ResulTus.Validation.FluentValidation`](src/ResulTus.Validation.FluentValidation/README.md) | Runs an `IValidator<T>` and returns a `Result<T, ValidationErrors>` carrying every failure. |
| [`ResulTus.Analyzers`](src/ResulTus.Analyzers/README.md) | Roslyn analyzers (`RESULT001`–`RESULT006`) that turn result misuse into build-time diagnostics. |
| [`ResulTus.SourceGenerators`](src/ResulTus.SourceGenerators/README.md) | The `[ErrorFactory]` source generator (`RESULTGEN001`). |
| [`ResulTus.Testing`](src/ResulTus.Testing/README.md) | Framework-agnostic result assertions for tests (`ShouldSucceed`, `ShouldFail`). |

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).

Every package above is **Stable** and shipped at 1.1.1 on [nuget.org](https://www.nuget.org/profiles/sergteje) — there are no preview or planned entries in the table. Every package ships its own focused README (linked in the table and packed into the `.nupkg`). The core plus `ResulTus.Extensions` are all you need to start:

```bash
dotnet add package ResulTus
dotnet add package ResulTus.Extensions
```

The same pattern installs any other package in the table (`dotnet add package ResulTus.Serialization`, `ResulTus.AspNetCore`, …).

## Quick start

In 30 seconds, with just those two packages:

```csharp
using ResulTus;
using ResulTus.Extensions;

Result<int> parsed = int.TryParse("42", out var value)
    ? Result<int>.Success(value)
    : Result<int>.Failure(Error.Validation("NOT_A_NUMBER"));

Result<string> message = parsed.Map(v => $"Value: {v}");

string output = message.Match(
    onSuccess: v => v,
    onFailure: e => e.Code);                // "Value: 42"

Result<int> notFound = Error.NotFound("USER_NOT_FOUND");   // failure, no exception
```

Success and failure are two states of one struct — no exceptions, no heap. The rest of this tour builds on one setup; every code block below is statement-level and the usings, types and helpers in it are implied by all of them.

```csharp
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResulTus;
using ResulTus.Collections;
using ResulTus.Extensions;
using ResulTus.HttpClient;
using ResulTus.Serialization;

public enum DomainError { NotFound, Invalid }

public sealed record Product(int Id, string Name);

[JsonSerializable(typeof(Product))]
internal partial class AppJsonContext : JsonSerializerContext;

static Result<int> Parse(string text) =>
    int.TryParse(text, out var n)
        ? Result<int>.Success(n)
        : Result<int>.Failure(Error.Validation("NOT_A_NUMBER"));

static Result<int, DomainError> Find(string key) =>
    key == "ok"
        ? Result<int, DomainError>.Success(1)
        : Result<int, DomainError>.Failure(DomainError.NotFound);

static Result<int> FetchRow(int id) =>
    id > 0
        ? Result<int>.Success(id * 10)
        : Result<int>.Failure(Error.Validation("BAD_ROW"));

static Result SaveRecord(int id) =>
    id <= 0
        ? Result.Failure(Error.Validation("BAD_ID"))
        : Result.Success();

static ValueTask<Result<int>> FetchAsync(int id) =>
    ValueTask.FromResult(Result<int>.Success(id));
```

Success and failure are two states of one struct — no exceptions, no heap:

```csharp
Result<int> ok = Result<int>.Success(42);
Result<int> notFound = Result<int>.Failure(Error.NotFound("USER_NOT_FOUND", "No user with that id"));

string verdict = notFound.Match(
    onSuccess: value => $"found {value}",
    onFailure: error => error.Code);        // "USER_NOT_FOUND"

if (ok.TryGetValue(out var value, out var error))
{
    Console.WriteLine($"got {value}");      // got 42
}
else
{
    Console.WriteLine(error.Code);
}
```

The value is never a naked property to read: `Match`, `TryGetValue`, `GetValueOrDefault` and `GetValueOr` are the public interface, and the analyzer `RESULT002` warns when `GetValueOrDefault` is read without checking the outcome first.

A result flows through one extension pipeline:

```csharp
Result<string> pipeline = Parse("42")             // Result<int>
    .Map(n => n * 2)                              // Result<int>
    .Ensure(n => n > 0, Error.Validation("NOT_POSITIVE"))
    .Map(n => $"doubled to {n}");                 // Result<string>
```

The generic tier carries a custom error type instead of the shared `Error`:

```csharp
Result<int, DomainError> found = Find("ok");

string text = found.Match(
    onSuccess: value => $"value {value}",
    onFailure: error => $"failed {error}");
```

A `System.Text.Json` round trip goes through the converters in `ResulTus.Serialization`:

```csharp
var options = new JsonSerializerOptions();
options.AddResulTusConverters();

Result<int> original = Result<int>.Success(42);
string json = JsonSerializer.Serialize(original, options);        // {"isSuccess":true,"value":42}
Result<int> back = JsonSerializer.Deserialize<Result<int>>(json, options)!;
```

The wire shape is fixed: `isSuccess`, `value` and `error` are emitted in camelCase and never follow a configured naming policy, so the same result serializes identically everywhere.

## Guides

The deep tours live in focused guides, each one verified against the shipped code:

| Guide | Content |
| --- | --- |
| [`docs/getting-started.md`](docs/getting-started.md) | Install, the 30-second start, the full tour setup. |
| [`docs/core-concepts.md`](docs/core-concepts.md) | `Error`, the three tiers, the default and null-success contracts, conversions. |
| [`docs/functional-operations.md`](docs/functional-operations.md) | Every sync and async operator, the composition laws, the allocation contract. |
| [`docs/collections.md`](docs/collections.md) | The collection operations per tier, and the two `Combine`s. |
| [`docs/error-handling.md`](docs/error-handling.md) | Typed errors, `[ErrorFactory]` code generation, validation, the analyzers. |
| [`docs/serialization.md`](docs/serialization.md) | The `System.Text.Json` wire format, registration, and the AOT-safe routes. |
| [`docs/observability.md`](docs/observability.md) | Structured logging fields, opt-in `Activity`/`DiagnosticListener` timing. |
| [`docs/testing.md`](docs/testing.md) | `ShouldSucceed`/`ShouldFail` and the repo's test conventions. |
| [`docs/performance.md`](docs/performance.md) | Every measured number, its stamp and the methodology. |
| [`docs/httpclient.md`](docs/httpclient.md) | The HTTP module guide. |

## The operators in one pipeline

The operators compose because they all share one shape: a failure short-circuits the pipeline untouched, and nothing allocates on either path beyond the delegate the caller passes. A validation pipeline that reads as a sentence — `Map` → `Bind` → `Ensure` → `Recover` → `Tap` → `Match`:

```csharp
string outcome = Parse("21")
    .Map(age => age + 1)                                    // Map
    .Bind(age => age < 0
        ? Result<int>.Failure(Error.Validation("NEGATIVE_AGE"))
        : Result<int>.Success(age))                         // Bind
    .Ensure(age => age >= 18, Error.Forbidden("UNDERAGE"))  // Ensure
    .Recover(error => error.Code == "UNDERAGE" ? 18 : -1)   // Recover
    .Tap(age => Console.WriteLine($"accepted {age}"))       // Tap
    .Match(
        onSuccess: age => $"age {age}",
        onFailure: error => $"rejected: {error.Code}");     // Match
```

`await` composes with the same operators over `Task` and `ValueTask`:

```csharp
Result<int> fetched = await FetchAsync(21)
    .MapAsync((n, token) => ValueTask.FromResult(n * 2))
    .EnsureAsync((n, token) => ValueTask.FromResult(n >= 0), Error.Validation("NEGATIVE"))
    .RecoverAsync((error, token) => ValueTask.FromResult(0));
```

The operators have `Async` twins over `Task` and `ValueTask` — `Map`, `Bind`, `MapError`, `Ensure`,
`Recover`, `Tap`, `OnFailure`, `Switch`, `Match`, `Finally` — with cancellation-aware delegates and
the token honoured before the awaited work runs. Two deliberate asymmetries: `OnSuccessAsync` exists
on the typed tier only (the unit and value tiers expose `TapAsync`), and the unit tier's `BindAsync`
is value-producing only. The typed tier takes one `Ensure` shape (the error value you already hold);
the unit and `Result<T>` tiers also carry the error-factory shapes. A cancelled token surfaces as
`OperationCanceledException`, never as a failure. The full operator surface, the composition laws and
the zero-allocation contract are in [`docs/functional-operations.md`](docs/functional-operations.md).

## What sets it apart

Each differentiator is a verifiable fact, traceable to a file in `src/`.

### Analyzers that catch result misuse at build time

`ResulTus.Analyzers` ships six rules whose ids are part of the public contract (`src/ResulTus.Analyzers/DiagnosticDescriptors.cs`):

| Id | Title | Severity |
| --- | --- | --- |
| `RESULT001` | Result is ignored | Warning |
| `RESULT002` | Outcome is not checked | Warning |
| `RESULT003` | Deconstruction discards the error | Warning |
| `RESULT004` | Bind is a Map | Info |
| `RESULT005` | Result nested in a Result | Warning |
| `RESULT006` | Ensure predicate is constant | Warning |

### A source generator for error factories

`[ErrorFactory]` turns a declared factory method into a generated body. Declare the error as a `static partial` method on a type marked `[ResultErrors]`, and `ResulTus.SourceGenerators` emits the implementation that calls the matching `Error` factory (`src/ResulTus.SourceGenerators/ErrorFactoryGenerator.cs`, `FactoryEmitter.cs`):

```csharp
using ResulTus;

[ResultErrors]
public static partial class UserErrors
{
    [ErrorFactory("USER_NOT_FOUND", ErrorType.NotFound, "User {id} not found")]
    public static partial Error NotFound(Guid id);
}
```

The message template is rendered as an interpolated string, so the emitted body is `Error.NotFound("USER_NOT_FOUND", $"User {id} not found")` (with the `global::` qualifiers the emitter writes omitted for readability).

A declaration the generator cannot emit is reported as `RESULTGEN001`, an error naming the offending member and the reason.

### Structured logging, not rendered strings

`ResulTus.Logging` hands the logger an `IReadOnlyList<KeyValuePair<string, object?>>` (`src/ResulTus.Logging/ResultLogState.cs`): `ResultType` and `IsSuccess` are always present, and `ErrorType`, `ErrorCode` and `ErrorMessage` appear only on failure. A provider reads `ErrorCode` as a named field instead of parsing it back out of text. A disabled log level allocates nothing.

### Opt-in `Activity` and `DiagnosticSource` diagnostics

`ResulTus.Diagnostics` publishes through a single `ActivitySource` and `DiagnosticListener` named `ResulTus` (`src/ResulTus.Diagnostics/ResultDiagnostics.cs`). Nothing is instrumented unless the caller writes `Measure`, and a measured call allocates nothing when nobody is listening.

### Strict JSON that refuses to guess

`ResulTus.Serialization` serializes `ErrorType` as its enum name and, on read, rejects a numeric token and any unknown name with a `JsonException` instead of degrading to a default value (`src/ResulTus.Serialization/ErrorTypeJsonConverter.cs`). Names are matched case-insensitively; a bare number never round-trips to an `ErrorType`.

### Measured against the alternatives

The comparison set is fixed in the charter: [ZeroResult](https://www.nuget.org/packages/ZeroResult), [ErrorOr](https://www.nuget.org/packages/ErrorOr), [FluentResults](https://www.nuget.org/packages/FluentResults) and [CSharpFunctionalExtensions](https://www.nuget.org/packages/CSharpFunctionalExtensions). Where ResulTus wins it is measured; where it loses, the loss is shown in [Benchmarks](#benchmarks). Two facts shape the honest picture: ZeroResult reaches 16 B by storing its `SingleError` *class* behind a reference, so its failure path allocates unless the caller reuses a cached instance; and FluentResults is class-based, so every result is a heap object.

> Verification note: ErrorOr and ZeroResult were checked against their published READMEs on 2026-11-06; FluentResults and CSharpFunctionalExtensions are stated as public-API knowledge.

## Benchmarks

Every number lives in the [performance charter](docs/performance.md): the recorded rows grouped by series, each table with its measurement stamp, and the losses shown next to the wins — `Result<int>` at 32 B against the 16 B tier, array traversal 1.25x-1.47x slower on the success path. The charter is the authority; nothing here is quoted a second time.

## Runtime compatibility

Libraries target `net8.0`, `net9.0`, `net10.0` and `net11.0`; the analyzers and the source generator target
`netstandard2.0`. Trimming and Native AOT are **verified by an observed publish-and-run** on every
CI push (jobs `aot-smoke` and `aot-smoke-linux`, runners `windows-latest` and `ubuntu-24.04`; e.g. run
[43127928009](https://github.com/sergteje/ResulTus/actions/runs/43127928009)): a console application published both trimmed and natively — on Windows and on Linux — executes the core result types, the AOT-safe System.Text.Json surface — `ResultJsonConverter<T>(JsonTypeInfo<T>)` — and the end-to-end `JsonSerializer` route through a source-generated carrier with the converter registered on the context's options, passing every assertion. Register converters through that surface in AOT applications; the factory-based convenience registration remains the ordinary path for non-AOT apps.

## HTTP responses as results

`ResulTus.HttpClient` is a separate, optional package that turns an HTTP response, an RFC 9457 ProblemDetails document and a transport failure into a result instead of an exception. It never retries, never throws for ordinary HTTP failures, and never disposes the caller's `HttpClient` or `HttpRequestMessage`. The full module is documented in [`docs/httpclient.md`](docs/httpclient.md).

```csharp
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");

Result<Product, HttpClientError> result =
    await client.SendResultAsync(request, AppJsonContext.Default.Product);

result.Deconstruct(out var isSuccess, out var product, out var error);
if (isSuccess)
{
    Console.WriteLine(product!.Name);
}
else
{
    Console.WriteLine($"HTTP failure: {error!.Kind}");
}
```

`SendResultAsync` maps transport failures to `HttpClientFailureKind.Transport`, client timeouts to `Timeout`, a non-2xx status to `StatusError` (carrying the parsed `HttpProblemDetails` when one is present), and a malformed 2xx body to `InvalidResponse`. Caller cancellation is control flow, not an HTTP failure: a cancelled token rethrows `OperationCanceledException` and is never folded into an `HttpClientError`.

## Contributing

Contributions are welcome — read [`CONTRIBUTING.md`](CONTRIBUTING.md) first: it covers the
prerequisites (SDK 11 plus the .NET 10 runtime), the ODD workflow with `odd/tasks/*.md` feature
documents, the allocation and coverage rules that decide how code is written here, and the
conventions for tests, docs and commits.

## License

[MIT](LICENSE)
