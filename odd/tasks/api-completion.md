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
| T6 | S7, S8 | inline | done | see L15 |
| T7 | S9 | delegated direct | done | see L17, L18 |

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

### L15 — T6 completed: the public AOT-safe JSON converter

S7 landed as a new public type, `src/ResulTus.Serialization/ResultJsonConverter.cs`. The closed generic moved out of `ResultJsonConverterFactory.cs` and became `public sealed class ResultJsonConverter<T> : JsonConverter<Result<T>>` with two construction paths:

- **Ambient options (pre-existing behavior, unchanged).** `new ResultJsonConverter<T>()` resolves the payload converter from the `JsonSerializerOptions` in effect at each call, exactly as before, still behind the two `[UnconditionalSuppressMessage]`s on `GetValueConverter`. `ResultJsonConverterFactory` and `AddResulTusConverters()` are untouched and still instantiate it.
- **Source-generated metadata (the AOT-suitable path).** `new ResultJsonConverter<T>(JsonTypeInfo<T> valueTypeInfo)` stores the metadata a source-generated `JsonSerializerContext` emitted for `T`, and both `Write` and `Read` use it: `JsonSerializer.Serialize(writer, value, typeInfo)` and `JsonSerializer.Deserialize(ref reader, typeInfo)`. Neither touches `options.GetConverter`, and the path carries no suppression attributes — the 0-warning build of the library, whose `IsAotCompatible` analyzers run on every project, is the compile-level evidence that this path asks no dynamic code.

The XML remarks on both `ResultJsonConverter<T>` and `ResultJsonConverterFactory` now state which path is AOT-suitable; the factory remarks no longer say the direct-registration path is "tracked separately".

**Two observed System.Text.Json constraints, corrected after independent verification.**

1. **Reachability rule for the root type.** When an explicit source-generated `TypeInfoResolver` is configured, a root-level `Result<T>` resolves only when the *closed* `Result<T>` is in the context's reachable graph — most naturally because an annotated carrier payload declares it as a member. It is enough that the carrier is annotated; `T` being annotated makes no difference, and options reuse makes no difference (a fresh `JsonSerializerOptions` resolves the root as well). When the closed type is not in the graph — the first shape the initial L15 draft observed — System.Text.Json throws `NotSupportedException: JsonTypeInfo metadata for type 'Result<T>' was not provided by TypeInfoResolver ...`, and the exception text names the exact closed type. The constraint is NOT "the root must be annotated" and NOT "a root result always throws": both halves are pinned by tests. `Reachability_InTheContextGraph_ShouldResolveARootResult` and `Reachability_OutsideTheContextGraph_ShouldThrowNotSupportedException` hold both halves, and the round-trip tests pin the carrier shape (`SourceGeneratedContext_ShouldRoundTripACarrierPayload`) where results nested in an annotated payload resolve from `options.Converters`.
2. **Runtime metadata resolution for result members.** A generated context does not expand `Result<T>` members of a payload; they are resolved at runtime through the registered converters. No generator diagnostic was observed on any complete build — 0 warnings in the test project, in the verifier's independent net8.0 and net10.0 contexts, and in the reflection-disabled probe app. An earlier `SYSLIB1030` observation was recorded here and is retracted: it came from a probe app whose project reference was broken at the time, and the warning did not reproduce in either reflection mode afterward.

Behavior proof: both construction paths emit the same JSON. `ContextBoundConverter_ShouldProduceTheSameJsonAsTheFactory` compares them byte for byte for a success and a failure, and the carrier test does the same against the factory path plus an exact-wire assertion (`{"Name":"Ada","Age":{"isSuccess":true,"value":36}}`). `ContextBoundConverter_ShouldIgnoreTheAmbientPayloadConverter` is the sharpest proof of "through context metadata, not reflection": an `OffsetIntJsonConverter(100)` sits in the ambient options, and the bound converter still writes a plain `5` and reads it back as `5` on both the root and the carrier shape. The parameterless constructor keeps resolving the ambient converter (`105` under the same probe), so both paths' semantics are pinned side by side. Round trips cover success, failure, a `null` reference value, and a `null` metadata argument throwing `ArgumentNullException`.

