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
| T2 | S3 | inline | done | see L11 |
| T3 | S4 | inline | deferred to P2 | — |
| T4 | S5 | inline | done | see L12 |
| T5 | S6 | inline | done | see L14 |
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

- `Recover` is overloaded twice, once with `Func<TError, TSuccess>` and once with `Func<TError, Result<TSuccess, TError>>`. The two do not collide: a lambda returning a `TSuccess` binds the value overload because the identity conversion beats the user-defined conversion into the struct, and a lambda returning the struct binds the other. The author wrote the opposite in the first draft of the XML docs; independent verification disproved it and the docs were corrected (see L10).
- The typed tier has no error sentinel, so `Finally` passes `default` for the error on a success and `default` for the value on a failure. The alternative — inventing something like `Error.None` for an arbitrary `TError` — was rejected.

S2 landed in `src/ResulTus.Extensions/ResultConversions.cs`: `ToErrorResult(Func<TError, Error>)`, the direct `ToErrorResult()` for `Result<TSuccess, Error>`, and `ToGenericResult(Func<Error, TError>)`. All three preserve the success value and map only a failure, and the two mappers are null-checked.

**RED → GREEN:** the tests were written first. S1 produced 68 compile errors (`CS0411`/`CS1929`: the operators did not exist on `Result<int, string>`) and S2 produced 16 (`CS1061`). After the implementations the Extensions project is 120/120 (19 pipeline tests, one allocation probe and 7 bridge tests added to the 93 from P2/T1), the solution build is 0 warnings and 0 errors, and the suite is 520/520.

**Risk:** item 3 (contracts others consume) — new public extension surface in a shipped package. The verifier's overload-resolution concern from P2/T1 does not extend here: every typed operator's receiver is `Result<TSuccess, TError>`, which no existing overload accepts, so no current call site can move. The allocation behavior is the property a verifier should probe hardest.

### L10 — independent verification of T1 and the corrections it forced

A separate verifier reviewed `git diff ad889be..ac24bfc` in a scratch tree outside the workspace. It rebuilt the RED from the baseline source plus both test files (88 errors: `CS0411`×10, `CS1061`×42, `CS1929`×36 — L9's 68 and 16 are the two files measured on their own, in the order they were written), and then confirmed the reviewed tree: build 0 warnings / 0 errors, suite 520/520, baseline 493/493.

**Verdicts: S1 pass, S2 pass, build pass. No blockers.** Evidence beyond the change's own tests: an independent allocation harness with a sensitivity control (`new byte[64]` per call reads 880,000 B, so the 0 B readings are not a dead sensor) measured every operator on both paths and the chain at **0 B**, equal to hand-written `IsSuccess` branches; a custom `readonly record struct` error survived the whole pipeline including a `default` error; counters proved each callback fires zero times when inapplicable and exactly once when applicable; every public method throws on a `null` delegate; `ToErrorResult()` was confirmed to bind the parameterless overload; the public API dump showed 15 additions and no removals, renames or re-types; bridged results serialize byte-identically to hand-built ones.

Two advisories, both accepted:

1. **The `Recover` ambiguity note was false** — and it was false in the pre-existing `Result<T>` docs too, not only in the new file. C# overload betterness prefers the identity conversion into `TSuccess` over the user-defined conversion into the struct, so `failure.Recover(error => error.Code)` compiles without a cast. Both XML documents were corrected and a test now proves the binding (`Recover_WithANaturallyTypedLambda_ShouldBindTheValueOverload`); the pre-existing remarks in `ResultExtensions.cs` were corrected in the same edit because a false statement in public docs is a defect regardless of when it shipped.
2. **The RED count in L9 was imprecise**, now stated exactly above. The evidence was real; only the arithmetic in the narrative was ambiguous.

Seven of the verifier's probes were promoted to `tests/ResulTus.Extensions.Tests/TypedResultPipelineVerifierTests.cs`: per-operator allocation on both paths (the author's probe only covered a chain), a chained pipeline, `default`-error preservation, `Finally` sentinels for value and reference payloads, the parameterless bridge binding, both bridge mappers skipped on success, and callback counting. After promotion the Extensions project is 128/128 and the suite is **528/528**.

### L11 — T2 completed: synchronous exception capture

S3 landed in the core package. `Error.FromException(Exception)` produces an `ErrorType.Unexpected` error whose code is the exception's **runtime** type full name and whose message is the exception's message; the code is taken from the type rather than a fixed string so two different failures stay distinguishable, and it does not depend on culture. No stack trace is captured, which is what keeps `Error` at its two-reference layout and `Result<T>` at 32 B.

