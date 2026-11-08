// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using ResulTus.Extensions;

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Compiles a source snippet the way a consumer's project would and runs one analyzer over it.
/// </summary>
/// <remarks>
/// <para>
/// This drives Roslyn directly instead of using the <c>Microsoft.CodeAnalysis.Testing</c> verifier
/// packages, which ship for xunit v2, NUnit and MSTest while this repository is on xunit.v3.
/// The verifiers wrap exactly these two calls.
/// </para>
/// <para>
/// Every analysis asserts that the snippet itself compiles first. Without that, a test could pass
/// because the code it fed the analyzer was broken and the analyzer found nothing to report.
/// </para>
/// </remarks>
internal static class AnalyzerHarness
{
    private const string AssemblyName = "AnalyzerTestAssembly";

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.DynamicallyLinkedLibrary,
        nullableContextOptions: NullableContextOptions.Enable);

    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    /// <summary>
    /// Compiles <paramref name="source"/> and returns the diagnostics produced by
    /// <paramref name="analyzer"/> alone.
    /// </summary>
    /// <param name="analyzer">The analyzer to run.</param>
    /// <param name="source">A complete C# compilation unit.</param>
    /// <param name="outputKind">The kind of assembly to build; console applications allow top-level statements.</param>
    /// <returns>The analyzer's diagnostics, in the order Roslyn reports them.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="source"/> does not compile, which would make the result meaningless.
    /// </exception>
    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        var compilation = CreateCompilation(source, outputKind);
        ThrowIfSourceDoesNotCompile(compilation);

        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));

        return await withAnalyzers
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Compiles <paramref name="source"/> and returns the diagnostics of one rule only, so a test
    /// that expects nothing cannot be fooled by a different rule firing.
    /// </summary>
    /// <param name="analyzer">The analyzer to run.</param>
    /// <param name="source">A complete C# compilation unit.</param>
    /// <param name="ruleId">The rule id to keep.</param>
    /// <param name="outputKind">The kind of assembly to build; console applications allow top-level statements.</param>
    /// <returns>The diagnostics whose id is <paramref name="ruleId"/>.</returns>
    public static async Task<ImmutableArray<Diagnostic>> AnalyzeRuleAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        string ruleId,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        var diagnostics = await AnalyzeAsync(analyzer, source, outputKind).ConfigureAwait(false);

        return diagnostics.Where(diagnostic => diagnostic.Id == ruleId).ToImmutableArray();
    }

    /// <summary>
    /// Runs <paramref name="analyzer"/> over source that is allowed not to compile.
    /// </summary>
    /// <param name="analyzer">The analyzer to run.</param>
    /// <param name="source">A complete C# compilation unit, which may contain errors.</param>
    /// <returns>Every diagnostic the analyzer produced, including any crash it reported.</returns>
    /// <remarks>
    /// A consumer's editor runs analyzers on code that is half-typed and does not compile yet, so an
    /// analyzer that throws on unresolved symbols turns every keystroke into a build error.
    /// </remarks>
    public static async Task<ImmutableArray<Diagnostic>> AnalyzeBrokenSourceAsync(
        DiagnosticAnalyzer analyzer,
        string source)
    {
        var compilation = CreateCompilation(source);
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));

        return await withAnalyzers
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the compilation the analyzer runs over.
    /// </summary>
    /// <param name="source">A complete C# compilation unit.</param>
    /// <param name="outputKind">The kind of assembly to build; console applications allow top-level statements.</param>
    /// <returns>A compilation referencing the runtime and the libraries under analysis.</returns>
    internal static CSharpCompilation CreateCompilation(
        string source,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
        => CSharpCompilation.Create(
            AssemblyName,
            [CSharpSyntaxTree.ParseText(source, ParseOptions)],
            References,
            CompilationOptions.WithOutputKind(outputKind));

    private static void ThrowIfSourceDoesNotCompile(CSharpCompilation compilation)
    {
        var errors = compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "The test source does not compile, so any analyzer result would be meaningless:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var references = ImmutableArray.CreateBuilder<MetadataReference>();

        // Everything the runtime loaded, which is how the verifier packages seed their references.
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
        {
            foreach (var path in trusted.Split(Path.PathSeparator))
            {
                if (path.Length > 0)
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        // The libraries under analysis are project references, so they are not in the list above.
        references.Add(MetadataReference.CreateFromFile(typeof(Result).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(ResultExtensions).Assembly.Location));

        return references.ToImmutable();
    }
}
