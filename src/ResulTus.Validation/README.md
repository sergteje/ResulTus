# ResulTus.Validation — structured validation failures

[![NuGet](https://img.shields.io/nuget/v/ResulTus.Validation?label=nuget)](https://www.nuget.org/packages/ResulTus.Validation)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.Validation?label=downloads)](https://www.nuget.org/packages/ResulTus.Validation)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package carries validation detail as the typed error of a
[ResulTus](https://github.com/sergteje/ResulTus) result. `Error` deliberately keeps no untyped
payload, so structured detail belongs to a `TError` that owns its own data; `ValidationErrors` is
that type for validation, and `ValidateAnnotations<T>()` produces it from
`System.ComponentModel.DataAnnotations` attributes.

## Install

```bash
dotnet add package ResulTus.Validation
```

## Dependencies

[`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
types and `Error`. It adds no other package reference.

## What you get

| Type | Shape |
| --- | --- |
| `ValidationIssue` | A `readonly record struct` of `Field`, `Message` and an optional `Code`. |
| `ValidationErrors` | A sealed class that owns the ordered `Issues` list. |
| `DataAnnotationsValidationExtensions` | `ValidateAnnotations<T>()`, returning `Result<T, ValidationErrors>`. |

`ValidationErrors` exposes `Issues`, `HasErrors`, `ToDictionary()` and `ToError()`:

```csharp
using ResulTus;
using ResulTus.Validation;

ValidationErrors errors = new(new[]
{
    new ValidationIssue("Age", "Age must be 18 or older"),
    new ValidationIssue("Age", "Age must be under 120"),
});

bool hasErrors = errors.HasErrors;                            // true
string[] ageMessages = errors.ToDictionary()["Age"];          // both messages, in field order
Error collapsed = errors.ToError("VALIDATION_FAILED");        // one Error, lossy by design
```

`ToDictionary()` groups messages by field, preserving field order and per-field message order. An
object-level failure is kept under the empty field name, the convention ASP.NET Core's `ModelState`
uses. `ToError(code = "Validation.Failed")` collapses the issues into a single
`ErrorType.Validation` error whose message states the count — use it only for paths that cannot
carry structured detail.

## DataAnnotations

`ValidateAnnotations<T>()` runs DataAnnotations over **every** property of the instance, not only
the properties carrying `[Required]`, and returns a failure with one issue per violated attribute:

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class SignUpRequest
{
    [Required]
    public string? Email { get; set; }

    [Range(18, 120)]
    public int Age { get; set; }
}
```

```csharp
using System;
using ResulTus;
using ResulTus.Validation;

var (isSuccess, _, errors) = new SignUpRequest { Email = "", Age = 17 }.ValidateAnnotations();

if (!isSuccess)
{
    foreach (ValidationIssue issue in errors!.Issues)
    {
        Console.WriteLine($"{issue.Field}: {issue.Message}");
    }
}
```

The method reflects over the validated type's properties, so it carries
`[RequiresUnreferencedCode]` instead of silencing the trim analyzer. A caller that trims must keep
the validated type's members reachable, or call it from code that is not trimmed.

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — required
  dependency: the result types and `Error`.
- [`ResulTus.Validation.FluentValidation`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Validation.FluentValidation/README.md)
  — the same `ValidationErrors`, produced from an `IValidator<T>`.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md),
  [getting started](https://github.com/sergteje/ResulTus/blob/main/docs/getting-started.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