`Result.Try` has four overloads: value-producing and unit delegates, each with a default capture and a mapper that translates the exception into a domain error. The four behaviors the spec fixes are all pinned: the delegate runs exactly once, a thrown exception becomes a failure, the mapper runs only when an exception was thrown, and `OperationCanceledException` — including derived types such as `TaskCanceledException` — is **rethrown before the mapper**, so cancellation keeps its normal control flow. `null` delegates and `null` mappers throw `ArgumentNullException`.

One boundary is worth stating rather than leaving implicit: `catch (Exception)` also captures `OutOfMemoryException`, because the spec says to capture synchronous exceptions and names cancellation as the only exclusion. Filtering more would be a product decision the spec does not authorize, and in practice the capture fails again while building the error, so the caller still observes the original condition. `Error.FromException` documents that it captures whatever it is given, cancellation included, and points at `Result.Try` for the control-flow-aware path.

**RED → GREEN:** the tests were written first and the project failed with 50 errors (`CS0117`: neither `Result.Try` nor `Error.FromException` existed). After the implementation `tests/ResulTus.Tests` is 60/60 (13 new), the solution build is 0 warnings and 0 errors, and the suite is **541/541**. The success path is pinned at 0 B by an allocation probe; the failure path necessarily touches the exception's message string, so no 0 B claim is made for it.

**Risk:** none of items 1-6. Both members are additive statics — `Result.Try` on the static `Result` class and `Error.FromException` on the struct — so no existing call site can change binding, and the two behaviors that could silently regress (cancellation control flow and mapper short-circuiting) are covered by tests that would fail loudly.

### L12 — T4 completed: deferred `Ensure`, `ToUnitResult` and the unit `Bind`

S5 added five public members across three files:

- `ResultExtensions.Ensure<T>(this Result<T>, Func<T, bool>, Func<Error>)` and `Ensure<T>(this Result<T>, Func<T, bool>, Func<T, Error>)`. Both read the receiver first and return it unchanged on an existing failure, so neither the predicate nor the factory runs there. The factory is invoked at most once, and only when a successful result fails its predicate. The value-aware overload exists so a message can name the rejected value (`Ensure(v => v > 10, v => Error.Validation("too-small", $"got {v}"))`), which the zero-parameter form cannot do because it never sees the value.
- `UnitResultExtensions.Ensure(this Result, Func<bool>, Func<Error>)`, the same contract on a unit result.
- `ResultExtensions.Bind<T>(this Result<T>, Func<T, Result>)`, which projects a value-bearing pipeline onto the unit result without wrapping it in a dummy success value.
- `ResultConversions.ToUnitResult<T>(this Result<T>)`, the inverse of the unit `Map`: it drops the value and keeps the error.

Three overloads now share the name `Ensure`. They are separable by the third argument — an `Error` value binds the eager form, a zero-parameter lambda binds `Func<Error>`, a one-parameter lambda binds `Func<T, Error>` — and every pre-existing call site in the repository and its tests passes an `Error` value, so none moved. `Bind` gained a second family: the existing one returns `Result<TOut>` and the new one returns `Result`, and a call site that produced a unit result did not compile before, so nothing shifted there either.

**RED → GREEN:** the tests were written first and the project failed with 54 `CS1660` errors (a lambda cannot convert to `Error`, because only the eager overload existed). After the implementation `tests/ResulTus.Extensions.Tests` is 143/143 (15 new), the solution build is 0 warnings and 0 errors, and the suite is **556/556**. The new file pins both outcomes of every member, the exact call counts of the predicates and factories, the value the value-aware factory receives, error preservation through `ToUnitResult` and the unit `Bind`, every null-delegate shape, and 0 B of allocation for seven call shapes built from `static` delegates and interned literals.

**Risk:** item 3 (contracts others consume). The members are additive and the repository's own call sites are unaffected, but three `Ensure` overloads now sit beside the existing one and a caller outside this repository that passes something like a bare `default` for the error could find it ambiguous where it used to bind. That is exactly the class of risk P2/T1 carried, so this unit goes to independent verification.

### L13 — independent verification of T4 and the promoted probes

A separate verifier reviewed `git diff d50cbb4..4bbd109` in a scratch tree outside the workspace, with `HOME`, `TMP`, `TEMP` and `TMPDIR` redirected into that tree. It confirmed both trees: build 0 warnings / 0 errors, suite **556/556** on the change against **541/541** at baseline.

**Verdicts: S5 pass, build requirement pass. No blockers.** Evidence beyond the change's own tests:

