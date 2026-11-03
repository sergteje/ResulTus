# Phase 5 — ASP.NET Core

Objective: ship `ResulTus.AspNetCore` with `ToHttpResult()` for Minimal APIs, `ToActionResult()` for MVC, a total `ErrorType` to HTTP status mapping and RFC 9457 ProblemDetails bodies. Branch: `feature-1`. Test runner: `dotnet test tests/ResulTus.AspNetCore.Tests/ResulTus.AspNetCore.Tests.csproj`.

Delivery strategy: `auto-chain` forecast — ~800 authored changed lines, so work-unit commits on `feature-1`; PR/merge stays the user's call. Phases are being stacked on `feature-1` on purpose: no PR until the user says so.

## Why this phase is safe to build now

It was blocked before Phase 7. `Result<T>` exposed only `IsSuccess` and `IsFailure`, so any API returning one serialized to `{"isSuccess":true,"isFailure":false}` and dropped the value. Phase 7 shipped the converters, so an ASP.NET surface now has a wire format that does not lose data.

## The HTTP contract

### `ErrorType` to status — total over all ten values

| ErrorType | Status | Rationale |
| --- | --- | --- |
| `None` | 500 | A failure carrying `Error.None` is a contradiction; `default(Result)` is exactly that, so it must not look like a client error. |
| `Failure` | 500 | General failure. |
| `Validation` | 400 | Bad request. |
| `NotFound` | 404 | Not found. |
| `Conflict` | 409 | Conflict. |
| `Unauthorized` | 401 | Authentication required. |
| `Forbidden` | 403 | Access denied. |
| `Timeout` | 504 | The operation timed out server-side; 408 would blame the client. |
| `Cancelled` | 499 | Client closed request. Non-standard but the only honest code; ASP.NET Core defines the constant. |
| `Unexpected` | 500 | Unexpected server error. |

The core spec's table is marked informative and covers only seven of the ten values. `None`, `Timeout` and `Cancelled` are decided here and recorded in the log as decisions, not as user requirements. Every one of them is overridable through the mapping delegate, so no consumer is stuck with this table.

### ProblemDetails on failure

RFC 9457 shape:

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,"detail":"not there","code":"missing"}
```

- `status` mirrors the HTTP status code.
- `title` is the HTTP reason phrase for that status.
- `detail` is `Error.Message`, omitted when `null`.
- `code` is `Error.Code`, carried as an extension member.
- `type` is **not set by this library**. ASP.NET Core's `ProblemDetailsDefaults` fills it with the RFC 9110 section URI for standard statuses, and omits it entirely for statuses with no RFC section (499). Leaving it alone is correct: the framework supplies the real spec link, and nothing fabricates a URI scheme.

### Success

- `Result` -> `200 OK` with no body.
- `Result<T>` -> `200 OK` with the value as the body.
- A `null` value yields `200 OK` with an **empty** body, not a literal `null`. That is `Ok<T>.ExecuteAsync` in ASP.NET Core: it writes nothing when the value is null. Forcing a `null` body would require a custom `IResult` and would lose the response metadata the framework emits for OpenAPI.

## Specs

- **S1 — the project exists and is wired.** New `src/ResulTus.AspNetCore/ResulTus.AspNetCore.csproj` targeting `net8.0`, `RootNamespace`/`AssemblyName` = `ResulTus.AspNetCore`, referenced from `ResulTus.slnx`, using `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. No new NuGet package. It MUST build under `TreatWarningsAsErrors` and `CS1591 = error`.
- **S2 — `ToHttpResult()` succeeds as 200.** `Result.Success().ToHttpResult()` executes to 200 with an empty body. `Result<T>.Success(v).ToHttpResult()` executes to 200 and the response body is `v` serialized as JSON. A `null` value executes to 200 with an empty body, which is `Ok<T>`'s documented behavior.
- **S3 — `ToHttpResult()` fails with the mapped status.** For every defined `ErrorType`, the executed response status equals the table above, and the body is the ProblemDetails document.
- **S4 — ProblemDetails carries the error.** `status` equals the response status, `title` is the reason phrase, `detail` is the message and is absent when the message is `null`, and the `code` extension equals `Error.Code`. `type` is left to the framework: present as an RFC 9110 section URI for standard statuses, absent for non-standard ones.
- **S5 — the mapping delegate overrides the default.** Both `ToHttpResult` and `ToActionResult` accept a `Func<Error, int>`; when supplied, its return value wins over the default table for every error type. A `null` delegate throws `ArgumentNullException` rather than silently falling back.
- **S6 — `ToActionResult()` for MVC.** On success, `Result` yields an `OkResult` (200, no body) and `Result<T>` yields an `OkObjectResult` carrying the value. On failure, both yield an `ObjectResult` whose `StatusCode` is the mapped status and whose `Value` is the ProblemDetails document.
- **S7 — no data loss.** The success value and the full `Error` (`Code`, `Message`, `Type`) survive the trip to the response untouched. `Message == null` must stay `null`, not become `""`.
- **S8 — AOT and trimming stay clean.** The project inherits `IsAotCompatible` and `IsTrimmable` and MUST build with 0 warnings, with no suppression attributes. If an API forces one, that is a finding to record, not to silence.
- **S9 — tests cover both paths.** New `tests/ResulTus.AspNetCore.Tests` targeting `net10.0` with xUnit v3 + FluentAssertions. `IResult` is executed against a `DefaultHttpContext` and the response is asserted, so no test server is needed. One RED observed per behavior group before its implementation.
- **S10 — no regression.** `dotnet build ResulTus.slnx` with 0 warnings and the whole suite green, including the existing 224 tests.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S3, S4, S8 | inline | done — RED `CS0103`/`CS1061`, GREEN 24/24 | see git log |
| T2 | S2, S6, S7 | inline | done — RED `CS1061`, GREEN 33/33 | see git log |
| T3 | S5, S9, S10 | inline | done — GREEN 41/41, 100% lines and branches | see git log |

