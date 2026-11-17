# Serialization

`ResulTus.Serialization` provides `System.Text.Json` converters for the result types —
`Result<T>`, the typed tier `Result<TSuccess, TError>` and the unit `Result` — plus the `Error`
and `ErrorType` values they carry, with a fixed, strict wire format.

The wire shape is a contract, so the converters emit a fixed camelCase shape and never follow
a configured naming policy. `ErrorType` is written as its enum name (a self-describing string
that survives reordering), and on read a numeric token or an unknown name is rejected with a
`JsonException` instead of degrading to a default value.

```csharp
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResulTus;
using ResulTus.Serialization;
```

## The wire format

| Type | Success | Failure |
| --- | --- | --- |
| `Result<T>` | `{"isSuccess":true,"value":<value>}` | `{"isSuccess":false,"error":{...}}` |
| `Result<TSuccess, TError>` | `{"isSuccess":true,"value":<value>}` | `{"isSuccess":false,"error":<error>}` |
| unit `Result` | `{"isSuccess":true}` | `{"isSuccess":false,"error":{...}}` |

An `error` object is `{"code":...,"message":...,"type":...}`, with `message` omitted when it
is `null`. `type` is the `ErrorType` enum name. `isFailure` is never written — it is
derivable from `isSuccess`, and two fields that can disagree are a liability. On the typed
tier the `error` is written by the converter registered for `TError`: with `Error` it is the
same error object, with `string` it is a JSON string.

Reading is strict: a success without a `value`, a failure without an `error`, or an object
without `isSuccess` is a `JsonException` naming the missing property. Unknown properties are
skipped, and `ErrorType` names are matched case-insensitively.

## Ordinary applications: `AddResulTusConverters()`

Register the converters on a `JsonSerializerOptions` once, before its first use. The factory it
adds covers both generic tiers, `Result<T>` and `Result<TSuccess, TError>`:

```csharp
var options = new JsonSerializerOptions();
options.AddResulTusConverters();

Result<int> original = Result<int>.Success(42);
string json = JsonSerializer.Serialize(original, options);
// {"isSuccess":true,"value":42}

Result<int> back = JsonSerializer.Deserialize<Result<int>>(json, options)!;
```

The same options serialize `Error`, `ErrorType` and the unit `Result` directly:

```csharp
Error error = Error.NotFound("USER_NOT_FOUND", "No such user");
string errorJson = JsonSerializer.Serialize(error, options);
// {"code":"USER_NOT_FOUND","message":"No such user","type":"NotFound"}

string typeJson = JsonSerializer.Serialize(ErrorType.Conflict, options);
// "Conflict"

string savedJson = JsonSerializer.Serialize(Result.Success(), options);
// {"isSuccess":true}
```

`AddResulTusConverters()` returns the same `options` instance for chaining, and mutating an
options instance after its first serialization throws `InvalidOperationException`.

## The typed tier

`Result<TSuccess, TError>` is the tier `ResulTus.Validation` and `ResulTus.HttpClient` produce, and
the one an API puts in a contract. No second registration call is needed: the factory registered by
`AddResulTusConverters()` closes the converter over both type arguments. Each payload goes through
the converter registered for its own type, so the success value and the error are never
special-cased.

```csharp
internal sealed record Order(int Id);

Result<Order, Error> success = Result<Order, Error>.Success(new Order(42));
string successJson = JsonSerializer.Serialize(success, options);
// {"isSuccess":true,"value":{"Id":42}}

Result<Order, Error> failure = Result<Order, Error>.Failure(Error.NotFound("ORDER_MISSING"));
string failureJson = JsonSerializer.Serialize(failure, options);
// {"isSuccess":false,"error":{"code":"ORDER_MISSING","type":"NotFound"}}

Result<Order, Error> back = JsonSerializer.Deserialize<Result<Order, Error>>(failureJson, options)!;
```

`TError` needs no special handling beyond the converter the options already have. A plain string
error is `{"isSuccess":false,"error":"boom"}` for `Result<int, string>.Failure("boom")`, and a
success carrying a `null` reference value is `{"isSuccess":true,"value":null}`.

A default `Result<TSuccess, TError>` is a failure carrying `Error.None`, not a success hiding a
`null` value, so it round-trips as `{"isSuccess":false,"error":{"code":"","type":"None"}}`. Reading
is strict in exactly the way the section above describes.

