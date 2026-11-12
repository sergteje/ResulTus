# Getting started

ResulTus represents success and failure as one inline `readonly record struct`, with zero heap
allocation on both paths. This guide takes you from a fresh project to a working pipeline in a few
minutes.

## Install

Start with the two core packages:

```bash
dotnet add package ResulTus
dotnet add package ResulTus.Extensions
```

`ResulTus` carries the core types (`Result`, `Result<T>`, `Result<TSuccess, TError>`, `Error`,
`ErrorType`); `ResulTus.Extensions` carries the fluent operators and the conversions. The same
`dotnet add package ResulTus.X` pattern installs the optional packages — `ResulTus.Collections`,
`ResulTus.Logging`, `ResulTus.Serialization`, `ResulTus.Diagnostics`, `ResulTus.AspNetCore`,
`ResulTus.HttpClient`, `ResulTus.Validation`, `ResulTus.Testing`, and the analyzer and
source-generator packages. See the [Packages table](../README.md#packages) for the full list.

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

Success and failure are two states of one struct — no exceptions, no heap. A failed parse returns an
`Error` instead of throwing, and the value is never read through a naked property: `Match` is the
public interface here, and [`TryGetValue`, `GetValueOrDefault` and `GetValueOr`](core-concepts.md#resultt--the-value-tier)
cover the other shapes you will reach for.

## The tour setup

The rest of the tour builds on one shared setup. Every code block after it is statement-level, and
the usings, types and helpers declared here are implied by all of them:

```csharp
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResulTus;
using ResulTus.Collections;
using ResulTus.Extensions;
using ResulTus.HttpClient;
using ResulTus.Serialization;

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

public enum DomainError { NotFound, Invalid }

public sealed record Product(int Id, string Name);

[JsonSerializable(typeof(Product))]
internal partial class AppJsonContext : JsonSerializerContext;
```

The helpers are top-level local functions, so the type declarations (`DomainError`, `Product`,
`AppJsonContext`) follow them — C# requires top-level statements to precede type declarations. The
`using` directives pull in namespaces from several packages: the two core ones plus
`ResulTus.Collections`, `ResulTus.HttpClient` and `ResulTus.Serialization`, each installed with the
same `dotnet add package` pattern. The tour below only uses the pieces it needs.

## What to read next

- [Core concepts](core-concepts.md) — `Error` and the three result tiers, the default-state and
  null-success contracts, and converting between tiers.
- [Functional operations](functional-operations.md) — the fluent operators (`Map`, `Bind`,
  `Ensure`, `Recover`, `MapError`, `Tap`, `Finally`, `Switch`), synchronous and asynchronous.
- [Collections](collections.md) — `Traverse`, `Sequence`, `Fold` and `Partition` over sequences,
  arrays and read-only lists of results.
- [Error handling](error-handling.md) — structured errors, the built-in factories, the analyzers
  and the `[ErrorFactory]` source generator.
- [Serialization](serialization.md) — the `System.Text.Json` converters for `Result<T>`, `Error`
  and `ErrorType`.
- [Observability](observability.md) — structured logging and the opt-in `Activity` /
  `DiagnosticListener` diagnostics.
- [Performance](performance.md) — the measured allocation and latency numbers that back the
  contract.
- [HTTP client](httpclient.md) — turning HTTP responses, ProblemDetails documents and transport
  failures into results.