## Log

### L1 — original request (verbatim)

> APILAMOS FASES

In response to the close of Phase 7 and the open question of whether to open a PR for the accumulated `feature-1` work or keep stacking phases. So: no PR yet, Phase 5 runs on top of Phase 7 on `feature-1`.

### L2 — starting evidence

- `openspec/changes/phase-5-aspnetcore/proposal.md` scopes: `ToActionResult()`, `ToHttpResult()`, error to status mapping, a customizable mapping delegate, ProblemDetails, project `src/ResulTus.AspNetCore` on `net8.0`, tests on `net10.0`. The proposal still says `test/` and `ResulTus.sln`; this repo uses `tests/` and `ResulTus.slnx`.
- `openspec/specs/result-core/spec.md` has an "informative" `ErrorType` to HTTP table covering 7 of 10 values. `Timeout` and `Cancelled` are absent, and so is `None`.
- `ErrorType.cs` XML docs confirm 400/401/403/404/409/500 for the seven, and say nothing for `Timeout` and `Cancelled`.
- Core API available to the mapping: `Result.TryGetError(out Error)`, `Result<T>.TryGetValue(out T?, out Error)`, `Result<T>.GetValueOrDefault()`, `IsSuccess`, `IsFailure`, `Error.Code`, `Error.Message`, `Error.Type`.
- `Microsoft.AspNetCore.App` is installed at 8.0.33 and 10.0.8/10.0.14, so both the `net8.0` library and the `net10.0` test project have a runtime.

### L3 — decisions taken while implementing (not user requirements)

- `Timeout` -> 504 and `Cancelled` -> 499, `None` -> 500. The spec left them undefined; 408 would blame the client for a server-side timeout, and `default(Result)` is a failure with `Error.None`, which must not masquerade as a 4xx.
- Success is always 200, never 204. A `Result<T>` with a null value still succeeded.
- `ToActionResult` returns `IActionResult` for the unit result and `ActionResult<T>` for `Result<T>`, which is the shape MVC controllers actually declare.
- Tests execute `IResult` against `DefaultHttpContext` instead of pulling in `Microsoft.AspNetCore.TestHost`. No new package, and it asserts the real status code and body rather than the object graph.

### L4 — two assumptions disproved by running the code

**`type` is written, and that is fine.** The decision above said ProblemDetails would not write `type`. Probing the real response showed ASP.NET Core's `ProblemDetailsDefaults` fills it with the RFC 9110 section URI for standard statuses (`section-15.5.5` for 404, `section-15.6.5` for 504), and omits it entirely for 499 because no RFC section exists. So the library leaves `Type` null on purpose and the framework supplies the real spec link. That is better than `about:blank`, and the tests now pin both halves: present for a standard status, absent for a non-standard one. The original note was wrong about the outcome, not about the intent.

**A null value produces an empty body, not `null`.** `Results.Ok<T>(null)` writes nothing at all. Forcing a literal `null` would mean a custom `IResult` and losing the response metadata the framework emits for OpenAPI, so the framework behavior stands and the test asserts 200 with an empty body.

**The probe needed a service provider.** `ProblemHttpResult.ExecuteAsync` resolves `IOptions<JsonOptions>` from `HttpContext.RequestServices`, so a bare `DefaultHttpContext` throws `ArgumentNullException: provider`. The probe now builds a minimal container with `AddLogging`, `AddOptions` and `ConfigureHttpJsonOptions`. Without that, every failure-path test failed for a reason unrelated to the code under test.

### L5 — verification evidence

- T1 RED: `CS0103` for `ResultHttpStatus`, `CS1061` for `ToHttpResult`. GREEN: 24/24.
- T2 RED: `CS1061` for `ToActionResult`. GREEN: 33/33.
- T3 GREEN: 41/41.
- Coverage on `ResulTus.AspNetCore`: **100% lines, 100% branches**, all four classes at 100%. No suppressions were needed: `Status499ClientClosedRequest` and `ReasonPhrases.GetReasonPhrase` are both trim-safe, so S8 holds without a single `[UnconditionalSuppressMessage]`.
- Full solution: `dotnet build ResulTus.slnx` -> 0 warnings, 0 errors. `dotnet test ResulTus.slnx` -> 265 passed (41 aspnetcore, 59 serialization, 47 core, 80 extensions, 38 collections). No regression.
