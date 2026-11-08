# ResulTus

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
| `ResulTus` | The core types: `Result`, `Result<T>`, `Result<TSuccess, TError>`, `Error`, `ErrorType`. |
| `ResulTus.Extensions` | The fluent operators (`Map`, `Bind`, `Ensure`, `Recover`, `MapError`, `Tap`, `Finally`, `Switch`) and conversions, synchronous and asynchronous. |
| `ResulTus.Collections` | Collection operations over sequences, arrays and read-only lists of results (`Traverse`, `Sequence`, `Fold`, `Partition`). |
| `ResulTus.Logging` | Structured `ILogger` extensions that emit the outcome as named fields, not a rendered string. |
| `ResulTus.Serialization` | `System.Text.Json` converters for `Result<T>`, `Error` and `ErrorType` with a fixed, strict wire format. |
| `ResulTus.Diagnostics` | Opt-in `Activity`/`DiagnosticListener` timing and tracing around result-producing operations. |
| `ResulTus.AspNetCore` | Converts results into ASP.NET Core action results and RFC 9457 ProblemDetails. |
| `ResulTus.HttpClient` | Turns HTTP responses, ProblemDetails documents and transport failures into `Result<TSuccess, HttpClientError>`. |
| `ResulTus.Validation` | Structured `ValidationErrors`/`ValidationIssue` and DataAnnotations validation. |
| `ResulTus.Validation.FluentValidation` | Runs an `IValidator<T>` and returns a `Result<T, ValidationErrors>` carrying every failure. |
| `ResulTus.Analyzers` | Roslyn analyzers (`RESULT001`–`RESULT006`) that turn result misuse into build-time diagnostics. |
| `ResulTus.SourceGenerators` | The `[ErrorFactory]` source generator (`RESULTGEN001`). |
| `ResulTus.Testing` | Framework-agnostic result assertions for tests (`ShouldSucceed`, `ShouldFail`). |

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).

## Quick start

Every code block below is statement-level; the usings, types and helpers in this one setup are implied by all of them.

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

## Fluent by construction

The operators compose because they all share one shape: a failure short-circuits the pipeline untouched, and nothing allocates on either path beyond the delegate the caller passes. Each example is a shape a reader would actually write, and each compiles against the built assemblies.

A validation pipeline that reads as a sentence — `Map` → `Bind` → `Ensure` → `Recover` → `Tap` → `Match`:

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

An error-transforming chain — `MapError` → `Recover` → `Finally`:

```csharp
Result<int> cleaned = Parse("-3")
    .MapError(error => Error.Validation("INVALID_AGE", error.Message)) // MapError
    .Recover(_ => 0)                                                   // Recover
    .Finally((value, error) =>                                         // Finally
        Console.WriteLine($"ended {value} / {error.Code}"));
```

The unit `Result` for a command that returns nothing:

```csharp
Result saved = SaveRecord(42);

if (saved.IsSuccess)
{
    Console.WriteLine("saved");
}
```

`await` composes with the same operators, over `Task` and `ValueTask`:

```csharp
Result<int> fetched = await FetchAsync(21)
    .MapAsync((n, token) => ValueTask.FromResult(n * 2))
    .EnsureAsync((n, token) => ValueTask.FromResult(n >= 0), Error.Validation("NEGATIVE"))
    .RecoverAsync((error, token) => ValueTask.FromResult(0));
```

A collections pipeline — `Traverse` → `Fold` → `Partition` over a real list:

```csharp
// Traverse maps every id through a fallible fetch, stopping at the first failure.
Result<IReadOnlyList<int>> rows = new[] { 1, 2, 3 }.Traverse(FetchRow);

// Fold reduces a sequence of results to one accumulator without allocating.
Result<int> total = new[] { FetchRow(1), FetchRow(2), FetchRow(3) }
    .Fold(0, static (sum, value) => sum + value);

// Partition splits a mixed batch so neither side is lost.
(int[] succeeded, Error[] failed) = new[] { FetchRow(1), FetchRow(2) }.Partition();
```