**RED → GREEN:** the 10 tests were written first and the build failed with `CS0122` errors (`ResultJsonConverter<T>` is inaccessible; the verifier's RED replay against the baseline library counted 7 distinct error sites). After the implementation `tests/ResulTus.Serialization.Tests` is 74/74 (10 new), the build is 0 warnings and 0 errors, and the suite is **588/588** (baseline 578).

**The Native AOT publish smoke check is still not available on this host, observed twice.** `dotnet publish -c Release -r win-x64 -p:PublishAot=true` on a fresh console app fails with `error : Platform linker not found. Ensure you have all the required prerequisites documented at https://aka.ms/nativeaot-prerequisites, in particular the Desktop Development for C++ workload in Visual Studio.` The host has no `VC\Tools\MSVC` directory and no Windows Kits; installing the C++ workload is the owner's machine decision, so the clause stays pending and **no claim of Native AOT support is made**.

What *is* observed on this host, in a probe app outside the workspace that references the library and publishes a native win-x64 binary: `PublishTrimmed=true` with `JsonSerializerIsReflectionEnabledByDefault=false` (reflection-based serialization disabled for the whole application) and `TrimMode=full`. The published trimmed binary ran end to end: success and failure round-trips printed the byte-identical shapes the factory emits, and the process exited 0. The same run also proves the constraint behind the new constructor: the parameterless converter under reflection-disabled options failed with `InvalidOperationException: Reflection-based serialization has been disabled for this application...`, so the ambient path is *not* the AOT path, and the metadata path is the one that survives. Trace summary:

- `FALLBACK-PATH blocked as expected: InvalidOperationException: Reflection-based serialization has been disabled for this application. Either use the source generator APIs or explicitly configure the 'JsonSerializerOptions.TypeInfoResolver' property.`
- `SUCCESS-JSON: {"Name":"Ada","Age":{"isSuccess":true,"value":36},"Nickname":{"isSuccess":true,"value":null}}`
- `FAILURE-JSON: {"Name":"Bob","Age":{"isSuccess":false,"error":{"code":"user.missing","message":"no such user","type":"NotFound"}},"Nickname":{"isSuccess":false,"error":{"code":"nickname","message":"not allowed","type":"Validation"}}}` followed by `AOT-PATH OK`, exit 0.

**Risk:** item 3 (contracts others consume). A previously internal type of a shipped package is now public, and a public type changed file; the wire format is a contract and stays byte-identical, but a published package's API surface changed. Verification is delegated.

### L16 — owner decision, delegated verification of T6, and promoted probes

**Owner decision (2026-11-07, verbatim from the interactive answer):** asked whether to install the C++ workload to close the Native AOT clause, the owner chose **"Acepto la cláusula pendiente"**. The clause stays pending in this log with the exact host error above, and no AOT compatibility claim is made anywhere in the shipped docs.

**Delegated verification of `93a7667` against `ab8835c` (independent tree outside the workspace, redirected HOME/TMP/NUGET_PACKAGES, no writes to the workspace).**

| Clause | Verdict |
| --- | --- |
| S7.1 factory registration stays compatible | pass — diff is remarks-only for the factory; same probe source compiled against both builds: 62/62 wire lines byte-identical, both exit 0 |
| S7.2 public closed generic | pass — API dump delta: 1 type + 4 members added, 1 internal→public, 0 removals/renames/re-types; core `ResulTus.dll` dump byte-identical |
| S7.3 payload through context metadata, not `GetConverter` | pass — metadata path uses `JsonTypeInfo<T>` on both `Write` and `Read`; an ambient *poison* converter that throws on every call is never consulted on either path |
| S7.4 both paths documented, suitability stated | pass — type and factory remarks; 0-warning build (CS1591/CS1573 are errors) |
| S7.5 source-generated round trip proven | pass — writer's 74/74; independent net8.0 and net10.0 carriers round-trip success/failure/null, byte-equal to the factory; a camelCase context proves the passed metadata instance is the one consulted |
| S7.6 Native AOT publish/run smoke check | not verifiable — pending owner-decided clause (`Platform linker not found`) |
| S8 scope & compatibility | pass, in scope — 62 wire lines byte-identical incl. nested lists/arrays/dicts/nested/unit results and camelCase/indented variants; 13 malformed inputs throw the **same exception type and message** on both builds |

**Blockers: none.** Ten `PASS` consumer checks, suite 588/588 on the change against 578/578 at baseline, and the trim + reflection-disabled run reproduced (exit 0, byte-identical JSON, parameterless path blocked with `InvalidOperationException`).

**Advisories and their disposition:**

1. The original L15 statement of the root-type constraint did not reproduce as written. Reconciled by reproduction in the workspace: with a resolver configured, a root result works when the closed `Result<T>` is in the context's reachable graph and throws when it is not — the reachability rule in L15 above is the corrected form, and both halves now have a pin each.
2. `SYSLIB1030` did not reproduce under any complete build, in either reflection mode. Retracted from L15; see constraint 2 above.
3. L15's RED count overcounted: the verifier's replay of the new test file against the baseline library yields 7 distinct `CS0122` sites, not 12. Corrected in L15.
4. `ResultJsonConverterFactory.cs` gained a UTF-8 BOM in the doc-only edit. Noted only: the repository's `.editorconfig` declares `charset = utf-8-bom`, so the file now matches the declared convention.

**Promoted to `tests/ResulTus.Serialization.Tests/ResultJsonConverterVerifierTests.cs` (12 tests):** the two extra checks — a shared converter instance caching per `JsonSerializerOptions`, sentinel errors (`Error.None`, `default(Error)`) staying failures, a poisoned ambient converter being ignored on both paths, null/default payload round-trips, both-paths byte-equality, the net-agnostic camelCase context proving the passed metadata is the one used, exact read-error texts, and the two reachability halves. After promotion the Serialization project is **86/86** and the suite is **600/600**, 0-warning build. `ScalarOnlyContext` — a context with no result member anywhere in its reachable graph — is the pin's companion: annotating only `int` still rejects a root `Result<int>` with the same text.

### L17 — T7 completed: collection composition for the generic typed tier

S9 landed as a new file, `src/ResulTus.Collections/TypedResultCollectionExtensions.cs`. **Route: new file, not an extension of `ResultCollectionExtensions.cs`.** The directory already follows one file per result tier (`ResultCollectionExtensions` for `Result<T>`, `UnitResultCollectionExtensions` for unit `Result`, `ResultCollectionAsyncExtensions` for async), and the sibling `ResulTus.Extensions` package established the same convention for the typed tier by giving it `TypedResultExtensions.cs` rather than appending to `ResultExtensions.cs`. A dedicated file keeps the typed tier's two-type-parameter signatures out of the single-parameter family and mirrors both conventions exactly.

Nine public extension methods, each with `IEnumerable`, array and `IReadOnlyList` sources:

- `Sequence<TSuccess, TError>` ×3 → `Result<IReadOnlyList<TSuccess>, TError>`.
- `Traverse<TSource, TSuccess, TError>` ×3 → `Result<IReadOnlyList<TSuccess>, TError>`.
- `Fold<TSuccess, TError, TAcc>` ×3 → `Result<TAcc, TError>`.

All failure detection reads the receiver through `Deconstruct` — the only value/error accessor the typed struct actually exposes (it has no `TryGetValue`/`TryGetError`; nothing was invented) — and every method returns `Failure(error!)` on the first failed element in source order, so the caller receives the exact `TError` of the first failure, never an aggregate. `ValidationErrors` is just a `TError` here: a generic collection helper cannot combine arbitrary error types, so a `Result<TSuccess, ValidationErrors>` sequence fails with the first `ValidationErrors` object and leaves explicit merging to the caller. `null` sources, selectors and accumulators all throw `ArgumentNullException` before any work.

**Allocation.** The array and read-only-list `Fold` overloads allocate 0 B (no buffer, static accumulator), pinned beside a live sensitivity control — a `new byte[64]` probe reads a non-zero byte count, so a dead sensor cannot explain the 0 B reading. The materializing `Sequence`/`Traverse` overloads allocate only their output arrays, matched byte-for-byte against hand-written indexed loops, mirroring the sibling's allocation harness.

**Overload resolution.** The new methods share three names with the existing single-parameter family, but the receivers are disjoint (`Result<T>` vs `Result<TSuccess, TError>`), so no existing call site can move. Two consumer-shaped pins hold both sides: `Result<int>[]` still binds the generic-tier `Sequence<T>` and `Result<int, string>[]` binds the typed-tier one.

**RED → GREEN:** the tests were written first. The Collections test project failed with 39 errors — `CS0411`×28 (type inference failed because no typed-tier overload exists) and `CS1929`×11 (no `Fold` accepts a typed-result receiver). After the implementation `tests/ResulTus.Collections.Tests` is 84/84 (20 new), the solution build is 0 warnings and 0 errors, and the suite is **620/620** (baseline 600). No async variants and no LINQ-query syntax were added, per the owner's P2 rejection.

**Risk:** item 3 (contracts others consume) — nine new public extension members in a shipped package. The overload-resolution concern from L12/L13 does not extend here because the typed receivers are disjoint from every existing overload, but the two binding pins above exist precisely so a future change that did overlap would fail loudly.

### L18 — independent verification of T7 and the promoted probes

A separate verifier reviewed `git diff 7744e08..0a961e0` in a scratch tree outside the workspace, with home, temp and NuGet variables redirected.

**Verdict: S9 pass on every clause. No blockers.** The 135-check consumer probe found zero failures: failures placed at every index position, across all three source shapes, return the exact first-failure object by reference; selector and accumulator invocation counts track the failing index exactly (fail-fast, with lazy sources stopped at the failing element for both `Traverse` and `Sequence`); two distinct failure objects resolve to the lowest index; order is preserved with duplicates. A reference-type `TError`-identity check — the only proof that "the exact first error object" is preserved rather than merely an equal one — passed on all nine members. The API delta is exactly one type plus the nine methods, no other assembly moved a byte, the RED replay reproduced the 39 errors (`CS0411`x28, `CS1929`x11) against the baseline tree, and there are no async or query-syntax members.

**Allocation readings (all with a live sensitivity control):** array/list `Fold` 0 B; array/list `Sequence`/`Traverse` byte-equal to the hand-written output array; the IEnumerable steady state is the output array only, and its failure path reads **0 B**; the 32 B an array-backed success path shows is the BCL array enumerator, which an equivalent hand-written `foreach` over the interface also pays — the library adds nothing.

**Advisories, none change-caused.** (1) Array/list `Sequence`/`Traverse` allocate their exact output array before scanning, so a failure at index 0 discards it — the pre-existing generic tier (`ResultCollectionExtensions`) does the same, and the 0 B rule names the non-materializing operations, so this is documented behavior, not a breach. (2) The 32 B enumerator cost is the caller's, not the library's. (3) A throwing selector/accumulator adds a constant 272 B from deeper stack capture, not a buffer; repeating an enumeration 200 times over a failure path reads 0 B and never surfaces a pool error — the rented pool buffers are returned through the `finally`.

**Promoted to `tests/ResulTus.Collections.Tests/TypedResultCollectionVerifierTests.cs` (9 test cases):** the any-position first-failure matrix across all nine members, the two-failure lowest-index case with an instrumented accumulator, the lazy-source stop-at-failing-element consumption count, the grown-pool no-aliasing check, `Fold` 0 B with a live sensor, materializing overloads byte-equal to hand-written equivalents on all three shapes, and the throwing-selector pool-boundedness run. During promotion three verifier transcription-level assertion mismatches were corrected instead of forced through: the IEnumerable equivalent must enumerate through the same `(IEnumerable<T>)` cast so both sides pay the same 32 B enumerator, `list.ToArray()` must be hoisted out of the measured lambda because its copy is caller cost, and the throwing-ternary probe needed an Action-shaped call. After promotion Collections is **93/93** over three consecutive runs and the suite is **629/629**, 0-warning build.

**Feature state:** `api-completion` is complete. T1 (verified), T2 (assessed), T4 (verified), T5 (assessed `none`), T6 (verified, pending-clause decision recorded by the owner), T7 (verified) are done; T3 stays deferred to P2 as recorded. Per the roadmap, the next feature is `ResulTus.HttpClient`, then P3 README.