## Trimmed and Native AOT applications

The convenience registration resolves the converter for the payload type from the ambient
options at runtime, which requires dynamic and unreferenced code. Trimmed and Native AOT
applications use one of the two source-generated routes instead. Both emit the same JSON.

Both routes share one source-generated context and one carrier payload:

```csharp
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(ConsumerCarrier))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

internal sealed record ConsumerCarrier(int Tag, Result<int> Result);
```

### The direct converter

`ResultJsonConverter<T>` has two constructors. The `JsonTypeInfo<T>` constructor stores the
metadata a source-generated `JsonSerializerContext` emitted for the value type and never
consults the ambient options for it. Call its `Write` and `Read` methods directly:

```csharp
var converter = new ResultJsonConverter<int>(AppJsonContext.Default.Int32);

using var stream = new MemoryStream();
using (var writer = new Utf8JsonWriter(stream))
{
    converter.Write(writer, Result<int>.Success(42), new JsonSerializerOptions());
}

string json = Encoding.UTF8.GetString(stream.ToArray());
// {"isSuccess":true,"value":42}
```

Reading is the mirror image:

```csharp
var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
reader.Read();
Result<int> back = converter.Read(ref reader, typeof(Result<int>), new JsonSerializerOptions());
```

The factory that `AddResulTusConverters()` registers instantiates `ResultJsonConverter<T>` and
`ResultJsonConverter<TSuccess, TError>` through their parameterless constructors, which resolve the
payload converters from the options at each call — fine in ordinary applications, but not the Native
AOT route.

### The direct converter for the typed tier

The typed tier has two generic arguments to resolve, so its converter takes one `JsonTypeInfo<T>`
per argument: `ResultJsonConverter<TSuccess, TError>`. Neither the success payload nor `TError` is
ever resolved through reflection.

```csharp
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Error))]
internal sealed partial class TypedAppJsonContext : JsonSerializerContext;

var typedOptions = new JsonSerializerOptions();
typedOptions.Converters.Add(new ErrorJsonConverter());
var typedContext = new TypedAppJsonContext(typedOptions);
var typedConverter = new ResultJsonConverter<int, Error>(typedContext.Int32, typedContext.Error);
```

The context's options must carry `ErrorJsonConverter` before the context is created: that is what
makes the generated `Error` metadata write the package's error object instead of the type's public
properties. Without it the `error` object would use the generic property shape, which is not the
wire contract. The converter then emits exactly the bytes the registered path emits.

### The source-generated carrier

To go through `JsonSerializer` itself under AOT, hand the serializer a type the context
knows: annotate a carrier record with `[JsonSerializable]`, register the `ResultJsonConverter`
on the context's options, and serialize through the context overload that takes a
`JsonTypeInfo<T>`.

```csharp
var converter = new ResultJsonConverter<int>(AppJsonContext.Default.Int32);

var carrierOptions = new JsonSerializerOptions();
carrierOptions.Converters.Add(converter);
var context = new AppJsonContext(carrierOptions);

var carrier = new ConsumerCarrier(1, Result<int>.Success(41));
string json = JsonSerializer.Serialize(carrier, context.ConsumerCarrier);
// {"Tag":1,"Result":{"isSuccess":true,"value":41}}

ConsumerCarrier back = JsonSerializer.Deserialize(json, context.ConsumerCarrier)!;
```

The carrier's `Result` member is what makes the closed `Result<int>` reachable in the
generated context; its value is read and written by the converter registered on the context's
options. This is the shape the `AotSmoke` sample publishes and runs natively, and the
serialization suite's `SourceGeneratedCarrier` test verifies.

### The plain options overload is not the AOT-safe route

`JsonSerializer.Serialize<TValue>(value, options)` — the reflection-based overload used in the
ordinary-app examples — is annotated `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`.
Even with a `ResultJsonConverter` registered on the options, calling this overload in a
trimmed or AOT-published application emits `IL2026`/`IL3050` from the .NET trimming and AOT
analyzers, because the serializer cannot prove the closed `Result<T>` is reachable without
reflection. Use the direct converter's `Read`/`Write`, or the source-generated carrier and the
context overload above, in those applications.
