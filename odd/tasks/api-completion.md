# ResulTus API completion — typed results, boundaries, testing, and AOT

Objective: finish the already-shipped `Result<TSuccess, TError>` tier and close the API and implementation gaps explicitly accepted by the owner. Branch: `feature-1`.

Test runner: `dotnet test ResulTus.slnx`; targeted commands are recorded per task. New public APIs require XML docs and focused public-interface tests.

Delivery strategy: `auto-chain`. Forecast exceeds 400 authored lines; work units remain independent commits on `feature-1`. Push, PR creation, and merge remain the owner's decisions. Start implementation after P0 measurement, P1 allocation fixes, and the now-authorized P2 phase. P2 owns `Combine(Result<T>)`; implement it once there, not again in this feature. The generic typed-result collection operators follow the existing API completion tasks; the separate `ResulTus.HttpClient` module follows them and precedes P3.

Risk: none anticipated. New APIs are additive; existing behavior must remain unchanged and every new behavior is pinned by tests. Reassess if implementation touches a listed high-risk surface.

## Specs

- **S1 — complete the typed-result tier.** The owner said: "Esto no es copiarle algo a ErrorOr. Es terminar lo que ya empezaste." `Result<TSuccess, TError>` receives synchronous counterparts to the established `Result<T>` pipeline: `Map`, `Bind`, error mapping, success/failure observers and branching, `Ensure`, recovery, and finalization. Each operation preserves the typed error on the failure path, short-circuits callbacks that do not apply, and has tests for both outcomes and null delegates. No new async operator family is added; the owner explicitly said the existing async operators are complete and adding more would invent work.
- **S2 — bridge the two result tiers.** Add `ToErrorResult` conversions from `Result<TSuccess, TError>` to `Result<TSuccess>` using a caller-supplied `TError -> Error` mapping (and a direct overload when `TError` is `Error`), plus a `ToGenericResult` lift from `Result<TSuccess>` to `Result<TSuccess, TError>` using `Error -> TError`. Preserve the success value and map only a failure. This enables `ValidationErrors.ToError()` to bridge validation results without unpacking them manually.
- **S3 — capture synchronous exceptions.** Add `Error.FromException(Exception)` and `Result.Try` for value-returning delegates and unit actions. The default error is `ErrorType.Unexpected` with a stable exception-type code and the exception message; a mapper overload allows domain-specific translation. Null delegates throw `ArgumentNullException`. `OperationCanceledException` is rethrown, preserving .NET cancellation control flow. Do not add async capture methods.
- **S4 — combine value-bearing results.** Add a binary `Combine` for `Result<TFirst>` and `Result<TSecond>` that returns `Result<(TFirst First, TSecond Second)>`, evaluates the first failure before the second, and preserves the first failure in argument order. It complements, and does not change, the existing `IEnumerable<Result>` unit overload.
- **S5 — defer Ensure error creation and support projection.** Add `Ensure` overloads taking `Func<Error>` for `Result<T>` and unit `Result`, plus `Func<T, Error>` for `Result<T>`. Invoke the factory only when a successful result fails its predicate, pass the success value to the latter, and never invoke either factory for an existing failure or passing predicate. Add `ToUnitResult()` for direct value-to-unit projection and a `Bind` overload that returns unit `Result`, preserving failures and short-circuiting as the existing operators do. Do not add async variants.
- **S6 — add framework-agnostic test assertions.** Add a `ResulTus.Testing` package targeting `net8.0`, with no dependency on a test framework. Its `ShouldSucceed` and `ShouldFail` helpers cover unit `Result`, `Result<T>`, and `Result<TSuccess, TError>`, return the success/error payload for further assertions, and throw a documented `ResultAssertionException` on an unexpected state. Test the package with the repository's existing xUnit test infrastructure; xUnit is not a production dependency.
- **S7 — offer an AOT-safe System.Text.Json path.** Keep the existing factory-based convenience registration compatible for ordinary applications. Expose a closed generic `ResultJsonConverter<T>` that can be directly registered with a source-generated `JsonSerializerContext`; resolve the payload converter through the context's type metadata rather than reflection-based `GetConverter`. Document both paths and clearly state which is suitable for Native AOT. Prove a source-generated round trip in tests and run a Native AOT publish/run smoke check on the available host. No claim of AOT support without observed proof.
- **S8 — scope and compatibility.** The owner subsequently authorized starting P2 after P1. P2's previously named gaps (`FailIf`, `Combine(Result<T>)`, query syntax, multi-error inside `Error`, and `StackResult`) will be specified in a separate feature document after P1 and before the remaining tasks in this feature; `Combine(Result<T>)` is implemented exactly once in P2. Existing async extensions are not duplicated. Existing overload resolution, JSON shape, and behavior remain unchanged.
- **S9 — compose collections of the generic typed tier.** Add `Sequence`, `Traverse`, and `Fold` over `Result<TSuccess, TError>`, with `IEnumerable`, array, and `IReadOnlyList` sources. `Sequence` and `Traverse` preserve value order and return the first `TError` in source order; `Fold` stops at that first failure. Do not aggregate multiple `TError` values: a generic collection helper cannot combine arbitrary error types, and existing result collections are fail-fast. In particular, `ValidationErrors` remains the exact error object of the first failure; callers wanting aggregation must do it explicitly. Array/list non-materializing variants allocate 0 B; materializing variants allocate only their output arrays after pool warm-up.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2 | inline | done | see L9 |
| T2 | S3 | inline | pending | — |
| T3 | S4 | inline | deferred to P2 | — |
| T4 | S5 | inline | pending | — |
| T5 | S6 | inline | pending | — |
| T6 | S7, S8 | inline | pending | — |
| T7 | S9 | inline | pending | — |

