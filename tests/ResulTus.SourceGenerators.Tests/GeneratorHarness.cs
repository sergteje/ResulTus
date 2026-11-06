// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;

namespace ResulTus.SourceGenerators.Tests;

/// <summary>
/// One file the generator produced.
/// </summary>
/// <param name="HintName">The name the generator gave the file.</param>
/// <param name="Text">The file's contents.</param>
internal sealed record GeneratedSource(string HintName, string Text);

/// <summary>
/// The result of one generator run.
/// </summary>
/// <param name="Sources">Every file the generator produced.</param>
/// <param name="Diagnostics">Every diagnostic the generator reported.</param>
/// <param name="Compilation">The compilation after the generated files were added to it.</param>
internal sealed record GeneratorRun(
    ImmutableArray<GeneratedSource> Sources,
    ImmutableArray<Diagnostic> Diagnostics,
    Compilation Compilation)
{
    /// <summary>
    /// Gets every generated file's contents, concatenated.
    /// </summary>
    public string Text => string.Join(Environment.NewLine, Sources.Select(source => source.Text));

    /// <summary>
    /// Gets the errors left in the compilation after generation.
    /// </summary>
    public ImmutableArray<Diagnostic> OutputErrors => Compilation
        .GetDiagnostics(TestContext.Current.CancellationToken)
        .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        .ToImmutableArray();

    /// <summary>
    /// Asserts that the generated code merges with the declared part and compiles.
    /// </summary>
    /// <remarks>
    /// The source handed to the generator always has errors on its own: a partial method with no
    /// implementation is <c>CS8795</c>. Asserting on the output compilation, not the input one, is
    /// what makes "the generator did nothing" fail instead of pass.
    /// </remarks>
    public void ShouldCompile()
    {
        OutputErrors.Should().BeEmpty(
            "the generated code must complete the declared part, and the errors were:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, OutputErrors.Select(error => error.ToString())));
    }

    /// <summary>
    /// Emits the compilation and invokes one generated factory, so the assertion is about the
    /// library's behaviour rather than about a string.
    /// </summary>
    /// <param name="typeName">The full name of the type that declares the factory.</param>
    /// <param name="methodName">The factory's name.</param>
    /// <param name="arguments">The arguments to pass.</param>
    /// <returns>The <see cref="Error"/> the factory produced.</returns>
    public Error InvokeError(string typeName, string methodName, params object?[] arguments)
    {
        ShouldCompile();

        using var stream = new MemoryStream();
        var emit = Compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);

        emit.Success.Should().BeTrue(
            "the compilation must emit before its behaviour can be observed; the errors were:"
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));

        var assembly = Assembly.Load(stream.ToArray());
        var type = assembly.GetType(typeName, throwOnError: true)!;
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;

        return (Error)method.Invoke(null, arguments)!;
    }
}

/// <summary>
/// Runs <see cref="ErrorFactoryGenerator"/> over a source snippet the way a consumer's build would.
/// </summary>
internal static class GeneratorHarness
{
    private const string AssemblyName = "GeneratorTestAssembly";

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.DynamicallyLinkedLibrary,
        nullableContextOptions: NullableContextOptions.Enable);

    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    /// <summary>
    /// Compiles <paramref name="source"/>, runs the generator and returns everything the run
    /// produced.
    /// </summary>
    /// <param name="source">A complete C# compilation unit.</param>
    /// <returns>The generated files, the generator's diagnostics and the updated compilation.</returns>
    public static GeneratorRun Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            AssemblyName,
            [CSharpSyntaxTree.ParseText(source, ParseOptions)],
            References,
            CompilationOptions);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new ErrorFactoryGenerator().AsSourceGenerator() },
            parseOptions: ParseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var output,
            out _,
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        return new GeneratorRun(
            result.Results
                .SelectMany(run => run.GeneratedSources)
                .Select(generated => new GeneratedSource(generated.HintName, generated.SourceText.ToString()))
                .ToImmutableArray(),
            result.Diagnostics,
            output);
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

        // The generated code calls into the core assembly, and the test sources carry its attributes.
        references.Add(MetadataReference.CreateFromFile(typeof(Error).Assembly.Location));

        return references.ToImmutable();
    }
}
