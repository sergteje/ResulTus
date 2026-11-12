# ResulTus.Analyzers — build-time result diagnostics

[![NuGet](https://img.shields.io/nuget/v/ResulTus.Analyzers?label=nuget)](https://www.nuget.org/packages/ResulTus.Analyzers)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus.Analyzers?label=downloads)](https://www.nuget.org/packages/ResulTus.Analyzers)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-dark.png">
  <img src="https://raw.githubusercontent.com/sergteje/ResulTus/main/assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="420">
</picture>

This package ships six Roslyn analyzers that turn [ResulTus](https://github.com/sergteje/ResulTus)
misuse into build-time diagnostics instead of a runtime surprise. The ids `RESULT001` through
`RESULT006` are part of the public contract, so a rule is suppressed or escalated by id, not by
toggling the package.

## Install

```bash
dotnet add package ResulTus.Analyzers
```

## Dependencies

None at runtime. The package is standalone: `ResulTus` does not reference it, so adding `ResulTus`
alone does not bring the rules — add this package explicitly. Its reference to the Roslyn API
(`Microsoft.CodeAnalysis.CSharp`) is private, so nothing is added to your package graph. The pack
includes `build/ResulTus.Analyzers.globalconfig`, which records the shipped default severities.

## What you get

| Id | Title | Severity | What it reports |
| --- | --- | --- | --- |
| `RESULT001` | Result is ignored | Warning | A result produced as an expression statement and then dropped, so its outcome is never read. Assigning it, discarding it with `_ =`, returning it, or passing it to another call are all deliberate and do not report. |
| `RESULT002` | Outcome is not checked | Warning | A `GetValueOrDefault()` read with no argument whose receiver is never checked — by `IsSuccess`, `IsFailure`, `TryGetValue`, `Match`, `GetValueOr` or `Deconstruct` — anywhere in the enclosing member. |
| `RESULT003` | Deconstruction discards the error | Warning | A result deconstruction whose last element is a discard (`_`). Every result tier puts the error last, so discarding it loses the failure. |
| `RESULT004` | Bind is a Map | Info | A `Bind` whose lambda only wraps the value back in `Success`, which is what `Map` already does. |
| `RESULT005` | Result nested in a Result | Warning | A `ToResult()` call on a receiver that is already a result, producing a result wrapped inside another. |
| `RESULT006` | Ensure predicate is constant | Warning | An `Ensure` guard whose predicate is a lambda returning a constant `true` or `false`, so it cannot depend on the guarded value. |

`RESULT004` is `Info` because a redundant `Bind` is noise, not a lost outcome. Every other rule is
a `Warning`.

```csharp
using ResulTus;
using ResulTus.Extensions;

public static class AnalyzerSamples
{
    public static void Dropped(Result<int> result)
    {
        result.Map(value => value + 1);                                       // RESULT001
    }

    public static int SilentDefault(Result<int> result)
        => result.GetValueOrDefault();                                        // RESULT002

    public static Result<string> RedundantBind(Result<int> result)
        => result.Bind(value => Result<string>.Success(value.ToString()));    // RESULT004

    public static Result<int> ConstantGuard(Result<int> result)
        => result.Ensure(_ => true, Error.Failure("NEVER"));                  // RESULT006
}
```

```csharp
using System;
using ResulTus;
using ResulTus.Extensions;

public static class AnalyzerSamplesDeconstructor
{
    public static void LostError(Result<int, string> result)
    {
        var (isSuccess, value, _) = result;                                   // RESULT003
        Console.WriteLine($"{isSuccess}: {value}");
    }

    public static Result<Result<int>> Nested(Result<int> result)
        => result.ToResult();                                                 // RESULT005
}
```

## Configuring a rule

Severity is read from an analyzer config file, never from an MSBuild property. Override a rule per
project in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.RESULT004.severity = none
```

The same key with `warning`, `error` or `suggestion` escalates or downgrades a rule.

## Related packages

- [`ResulTus`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus/README.md) — the result
  types these analyzers inspect; this package does not depend on it.
- [`ResulTus.SourceGenerators`](https://github.com/sergteje/ResulTus/blob/main/src/ResulTus.SourceGenerators/README.md)
  — the `[ErrorFactory]` generator and its `RESULTGEN001` diagnostic.
- [All ResulTus packages](https://github.com/sergteje/ResulTus#packages) — the full package table.

## Learn more

- [Full documentation on GitHub](https://github.com/sergteje/ResulTus#readme) — contract, guides,
  benchmarks.
- Guides: [error handling](https://github.com/sergteje/ResulTus/blob/main/docs/error-handling.md),
  [core concepts](https://github.com/sergteje/ResulTus/blob/main/docs/core-concepts.md),
  [getting started](https://github.com/sergteje/ResulTus/blob/main/docs/getting-started.md).

MIT licensed. Source: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).