The generic tier carries a domain error through the same operators without dropping the typed failure:

```csharp
Result<int, DomainError> handled = Find("ok")
    .Map(value => value * 2)
    .Bind(value => value < 100
        ? Result<int, DomainError>.Success(value)
        : Result<int, DomainError>.Failure(DomainError.Invalid))
    .Ensure(value => value > 0, DomainError.Invalid)
    .Recover(_ => 0)
    .Tap(value => Console.WriteLine(value));

string summary = handled.Match(
    onSuccess: value => $"handled {value}",
    onFailure: error => $"failed {error}");
```

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

These are the recorded rows, grouped by series. Every number is quoted verbatim from [`docs/performance.md`](docs/performance.md), and each table names its measurement stamp and links its section. A series with no recorded rows is absent, never estimated.

### Creation — measured 2026-10-29, Intel Core i7-6700, .NET SDK 10.0.403, Release, `DefaultJob`

| Method | Mean | Allocated |
| --- | ---: | ---: |
| **ResulTus_Create_Success** | 0.0118 ns | **0 B** |
| **ResulTus_Create_Failure** | 0.0192 ns | **0 B** |
| ResulTus_Create_Implicit_From_Value | 0.0130 ns | 0 B |
| ResulTus_Create_Implicit_From_Error | 0.1153 ns | 0 B |
| ZeroResult_Create_Success | 0.0150 ns | 0 B |
| ZeroResult_Create_Failure | 0.3182 ns | 0 B |
| CSharpFunctionalExtensions_Create_Success | 2.2914 ns | 0 B |
| CSharpFunctionalExtensions_Create_Failure | 0.0544 ns | 0 B |
| ErrorOr_Create_Success | 0.1295 ns | 0 B |
| ErrorOr_Create_Failure | 11.6516 ns | **88 B** |
| FluentResults_Create_Success | 18.7471 ns | **112 B** |
| FluentResults_Create_Failure | 40.6202 ns | **240 B** |

