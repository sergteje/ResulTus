# ResulTus.HttpClient

`ResulTus.HttpClient` is the optional client-side adapter for the `ResulTus` result type. It turns three things into a `Result` instead of an exception: an HTTP response, an RFC 9457 ProblemDetails document, and a transport failure. It never retries, never throws for ordinary HTTP failures, and never touches the caller's `HttpClient` or `HttpRequestMessage` beyond what it was handed.

The adapter is framework-independent: it targets `net8.0`, `net9.0`, `net10.0` and `net11.0`, references only `ResulTus` and the built-in HTTP/JSON APIs, and does not reference ASP.NET Core. Its ProblemDetails type is a minimal RFC 9457 reader, not `Microsoft.AspNetCore.Mvc.ProblemDetails`.

All types live in the `ResulTus.HttpClient` namespace. Every operation returns a `Result<TSuccess, HttpClientError>`, except the raw-response operations, which return `Result<HttpResponseMessage, HttpClientError>`. The error carries four pieces of context:

| Member | Type | Meaning |
| --- | --- | --- |
| `HttpClientError.Kind` | `HttpClientFailureKind` | The stage at which the operation failed. |
| `HttpClientError.Status` | `HttpStatusCode?` | The actual response status, when a response was received. |
| `HttpClientError.ProblemDetails` | `HttpProblemDetails?` | A parsed RFC 9457 document, when the response carried one. |
| `HttpClientError.Exception` | `Exception?` | The originating exception, when one exists. |

`HttpClientFailureKind` is a closed set of four values:

| Kind | When it is returned |
| --- | --- |
| `Transport` | The request failed before any response: DNS, connection, protocol. |
| `Timeout` | `HttpClient.Timeout` elapsed. |
| `StatusError` | A response arrived but its status was not 2xx. |
| `InvalidResponse` | A 2xx response arrived but its body could not be read as the expected JSON. |

There is deliberately no cancellation member: a cancelled caller token rethrows `OperationCanceledException` rather than being captured as an error.

```csharp
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResulTus;
using ResulTus.HttpClient;
```

The examples below use this payload type and its source-generated metadata:

```csharp
public sealed record Product(int Id, string Name);

[JsonSerializable(typeof(Product))]
internal partial class AppJsonContext : JsonSerializerContext;
```

## The three result families

There are three ways to obtain a result, matching the three overload families on the two extension classes: `HttpClientSendResultExtensions.SendResultAsync` (send **and** convert) and `HttpResponseMessageResultExtensions.ToResultAsync` (convert an existing response).

### 1. Raw — the response itself

The raw `SendResultAsync` overload returns the received `HttpResponseMessage` for caller inspection and disposal. HTTP status is not interpreted here: a 500 is still a *success* at this layer, because "success" means "a response arrived".

```csharp
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");

Result<HttpResponseMessage, HttpClientError> result = await client.SendResultAsync(request);

result.Deconstruct(out var isSuccess, out var response, out var error);
if (isSuccess)
{
    using (response)
    {
        string body = await response!.Content.ReadAsStringAsync();
        Console.WriteLine($"{(int)response.StatusCode}: {body}");
    }
}
else
{
    Console.WriteLine($"No response arrived: {error!.Kind}.");
}
```

### 2. Typed — deserialized JSON

The typed `SendResultAsync` overloads deserialize a 2xx JSON body into `TSuccess` and dispose the response they received. A non-2xx response is a `StatusError`; a 2xx body that fails to deserialize is an `InvalidResponse`.

```csharp
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");

Result<Product, HttpClientError> result =
    await client.SendResultAsync(request, AppJsonContext.Default.Product);

result.Deconstruct(out var isSuccess, out var product, out var error);
if (isSuccess)
{
    Console.WriteLine($"{product!.Id}: {product.Name}");
}
else
{
    Console.WriteLine($"Request failed with {error!.Kind}.");
}
```

### 3. Value-returning — convert a response you already sent

When you already hold an `HttpResponseMessage` — from a plain `SendAsync`, a unit test, or a middleware pipeline — use `ToResultAsync` to convert it without re-sending. None of these conversions dispose the caller-owned response.

```csharp
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");

using var response = await client.SendAsync(request);
Result<Product, HttpClientError> result = await response.ToResultAsync(AppJsonContext.Default.Product);
```

## Inspecting status and ProblemDetails

A non-2xx response produces a `StatusError` carrying the exact `HttpStatusCode` and, when the response content type is `application/problem+json`, a parsed `HttpProblemDetails`. The five RFC 9457 standard members (`Type`, `Title`, `Status`, `Detail`, `Instance`) are strongly typed; any other member — including the validation `errors` member — is retained in `Extensions` as a faithful `JsonElement`.

