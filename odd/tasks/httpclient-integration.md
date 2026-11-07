# ResulTus.HttpClient — typed HTTP response and transport results

Objective: add an optional client-side HTTP adapter that returns ResulTus results for HTTP responses, RFC 9457 ProblemDetails, and transport failures. Branch: `feature-1`.

Test runner: `dotnet test tests/ResulTus.HttpClient.Tests/ResulTus.HttpClient.Tests.csproj`; feature closure also runs `dotnet build ResulTus.slnx` and `dotnet test ResulTus.slnx`.

Delivery strategy: `auto-chain`. Forecast exceeds 400 authored lines across the package, tests and usage guide. Work stays inline in reviewable commits; no push, PR or merge is authorized. Schedule after P1, P2 and the remaining API-completion tasks, but before P3's final README.

Risk: none anticipated. APIs are additive and HTTP outcomes, cancellation and ownership rules have public-interface tests. Reassess if implementation adds an external dependency or changes an existing contract.

## Specs

- **S1 — preserve HTTP-specific failure data.** Add `HttpClientFailureKind`, `HttpClientError` and `HttpProblemDetails` in the new `ResulTus.HttpClient` package. A client error carries its kind, optional response `HttpStatusCode`, optional parsed ProblemDetails and optional originating exception. ProblemDetails preserves the RFC 9457 standard fields (`type`, `title`, `status`, `detail`, `instance`) and unknown extension values, including `errors`, without referencing ASP.NET Core. Value operations return `Result<TSuccess, HttpClientError>`; a raw response operation returns `Result<HttpResponseMessage, HttpClientError>`. Do not collapse HTTP status or ProblemDetails into the core `Error` by default.
- **S2 — convert an `HttpResponseMessage` without throwing for HTTP failures.** Provide `ToResultAsync<T>` conversions for a typed value and for the raw response. Any 2xx response is success; the typed form deserializes JSON. A non-2xx response is a failure carrying the actual response status, and parses a ProblemDetails document when the content type is `application/problem+json`. If that body is malformed or absent, keep the status and leave ProblemDetails unset; malformed ProblemDetails may be retained as the optional parsing exception. A malformed successful JSON body returns an `InvalidResponse` error carrying the response status and parsing exception. Provide a `JsonTypeInfo<T>` overload for source-generated/AOT callers and an options-based convenience overload with honest trimming/AOT annotations. Response conversion does not dispose the caller-owned response.
- **S3 — capture transport failures through `HttpClient`.** Provide raw and typed `SendResultAsync` extensions accepting a caller-owned `HttpClient`, `HttpRequestMessage`, and optional `CancellationToken`. The raw form returns the successful `HttpResponseMessage` for caller inspection/disposal; the typed form delegates response conversion and disposes its response after consumption. Neither form creates or disposes the caller's client or disposes the request. Map `HttpRequestException` to `Transport`; map a client timeout to `Timeout`. If the caller token is cancelled, rethrow `OperationCanceledException`; cancellation is control flow, not an HTTP error. Do not retry automatically and do not catch arbitrary application exceptions.
- **S4 — keep the adapter optional and framework-independent.** The production project targets `net8.0`, references only `ResulTus` and built-in .NET HTTP/JSON APIs, and has no `Microsoft.AspNetCore.App` framework reference or external runtime package. HTTP status handling uses the actual `HttpStatusCode`; it does not duplicate or reverse the server-side `ErrorType` mapping from `ResulTus.AspNetCore`.
- **S5 — verify without network access.** Use a deterministic fake `HttpMessageHandler`; no real HTTP requests. Tests cover typed JSON success, raw response success, non-2xx ProblemDetails (including extension fields), non-ProblemDetails status failure, malformed ProblemDetails fallback, malformed success JSON, `HttpRequestException`, timeout, caller cancellation and response/request/client ownership.
- **S6 — document the boundary.** Add `docs/httpclient.md` showing raw, typed and value-returning results; status/ProblemDetails inspection; transport, timeout and cancellation handling. Explain that callers own `HttpClient` and `HttpRequestMessage`, the raw form transfers response ownership, the typed form disposes its response, the module does not retry, and `JsonTypeInfo<T>` is the Native AOT route. Add `ResulTus.HttpClient` to the P3 package table; README prose is completed in P3.
- **S7 — build and compatibility.** Every public member has full XML documentation. Add production and test projects to `ResulTus.slnx`; tests target `net10.0` and use the existing xUnit v3/FluentAssertions setup. Full solution build has 0 warnings/errors, the HTTP Client test project passes, and the full suite remains green.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S4 | delegated direct | done | 7146baa |
| T2 | S2 | delegated direct | done | f9700ad |
| T3 | S3 | delegated direct | done | a4c7c32 |
| T4 | S5-S7 | delegated direct | pending | — |