[Full section](docs/performance.md#result-creation--measured-2026-10-29). Every row below 1 ns is a tie by the noise-floor rule; the meaningful result is `Create_Failure` at **0.0192 ns and 0 B**, against ErrorOr's 88 B and FluentResults' 240 B — they must allocate an error object, ResulTus does not.

### Matching — measured 2026-10-29, Intel Core i7-6700, .NET SDK 10.0.403

| Method | Mean | Allocated |
| --- | ---: | ---: |
| **ResulTus_Match_Success** | 0.9235 ns | **0 B** |
| **ResulTus_Match_Failure** | 1.3834 ns | **0 B** |
| ErrorOr_Match_Success | **0.7175 ns** | 0 B |
| ErrorOr_Match_Failure | **0.5482 ns** | 0 B |
| ZeroResult_Match_Success | 0.9391 ns | 0 B |
| ZeroResult_Match_Failure | **0.7818 ns** | 0 B |
| CSharpFunctionalExtensions_Match_Success | 1.9599 ns | 0 B |
| CSharpFunctionalExtensions_Match_Failure | 1.8911 ns | 0 B |
| FluentResults_Match_Success | 21.4166 ns | **96 B** |
| FluentResults_Match_Failure | 19.0831 ns | **88 B** |

[Full section](docs/performance.md#result-matching--measured-2026-10-29). The settled wins are structural (2.1x/1.4x over CSharpFunctionalExtensions, 23x/14x over FluentResults with 0 B against 96/88 B). ErrorOr and the ZeroResult failure path are recorded as *unresolved* — the magnitude did not reproduce across runs on this hardware, so no verdict is claimed.

### Allocation, success path — measured 2026-10-29, Intel Core i7-6700, .NET SDK 10.0.403

| Method | Mean | Allocated |
| --- | ---: | ---: |
| Error_NotFound_Allocation | 0.0031 ns | 0 B |
| **ResulTus_Success_Allocation** | 0.0187 ns | **0 B** |
| **ResulTus_Failure_Allocation** | 0.0000 ns | **0 B** |
| ErrorOr_Success_Allocation | 0.0109 ns | 0 B |
| ZeroResult_Success_Allocation | 0.0000 ns | 0 B |
| ZeroResult_Failure_Allocation | 0.2633 ns | 0 B |
| FluentResults_Success_Allocation | 16.8744 ns | **112 B** |

[Full section](docs/performance.md#allocation--measured-2026-10-29). The nanosecond column is eliminated code, not a ranking; the allocation rule is the floor, and ResulTus allocates 0 B on success and on failure.

### Allocation, failure path from a unique message — measured 2026-11-06, Intel Core i7-6700, .NET SDK 10.0.403, BenchmarkDotNet 0.15.8

| Method | Mean run A | Mean run B | Allocated | Ratio |
| --- | ---: | ---: | ---: | ---: |
| **ResulTus_Failure_Unique_Message** | 2.807 ns | 2.830 ns | **0 B** | 1.00 |
| ErrorOr_Failure_Unique_Message | 15.159 ns | 14.955 ns | **88 B** | 5.3x |
| ZeroResult_Failure_Unique_Message | 8.859 ns | 8.268 ns | **32 B** | 3.0x |
| FluentResults_Failure_Unique_Message | 50.266 ns | 51.690 ns | **272 B** | 18x |
| CSharpFunctionalExtensions_Failure_Unique_Message | 3.033 ns | 3.053 ns | **0 B** | 1.08x |

[Full section](docs/performance.md#failure-path-from-a-unique-message--measured-2026-11-06). This is the failure-path head-to-head with a distinct message on every invocation. ResulTus wins allocation against three of four and ties the fourth: `CSharpFunctionalExtensions` allocates nothing because its error is a plain `string`, while ResulTus carries `Code`, `Message` and `Type` inline for the same 0 B.

### Struct size — the 32 B default tier is a documented loss

Measured 2026-10-29 ([§4.2](docs/performance.md#42-struct-size-is-a-design-problem-not-a-match-problem--resolved-2026-10-28)):

| Element type | Kind | `SizeOf` |
| --- | --- | ---: |
| `ResulTus.Result<int>` | struct | 32 |
| `ResulTus.Error` | struct | 24 |
| `ResulTus.Result<int, EightByteError>` | struct | **13** |
| `ErrorOr.ErrorOr<int>` | struct | 16 |
| `ErrorOr.Error` | struct | 32 |
| `ZeroResult.Result<int, SingleError>` | struct | 16 |
| `ZeroResult.Errors.SingleError` | **class** | 8 |
| `FluentResults.Result<int>` | **class** | 8 |
| `CSharpFunctionalExtensions.Result<int>` | struct | 16 |

`Result<int>` is **32 B against their 16 B** — the only one of the five that keeps a rich error inline, and the reason 0 B on both paths is possible at all. The generic tier is the other side of the trade: **13 B**, tighter than every competitor.

### Array traversal — a documented loss, shown as a number

Measured 2026-10-29, `DefaultJob` ([§4.4](docs/performance.md#44-array-traversal-against-competitors--measured-2026-10-29)), success path, ns per whole array:

| N | ResulTus 32 B | ErrorOr 16 B | ZeroResult 16 B | CSharpFunctionalExtensions 16 B | FluentResults 8 B (class) |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 000 | 804.9 | **644.4** | **551.6** | **546.7** | 20,806.2 — 96 KB allocated |
| 7 000 | 5,711.2 | **4,239.4** | **4,506.9** | **4,221.4** | 159,103.7 — 672 KB allocated |
| 10 000 | 8,886.7 | **6,526.1** | **6,178.2** | **6,338.7** | 250,669.9 — 960 KB allocated |

On the success path ResulTus is **1.25x–1.47x slower per element** than every 16-byte competitor; on the failure path it is 1.08x–1.26x slower. The cause is the 32 B stride — two elements per 64-byte cache line instead of four. That is the deliberate price of the rich inline error, and it is stated here, not in a footnote. FluentResults is the exception that proves the point: 8 B per element and 25x–28x slower, because each element is a heap object it also allocates by the kilobyte.

### Collections — measured 2026-11-06, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14, BenchmarkDotNet 0.15.8

Array source, N = 1,000 fully successful elements:

| Method | Source | Mean | Allocated |
| --- | --- | ---: | ---: |
| Sequence | Array | 4.121 μs | 4,056 B |
| Traverse | Array | 2.060 μs | 4,024 B |
| Fold | Array | 1.128 μs | 0 B |
| FirstFailure | Array | 863.2 ns | 0 B |
| Partition | Array | 3.095 μs | 12,480 B |
| Combine (unit `Result`) | Array | 845.5 ns | 0 B |

[Full section](docs/performance.md#collections--array-versus-list-at-n--1000--measured-2026-11-06). `Fold`, `FirstFailure` and unit `Combine` allocate nothing; `Sequence` and `Traverse` allocate their output array. `Partition` in this baseline still allocates 12,480 B because it grows two lists: the later P1/T5 change rents pooled buffers and cuts that to **4,048 B**, the two exactly sized output arrays. `List<T>` is slower than `T[]` in all six baseline rows. Two later measurements stay visible here rather than buried: the indexed array `Traverse` is slower than its `IEnumerable<T>` control (7.447 μs against 3.055 μs, saving 32 B), and the pooled `Partition` is reproducibly ~30% slower on a `List<T>` source. Both are documented structural differences in [§7](docs/performance.md#7-known-open-gaps), accepted because the allocation floor is mandatory.

### Serialization — measured 2026-11-06, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14

| Method | Mean | Allocated |
| --- | ---: | ---: |
| Serialize `Result<int>` success | 177.8 ns | 80 B |
| Serialize `Result<int>` failure | 310.9 ns | 224 B |
| Deserialize `Result<int>` success | 212.3 ns | 0 B |
| Deserialize `Result<int>` failure | 500.20 ns | 112 B |
| Serialize `Result<int>[1000]` | 85.444 μs | 61,808 B |
| Deserialize `Result<int>[1000]` | 159.317 μs | 97,680 B |
| Serialize `Error` | 246.1 ns | 176 B |
| Deserialize `Error` | 343.33 ns | 120 B |
| Serialize `ErrorType` | 110.9 ns | 48 B |
| Deserialize `ErrorType` | 81.57 ns | 0 B |

[Full section](docs/performance.md#serialization--p0-baseline-and-p1-remeasurements--measured-2026-11-06). A satellite baseline, not a cross-library ranking: `Result<int>` success deserialization allocates 0 B, and the 1,000-element rows include the JSON string or the materialized array.

### Logging — measured 2026-11-06, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14

| Method | Enabled | Mean | Allocated |
| --- | --- | ---: | ---: |
| LogFailure | false | 5.057 ns | 0 B |
| LogFailure | true | 26.054 ns | 64 B |

[Full section](docs/performance.md#logging--enabled-versus-disabled-failure-logging--measured-2026-11-06). The disabled-level path allocates 0 B; the enabled path allocates the 64 B structured logging state, with no rendered-message allocation.

### Validation — measured 2026-11-06, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14

| Method | Mean | Allocated |
| --- | ---: | ---: |
| ToDictionary | 21.385 μs | 31,624 B |
| ToError | 33.04 ns | 80 B |

[Full section](docs/performance.md#validation-conversions--1000-issues--measured-2026-11-06). `ToDictionary` materializes the grouped dictionary and per-field message arrays over 1,000 issues; `ToError` creates the summary message and a value-type `Error`.

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

## License

[MIT](LICENSE)
