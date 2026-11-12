# ResulTus.AspNetCore — results as ASP.NET Core responses

[![NuGet](https://img.shields.io/nuget/v/ResulTus.AspNetCore?label=nuget)](https://www.nuget.org/packages/ResulTus.AspNetCore)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.AspNetCore?label=downloads)](https://www.nuget.org/packages/ResulTus.AspNetCore)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package turns a [ResulTus](https://github.com/sergteje/ResulTus) `Result` or `Result<T>` into an
ASP.NET Core response. A success becomes `200 OK` carrying the value; a failure becomes a mapped
status code carrying an RFC 9457 ProblemDetails document that describes the `Error`. MVC controllers
use `ToActionResult`, Minimal APIs use `ToHttpResult`, and a validation result renders its field
errors as the `errors` member RFC 9457 defines.

## Install

```bash
dotnet add package ResulTus.AspNetCore
```

## Dependencies

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
  types the conversions extend.
- [`ResulTus.Validation`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Validation/README.md)
  — supplies `ValidationErrors` and `ValidationIssue` for the validation conversions.
- The ASP.NET Core shared framework (`Microsoft.AspNetCore.App`): the package multi-targets
  `net8.0`, `net9.0`, `net10.0` and `net11.0` and adds no third-party dependency.

## What you get

Two conversion families, each with a default status mapping and a custom-selector overload:

| Source | Method | Returns |
| --- | --- | --- |
| `Result` | `ToActionResult()` | `IActionResult` — `OkResult`, or an `ObjectResult` carrying ProblemDetails |
| `Result` | `ToActionResult(statusSelector)` | Same, with your `Func<Error, int>` choosing the status |
| `Result<T>` | `ToActionResult()` | `ActionResult<T>` carrying the value, or ProblemDetails |
| `Result<T>` | `ToActionResult(statusSelector)` | Same, with your selector |
| `Result` | `ToHttpResult()` | `IResult` — `Results.Ok()`, or a problem result |
| `Result` | `ToHttpResult(statusSelector)` | Same, with your selector |
| `Result<T>` | `ToHttpResult()` | `IResult` carrying the value, or a problem result |
| `Result<T>` | `ToHttpResult(statusSelector)` | Same, with your selector |

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ResulTus;
using ResulTus.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/products/{id:int}", (int id) =>
{
    Result<string> product = id == 0
        ? Result<string>.Failure(Error.NotFound("PRODUCT_NOT_FOUND", "No product with that id"))
        : Result<string>.Success($"product {id}");

    return product.ToHttpResult();
});

app.Run();
```

The same result inside a controller:

```csharp
using Microsoft.AspNetCore.Mvc;
using ResulTus;
using ResulTus.AspNetCore;

[ApiController]
[Route("products")]
public sealed class ProductsController : ControllerBase
{
    [HttpGet("{id:int}")]
    public ActionResult<string> Get(int id) =>
        (id == 0
            ? Result<string>.Failure(Error.NotFound("PRODUCT_NOT_FOUND"))
            : Result<string>.Success($"product {id}"))
        .ToActionResult();
}
```

A selector receives the whole `Error`, so it can map on the code as well as the type, and it always
wins over the default mapping:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResulTus;
using ResulTus.AspNetCore;

Result<int> conflict = Result<int>.Failure(Error.Conflict("ALREADY_EXISTS"));

ActionResult<int> mapped = conflict.ToActionResult(_ => 422);
IResult minimal = conflict.ToHttpResult(_ => 422);
```

On .NET 10 and later, `ActionResult<T>` implements `IConvertToActionResult` instead of
`IActionResult` directly. Return it from the action and MVC converts it — the controller example
above does exactly that. When you need an `IActionResult` in hand, the unit-tier
`Result.ToActionResult` overloads return it directly.

## Status mapping

`ResultHttpStatus.GetStatusCode` is the default, total mapping:

| `ErrorType` | Status |
| --- | --- |
| `Validation` | `400 Bad Request` |
| `Unauthorized` | `401 Unauthorized` |
| `Forbidden` | `403 Forbidden` |
| `NotFound` | `404 Not Found` |
| `Conflict` | `409 Conflict` |
| `Cancelled` | `499 Client Closed Request` |
| `Timeout` | `504 Gateway Timeout` |
| `Failure`, `Unexpected`, `None` | `500 Internal Server Error` |

An undefined `ErrorType` value also falls back to `500`, so no request throws because of an
unmapped category. `ResultHttpStatus` is public, so a selector can delegate to it for the cases it
does not override.

## The ProblemDetails document

A failure renders these members:

| Member | Value |
| --- | --- |
| `status` | The mapped status code. |
| `title` | The reason phrase for that status (`"Not Found"`, …). |
| `detail` | The error's message, omitted entirely when the error carries none — not an empty string. |
| `code` | An extension member carrying the error's code. |
| `type` | Left unset. RFC 9457 defaults it to `about:blank`, and the package does not invent a URI scheme. |

Validation failures get their own document: status `400`, title `"Bad Request"`, no `detail` —
there is no single message that honestly summarizes several field failures — and an `errors`
extension mapping each field name to its messages. An object-level issue uses the empty field name,
the same convention ASP.NET Core's `ModelState` uses:

```csharp
using Microsoft.AspNetCore.Mvc;
using ResulTus;
using ResulTus.AspNetCore;
using ResulTus.Validation;

[ApiController]
[Route("products")]
public sealed class ProductValidationController : ControllerBase
{
    [HttpPost]
    public ActionResult<string> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            var errors = new ValidationErrors(
            [
                new ValidationIssue("Name", "required"),
                new ValidationIssue("", "the request must name a product"),
            ]);

            return Result<string, ValidationErrors>.Failure(errors).ToActionResult();
        }

        return Result<string, ValidationErrors>.Success(name).ToActionResult();
    }
}
```

The validation family is `Result<T, ValidationErrors>` only, and it has the two parameterless
overloads `ToHttpResult()` and `ToActionResult()` — a validation failure is always `400`.

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — required
  dependency: the core result types.
- [`ResulTus.Validation`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Validation/README.md)
  — required dependency for the validation conversions.
- [`ResulTus.HttpClient`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.HttpClient/README.md)
  — the client-side counterpart: reads these problem documents into a result.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md),
  [getting started](https://github.com/sergteje/ResulTus/blob/main/docs/getting-started.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