## Log

### L1 — authorized module request (verbatim)

> Esto si se hace:El único candidato que investigaría más adelante es ResulTus.HttpClient: convertir respuestas HTTP, ProblemDetails y fallos de transporte en resultados. Pero el mapeo de status, errores y cuerpos JSON es una decisión de política; sin usos reales repetidos, sería una capa opinada de más.

### L2 — selected error model (verbatim)

> el recomendado

The selected contract is `Result<T, HttpClientError>` so HTTP status, ProblemDetails and transport context are preserved instead of being collapsed into the core `Error`.

### L3 — repository evidence and implementation implications

- `ResulTus.AspNetCore` maps Results to server responses, not the reverse: `ResultHttpResultExtensions.ToHttpResult` maps `Result`/`Result<T>` using `ResultHttpStatus`, while `ValidationResultHttpExtensions` renders structured `ValidationErrors` in the RFC 9457 `errors` extension.
- `ResultProblemDetails` writes status, title, optional detail and a `code` extension. The validation bridge preserves each field/message instead of flattening the issue list.
- Existing production integrations target `net8.0`; the ASP.NET Core package alone references `Microsoft.AspNetCore.App`. This client package deliberately avoids that framework dependency.
- Microsoft .NET documentation describes `HttpRequestException`, cancellation/timeout behavior and distinguishing a timeout from caller cancellation through the inner `TimeoutException`: https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient
- `System.Net.Http.Json` provides built-in JSON HTTP helpers; the `JsonTypeInfo<T>` overload is the source-generated/AOT-safe route. See https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation
- **Ordering:** P1 is active. P2, core API completion and the typed-result collection extensions run first; this module follows them and ships before the final P3 README. No source file has been changed for this module yet.

### L4 — test-first and ownership

Use a fake handler and add one public-interface test per S# behavior before implementation. Keep caller cancellation distinct from HttpClient timeout, and verify response/request/client disposal boundaries. After task-specific checks, run the full HTTP Client test project; full solution build/test is the feature gate.

### L5 — delivery

All work stays inline. Keep code, tests and documentation for each behavior in the same work-unit commit. The module performs no outbound work by itself; every request uses a caller-supplied client and request message.

### L6 — T1 layout decision (struct vs class)

S1/S4 implemented. `HttpClientFailureKind`, `HttpClientError` and `HttpProblemDetails` are all `sealed class`, not structs. The library's inline struct convention (`Error`) is justified by a tiny, reference-free, zero-allocation layout; `HttpClientError` owns an optional `Exception` and an optional `HttpProblemDetails` (which itself owns a `Dictionary<string, JsonElement>` of extension members), so that convention does not apply. `HttpProblemDetails` stores extension values as `JsonElement` (built-in, faithful, no ASP.NET Core reference) and normalizes a null/empty extensions dictionary to null. Cancellation is deliberately absent from `HttpClientFailureKind` (closed set: transport, timeout, status error, invalid response): S3 rethrows `OperationCanceledException` rather than capturing it as an error. File layout: one file per type, matching `Error.cs`/`ErrorType.cs`.

### L7 — S2 implementation

