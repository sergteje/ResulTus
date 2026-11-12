# ResulTus

[![CI](https://github.com/sergteje/ResulTus/actions/workflows/ci.yml/badge.svg?branch=main&label=CI)](https://github.com/sergteje/ResulTus/actions/workflows/ci.yml)
[![Downloads](https://img.shields.io/nuget/dt/ResulTus?label=downloads)](https://www.nuget.org/packages/ResulTus)
[![NuGet](https://img.shields.io/nuget/v/ResulTus?label=nuget)](https://www.nuget.org/packages/ResulTus)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/branding/logo-dark.png">
  <img src="assets/branding/logo-light.png" alt="ResulTus - Make every result count." width="540">
</picture>

[English](README.md) · **Español**

ResulTus es una biblioteca `Result` de alto rendimiento para .NET que representa el éxito o el fallo como un `readonly record struct` en línea, sin asignación de heap en ninguna de las dos rutas.

## El contrato

Cada afirmación siguiente es un hecho medido, no una promesa. Cada número enlaza a la fila de [`docs/performance.md`](docs/performance.md) que lo registró.

- **Cero asignación de heap en la ruta de éxito *y* en la ruta de fallo.** Construir un éxito y construir un fallo asignan [0 B](docs/performance.md#allocation--measured-2026-10-29), incluso cuando el fallo se construye a partir de un mensaje distinto que no está en caché.
- **`Error` es un `readonly record struct` en línea de 24 bytes** que contiene un código, un mensaje opcional y una categoría — sin carga útil sin tipo, sin heap ([§4.2](docs/performance.md#42-struct-size-is-a-design-problem-not-a-match-problem--resolved-2026-10-28)).
- **`Result<int>` ocupa 32 bytes**, exactamente dos resultados por línea de caché de 64 bytes sin cruzar de línea. El `Error` en línea es lo que hace posible el 0 B; los 32 B son el precio deliberado de mantenerlo en línea en lugar de detrás de una referencia ([§4.2](docs/performance.md#42-struct-size-is-a-design-problem-not-a-match-problem--resolved-2026-10-28)).
- **El tier genérico `Result<TSuccess, TError>` ocupa 13 bytes** con un tipo de error sin referencias, más compacto que los 16 B de todos los competidores ([§4.2](docs/performance.md#42-struct-size-is-a-design-problem-not-a-match-problem--resolved-2026-10-28), [§4.5](docs/performance.md#45-generic-tier--packed-13-b-versus-aligned-16-b-measured-2026-10-29)).

Esos cuatro hechos son un intercambio, declarado abiertamente: el tier por defecto de 32 B y su coste en el recorrido de arrays se muestran junto a las victorias en [Benchmarks](#benchmarks). Aquí no se oculta nada.

## Paquetes

| Paquete | Propósito |
| --- | --- |
| `ResulTus` | Los tipos centrales: `Result`, `Result<T>`, `Result<TSuccess, TError>`, `Error`, `ErrorType`. |
| `ResulTus.Extensions` | Los operadores fluidos (`Map`, `Bind`, `Ensure`, `Recover`, `MapError`, `Tap`, `Finally`, `Switch`) y las conversiones, síncronas y asíncronas. |
| `ResulTus.Collections` | Operaciones de colecciones sobre secuencias, arrays y listas de solo lectura de resultados (`Traverse`, `Sequence`, `Fold`, `Partition`). |
| `ResulTus.Logging` | Extensiones de `ILogger` que emiten el resultado como campos con nombre, no como una cadena renderizada. |
| `ResulTus.Serialization` | Conversores de `System.Text.Json` para `Result<T>`, `Error` y `ErrorType` con un formato de cable fijo y estricto. |
| `ResulTus.Diagnostics` | Medición y trazado opcionales mediante `Activity`/`DiagnosticListener` alrededor de operaciones que producen resultados. |
| `ResulTus.AspNetCore` | Convierte resultados en action results de ASP.NET Core y en ProblemDetails RFC 9457. |
| `ResulTus.HttpClient` | Convierte respuestas HTTP, documentos ProblemDetails y fallos de transporte en `Result<TSuccess, HttpClientError>`. |
| `ResulTus.Validation` | `ValidationErrors`/`ValidationIssue` estructurados y validación con DataAnnotations. |
| `ResulTus.Validation.FluentValidation` | Ejecuta un `IValidator<T>` y devuelve un `Result<T, ValidationErrors>` con todos los fallos. |
| `ResulTus.Analyzers` | Analizadores de Roslyn (`RESULT001`–`RESULT006`) que convierten el mal uso de resultados en diagnósticos de compilación. |
| `ResulTus.SourceGenerators` | El generador de código `[ErrorFactory]` (`RESULTGEN001`). |
| `ResulTus.Testing` | Aserciones de resultados para tests, independientes del framework (`ShouldSucceed`, `ShouldFail`). |

Licencia MIT. Código fuente: [`sergteje/ResulTus`](https://github.com/sergteje/ResulTus).

Todos los paquetes de la tabla son **estables** y están publicados en 1.1.1 en [nuget.org](https://www.nuget.org/profiles/sergteje) — no hay entradas en preview ni planificadas. Cada paquete lleva su propio README enfocado (enlazado en la tabla y empaquetado en el `.nupkg`). El core más `ResulTus.Extensions` son todo lo necesario para empezar:

```bash
dotnet add package ResulTus
dotnet add package ResulTus.Extensions
```

El mismo patrón instala cualquier otro paquete de la tabla (`dotnet add package ResulTus.Serialization`, `ResulTus.AspNetCore`, …).

## Primeros pasos

En 30 segundos, con solo esos dos paquetes:

```csharp
using ResulTus;
using ResulTus.Extensions;

Result<int> parsed = int.TryParse("42", out var value)
    ? Result<int>.Success(value)
    : Result<int>.Failure(Error.Validation("NOT_A_NUMBER"));

Result<string> message = parsed.Map(v => $"Value: {v}");

string output = message.Match(
    onSuccess: v => v,
    onFailure: e => e.Code);                // "Value: 42"

Result<int> notFound = Error.NotFound("USER_NOT_FOUND");   // fallo, sin excepción
```

Éxito y fallo son dos estados de una misma estructura — sin excepciones, sin heap. El resto de este recorrido se apoya en un bloque de preparación; cada bloque de código siguiente es de nivel de sentencia y los usings, tipos y helpers de ese bloque se sobreentienden en todos ellos.

```csharp
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResulTus;
using ResulTus.Collections;
using ResulTus.Extensions;
using ResulTus.HttpClient;
using ResulTus.Serialization;

public enum DomainError { NotFound, Invalid }

public sealed record Product(int Id, string Name);

[JsonSerializable(typeof(Product))]
internal partial class AppJsonContext : JsonSerializerContext;

static Result<int> Parse(string text) =>
    int.TryParse(text, out var n)
        ? Result<int>.Success(n)
        : Result<int>.Failure(Error.Validation("NOT_A_NUMBER"));

static Result<int, DomainError> Find(string key) =>
    key == "ok"
        ? Result<int, DomainError>.Success(1)
        : Result<int, DomainError>.Failure(DomainError.NotFound);

static Result<int> FetchRow(int id) =>
    id > 0
        ? Result<int>.Success(id * 10)
        : Result<int>.Failure(Error.Validation("BAD_ROW"));

static Result SaveRecord(int id) =>
    id <= 0
        ? Result.Failure(Error.Validation("BAD_ID"))
        : Result.Success();

static ValueTask<Result<int>> FetchAsync(int id) =>
    ValueTask.FromResult(Result<int>.Success(id));
```

El éxito y el fallo son dos estados de una misma estructura — sin excepciones, sin heap:

```csharp
Result<int> ok = Result<int>.Success(42);
Result<int> notFound = Result<int>.Failure(Error.NotFound("USER_NOT_FOUND", "No user with that id"));

string verdict = notFound.Match(
    onSuccess: value => $"found {value}",
    onFailure: error => error.Code);        // "USER_NOT_FOUND"

if (ok.TryGetValue(out var value, out var error))
{
    Console.WriteLine($"got {value}");      // got 42
}
else
{
    Console.WriteLine(error.Code);
}
```

El valor nunca se lee como una propiedad desnuda: `Match`, `TryGetValue`, `GetValueOrDefault` y `GetValueOr` son la interfaz pública, y el analizador `RESULT002` advierte cuando se lee `GetValueOrDefault` sin comprobar antes el resultado.

Un resultado fluye por una única cadena de extensiones:

```csharp
Result<string> pipeline = Parse("42")             // Result<int>
    .Map(n => n * 2)                              // Result<int>
    .Ensure(n => n > 0, Error.Validation("NOT_POSITIVE"))
    .Map(n => $"doubled to {n}");                 // Result<string>
```

El tier genérico transporta un tipo de error propio en lugar del `Error` compartido:

```csharp
Result<int, DomainError> found = Find("ok");

string text = found.Match(
    onSuccess: value => $"value {value}",
    onFailure: error => $"failed {error}");
```

Un ida y vuelta por `System.Text.Json` pasa por los conversores de `ResulTus.Serialization`:

```csharp
var options = new JsonSerializerOptions();
options.AddResulTusConverters();

Result<int> original = Result<int>.Success(42);
string json = JsonSerializer.Serialize(original, options);        // {"isSuccess":true,"value":42}
Result<int> back = JsonSerializer.Deserialize<Result<int>>(json, options)!;
```

El formato de cable es fijo: `isSuccess`, `value` y `error` se emiten en camelCase y nunca siguen una política de nombres configurada, de modo que el mismo resultado se serializa de forma idéntica en todas partes.

## Guías

Los recorridos en profundidad viven en guías enfocadas, cada una verificada contra el código publicado:

| Guía | Contenido |
| --- | --- |
| [`docs/getting-started.md`](docs/getting-started.md) | Instalación, el arranque de 30 segundos, el bloque de preparación completo. |
| [`docs/core-concepts.md`](docs/core-concepts.md) | `Error`, los tres tiers, los contratos de default y de null-success, conversiones. |
| [`docs/functional-operations.md`](docs/functional-operations.md) | Todos los operadores sincrónicos y asíncronos, las leyes de composición, el contrato de asignaciones. |
| [`docs/collections.md`](docs/collections.md) | Las operaciones de colección por tier, y los dos `Combine`. |
| [`docs/error-handling.md`](docs/error-handling.md) | Errores tipados, generación con `[ErrorFactory]`, validación, los analizadores. |
| [`docs/serialization.md`](docs/serialization.md) | El formato de cable de `System.Text.Json`, registro, y las rutas AOT-safe. |
| [`docs/observability.md`](docs/observability.md) | Campos de logging estructurado, timing opcional con `Activity`/`DiagnosticListener`. |
| [`docs/testing.md`](docs/testing.md) | `ShouldSucceed`/`ShouldFail` y las convenciones de testing del repo. |
| [`docs/performance.md`](docs/performance.md) | Todos los números medidos, su sello y la metodología. |
| [`docs/httpclient.md`](docs/httpclient.md) | La guía del módulo HTTP. |

## Los operadores en una cadena

Los operadores componen porque todos comparten una misma forma: un fallo cortocircuita la cadena sin tocarla, y nada asigna en ninguna de las dos rutas más allá del delegado que pasa el llamador. Una cadena de validación que se lee como una frase — `Map` → `Bind` → `Ensure` → `Recover` → `Tap` → `Match`:

```csharp
string outcome = Parse("21")
    .Map(age => age + 1)                                    // Map
    .Bind(age => age < 0
        ? Result<int>.Failure(Error.Validation("NEGATIVE_AGE"))
        : Result<int>.Success(age))                         // Bind
    .Ensure(age => age >= 18, Error.Forbidden("UNDERAGE"))  // Ensure
    .Recover(error => error.Code == "UNDERAGE" ? 18 : -1)   // Recover
    .Tap(age => Console.WriteLine($"accepted {age}"))       // Tap
    .Match(
        onSuccess: age => $"age {age}",
        onFailure: error => $"rejected: {error.Code}");     // Match
```

`await` compone con los mismos operadores sobre `Task` y `ValueTask`:

```csharp
Result<int> fetched = await FetchAsync(21)
    .MapAsync((n, token) => ValueTask.FromResult(n * 2))
    .EnsureAsync((n, token) => ValueTask.FromResult(n >= 0), Error.Validation("NEGATIVE"))
    .RecoverAsync((error, token) => ValueTask.FromResult(0));
```

Los operadores tienen gemelos `Async` sobre `Task` y `ValueTask` — `Map`, `Bind`, `MapError`,
`Ensure`, `Recover`, `Tap`, `OnFailure`, `Switch`, `Match`, `Finally` — con delegates conscientes
del token y el token honrado antes del trabajo awaitado. Dos asimetrías deliberadas:
`OnSuccessAsync` existe solo en el tier tipado (los tiers unit y de valor exponen `TapAsync`), y el
`BindAsync` del tier unitario solo produce valores. El tier tipado toma una sola forma de `Ensure`
(el valor de error que ya se tiene); los tiers unit y `Result<T>` llevan además las formas con
fábrica de error. Un token cancelado se manifiesta como `OperationCanceledException`, nunca como un
fallo. La superficie completa de operadores, las leyes de composición y el contrato de asignaciones
están en [`docs/functional-operations.md`](docs/functional-operations.md).

## Qué la diferencia

Cada diferenciador es un hecho verificable, trazable a un archivo de `src/`.

### Analizadores que detectan el mal uso de resultados en tiempo de compilación

`ResulTus.Analyzers` incluye seis reglas cuyos identificadores forman parte del contrato público (`src/ResulTus.Analyzers/DiagnosticDescriptors.cs`):

| Id | Título | Severidad |
| --- | --- | --- |
| `RESULT001` | Result is ignored | Warning |
| `RESULT002` | Outcome is not checked | Warning |
| `RESULT003` | Deconstruction discards the error | Warning |
| `RESULT004` | Bind is a Map | Info |
| `RESULT005` | Result nested in a Result | Warning |
| `RESULT006` | Ensure predicate is constant | Warning |

### Un generador de código para fábricas de errores

`[ErrorFactory]` convierte un método de fábrica declarado en un cuerpo generado. Declara el error como un método `static partial` en un tipo marcado con `[ResultErrors]`, y `ResulTus.SourceGenerators` emite la implementación que llama a la fábrica de `Error` correspondiente (`src/ResulTus.SourceGenerators/ErrorFactoryGenerator.cs`, `FactoryEmitter.cs`):

```csharp
using ResulTus;

[ResultErrors]
public static partial class UserErrors
{
    [ErrorFactory("USER_NOT_FOUND", ErrorType.NotFound, "User {id} not found")]
    public static partial Error NotFound(Guid id);
}
```

La plantilla del mensaje se renderiza como cadena interpolada, así que el cuerpo emitido es `Error.NotFound("USER_NOT_FOUND", $"User {id} not found")` (con los calificadores `global::` que escribe el emisor omitidos por legibilidad).

Una declaración que el generador no puede emitir se reporta como `RESULTGEN001`, un error que nombra el miembro problemático y el motivo.

### Logging estructurado, no cadenas renderizadas

`ResulTus.Logging` entrega al logger un `IReadOnlyList<KeyValuePair<string, object?>>` (`src/ResulTus.Logging/ResultLogState.cs`): `ResultType` e `IsSuccess` siempre están presentes, y `ErrorType`, `ErrorCode` y `ErrorMessage` aparecen solo en el fallo. Un proveedor lee `ErrorCode` como campo con nombre en lugar de volver a extraerlo del texto. Un nivel de log deshabilitado no asigna nada.

### Diagnósticos `Activity` y `DiagnosticSource` opcionales

`ResulTus.Diagnostics` publica a través de un único `ActivitySource` y un `DiagnosticListener` llamados `ResulTus` (`src/ResulTus.Diagnostics/ResultDiagnostics.cs`). Nada se instrumenta salvo que el llamador escriba `Measure`, y una llamada medida no asigna nada cuando nadie escucha.

### JSON estricto que no adivina

`ResulTus.Serialization` serializa `ErrorType` como el nombre del enum y, al leer, rechaza un token numérico y cualquier nombre desconocido con una `JsonException` en lugar de degradar a un valor por defecto (`src/ResulTus.Serialization/ErrorTypeJsonConverter.cs`). Los nombres se comparan sin distinguir mayúsculas; un número suelto nunca vuelve a convertirse en un `ErrorType`.

### Medida frente a las alternativas

El conjunto de comparación está fijado en el charter: [ZeroResult](https://www.nuget.org/packages/ZeroResult), [ErrorOr](https://www.nuget.org/packages/ErrorOr), [FluentResults](https://www.nuget.org/packages/FluentResults) y [CSharpFunctionalExtensions](https://www.nuget.org/packages/CSharpFunctionalExtensions). Donde ResulTus gana, está medido; donde pierde, la pérdida se muestra en [Benchmarks](#benchmarks). Dos hechos conforman el cuadro honesto: ZeroResult llega a 16 B almacenando su *clase* `SingleError` detrás de una referencia, así que su ruta de fallo asigna salvo que el llamador reutilice una instancia en caché; y FluentResults está basada en clases, así que cada resultado es un objeto del heap.

> Nota de verificación: ErrorOr y ZeroResult se contrastaron con sus README publicados el 2026-11-06; FluentResults y CSharpFunctionalExtensions se afirman como conocimiento de su API pública.
## Benchmarks

Cada número vive en el [charter de performance](docs/performance.md): las filas registradas agrupadas por serie, cada tabla con su sello de medición, y las pérdidas mostradas junto a las ganancias — `Result<int>` en 32 B contra el tier de 16 B, el recorrido de arrays 1.25x-1.47x más lento en el camino de éxito. El charter es la autoridad; acá nada se cita por segunda vez.

## Compatibilidad de runtime

Las bibliotecas apuntan a `net8.0`, `net9.0`, `net10.0` y `net11.0`; los analizadores y el generador de código apuntan a `netstandard2.0`. El trimming y el Native AOT están **verificados por un publish-and-run observado** en cada push de CI (jobs `aot-smoke` y `aot-smoke-linux`, runners `windows-latest` y `ubuntu-24.04`; p. ej. la corrida [43127928009](https://github.com/sergteje/ResulTus/actions/runs/43127928009)): una aplicación de consola, publicada tanto trimmed como nativamente —en Windows y en Linux—, ejecuta los tipos core del resultado, la superficie AOT-safe de System.Text.Json — `ResultJsonConverter<T>(JsonTypeInfo<T>)` — y la ruta end-to-end de `JsonSerializer` mediante un carrier source-generated con el conversor registrado en las opciones del contexto, pasando todas sus aserciones. En aplicaciones AOT, registra los convertidores por esa superficie; el registro por fábrica sigue siendo el camino ordinario para apps que no compilan AOT.

## Respuestas HTTP como resultados

`ResulTus.HttpClient` es un paquete opcional e independiente que convierte una respuesta HTTP, un documento ProblemDetails RFC 9457 y un fallo de transporte en un resultado en lugar de una excepción. Nunca reintenta, nunca lanza por fallos HTTP ordinarios y nunca dispone del `HttpClient` ni del `HttpRequestMessage` del llamador. El módulo completo está documentado en [`docs/httpclient.md`](docs/httpclient.md).

```csharp
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/products/1");

Result<Product, HttpClientError> result =
    await client.SendResultAsync(request, AppJsonContext.Default.Product);

result.Deconstruct(out var isSuccess, out var product, out var error);
if (isSuccess)
{
    Console.WriteLine(product!.Name);
}
else
{
    Console.WriteLine($"HTTP failure: {error!.Kind}");
}
```

`SendResultAsync` asigna los fallos de transporte a `HttpClientFailureKind.Transport`, los timeouts del cliente a `Timeout`, un estado no 2xx a `StatusError` (con el `HttpProblemDetails` ya analizado cuando está presente) y un cuerpo 2xx malformado a `InvalidResponse`. La cancelación del llamador es flujo de control, no un fallo HTTP: un token cancelado relanza `OperationCanceledException` y nunca se pliega en un `HttpClientError`.

## Contribuir

Las contribuciones son bienvenidas — leé [`CONTRIBUTING.md`](CONTRIBUTING.md) primero: cubre los
prerequisitos (SDK 11 más el runtime de .NET 10), el flujo ODD con los feature documents
`odd/tasks/*.md`, las reglas de allocations y coverage que deciden cómo se escribe el código acá,
y las convenciones de tests, docs y commits.

## Licencia

[MIT](LICENSE)