```csharp
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");

Result<Product, HttpClientError> result =
    await client.SendResultAsync(request, AppJsonContext.Default.Product);

result.Deconstruct(out var isSuccess, out _, out var error);
if (!isSuccess && error!.Kind == HttpClientFailureKind.StatusError)
{
    HttpStatusCode? status = error.Status;
    HttpProblemDetails? problem = error.ProblemDetails;

    if (problem is not null)
    {
        Console.WriteLine($"{problem.Title} ({problem.Status})");
        Console.WriteLine(problem.Detail);

        if (problem.Extensions is { } extensions)
        {
            foreach (var (name, value) in extensions)
            {
                Console.WriteLine($"{name}: {value.GetRawText()}");
            }
        }
    }
    else
    {
        // The response was not application/problem+json, or its body was
        // absent/malformed: only the status is available.
        Console.WriteLine($"HTTP {status}");
    }
}
```

When the problem body is malformed, `ProblemDetails` stays `null` and the parsing exception is retained on `HttpClientError.Exception`; the status is never lost.

## Transport, timeout and cancellation

Transport and timeout failures are captured in the result. Caller cancellation is not — it is control flow and rethrows.

```csharp
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");
using var cts = new CancellationTokenSource();

Result<Product, HttpClientError> result =
    await client.SendResultAsync(request, AppJsonContext.Default.Product, cts.Token);

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

The timeout-versus-cancellation discrimination is what keeps these two paths apart. `HttpRequestException` maps to `Transport`. `HttpClient.Timeout` elapsing surfaces as a `TaskCanceledException` whose `InnerException` is a `TimeoutException`, which the adapter maps to `Timeout`. A cancelled caller token surfaces as an `OperationCanceledException` with no such inner exception, which the adapter **rethrows**:

```csharp
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    Result<Product, HttpClientError> result =
        await client.SendResultAsync(request, AppJsonContext.Default.Product, cts.Token);
}
catch (OperationCanceledException)
{
    // The caller token was cancelled — control flow, not an HTTP error.
    // HttpClient.Timeout, by contrast, would have returned a Timeout result above.
    throw;
}
```

The same rule holds during response conversion: a token cancelled mid-read rethrows `OperationCanceledException` from `ToResultAsync`, and is never folded into an `HttpClientError`.

## Ownership and disposal

The adapter creates nothing and owns nothing:

- **The caller owns and disposes the `HttpClient`.** It is never created, disposed or replaced by the adapter.
- **The caller owns and disposes the `HttpRequestMessage`.** It is never disposed by the adapter.
- **The raw form transfers response ownership to the caller.** `SendResultAsync` (raw) and `ToResultAsync` (raw) return the response untouched for the caller to inspect and dispose.
- **The typed form disposes its response.** `SendResultAsync` (typed) disposes the response exactly once, after it has been deserialized. `ToResultAsync` (typed) never disposes the response it was handed.

```csharp
using var client = new HttpClient();   // yours to dispose
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/"); // yours to dispose

// Raw: the response is yours to inspect and dispose.
Result<HttpResponseMessage, HttpClientError> raw = await client.SendResultAsync(request);
if (raw.IsSuccess)
{
    using var response = raw.Match(static r => r, static _ => null!);
    // ... disposed at the end of this block
}

// Typed: the adapter deserializes and disposes the response; you only see the value.
using var typedRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");
Result<Product, HttpClientError> typed =
    await client.SendResultAsync(typedRequest, AppJsonContext.Default.Product);
```

## No retries

The adapter performs exactly one send per call and never retries, and it never catches an arbitrary application exception thrown by the handler. A non-HTTP exception propagates unchanged; only `HttpRequestException` is mapped to `Transport`, only `OperationCanceledException` with an inner `TimeoutException` is mapped to `Timeout`, and a plain `OperationCanceledException` is rethrown.

## JSON: the Native AOT route versus the reflective convenience

Both the typed `SendResultAsync` and `ToResultAsync` families offer two overloads, and the difference between them is exactly the difference between what Native AOT can resolve and what reflection can:

- **`JsonTypeInfo<TSuccess>` — the Native AOT route.** You supply the metadata a source-generated `JsonSerializerContext` emitted (as in `AppJsonContext.Default.Product` above). The adapter deserializes through that metadata and never consults ambient options. Use this in trimmed or AOT-published applications.

```csharp
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");

Result<Product, HttpClientError> result =
    await client.SendResultAsync(request, AppJsonContext.Default.Product);
```

- **`JsonSerializerOptions` — the reflective convenience.** The adapter resolves the converter for `TSuccess` from the supplied options, which requires dynamic and unreferenced code. Use this where reflection is available; Native AOT callers should prefer the `JsonTypeInfo<TSuccess>` overload.

```csharp
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
Result<Product, HttpClientError> result =
    await client.SendResultAsync<Product>(request, options);
```

The raw overloads take neither, because they deserialize nothing.