S2 implemented. `HttpResponseMessageResultExtensions` (one file, mirroring the repo's `*Extensions` naming) adds three conversions: the typed `JsonTypeInfo<TSuccess>` overload (AOT-safe), the options-based convenience overload (reflection path, with `[UnconditionalSuppressMessage]` kept only on the private helper and phrasing reused from `ResultJsonConverter`), and the raw overload. Reading is hand-rolled over `System.Text.Json` rather than `System.Net.Http.Json` for three reasons: null/absent content must map to `InvalidResponse` (not `ArgumentNullException`), only `JsonException`-family parse failures must map to `InvalidResponse` while `OperationCanceledException` propagates untouched, and the ProblemDetails body is read as raw bytes anyway for the module's own minimal RFC 9457 reader. That reader keeps unknown members (including `errors`) as extensions and `JsonElement.Clone()`s each extension so it stays detached from the disposed source `JsonDocument`. The caller-owned response is never disposed. Tests: 14 new public-interface tests (typed success, options success, raw success, exact-status theory, ProblemDetails with extension + `errors`, status-only non-problem, malformed-problem fallback, malformed success → `InvalidResponse`, no-dispose across the four paths, null arguments, cancellation-is-not-an-error); HttpClient.Tests 16 → 30, full suite 645 → 659.

### L8 — S3 implementation

S3 implemented. `HttpClientSendResultExtensions` (one file) adds three `SendResultAsync` extensions on a caller-owned `HttpClient`: the raw overload returning `Result<HttpResponseMessage, HttpClientError>`, and two typed overloads (`JsonTypeInfo<TSuccess>` AOT-safe + `JsonSerializerOptions` convenience) that send, then delegate to `HttpResponseMessageResultExtensions.ToResultAsync` (the public surface, never re-implemented) and dispose the received response exactly once via `using`. Neither form creates/disposes the client or disposes the request; there is no retry.

**Namespace shadowing (resolved empirically).** The `ResulTus.HttpClient` namespace shadows `System.Net.Http.HttpClient`: inside it (and inside `ResulTus.HttpClient.Tests`), the simple name `HttpClient` binds to the namespace, producing CS0118 "is a namespace but is used like a type" (verified with a scratch build). The fix is a single alias `using HttpCli = System.Net.Http.HttpClient;` at the top of both `HttpClientSendResultExtensions.cs` and `SendResultTests.cs`. XML crefs stay IntelliSense-honest by using the fully qualified `System.Net.Http.HttpClient` / `System.Net.Http.HttpRequestMessage` / `System.Net.Http.HttpResponseMessage` (no CS1574/CS1580).

**Timeout vs caller cancellation (measured, not assumed).** A scratch net8.0 console app with a fake handler observed the BCL behavior directly: (a) `HttpClient.Timeout` elapsing → `System.Threading.Tasks.TaskCanceledException` whose `InnerException` is `System.TimeoutException` (message "The request was canceled due to the configured HttpClient.Timeout of … seconds elapsing."); (b) a caller-cancelled token (pre-cancelled and mid-flight) → `TaskCanceledException` whose `InnerException` is a nested `TaskCanceledException` (NOT `TimeoutException`); (c) an `HttpRequestException` from the handler surfaces as `HttpRequestException` with null inner; (d) an arbitrary `InvalidOperationException` from the handler surfaces unchanged (the BCL does not wrap it). The discriminator is therefore `ex is OperationCanceledException && ex.InnerException is TimeoutException` → `Timeout` kind; `ex is OperationCanceledException` (no timeout inner) → caller cancellation → rethrow; `ex is HttpRequestException` → `Transport` kind; any other exception is not caught and propagates. This matches the L3 doc link ("distinguishing a timeout from caller cancellation through the inner `TimeoutException`").

Tests: 11 new public-interface tests (raw success; typed success via both JSON paths; `HttpRequestException` → Transport on both raw and typed; client-timeout → Timeout with the inner `TimeoutException`; caller cancellation rethrown as `OperationCanceledException` on both raw and typed; exactly-one-send via a counting handler; arbitrary non-HTTP exception surfaced unchanged by reference; typed disposes the response once while request/client are untouched; raw disposes nothing; null client/request/jsonTypeInfo/options throw); HttpClient.Tests 30 → 41, full suite 659 → 670.
