# Phase 6 — Validation

Objective: ship validation adapters for FluentValidation and DataAnnotations that return `Result<T, ValidationErrors>`, plus the ProblemDetails bridge that renders those failures as the RFC 9457 `errors` extension. Branch: `feature-1`.

Test runner: `dotnet test tests/ResulTus.Validation.Tests/ResulTus.Validation.Tests.csproj`.

Delivery strategy: `auto-chain` forecast — ~1 100 authored changed lines, so work-unit commits on `feature-1`; PR/merge stays the user's call.

## The design conflict this phase had to resolve

`openspec/changes/phase-6-validation/proposal.md` says validation errors are converted "with `Error.Validation()` and details in Metadata". **`Error` has no Metadata, and that is deliberate**: its XML docs state it "keeps no untyped payload… Structured extras belong to a typed `TError` in `Result<TSuccess, TError>`".

Validation produces N failures, each tied to a field. A single `Error` (one code, one optional message, one type) cannot hold them without destroying the structure, and a client that cannot tell which field failed cannot render a form.

The user chose, when asked: **`Result<T, ValidationErrors>`**, with the `errors` extension in ProblemDetails. That matches the core's stated design and RFC 9457, which defines `errors` for exactly this.

## Project layout, and why it is three projects

- `ResulTus.Validation` — `ValidationIssue`, `ValidationErrors`, the DataAnnotations adapter. **No external package.**
- `ResulTus.Validation.FluentValidation` — the FluentValidation adapter. Depends on `ResulTus.Validation` and on `FluentValidation` 12.2.0.
- `ResulTus.AspNetCore` — gains a reference to `ResulTus.Validation` and the ProblemDetails bridge.

The proposal lists "FluentValidation as optional dependency" as a risk to mitigate. A single project holding both adapters would make FluentValidation a transitive dependency of every DataAnnotations user, which does not mitigate it — it ignores it. Hence the split. `ResulTus.Validation` itself has zero external dependencies, so `ResulTus.AspNetCore` depending on it costs nothing beyond one small assembly, and "HTTP layer renders validation failures" is how ASP.NET Core's own ModelState works.

## Wire shape

`ValidationIssue` is `readonly record struct ValidationIssue(string Field, string Message, string? Code = null)`.

`ValidationErrors` is a sealed class holding `IReadOnlyList<ValidationIssue> Issues`, with `HasErrors`, `ToDictionary()` (field to messages, `string.Empty` for object-level failures) and `ToError()` for callers that need one collapsed `Error`.

ProblemDetails for a failed validation:

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Bad Request","status":400,
 "errors":{"Name":["'Name' must not be empty."],"Age":["Age must be 18 or older."]}}