- **Overload resolution from a consumer's seat.** A probe project referencing only the built `ResulTus` and `ResulTus.Extensions` assemblies compiled 28 call shapes against both trees. Every shape that compiled at baseline still binds the same member — `Ensure(pred, errorVar)`, `Ensure(pred, Error.NotFound(...))`, `Ensure(pred, Error.None)`, `Ensure(pred, default(Error))`, `Ensure(pred, cond ? a : b)`, both unit forms with an `Error` argument, and the three value-returning `Bind` shapes. Every shape that compiles only now did not compile at baseline, so it cannot have shifted.
- **Behavior.** 36 checks passed: counters showed 0/0 invocations on an existing failure, 1/0 when the predicate holds and 1/1 when a success fails it — for all three factory overloads including a method group; the value-aware factory received `987654321` and `default(int)` exactly.
- **Allocation.** An independent harness with two live sensitivity controls (879,912 B for `new byte[64]` per call, 240,000 B for `new object()`) read **0 B** on every new member on every path, equal to hand-written equivalents.
- **API and analyzer.** The public API dump moved from 74 to 79 members: exactly the 5 additions, nothing removed, renamed or re-typed. `ConstantEnsureAnalyzer` still reports only on constant predicates and produced no false diagnostic on either new overload, because it keys on the written argument count and `Arguments[0]`.
- **Error preservation through the sentinels.** `ToUnitResult` and the unit `Bind` keep a failure a failure for `Error.None` and `default(Error)`, rather than reading the zero error as a success.

One finding with real weight, now closed: **a bare `default` literal is ambiguous** between the eager `Error` parameter and `Func<Error>` (`CS0121`), where it compiled before. L12 predicted this risk in prose; the XML remarks of both deferred overloads now state it and point at `default(Error)` as the explicit form, and a test pins that `default(Error)` still binds the eager overload and stores that error. Three further advisories were recorded without code: an overloaded method-group factory (`Error Make()` beside `Error Make(int)`) needs a cast, but that shape did not compile at baseline either; `.Bind(v => Error.Forbidden("nope"))` compiles now through the implicit `Error`-to-`Result` conversion and returns a failure, which is new capability rather than a rebinding; and one never-compiling shape changed its compiler error code, which is not an output change for working code.

Six probes were promoted to `tests/ResulTus.Extensions.Tests/EnsureFactoryAndProjectionVerifierTests.cs`: the method-group factory skipped on an existing failure, the explicitly typed `default(Error)` still binding eager, the value-aware factory receiving `default(int)`, sentinel-error preservation through `ToUnitResult` and the unit `Bind`, and a second allocation sweep over cached non-capturing delegates. After promotion the Extensions project is 149/149 and the suite is **562/562** with a 0-warning, 0-error build.

### L14 — T5 completed: the `ResulTus.Testing` package

S6 landed as a new project, `src/ResulTus.Testing`, targeting `net8.0` with `IsTrimmable` and `IsAotCompatible` like the rest of the libraries. Its only reference is the core `ResulTus` project: there is no `PackageReference` of any kind in the csproj, so no test framework or assertion library is a production dependency.

`ResultAssertions` exposes six helpers, two per result shape:

| Shape | `ShouldSucceed` returns | `ShouldFail` returns |
| --- | --- | --- |
| unit `Result` | the receiver | the `Error` |
| `Result<T>` | the success value `T` | the `Error` |
| `Result<TSuccess, TError>` | the success value | the typed error `TError` |

Every helper returns the payload the caller usually wants next, so a test asserts on it without a cast or a second call. `ResultAssertionException` is sealed, lives in the same package, derives from `Exception`, and carries the actual error in the message — code, type and message for the `Error` tier, and the rendered typed error for the generic tier. One deliberate omission: `ShouldFail` on a successful `Result<T>` never puts the success value in the message, because a value that failed an assertion is data the test did not choose to print. A test pins that with a token-shaped string.

Two of the package's own constraints are pinned by tests rather than trusted: the assembly's `TargetFrameworkAttribute` must read `.NETCoreApp,Version=v8.0`, and `GetReferencedAssemblies()` must contain nothing named `xunit`, `NUnit`, `MSTest`, `FluentAssertions` or `Microsoft.NET.Test.Sdk`. The second needed an `[UnconditionalSuppressMessage("Trimming", "IL2026")]` because the repository sets `IsTrimmable` for every project, test projects included, and `GetReferencedAssemblies()` is annotated as unsafe under trimming. That is a property of the test, not of the package.

**RED → GREEN:** the two csproj files and their solution entries were created first as scaffolding, then the 16 tests were written and the project failed with 46 errors (`CS1061` for the missing helpers, `CS0246` for the missing exception type). After the implementation `tests/ResulTus.Testing.Tests` is 16/16, the solution build is 0 warnings and 0 errors, and the suite is **578/578**.

**Risk:** none of items 1-6. Nothing references the new project, so no existing behavior can move; the work is a new directory plus two solution lines, and undoing it is deleting them. Worth stating explicitly rather than hiding: the project is packable by default, so a future `dotnet pack` over the solution will produce a `ResulTus.Testing` package. The repository has no CI, pack script or publishing configuration at all, so this change publishes nothing, and whether that package ships is the owner's decision under ordinary policy — which is why it is not counted as a release-surface risk here, and is noted for the P3 README instead.






