# ResulTus.Validation.FluentValidation — FluentValidation results

[![NuGet](https://img.shields.io/nuget/v/ResulTus.Validation.FluentValidation?label=nuget)](https://www.nuget.org/packages/ResulTus.Validation.FluentValidation)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.Validation.FluentValidation?label=downloads)](https://www.nuget.org/packages/ResulTus.Validation.FluentValidation)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package adapts [FluentValidation](https://www.nuget.org/packages/FluentValidation) to
[ResulTus](https://github.com/sergteje/ResulTus): `Validate` and `ValidateAsync` run an
`IValidator<T>` and return `Result<T, ValidationErrors>`, so every failure becomes one
[`ValidationIssue`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Validation/README.md)
carrying its property name, message and error code.

## Install

```bash
dotnet add package ResulTus.Validation.FluentValidation
```

## Dependencies

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
  types and `Error`.
- [`ResulTus.Validation`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Validation/README.md)
  — `ValidationErrors` and `ValidationIssue`, the failure type both overloads return.
- [`FluentValidation`](https://www.nuget.org/packages/FluentValidation) — the validators this package
  runs. This is the only ResulTus assembly that references it, so a consumer using only
  DataAnnotations never takes it transitively.

## What you get

Both methods are extension methods on `FluentValidationExtensions`:

| Method | Returns |
| --- | --- |
| `Validate<T>(this T instance, IValidator<T> validator)` | `Result<T, ValidationErrors>` |
| `ValidateAsync<T>(this T instance, IValidator<T> validator, CancellationToken cancellationToken = default)` | `Task<Result<T, ValidationErrors>>` |

```csharp
using FluentValidation;

public sealed class Customer
{
    public string? Name { get; set; }

    public int Age { get; set; }
}

public sealed class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(customer => customer.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(customer => customer.Age).GreaterThanOrEqualTo(18).WithMessage("Age must be 18 or older.");
    }
}
```

```csharp
using System;
using ResulTus;
using ResulTus.Validation;
using ResulTus.Validation.FluentValidation;

var (isSuccess, _, errors) = new Customer { Name = "", Age = 17 }.Validate(new CustomerValidator());

if (!isSuccess)
{
    foreach (ValidationIssue issue in errors!.Issues)
    {
        Console.WriteLine($"{issue.Field}: {issue.Message} ({issue.Code})");
    }
}
```

A valid instance comes back as a success carrying the same instance. A failed one carries a
`ValidationErrors` with one issue per FluentValidation failure, in the validator's order.

## Asynchronous rules

`Validate` throws `AsyncValidatorInvokedSynchronouslyException` when the validator contains
asynchronous rules; use `ValidateAsync` for those. It awaits the rules and forwards the caller's
`CancellationToken` to the validator.

```csharp
using ResulTus;
using ResulTus.Validation;
using ResulTus.Validation.FluentValidation;

Result<Customer, ValidationErrors> validated = await new Customer { Name = "Ada", Age = 36 }
    .ValidateAsync(new CustomerValidator());
```

Both methods throw `ArgumentNullException` for a `null` instance or a `null` validator.

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — required
  dependency: the result types and `Error`.
- [`ResulTus.Validation`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.Validation/README.md)
  — required dependency: the failure types this package fills.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md),
  [getting started](https://github.com/sergteje/ResulTus/blob/main/docs/getting-started.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
