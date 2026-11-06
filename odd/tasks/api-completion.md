# ResulTus API completion — typed results, boundaries, testing, and AOT

Objective: finish the already-shipped `Result<TSuccess, TError>` tier and close the API and implementation gaps explicitly accepted by the owner. Branch: `feature-1`.

Test runner: `dotnet test ResulTus.slnx`; targeted commands are recorded per task. New public APIs require XML docs and focused public-interface tests.

Delivery strategy: `auto-chain`. Forecast exceeds 400 authored lines; work units remain independent commits on `feature-1`. Push, PR creation, and merge remain the owner's decisions. Start implementation after P0 measurement and P1 allocation fixes, so baselines are recorded before implementation changes.

Risk: none anticipated. New APIs are additive; existing behavior must remain unchanged and every new behavior is pinned by tests. Reassess if implementation touches a listed high-risk surface.

## Specs

- **S1 — complete the typed-result tier.** The owner said: "Esto no es copiarle algo a ErrorOr. Es terminar lo que ya empezaste." `Result<TSuccess, TError>` receives synchronous counterparts to the established `Result<T>` pipeline: `Map`, `Bind`, error mapping, success/failure observers and branching, `Ensure`, recovery, and finalization. Each operation preserves the typed error on the failure path, short-circuits callbacks that do not apply, and has tests for both outcomes and null delegates. No new async operator family is added; the owner explicitly said the existing async operators are complete and adding more would invent work.
- **S2 — bridge the two result tiers.** Add `ToErrorResult` conversions from `Result<TSuccess, TError>` to `Result<TSuccess>` using a caller-supplied `TError -> Error` mapping (and a direct overload when `TError` is `Error`), plus a `ToGenericResult` lift from `Result<TSuccess>` to `Result<TSuccess, TError>` using `Error -> TError`. Preserve the success value and map only a failure. This enables `ValidationErrors.ToError()` to bridge validation results without unpacking them manually.
- **S3 — capture synchronous exceptions.** Add `Error.FromException(Exception)` and `Result.Try` for value-returning delegates and unit actions. The default error is `ErrorType.Unexpected` with a stable exception-type code and the exception message; a mapper overload allows domain-specific translation. Null delegates throw `ArgumentNullException`. `OperationCanceledException` is rethrown, preserving .NET cancellation control flow. Do not add async capture methods.
- **S4 — combine value-bearing results.** Add a binary `Combine` for `Result<TFirst>` and `Result<TSecond>` that returns `Result<(TFirst First, TSecond Second)>`, evaluates the first failure before the second, and preserves the first failure in argument order. It complements, and does not change, the existing `IEnumerable<Result>` unit overload.
- **S5 — defer Ensure error creation and support projection.** Add `Ensure` overloads taking `Func<Error>` for `Result<T>` and unit `Result`; invoke the factory only when the predicate fails on a success, never for an existing failure or passing predicate. Add `ToUnitResult()` for direct value-to-unit projection and a `Bind` overload that returns unit `Result`, preserving failures and short-circuiting as the existing operators do. Do not add async variants.
- **S6 — add framework-agnostic test assertions.** Add a `ResulTus.Testing` package targeting `net8.0`, with no dependency on a test framework. Its `ShouldSucceed` and `ShouldFail` helpers cover unit `Result`, `Result<T>`, and `Result<TSuccess, TError>`, return the success/error payload for further assertions, and throw a documented `ResultAssertionException` on an unexpected state. Test the package with the repository's existing xUnit test infrastructure; xUnit is not a production dependency.
- **S7 — offer an AOT-safe System.Text.Json path.** Keep the existing factory-based convenience registration compatible for ordinary applications. Expose a closed generic `ResultJsonConverter<T>` that can be directly registered with a source-generated `JsonSerializerContext`; resolve the payload converter through the context's type metadata rather than reflection-based `GetConverter`. Document both paths and clearly state which is suitable for Native AOT. Prove a source-generated round trip in tests and run a Native AOT publish/run smoke check on the available host. No claim of AOT support without observed proof.
- **S8 — scope and compatibility.** The owner explicitly excluded P2: `FailIf`, query syntax, multi-error inside `Error`, and `StackResult` remain unimplemented and undocumented. Existing async extensions are not duplicated. Existing overload resolution, JSON shape, and behavior remain unchanged.

## Tasks

| ID | Spec | Route | Status | Commit |
| --- | --- | --- | --- | --- |
| T1 | S1, S2 | inline | pending | — |
| T2 | S3 | inline | pending | — |
| T3 | S4 | inline | pending | — |
| T4 | S5 | inline | pending | — |
| T5 | S6 | inline | pending | — |
| T6 | S7, S8 | inline | pending | — |

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
- **Ordering:** finish P0 baselines and P1 allocation changes first. Then T1–T6. P3's final README examples depend on T1; its benchmark tables depend on P0 and P1.

### L4 — test-first policy

For each behavior task, add one public-interface test per rule and observe the focused test fail before implementation, then pass after implementation. Keep relevant existing behavior pinned when a shared helper or extension overload-resolution path is touched. Run the focused test project and `dotnet test ResulTus.slnx` at feature closure. The AOT path also requires a source-generated JSON round trip and an actual publish/run smoke check; if the native toolchain is unavailable, record that proof as unavailable rather than claiming AOT success.

### L5 — delivery and risk

All work stays inline; the owner asked for no writer delegation in this ODD stream and no delegation trigger is being invoked. Keep each behavior and its tests/docs in one work-unit commit. No push, PR, or merge is authorized. Reassess the risk line if a change modifies an existing public contract or another high-risk surface.
