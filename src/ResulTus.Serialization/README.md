# ResulTus.Serialization — System.Text.Json converters with a fixed wire format

[![NuGet](https://img.shields.io/nuget/v/ResulTus.Serialization?label=nuget)](https://www.nuget.org/packages/ResulTus.Serialization)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.Serialization?label=downloads)](https://www.nuget.org/packages/ResulTus.Serialization)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package adds `System.Text.Json` converters for the [ResulTus](https://github.com/sergteje/ResulTus)
types that carry the shared `Error`: `Result<T>`, the unit `Result`, `Error` and `ErrorType`.
The wire shape is a contract — it is always camelCase and never follows a configured naming policy.
The typed tier `Result<TSuccess, TError>` is not covered; use a custom converter or map it to a DTO.

## Install

```bash
dotnet add package ResulTus.Serialization
```

## Dependencies

[`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
types these converters read and write. `System.Text.Json` ships with .NET, so the package adds no
other package reference.

## What you get

| Type | Success | Failure |
| --- | --- | --- |
| `Result<T>` | `{"isSuccess":true,"value":<value>}` | `{"isSuccess":false,"error":{...}}` |
| unit `Result` | `{"isSuccess":true}` | `{"isSuccess":false,"error":{...}}` |

An `error` object is `{"code":...,"message":...,"type":...}`, with `message` omitted when it is
`null`. `type` is the `ErrorType` enum name, so it survives reordering. `isFailure` is never
written: it is derivable from `isSuccess`, and two fields that can disagree are a liability.

### Register the converters

`AddResulTusConverters()` registers all four converters on a `JsonSerializerOptions` and returns the
same instance. Call it once, before the options are first used — an options instance becomes
read-only after its first serialization, and mutating it afterwards throws
`InvalidOperationException`.

```csharp
using System.Text.Json;
using ResulTus;
using ResulTus.Serialization;

var options = new JsonSerializerOptions();
options.AddResulTusConverters();

string json = JsonSerializer.Serialize(Result<int>.Success(42), options);
// {"isSuccess":true,"value":42}

Result<int> back = JsonSerializer.Deserialize<Result<int>>(json, options)!;
```

The same options serialize `Error`, `ErrorType` and the unit `Result` directly:

```csharp
using System.Text.Json;
using ResulTus;
using ResulTus.Serialization;

var options = new JsonSerializerOptions().AddResulTusConverters();

string errorJson = JsonSerializer.Serialize(
    Error.NotFound("USER_NOT_FOUND", "No such user"), options);
// {"code":"USER_NOT_FOUND","message":"No such user","type":"NotFound"}

string typeJson = JsonSerializer.Serialize(ErrorType.Conflict, options);
// "Conflict"

string savedJson = JsonSerializer.Serialize(Result.Success(), options);
// {"isSuccess":true}
```

### Reading is strict

A success without a `value`, a failure without an `error`, or an object without `isSuccess` throws
a `JsonException` naming the missing property. Unknown properties are skipped, and `ErrorType` names
are matched case-insensitively — but a numeric token or an unknown name is rejected with a
`JsonException` instead of degrading to a default value.

### Trimmed and Native AOT applications

`AddResulTusConverters()` resolves the payload converter from the ambient options at runtime, which
requires dynamic and unreferenced code. Trimmed and Native AOT applications use source-generated
metadata instead: the public `ResultJsonConverter<T>` accepts a `JsonTypeInfo<T>` from a
`JsonSerializerContext` that declares the payload type, and its `Write` and `Read` methods can be
called directly or the converter can be registered on the context's options.

```csharp
using System.Text.Json.Serialization;
using ResulTus;

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(ConsumerCarrier))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}

internal sealed record ConsumerCarrier(int Tag, Result<int> Result);
```

Construct the converter from the generated metadata with
`new ResultJsonConverter<int>(AppJsonContext.Default.Int32)`, then either write through it with a
`Utf8JsonWriter` and read through a `Utf8JsonReader`, or add it to the context's options and hand
the serializer a `JsonTypeInfo<T>` — `JsonSerializer.Serialize(carrier, context.ConsumerCarrier)`.
Both routes emit the same JSON as the options route above.

The reflection-based `JsonSerializer.Serialize<TValue>(value, options)` overload is annotated
`[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`: even with the converters registered, a
trimmed or AOT-published application emits `IL2026`/`IL3050` for a closed `Result<T>`. Use the
converter's `Read`/`Write`, or serialize through a source-generated `JsonTypeInfo<T>`.

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — required
  dependency: the core result types these converters cover.
- [`ResulTus.AspNetCore`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.AspNetCore/README.md)
  — the HTTP-facing package, which projects failures onto RFC 9457 ProblemDetails.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [serialization](https://github.com/sergteje/ResulTus/blob/main/docs/serialization.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md),
  [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md),
  [getting started](https://github.com/sergteje/ResulTus/blob/main/docs/getting-started.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
