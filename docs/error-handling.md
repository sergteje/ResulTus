# Error handling

`Error` is the shared failure type carried by `Result` and `Result<T>`. It is a 24-byte
inline `readonly record struct` holding exactly three pieces of data — a code, an optional
message and a category — with no untyped payload and no heap allocation. Structured failure
detail that does not fit those three fields belongs to a typed `TError` in the generic tier
`Result<TSuccess, TError>` (see [Validation](#validation), which builds on that tier).

Every factory below is a static method on `Error`, and every one allocates nothing: the
`Error` is returned by value.

```csharp
using System;
using ResulTus;
```

## The three members

An `Error` exposes exactly three read-only members:

| Member | Type | Meaning |
| --- | --- | --- |
| `Code` | `string` | The machine-readable code, never `null`. A `null` code passed to a factory is coalesced to `string.Empty`. |
| `Message` | `string?` | An optional human-readable message. `null` stays distinct from an empty one. |
| `Type` | `ErrorType` | The category, used to route the error. |

`Error` has value semantics: two errors are equal when their `Code`, `Message` and `Type`
are equal. `default(Error)` compares equal to `Error.None`, so the "no error" state and the
default struct are the same value under `==`.

## The built-in factories

`ErrorType` is a closed set of ten values, ordered by severity. Nine of them have a matching
`Error` factory; `None` is the absence of an error and is exposed as the `Error.None`
property, not as a factory.

| `ErrorType` | `Error` factory | Notes |
| --- | --- | --- |
| `None` | `Error.None` | The no-error singleton; not a factory. |
| `Failure` | `Error.Failure(code, message?)` | General failure. |
| `Validation` | `Error.Validation(code, message?)` | Invalid input. |
| `NotFound` | `Error.NotFound(code, message?)` | Missing resource. |
| `Conflict` | `Error.Conflict(code, message?)` | State conflict. |
| `Unauthorized` | `Error.Unauthorized(code, message?)` | Authentication required. |
| `Forbidden` | `Error.Forbidden(code, message?)` | Access denied. |
| `Timeout` | `Error.Timeout(code, message?)` | Operation timed out. |
| `Cancelled` | `Error.Cancelled(code, message?)` | Operation cancelled. |
| `Unexpected` | `Error.Unexpected(code, message?)` | Unexpected error. |

Every factory takes the same shape: a required `code` and an optional `message`.

```csharp
Error notFound = Error.NotFound("USER_NOT_FOUND", "No user with that id");
Error invalid = Error.Validation("INVALID_AGE", "Age must be 18 or older");
Error conflict = Error.Conflict("EMAIL_TAKEN");          // message is null
Error forbidden = Error.Forbidden("READ_ONLY");
Error unauthorized = Error.Unauthorized("NO_SESSION");
Error timeout = Error.Timeout("UPSTREAM_TIMEOUT");
Error cancelled = Error.Cancelled("CALLER_CANCELLED");
Error unexpected = Error.Unexpected("INTERNAL");
Error generic = Error.Failure("GENERIC");

string code = notFound.Code;        // "USER_NOT_FOUND"
string? message = notFound.Message; // "No user with that id"
ErrorType type = notFound.Type;     // ErrorType.NotFound
```

The code is your stable identifier across runs and cultures; the message is human-facing.
Pick a code that names the failing rule or resource, not the symptom.

## From an exception

`Error.FromException` captures an exception as an `ErrorType.Unexpected` error. Its `Code`
is the exception's runtime type name, its `Message` is the exception's message, and no
stack trace is kept.

```csharp
Error captured = Error.FromException(new InvalidOperationException("boom"));
// captured.Code == "System.InvalidOperationException"
// captured.Message == "boom"
// captured.Type == ErrorType.Unexpected
```

`FromException` captures whatever it is given, cancellation included, because it cannot know
the caller's control flow. To capture exceptions while preserving cancellation, use
`Result.Try`, which rethrows `OperationCanceledException` instead of converting it:

```csharp
Result<int> parsed = Result.Try(() => int.Parse("42"));    // success
Result<int> failed = Result.Try(() => int.Parse("nope"));  // failure, Error.FromException
```

`Result.Try` also takes an overload with a `Func<Exception, Error>` mapping function so the
captured error is yours, not the library's default.

## Error factories via the source generator

Writing `Error.NotFound("USER_NOT_FOUND", ...)` at every call site repeats the code and the
message. `ResulTus.SourceGenerators` turns a declared factory into a generated body, so the
code, category and message template live in one place.

Declare the error as a `public static partial Error` method on a type marked `[ResultErrors]`,
and annotate the method with `[ErrorFactory(code, type, message)]`:

```csharp
[ResultErrors]
public static partial class UserErrors
{
    [ErrorFactory("USER_NOT_FOUND", ErrorType.NotFound, "User {id} not found")]
    public static partial Error NotFound(Guid id);

    [ErrorFactory("INVALID_EMAIL", ErrorType.Validation, null)]
    public static partial Error InvalidEmail();
}
```

The generator emits the missing part in a separate file. The message template is rendered as
an interpolated string, so `"User {id} not found"` becomes `$"User {id} not found"` in the
generated body, and a `null` template produces an error with no message:

```csharp
Error notFound = UserErrors.NotFound(Guid.NewGuid());
// notFound.Code == "USER_NOT_FOUND"
// notFound.Type == ErrorType.NotFound
// notFound.Message == $"User {<the id>} not found"

Error invalid = UserErrors.InvalidEmail();
// invalid.Code == "INVALID_EMAIL", invalid.Message == null
```

The attributes are an opt-in: a method that carries `[ErrorFactory]` on a type that never
carries `[ResultErrors]` is reported, not silently ignored. The annotated type and every type
that encloses it must be `partial`. A declaration the generator cannot complete — a missing
`[ResultErrors]`, a non-partial or non-static method, a wrong return type, an `out`
parameter, `ErrorType.None`, or a method that already has a body — is reported as
`RESULTGEN001`, an error naming the offending member and the reason.

## Validation

The typed tier `Result<TSuccess, TError>` is the natural home for structured validation
detail: `Error` keeps no untyped payload, so a list of failures belongs to a `TError` that
owns a collection. `ResulTus.Validation` provides that type, plus two ways to produce it.

### `ValidationErrors` and `ValidationIssue`

`ValidationIssue` is a `readonly record struct` holding a field name, a message and an
optional code; `ValidationErrors` owns the ordered collection of issues.

```csharp
using ResulTus.Validation;

var issues = new[]
{
    new ValidationIssue("Age", "Age must be 18 or older"),
    new ValidationIssue("Age", "Age must be under 120"),
};

ValidationErrors errors = new(issues);

bool hasErrors = errors.HasErrors;                          // true
string[] ageMessages = errors.ToDictionary()["Age"];        // both messages, in order
Error collapsed = errors.ToError("VALIDATION_FAILED");      // a single Error, lossy by design
```

`ToDictionary()` groups messages by field, preserving field order and per-field message
order; an object-level failure uses the empty field name, matching ASP.NET Core's
`ModelState` convention. `ToError()` collapses the failures into one `ErrorType.Validation`
error whose message states the count — use it only for paths that cannot carry structured
detail.

### DataAnnotations

`ValidateAnnotations<T>()` runs `System.ComponentModel.DataAnnotations` over every property
of an instance and returns a `Result<T, ValidationErrors>` carrying all failures.

```csharp
using System.ComponentModel.DataAnnotations;
using ResulTus;
using ResulTus.Validation;

public sealed class SignUpRequest
{
    [Required]
    public string? Email { get; set; }

    [Range(18, 120)]
    public int Age { get; set; }
}

var (isSuccess, request, errors) = new SignUpRequest { Email = "", Age = 17 }.ValidateAnnotations();

if (!isSuccess)
{
    foreach (ValidationIssue issue in errors!.Issues)
    {
        Console.WriteLine($"{issue.Field}: {issue.Message}");
    }
}
```

DataAnnotations validation reflects over the validated type, so this method carries
`[RequiresUnreferencedCode]` honestly rather than silencing the trim analyzer; callers that
trim must keep the validated type's members reachable.

### FluentValidation

`ResulTus.Validation.FluentValidation` runs an `IValidator<T>` and returns the same
`Result<T, ValidationErrors>`. It is the only assembly that references FluentValidation, so
consumers using only DataAnnotations never take it transitively.

```csharp
using FluentValidation;
using ResulTus;
using ResulTus.Validation;
using ResulTus.Validation.FluentValidation;

public sealed class Customer
{
    public string? Name { get; set; }
    public int Age { get; set; }
}

public sealed class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(c => c.Age).GreaterThanOrEqualTo(18).WithMessage("Age must be 18 or older.");
    }
}

var (isSuccess, customer, errors) = new Customer { Name = "", Age = 17 }.Validate(new CustomerValidator());
```

`Validate<T>()` is the synchronous path and throws `AsyncValidatorInvokedSynchronouslyException`
if the validator contains asynchronous rules. `ValidateAsync<T>()` awaits any asynchronous
rules and forwards the caller's `CancellationToken`, returning a
`Task<Result<T, ValidationErrors>>`. Every FluentValidation failure becomes one
`ValidationIssue`, carrying the property name, the message and the error code.

## Build-time analyzers

`ResulTus.Analyzers` ships six diagnostics whose ids are part of the public contract, so a
consumer can suppress or escalate a rule by id. Together with `RESULTGEN001` from the source
generator, they turn result misuse into a build-time finding instead of a runtime surprise.

| Id | Title | Severity | What it reports |
| --- | --- | --- | --- |
| `RESULT001` | Result is ignored | Warning | A result produced as an expression statement and then dropped, so its outcome is never read. Assigning it, discarding it with `_ =`, returning it, or passing it to another call are all deliberate and do not report. |
| `RESULT002` | Outcome is not checked | Warning | A `GetValueOrDefault()` read with no argument whose receiver is never checked — by `IsSuccess`, `IsFailure`, `TryGetValue`, `Match`, `GetValueOr` or `Deconstruct` — anywhere in the enclosing member. |
| `RESULT003` | Deconstruction discards the error | Warning | A result deconstruction whose last element is a discard (`_`). Every result puts the error last, so discarding it loses the failure. |
| `RESULT004` | Bind is a Map | Info | A `Bind` whose lambda only wraps the value back in `Success`, which is exactly `Map`. |
| `RESULT005` | Result nested in a Result | Warning | A `ToResult()` call on a value that is already a result, producing a result wrapped inside a result. |
| `RESULT006` | Ensure predicate is constant | Warning | An `Ensure` guard whose predicate is a lambda returning a constant `true` or `false`, so it cannot depend on the value it guards. |

The severity levels are the shipped defaults; `RESULT004` is `Info` because a redundant
`Bind` is noise, not a lost outcome. The source generator adds `RESULTGEN001` (Error) when a
`[ErrorFactory]` declaration cannot be generated.
