// Copyright (c) ResulTus. All rights reserved.

// The stub harness SnippetGate copies into the generated project.
//
// A documentation fragment is not a program. Several documents continue, three fences later, a
// value an earlier fence introduced, and one ASP.NET Core listing is the body of a Program.cs.
// These are the only symbols the gate supplies from outside a document. A stub never changes a
// snippet: each one is a symbol the prose already assumes, with the type its own usage implies.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ResulTus;
using ResulTus.Serialization;

/// <summary>
/// Ambient state the fences of a document share. The generated files import this class with
/// <c>using static</c>, so a fragment reads exactly as it does in the prose.
/// </summary>
internal static class __SnippetGlobals
{
    /// <summary>src/ResulTus.AspNetCore/README.md shows a Program.cs body, and Program.cs has args.</summary>
    public static readonly string[] args = [];

    /// <summary>docs/core-concepts.md creates <c>failed</c> in one fence and deconstructs it in the next.</summary>
    public static readonly Result failed = Result.Failure(Error.Validation("BAD_ID"));

    /// <summary>docs/observability.md builds a capturing logger in one fence and logs with it in the next.</summary>
    public static readonly ILogger logger = NullLogger.Instance;

    /// <summary>docs/serialization.md writes JSON in one fence and hands it to a reader in the next.</summary>
    public static readonly string json = string.Empty;

    /// <summary>The converter that produced <see cref="json"/>.</summary>
    public static readonly ResultJsonConverter<int> converter = new(__SnippetJsonContext.Default.Int32);

    /// <summary>
    /// docs/serialization.md configures the options in one fence and serializes with them in the
    /// next, so the shared value carries the ResulTus converters the prose just added.
    /// </summary>
    public static readonly JsonSerializerOptions options = SerializationOptions();

    private static JsonSerializerOptions SerializationOptions()
    {
        var configured = new JsonSerializerOptions();
        configured.AddResulTusConverters();
        return configured;
    }
}

/// <summary>
/// Metadata for the value type the shared converter above closes over. Declared properly, with
/// <see cref="JsonSerializableAttribute"/>, because the AOT-safe converter needs a
/// <c>JsonTypeInfo</c> and this is how the documentation teaches readers to supply one.
/// </summary>
[JsonSerializable(typeof(int))]
internal sealed partial class __SnippetJsonContext : JsonSerializerContext;
