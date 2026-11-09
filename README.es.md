# ResulTus

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

## Primeros pasos

Cada bloque de código siguiente es de nivel de sentencia; los usings, tipos y helpers de este bloque de preparación se sobreentienden en todos ellos.

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

## Fluida por diseño

Los operadores componen porque todos comparten una misma forma: un fallo cortocircuita la cadena sin tocarla, y nada asigna en ninguna de las dos rutas más allá del delegado que pasa el llamador. Cada ejemplo es una forma que un lector escribiría realmente, y cada uno compila contra los ensamblados reales.

Una cadena de validación que se lee como una frase — `Map` → `Bind` → `Ensure` → `Recover` → `Tap` → `Match`:

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

Una cadena que transforma el error — `MapError` → `Recover` → `Finally`:

```csharp
Result<int> cleaned = Parse("-3")
    .MapError(error => Error.Validation("INVALID_AGE", error.Message)) // MapError
    .Recover(_ => 0)                                                   // Recover
    .Finally((value, error) =>                                         // Finally
        Console.WriteLine($"ended {value} / {error.Code}"));
```

El `Result` unitario para un comando que no devuelve nada:

```csharp
Result saved = SaveRecord(42);

if (saved.IsSuccess)
{
    Console.WriteLine("saved");
}
```

`await` compone con los mismos operadores, sobre `Task` y `ValueTask`:

```csharp
Result<int> fetched = await FetchAsync(21)
    .MapAsync((n, token) => ValueTask.FromResult(n * 2))
    .EnsureAsync((n, token) => ValueTask.FromResult(n >= 0), Error.Validation("NEGATIVE"))
    .RecoverAsync((error, token) => ValueTask.FromResult(0));
```

Una cadena de colecciones — `Traverse` → `Fold` → `Partition` sobre una lista real:

```csharp
// Traverse maps every id through a fallible fetch, stopping at the first failure.
Result<IReadOnlyList<int>> rows = new[] { 1, 2, 3 }.Traverse(FetchRow);

// Fold reduces a sequence of results to one accumulator without allocating.
Result<int> total = new[] { FetchRow(1), FetchRow(2), FetchRow(3) }
    .Fold(0, static (sum, value) => sum + value);

// Partition splits a mixed batch so neither side is lost.
(int[] succeeded, Error[] failed) = new[] { FetchRow(1), FetchRow(2) }.Partition();
```

El tier genérico transporta un error de dominio por los mismos operadores sin perder el fallo tipado:

```csharp
Result<int, DomainError> handled = Find("ok")
    .Map(value => value * 2)
    .Bind(value => value < 100
        ? Result<int, DomainError>.Success(value)
        : Result<int, DomainError>.Failure(DomainError.Invalid))
    .Ensure(value => value > 0, DomainError.Invalid)
    .Recover(_ => 0)
    .Tap(value => Console.WriteLine(value));

string summary = handled.Match(
    onSuccess: value => $"handled {value}",
    onFailure: error => $"failed {error}");
```

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

Estas son las filas registradas, agrupadas por serie. Cada número se cita literalmente de [`docs/performance.md`](docs/performance.md), y cada tabla indica su sello de medición y enlaza su sección. Una serie sin filas registradas se omite, nunca se estima.

### Creación — medido el 2026-10-29, Intel Core i7-6700, .NET SDK 10.0.403, Release, `DefaultJob`

| Método | Media | Asignado |
| --- | ---: | ---: |
| **ResulTus_Create_Success** | 0.0118 ns | **0 B** |
| **ResulTus_Create_Failure** | 0.0192 ns | **0 B** |
| ResulTus_Create_Implicit_From_Value | 0.0130 ns | 0 B |
| ResulTus_Create_Implicit_From_Error | 0.1153 ns | 0 B |
| ZeroResult_Create_Success | 0.0150 ns | 0 B |
| ZeroResult_Create_Failure | 0.3182 ns | 0 B |
| CSharpFunctionalExtensions_Create_Success | 2.2914 ns | 0 B |
| CSharpFunctionalExtensions_Create_Failure | 0.0544 ns | 0 B |
| ErrorOr_Create_Success | 0.1295 ns | 0 B |
| ErrorOr_Create_Failure | 11.6516 ns | **88 B** |
| FluentResults_Create_Success | 18.7471 ns | **112 B** |
| FluentResults_Create_Failure | 40.6202 ns | **240 B** |