```

- `status` is 400, the mapped status for `ErrorType.Validation`.
- `errors` maps field name to the list of messages for that field.
- An object-level failure (no member name) lands under the empty-string key, which is the convention ASP.NET Core uses.
- No `detail`: there is no single message to put there.

## Specs

- **S1 — the projects exist and are wired.** `src/ResulTus.Validation` (net8.0, no package), `src/ResulTus.Validation.FluentValidation` (net8.0, FluentValidation 12.2.0), `tests/ResulTus.Validation.Tests` (net10.0). All referenced from `ResulTus.slnx`. `FluentValidation` is added to `Directory.Packages.props`, never versioned inline. Everything builds under `TreatWarningsAsErrors` and `CS1591 = error`.
- **S2 — `ValidationErrors` is a real type, not a string bag.** It preserves every issue, its field, its message and its optional code; `ToDictionary()` groups by field preserving order; `HasErrors` is `false` only for an empty collection; `ToError()` produces `ErrorType.Validation`.
- **S3 — DataAnnotations adapter.** `ValidateAnnotations<T>()` returns `Success(instance)` when the object is valid, and `Failure(ValidationErrors)` otherwise. It MUST validate **all** properties, not just `[Required]`. A failure with no member name MUST still be captured, under the empty-string field.
- **S4 — FluentValidation adapter.** `Validate(validator)` and `ValidateAsync(validator, cancellationToken)` return the same shape. A null validator throws. The async overload forwards the token to FluentValidation and is genuinely awaited.
- **S5 — HTTP bridge.** `Result<T, ValidationErrors>.ToHttpResult()` executes to 400 with a ProblemDetails whose `errors` member maps field to messages. `ToActionResult()` yields an `ObjectResult` with the same status and document. A successful `Result<T, ValidationErrors>` still yields 200 with the value.
- **S6 — no silent loss.** Every failure reported by the underlying validator appears in the response. Nothing is truncated, deduplicated or dropped.
- **S7 — AOT and trimming.** `ResulTus.Validation` and `ResulTus.Validation.AspNetCore` bridge code MUST build warning-free. DataAnnotations is reflection-based, so `RequiresUnreferencedCode` on the adapter is acceptable **only** if declared explicitly with a justification; a bare suppression is not.
- **S8 — tests cover both adapters and the bridge.** One RED observed per behavior group.
- **S9 — no regression.** `dotnet build ResulTus.slnx` with 0 warnings and the whole suite green, including the existing 265 tests.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2, S3, S7 | inline | done — RED `CS0246`/`CS1061`, GREEN 10/10, then 100% lines and branches | see git log |
| T2 | S4 | inline | done — RED `CS1061`, GREEN 20/20, then 100% lines and branches | see git log |
| T3 | S5, S6 | inline | done — RED `CS1061`, GREEN 26/26, then 100% lines and branches | see git log |
| T4 | S8, S9 | inline | done — 28/28, full solution 293 tests, 0 warnings | see git log |

## Log

### L1 — original request (verbatim)

> SIGUE

Continuing the roadmap after Phase 5. The user then chose the return shape of `Validate()`:

> Result<T, ValidationErrors> + errors en ProblemDetails (Recommended)

### L2 — starting evidence

- `openspec/changes/phase-6-validation/proposal.md` scopes `Validate(this T, IValidator<T>)`, `ValidateAsync` with `CancellationToken`, a DataAnnotations adapter, a FluentValidation adapter, project `src/ResulTus.Validation` on `net8.0`, tests on `net10.0`. It still says `test/` and `ResulTus.sln`.
- The proposal's "details in Metadata" is impossible against the shipped core: `Error` is a `readonly record struct` of code, message and type only, and its docs say structured extras belong to `Result<TSuccess, TError>`.
- `Result<TSuccess, TError>` exists with `Success`, `Failure`, `IsSuccess`, `IsFailure`, `Match` and `Deconstruct`. It has **no** `TryGetValue`/`TryGetError`, so the HTTP bridge reads it through `Deconstruct`.
- `ResulTus.AspNetCore`'s existing `ToHttpResult`/`ToActionResult` accept only `Result` and `Result<T>`, which is why the bridge needs new overloads.
- `Directory.Packages.props` has no FluentValidation entry. NuGet's latest stable is 12.2.0.

### L3 — decisions taken while implementing (not user requirements)

- Three projects, not one. The proposal's own risk row says FluentValidation must stay optional; one project would make it transitive for everyone.
- `ValidationIssue`, not `ValidationFailure`, to avoid colliding with FluentValidation's own public type in a file that imports both namespaces.
- Object-level failures land under the empty-string field key, matching ASP.NET Core's `ModelState` convention.
- `ValidateAnnotations` for DataAnnotations and `Validate(validator)` for FluentValidation. The signatures cannot collide, and the bare `Validate` name is what the proposal specifies for the adapter that takes a validator.
- The bridge adds no custom status selector. S5 asks for `ToHttpResult()` and `ToActionResult()` only; consumers who need another status already have the Phase 5 overloads for `Result` and `Result<T>`.
- `errors` is the only member the bridge adds. No `code` and no `detail`: several field failures have no single honest summary, and a `code` would just restate `Validation.Failed`.

### L4 — things the code taught me, and where I had to change course

**DataAnnotations really is `RequiresUnreferencedCode`.** `ValidationContext(Object)` and `Validator.TryValidateObject` both carry the attribute, because they reflect over the validated type's properties. The first build failed with IL2026 on both calls. The honest fix is `[RequiresUnreferencedCode]` on `ValidateAnnotations` with a justification — a `[UnconditionalSuppressMessage]` would claim the code is trim-safe when it is not. S7 allowed this explicitly.

**`ValidationFailure.PropertyName` can be null only through a hand-written validator.** FluentValidation's own rules always supply a name (empty for object-level rules), so the `?? string.Empty` guard looks dead. It is not dead: a hand-written `IValidator<T>` is a supported extension point and can hand back `new ValidationFailure(null, ...)`, which would make `ToDictionary()` throw on a null key. The guard stays, and `NullPropertyValidator` in the tests is the proof.

**`ValidationFailure.ErrorCode` can never be empty through the public API.** `WithErrorCode(string.Empty)` throws `ArgumentException`. My first draft had `string.IsNullOrEmpty(code) ? null : code`, which was unreachable dead code. It is gone: the adapter passes `failure.ErrorCode` straight through. `ValidationIssue.Code` stays `string?` because DataAnnotations genuinely supplies no code.

**The test project needed `IsAotCompatible=false`.** `Directory.Build.props` turns it on for every project, and a test assembly that reflects and serializes would fail the build. Same opt-out the other test projects use.

**`Error` has no `Metadata`, and `Result<T, TError>` has no `TryGetValue`.** The proposal's "details in Metadata" does not exist. The bridge reads the result through `Deconstruct(out bool, out T?, out TError?)`, which is the only accessor the type exposes.

### L5 — verification evidence

- T1 RED: `CS0246` for `ValidationErrors`/`ValidationIssue`, `CS1061` for `ValidateAnnotations`. GREEN 10/10.
- T2 RED: `CS1061` for `Validate` (and `CS7036` from FluentValidation's own extension being the only `Validate` in scope). GREEN 20/20.
- T3 RED: `CS1061` for both bridge methods. GREEN 26/26.
- T4 GREEN 28/28 after the two coverage-driven additions.
- Coverage, `ResulTus.Validation`: **100% lines, 100% branches**. `ResulTus.Validation.FluentValidation`: **100% lines, 100% branches**. The bridge in `ResulTus.AspNetCore` (`ValidationResultHttpExtensions`, `ValidationProblemDetails`): **100% lines, 100% branches**.
- `dotnet build ResulTus.slnx` -> 0 warnings, 0 errors.
- `dotnet test ResulTus.slnx` -> **293 passed** (28 validation, 41 aspnetcore, 59 serialization, 47 core, 80 extensions, 38 collections). No regression.
- No `[UnconditionalSuppressMessage]` anywhere. The only trimming annotation is the explicit `[RequiresUnreferencedCode]` on the DataAnnotations adapter, which is what S7 permits.