## Log

### L1 — original request (verbatim)

> Ahora solo vamos a hablar, habria mas metodos de extension, mas implementaciones en la libreria, etc que podamos añadir?

### L2 — accepted scope (verbatim)

> Esto no es copiarle algo a ErrorOr. Es terminar lo que ya empezaste.
> Lo demás, ordenado por cuánto lo defendería
> 1. Capturar excepciones: el borde que todo el mundo necesita. Tenés ToException() (Error → Exception) pero no el camino inverso. No existe Result.Try(() => ...) ni Error.FromException. La librería te deja salir del mundo Result a excepciones, pero no entrar desde ellas. Y el lugar donde más se usa Result es justamente el borde: I/O, JSON, código legacy. Hoy cada usuario escribe ese try/catch a mano, y cada uno lo escribe distinto.
> 2. Combine para Result<T>. Existe solo para el Result unitario (UnitResultCollectionExtensions.cs:25). Combinar dos resultados con valor en Result<(A, B)> hoy exige desempaquetar a mano. Es una asimetría interna, no una feature nueva.
> 3. Ensure con error perezoso. Ensure(predicate, error) construye el error siempre. Error es struct, así que no hay heap — pero si el mensaje es interpolado, ese string se paga en el camino de éxito. Una sobrecarga Func<Error> lo difiere. Es la única propuesta de la lista motivada por performance, no por features: encaja con el charter sin discusión.
> 4. Proyección Result<T> → Result. UnitResultExtensions.Bind va de unitario a valorado en los dos sentidos, pero ResultExtensions no tiene Bind que devuelva unitario ni forma de descartar el valor. Asimetría chica, barata, molesta.
> 5. ResulTus.Testing. Helpers de aserción. No toca el core, no toca el charter de allocations, y es de lo que más empuja adopción.
> 6. Serialización AOT. ResultJsonConverterFactory usa Activator.CreateInstance en CreateConverter (:58). Eso es hostil a source-gen y trimming. No es un gap de API, es un gap de implementación para consumidores .NET 8+ con AOT. Lo dejo anotado porque tarde o temprano aparece como issue.
> Lo que NO propongo: async ya está completo para todos los operadores que existen (MapAsync, BindAsync, TapAsync, EnsureAsync, MatchAsync, RecoverAsync, FinallyAsync, con receptores ValueTask y Task). Agregar sobrecargas ahí sería inventar trabajo.
> Y los temas de P2 —FailIf, query syntax, multi-error dentro de Error, StackResult— siguen siendo una decisión de producto que no tomaste. No los meto en la bolsa.

### L3 — repository evidence and ordering

- The typed `Result<TSuccess, TError>` currently exposes factories, `Match`, and `Deconstruct`; it has no `TryGetValue` and no extension pipeline. Its packed 13-byte layout is deliberate and must not be changed.
- `ResulTus.Extensions` already supplies synchronous operations for `Result<T>` and unit `Result`, and async operations for those two types. The new typed-result surface is synchronous only as required by S8.
- `UnitResultCollectionExtensions.Combine(IEnumerable<Result>)` exists; there is no value-bearing binary overload.
- `Ensure(predicate, Error)` exists for `Result<T>` and unit `Result`; it accepts an eagerly constructed struct error.
- `ResultJsonConverter<T>` currently uses `JsonSerializerOptions.GetConverter(Type)`, and `ResultJsonConverterFactory.CreateConverter` closes it using `MakeGenericType` and `Activator.CreateInstance`. The current converter is internal.
- Microsoft .NET documentation describes direct registration of a closed generic converter with source-generated metadata as the AOT-compatible alternative to reflective factory creation: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/converters-how-to
- Existing project convention: production projects target `net8.0`; test projects target `net10.0`, use xUnit v3 and FluentAssertions. The new `ResulTus.Testing` library will not depend on either test framework.
- **Ordering:** P0 baselines, P1 allocation changes, P2, remaining API-completion tasks (T1, T2, T4–T6), T7, the separate `ResulTus.HttpClient` feature, then P3. T3/S4 is folded into P2 and must not be implemented twice. P3 examples depend on the API tasks; its benchmark tables depend on P0 and P1.

### L4 — test-first policy

For each behavior task, add one public-interface test per rule and observe the focused test fail before implementation, then pass after implementation. Keep relevant existing behavior pinned when a shared helper or extension overload-resolution path is touched. Run the focused test project and `dotnet test ResulTus.slnx` at feature closure. The AOT path also requires a source-generated JSON round trip and an actual publish/run smoke check; if the native toolchain is unavailable, record that proof as unavailable rather than claiming AOT success.