[Sección completa](docs/performance.md#result-creation--measured-2026-10-29). Toda fila por debajo de 1 ns es un empate según la regla del umbral de ruido; el resultado significativo es `Create_Failure` con **0.0192 ns y 0 B**, frente a los 88 B de ErrorOr y los 240 B de FluentResults — ellos deben asignar un objeto de error, ResulTus no.

### Evaluación con `Match` — medido el 2026-10-29, Intel Core i7-6700, .NET SDK 10.0.403

| Método | Media | Asignado |
| --- | ---: | ---: |
| **ResulTus_Match_Success** | 0.9235 ns | **0 B** |
| **ResulTus_Match_Failure** | 1.3834 ns | **0 B** |
| ErrorOr_Match_Success | **0.7175 ns** | 0 B |
| ErrorOr_Match_Failure | **0.5482 ns** | 0 B |
| ZeroResult_Match_Success | 0.9391 ns | 0 B |
| ZeroResult_Match_Failure | **0.7818 ns** | 0 B |
| CSharpFunctionalExtensions_Match_Success | 1.9599 ns | 0 B |
| CSharpFunctionalExtensions_Match_Failure | 1.8911 ns | 0 B |
| FluentResults_Match_Success | 21.4166 ns | **96 B** |
| FluentResults_Match_Failure | 19.0831 ns | **88 B** |

[Sección completa](docs/performance.md#result-matching--measured-2026-10-29). Las victorias asentadas son estructurales (2.1x/1.4x frente a CSharpFunctionalExtensions, 23x/14x frente a FluentResults con 0 B contra 96/88 B). ErrorOr y la ruta de fallo de ZeroResult quedan registrados como *sin resolver*: la magnitud no se reprodujo entre ejecuciones en este hardware, así que no se declara veredicto.

### Asignación, ruta de éxito — medido el 2026-10-29, Intel Core i7-6700, .NET SDK 10.0.403

| Método | Media | Asignado |
| --- | ---: | ---: |
| Error_NotFound_Allocation | 0.0031 ns | 0 B |
| **ResulTus_Success_Allocation** | 0.0187 ns | **0 B** |
| **ResulTus_Failure_Allocation** | 0.0000 ns | **0 B** |
| ErrorOr_Success_Allocation | 0.0109 ns | 0 B |
| ZeroResult_Success_Allocation | 0.0000 ns | 0 B |
| ZeroResult_Failure_Allocation | 0.2633 ns | 0 B |
| FluentResults_Success_Allocation | 16.8744 ns | **112 B** |

[Sección completa](docs/performance.md#allocation--measured-2026-10-29). La columna de nanosegundos es código eliminado por el JIT, no una clasificación; la regla de asignación es el piso, y ResulTus asigna 0 B en el éxito y en el fallo.

### Asignación, ruta de fallo desde un mensaje único — medido el 2026-11-06, Intel Core i7-6700, .NET SDK 10.0.403, BenchmarkDotNet 0.15.8

| Método | Media ejecución A | Media ejecución B | Asignado | Ratio |
| --- | ---: | ---: | ---: | ---: |
| **ResulTus_Failure_Unique_Message** | 2.807 ns | 2.830 ns | **0 B** | 1.00 |
| ErrorOr_Failure_Unique_Message | 15.159 ns | 14.955 ns | **88 B** | 5.3x |
| ZeroResult_Failure_Unique_Message | 8.859 ns | 8.268 ns | **32 B** | 3.0x |
| FluentResults_Failure_Unique_Message | 50.266 ns | 51.690 ns | **272 B** | 18x |
| CSharpFunctionalExtensions_Failure_Unique_Message | 3.033 ns | 3.053 ns | **0 B** | 1.08x |

[Sección completa](docs/performance.md#failure-path-from-a-unique-message--measured-2026-11-06). Es la comparación directa de la ruta de fallo con un mensaje distinto en cada invocación. ResulTus gana en asignación frente a tres de cuatro y empata con el cuarto: `CSharpFunctionalExtensions` no asigna nada porque su error es un `string` simple, mientras que ResulTus lleva `Code`, `Message` y `Type` en línea con el mismo 0 B.

### Tamaño de la estructura — el tier por defecto de 32 B es una pérdida documentada

Medido el 2026-10-29 ([§4.2](docs/performance.md#42-struct-size-is-a-design-problem-not-a-match-problem--resolved-2026-10-28)):

| Tipo de elemento | Tipo | `SizeOf` |
| --- | --- | ---: |
| `ResulTus.Result<int>` | struct | 32 |
| `ResulTus.Error` | struct | 24 |
| `ResulTus.Result<int, EightByteError>` | struct | **13** |
| `ErrorOr.ErrorOr<int>` | struct | 16 |
| `ErrorOr.Error` | struct | 32 |
| `ZeroResult.Result<int, SingleError>` | struct | 16 |
| `ZeroResult.Errors.SingleError` | **class** | 8 |
| `FluentResults.Result<int>` | **class** | 8 |
| `CSharpFunctionalExtensions.Result<int>` | struct | 16 |

`Result<int>` ocupa **32 B frente a los 16 B de ellos** — es el único de los cinco que mantiene un error rico en línea, y la razón por la que el 0 B en ambas rutas es posible. El tier genérico es el otro lado del intercambio: **13 B**, más compacto que cualquier competidor.

### Recorrido de arrays — una pérdida documentada, mostrada como número

Medido el 2026-10-29, `DefaultJob` ([§4.4](docs/performance.md#44-array-traversal-against-competitors--measured-2026-10-29)), ruta de éxito, ns por array completo:

| N | ResulTus 32 B | ErrorOr 16 B | ZeroResult 16 B | CSharpFunctionalExtensions 16 B | FluentResults 8 B (clase) |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 000 | 804.9 | **644.4** | **551.6** | **546.7** | 20,806.2 — 96 KB asignados |
| 7 000 | 5,711.2 | **4,239.4** | **4,506.9** | **4,221.4** | 159,103.7 — 672 KB asignados |
| 10 000 | 8,886.7 | **6,526.1** | **6,178.2** | **6,338.7** | 250,669.9 — 960 KB asignados |

En la ruta de éxito ResulTus es **1.25x–1.47x más lenta por elemento** que cada competidor de 16 bytes; en la ruta de fallo es 1.08x–1.26x más lenta. La causa es el paso de 32 B: dos elementos por línea de caché de 64 bytes en lugar de cuatro. Ese es el precio deliberado del error rico en línea, y se declara aquí, no en una nota al pie. FluentResults es la excepción que confirma la regla: 8 B por elemento y 25x–28x más lenta, porque cada elemento es un objeto del heap que además asigna por kilobytes.

### Colecciones — medido el 2026-11-06, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14, BenchmarkDotNet 0.15.8

Origen array, N = 1.000 elementos totalmente exitosos:

| Método | Origen | Media | Asignado |
| --- | --- | ---: | ---: |
| Sequence | Array | 4.121 μs | 4,056 B |
| Traverse | Array | 2.060 μs | 4,024 B |
| Fold | Array | 1.128 μs | 0 B |
| FirstFailure | Array | 863.2 ns | 0 B |
| Partition | Array | 3.095 μs | 12,480 B |
| Combine (`Result` unitario) | Array | 845.5 ns | 0 B |

[Sección completa](docs/performance.md#collections--array-versus-list-at-n--1000--measured-2026-11-06). `Fold`, `FirstFailure` y `Combine` unitario no asignan nada; `Sequence` y `Traverse` asignan su array de salida. `Partition` en esta línea base todavía asigna 12,480 B porque hace crecer dos listas: el cambio posterior P1/T5 alquila búferes de un pool y lo reduce a **4,048 B**, los dos arrays de salida con tamaño exacto. `List<T>` es más lenta que `T[]` en las seis filas de la línea base. Dos mediciones posteriores quedan visibles aquí en lugar de enterradas: el `Traverse` indexado sobre array es más lento que su control `IEnumerable<T>` (7.447 μs frente a 3.055 μs, ahorrando 32 B), y el `Partition` con pool es reproduciblemente ~30% más lento sobre un origen `List<T>`. Ambas son diferencias estructurales documentadas en [§7](docs/performance.md#7-known-open-gaps), aceptadas porque el piso de asignación es obligatorio.

### Serialización — medido el 2026-11-06, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14

| Método | Media | Asignado |
| --- | ---: | ---: |
| Serialize `Result<int>` success | 177.8 ns | 80 B |
| Serialize `Result<int>` failure | 310.9 ns | 224 B |
| Deserialize `Result<int>` success | 212.3 ns | 0 B |
| Deserialize `Result<int>` failure | 500.20 ns | 112 B |
| Serialize `Result<int>[1000]` | 85.444 μs | 61,808 B |
| Deserialize `Result<int>[1000]` | 159.317 μs | 97,680 B |
| Serialize `Error` | 246.1 ns | 176 B |
| Deserialize `Error` | 343.33 ns | 120 B |
| Serialize `ErrorType` | 110.9 ns | 48 B |
| Deserialize `ErrorType` | 81.57 ns | 0 B |

[Sección completa](docs/performance.md#serialization--p0-baseline-and-p1-remeasurements--measured-2026-11-06). Una línea base satélite, no una clasificación entre bibliotecas: la deserialización exitosa de `Result<int>` asigna 0 B, y las filas de 1.000 elementos incluyen la cadena JSON o el array materializado.

### Logging — medido el 2026-11-06, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14

| Método | Habilitado | Media | Asignado |
| --- | --- | ---: | ---: |
| LogFailure | false | 5.057 ns | 0 B |
| LogFailure | true | 26.054 ns | 64 B |

[Sección completa](docs/performance.md#logging--enabled-versus-disabled-failure-logging--measured-2026-11-06). La ruta con el nivel deshabilitado asigna 0 B; la ruta habilitada asigna los 64 B del estado de logging estructurado, sin asignación de mensaje renderizado.

### Validación — medido el 2026-11-06, Intel Core i7-6700, Windows 10 22H2, .NET SDK 10.0.403 / runtime 10.0.14

| Método | Media | Asignado |
| --- | ---: | ---: |
| ToDictionary | 21.385 μs | 31,624 B |
| ToError | 33.04 ns | 80 B |

[Sección completa](docs/performance.md#validation-conversions--1000-issues--measured-2026-11-06). `ToDictionary` materializa el diccionario agrupado y los arrays de mensajes por campo sobre 1.000 incidencias; `ToError` crea el mensaje de resumen y un `Error` de tipo valor.

## Compatibilidad de runtime

Las bibliotecas apuntan a `net8.0`. El trimming y el Native AOT están **verificados por un publish-and-run observado** en cada push de CI (job `aot-smoke`, runner `windows-latest`; p. ej. la corrida [42653755212](https://github.com/sergteje/ResulTus/actions/runs/42653755212)): una aplicación de consola, publicada tanto trimmed como nativamente, ejecuta los tipos core del resultado y la superficie AOT-safe de System.Text.Json — `ResultJsonConverter<T>(JsonTypeInfo<T>)` — pasando todas sus aserciones. En aplicaciones AOT, registra los convertidores por esa superficie; el registro por fábrica sigue siendo el camino ordinario para apps que no compilan AOT.

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

## Licencia

[MIT](LICENSE)
