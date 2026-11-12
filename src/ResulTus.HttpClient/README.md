# ResulTus.HttpClient — HTTP responses as results

[![NuGet](https://img.shields.io/nuget/v/ResulTus.HttpClient?label=nuget)](https://www.nuget.org/packages/ResulTus.HttpClient)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.HttpClient?label=downloads)](https://www.nuget.org/packages/ResulTus.HttpClient)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package turns three things into a [ResulTus](https://github.com/sergteje/ResulTus) result
instead of an exception: an HTTP response, an RFC 9457 ProblemDetails document, and a transport
failure. Every operation returns `Result<TSuccess, HttpClientError>` — or
`Result<HttpResponseMessage, HttpClientError>` for the raw-response form. It never retries, never
throws for ordinary HTTP failures, and never disposes or replaces your `HttpClient` or
`HttpRequestMessage`.

## Install

```bash
dotnet add package ResulTus.HttpClient
```

## Dependencies

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
  types these conversions return. It is the only dependency: the package multi-targets `net8.0`,
  `net9.0`, `net10.0` and `net11.0`, uses the built-in HTTP and JSON APIs, and does not reference
  ASP.NET Core. Its problem document type is a minimal RFC 9457 reader, not
  `Microsoft.AspNetCore.Mvc.ProblemDetails`.

## What you get

Two extension classes expose the same three result families: send and convert
(`HttpClientSendResultExtensions.SendResultAsync`) and convert a response you already hold
(`HttpResponseMessageResultExtensions.ToResultAsync`).

| Call | Returns |
| --- | --- |
| `client.SendResultAsync(request, ct)` | `Result<HttpResponseMessage, HttpClientError>` |
| `client.SendResultAsync(request, jsonTypeInfo, ct)` | `Result<TSuccess, HttpClientError>` |
| `client.SendResultAsync<TSuccess>(request, options, ct)` | `Result<TSuccess, HttpClientError>` |
| `response.ToResultAsync(ct)` | `Result<HttpResponseMessage, HttpClientError>` |
| `response.ToResultAsync(jsonTypeInfo, ct)` | `Result<TSuccess, HttpClientError>` |
| `response.ToResultAsync<TSuccess>(options, ct)` | `Result<TSuccess, HttpClientError>` |

### The raw form transfers response ownership

A raw call returns the received `HttpResponseMessage` for you to inspect and dispose. It does not
interpret the HTTP status, so a `500` is still a success at this layer — "success" means "a response
arrived".

```csharp
using System;
using System.Net.Http;
using ResulTus;
using ResulTus.HttpClient;

using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");

Result<HttpResponseMessage, HttpClientError> result = await client.SendResultAsync(request);

result.Deconstruct(out var isSuccess, out var response, out var error);
if (isSuccess)
{
    using (response)
    {
        string body = await response!.Content.ReadAsStringAsync();
        Console.WriteLine($"{(int)response!.StatusCode}: {body}");
    }
}
else
{
    Console.WriteLine($"No response arrived: {error!.Kind}.");
}
```

### The typed form reads the body

The typed `SendResultAsync` overloads deserialize a 2xx JSON body and dispose the response exactly
once, after consuming it. A non-2xx response is a `StatusError`; a 2xx body that cannot be
deserialized is an `InvalidResponse`. The typed `ToResultAsync` overloads never dispose the response
they were handed, so converting a response you already hold leaves its lifetime with you.

```csharp
using System.Net.Http;
using System.Text.Json;
using ResulTus;
using ResulTus.HttpClient;

using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");
using var response = await client.SendAsync(request);

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
Result<JsonElement, HttpClientError> product = await response.ToResultAsync<JsonElement>(options);
```

## The error model

`HttpClientError` is a sealed class that always represents a failure — it has no "no error" default
and is not collapsed into the core `Error`, whose code/message/type shape cannot preserve a status
or a problem document:

| Member | Type | Meaning |
| --- | --- | --- |
| `Kind` | `HttpClientFailureKind` | The stage at which the operation failed |
| `Status` | `HttpStatusCode?` | The actual response status, when a response was received |
| `ProblemDetails` | `HttpProblemDetails?` | A parsed RFC 9457 document, when the response carried one |
| `Exception` | `Exception?` | The originating exception, when one exists |

`HttpClientFailureKind` is a closed set of four values:

| Kind | When it is returned |
| --- | --- |
| `Transport` | The request failed before any response: DNS, connection, protocol |
| `Timeout` | `HttpClient.Timeout` elapsed |
| `StatusError` | A response arrived but its status was not 2xx |
| `InvalidResponse` | A 2xx response arrived but its body could not be read as the expected JSON |

There is deliberately no cancellation member: a cancelled caller token rethrows
`OperationCanceledException` rather than being captured as an error. Timeout and cancellation stay
apart because the runtime nests a `TimeoutException` inside the `TaskCanceledException` only when
`HttpClient.Timeout` elapses; a caller-cancelled token produces no such inner exception and is
rethrown.

```csharp
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using ResulTus;
using ResulTus.HttpClient;

using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");
using var cts = new CancellationTokenSource();

Result<JsonElement, HttpClientError> result = await client.SendResultAsync<JsonElement>(
    request,
    new JsonSerializerOptions(JsonSerializerDefaults.Web),
    cts.Token);

if (result.IsFailure)
{
    result.Deconstruct(out _, out _, out var error);

    switch (error!.Kind)
    {
        case HttpClientFailureKind.Transport:
            Console.WriteLine($"Transport failure: {error.Exception?.Message}");
            break;
        case HttpClientFailureKind.Timeout:
            Console.WriteLine("The request timed out.");
            break;
        case HttpClientFailureKind.StatusError:
            Console.WriteLine($"HTTP {(int)(error.Status ?? default)}: {error.ProblemDetails?.Title}");
            break;
        case HttpClientFailureKind.InvalidResponse:
            Console.WriteLine($"Unreadable body: {error.Exception?.Message}");
            break;
    }
}
```

## Inspecting status and ProblemDetails

A non-2xx response produces a `StatusError` carrying the exact `HttpStatusCode` and, when the
content type is `application/problem+json`, a parsed `HttpProblemDetails`. The five RFC 9457
standard members are strongly typed: `Type`, `Title`, `Status`, `Detail`, `Instance`. Any other
member — including the validation `errors` member — is retained in `Extensions` as a faithful
`JsonElement`, and `Extensions` is `null` when the document has none.

```csharp
using System;
using System.Net;
using System.Net.Http;
using ResulTus;
using ResulTus.HttpClient;

using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");
using var response = await client.SendAsync(request);

Result<HttpResponseMessage, HttpClientError> result = await response.ToResultAsync();
result.Deconstruct(out var isSuccess, out _, out var error);

if (!isSuccess && error!.Kind == HttpClientFailureKind.StatusError)
{
    HttpStatusCode? status = error.Status;

    if (error.ProblemDetails is { } problem)
    {
        Console.WriteLine($"{problem.Title} ({problem.Status})");
        Console.WriteLine(problem.Detail);
    }
    else
    {
        // Not application/problem+json, or the body was absent or malformed:
        // only the status is available.
        Console.WriteLine($"HTTP {status}");
    }
}
```

When the problem body is malformed, `ProblemDetails` stays `null` and the parsing exception is kept
on `HttpClientError.Exception`; the status is never lost.

## Ownership and disposal

The adapter creates nothing and owns nothing:

- **The caller owns and disposes the `HttpClient`.** It is never created, disposed or replaced.
- **The caller owns and disposes the `HttpRequestMessage`.** It is never disposed.
- **The raw form transfers response ownership to the caller.** `SendResultAsync` and
  `ToResultAsync` return the response untouched for the caller to inspect and dispose.
- **The typed `SendResultAsync` disposes its response** exactly once, after deserializing it. The
  typed `ToResultAsync` never disposes the response it was handed.

## No retries

The adapter performs exactly one send per call and never retries, and it never catches an arbitrary
application exception thrown by the handler. Only `HttpRequestException` is mapped to `Transport`,
only `OperationCanceledException` with an inner `TimeoutException` is mapped to `Timeout`, and any
other `OperationCanceledException` is rethrown.

## JSON and Native AOT

Each typed family has two deserialization overloads, and the difference is exactly what Native AOT
can resolve:

- **`JsonTypeInfo<TSuccess>` is the Native AOT route.** You supply the metadata a source-generated
  `JsonSerializerContext` emitted; the adapter deserializes through it and never consults ambient
  options. `TSuccess` is inferred from the metadata.
- **`JsonSerializerOptions` is the reflective convenience.** The adapter resolves the converter for
  `TSuccess` from the supplied options, which requires dynamic and unreferenced code. Native AOT
  callers should prefer the `JsonTypeInfo<TSuccess>` overload.

```csharp
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ResulTus;
using ResulTus.HttpClient;

public sealed record Product(int Id, string Name);

[JsonSerializable(typeof(Product))]
internal partial class AppJsonContext : JsonSerializerContext;

public sealed class ProductClient(HttpClient client)
{
    public Task<Result<Product, HttpClientError>> GetAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
        => client.SendResultAsync(request, AppJsonContext.Default.Product, cancellationToken);
}
```

The raw overloads take neither serializer argument, because they deserialize nothing.

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — required
  dependency: the core result types.
- [`ResulTus.AspNetCore`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.AspNetCore/README.md)
  — the server-side counterpart: renders results as RFC 9457 problem responses.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [HTTP client](https://github.com/sergteje/ResulTus/blob/main/docs/httpclient.md),
  [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