### L5 — delivery and risk

All work stays inline; the owner asked for no writer delegation in this ODD stream and no delegation trigger is being invoked. Keep each behavior and its tests/docs in one work-unit commit. No push, PR, or merge is authorized. Reassess the risk line if a change modifies an existing public contract or another high-risk surface.

### L6 — P2 sequencing authorization (verbatim)

> vale cuando acabes p1 comienzas p2

This supersedes the earlier P2 exclusion in L2. Once P1 is closed, create a separate P2 task document and begin those gaps before resuming this feature's remaining tasks. The already-authorized `Combine(Result<T>)` is shared scope; P2 owns its single implementation through T3/S4.

### L7 — generic collection and value-aware Ensure additions (verbatim)

> Entonces esto lo metemos tambien?
> Sí, pero no sumaría métodos por simetría nomás. El hueco que más defendería, después de terminar lo ya acordado, es componer colecciones del tier tipado: Sequence, Traverse y Fold para Result<TSuccess, TError>. Servirían para procesar lotes sin convertir ni perder el error de dominio.
> Antes habría que fijar una regla: ¿cortamos ante el primer TError o permitimos agregar varios? Para ValidationErrors, esa diferencia importa.
> Como mejora menor, consideraría Ensure(predicate, errorFactory) que recibe el valor —Func<T, Error)— para construir mensajes dependientes de él solo cuando falla. La sobrecarga perezosa Func<Error> que ya acordamos cubre el caso general; agregaría esta únicamente si aparece en usos reales.
> Antes habría que fijar una regla: ¿cortamos ante el primer TError o permitimos agregar varios? Para ValidationErrors, esa diferencia importa.
> Lo que tu consideres

This authorizes S9, typed-result `Sequence`/`Traverse`/`Fold`, and extends S5 with `Ensure(Func<T, Error>)`. For S9, choose the existing library convention: fail fast with the first `TError` in source order; do not invent generic multi-error aggregation. This keeps a `ValidationErrors` instance intact and leaves explicit merging to the caller.

### L8 — HttpClient module sequencing (verbatim)

> Esto si se hace:El único candidato que investigaría más adelante es ResulTus.HttpClient: convertir respuestas HTTP, ProblemDetails y fallos de transporte en resultados. Pero el mapeo de status, errores y cuerpos JSON es una decisión de política; sin usos reales repetidos, sería una capa opinada de más.
> el recomendado

`ResulTus.HttpClient` is separately tracked in `odd/tasks/httpclient-integration.md`. It runs after this feature's API work and before P3; its selected error model is `Result<T,HttpClientError>`.

### L9 — T1 completed: the typed tier and its bridges

S1 landed in `src/ResulTus.Extensions/TypedResultExtensions.cs`: eleven synchronous operators mirroring the `Result<T>` pipeline without dropping the typed error — `Map`, `Bind`, `MapError`, `Tap`, `OnSuccess`, `OnFailure`, `Switch`, `Ensure`, two `Recover` overloads and `Finally`. Every failure path carries the receiver's `TError` unchanged, every inapplicable callback is skipped, and a `null` delegate throws `ArgumentNullException` (all twelve call shapes are pinned by one test). Reading the receiver goes through `Deconstruct` rather than `Match`, so no closure is created inside the operator and a pipeline built from non-capturing delegates allocates 0 B on the success and failure paths — pinned by a test, because §2 of the performance charter makes that a hard rule.

Two behaviors are worth recording because a reader will hit them:

- `Recover` inherits the documented overload overlap of the `Error` tier: `Func<TError, TSuccess>` and `Func<TError, Result<TSuccess, TError>>` differ only in the delegate's return type, and a success value converts implicitly to the struct, so a lambda such as `error => error.Code` is ambiguous for `Result<int, TError>`. Both XML documents say so and point at the explicitly typed delegate.
- The typed tier has no error sentinel, so `Finally` passes `default` for the error on a success and `default` for the value on a failure. The alternative — inventing something like `Error.None` for an arbitrary `TError` — was rejected.

S2 landed in `src/ResulTus.Extensions/ResultConversions.cs`: `ToErrorResult(Func<TError, Error>)`, the direct `ToErrorResult()` for `Result<TSuccess, Error>`, and `ToGenericResult(Func<Error, TError>)`. All three preserve the success value and map only a failure, and the two mappers are null-checked.

**RED → GREEN:** the tests were written first. S1 produced 68 compile errors (`CS0411`/`CS1929`: the operators did not exist on `Result<int, string>`) and S2 produced 16 (`CS1061`). After the implementations the Extensions project is 120/120 (19 pipeline tests, one allocation probe and 7 bridge tests added to the 93 from P2/T1), the solution build is 0 warnings and 0 errors, and the suite is 520/520.

**Risk:** item 3 (contracts others consume) — new public extension surface in a shipped package. The verifier's overload-resolution concern from P2/T1 does not extend here: every typed operator's receiver is `Result<TSuccess, TError>`, which no existing overload accepts, so no current call site can move. The allocation behavior is the property a verifier should probe hardest.

