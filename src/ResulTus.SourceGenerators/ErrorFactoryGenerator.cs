// Copyright (c) ResulTus. All rights reserved.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ResulTus.SourceGenerators;

/// <summary>
/// Implements the <c>static partial Error</c> factories declared on a type marked with
/// <c>[ResultErrors]</c>.
/// </summary>
/// <remarks>
/// The pipeline is anchored on <c>[ErrorFactory]</c> rather than on <c>[ResultErrors]</c> so a
/// method that carries the attribute on a type that never opted in is still seen — and diagnosed —
/// instead of silently disappearing.
/// <para>
/// No candidate is ever grouped: every candidate flows on its own into the report and the emit
/// steps. Because <see cref="FactoryCandidate"/> compares by content (strings and spans, never
/// symbol references), an edit to one declaration re-runs only that one candidate; the others
/// come back through the cache untouched. A build produces the same files either way — the
/// compiler keys generated files by hint name, so the per-candidate registration cannot reorder
/// them.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ErrorFactoryGenerator : IIncrementalGenerator
{
    private const string ErrorFactoryAttribute = "ResulTus.ErrorFactoryAttribute";

    private const string UnusableDeclarationId = "RESULTGEN001";

    private static readonly DiagnosticDescriptor UnusableDeclaration = new(
        id: UnusableDeclarationId,
        title: "Error factory declaration cannot be generated",
        messageFormat: "{0}",
        category: "ResulTus.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ErrorFactoryAttribute,
                static (node, _) => node is MethodDeclarationSyntax,
                static (attributeContext, cancellationToken)
                    => FactoryExtraction.Extract(attributeContext, cancellationToken));

        context.RegisterSourceOutput(candidates, static (productionContext, candidate) =>
        {
            if (candidate.Diagnostic is not { } diagnostic)
            {
                return;
            }

            var location = Location.Create(
                diagnostic.Path ?? string.Empty,
                diagnostic.SourceSpan,
                diagnostic.LineSpan);
            productionContext.ReportDiagnostic(Diagnostic.Create(
                UnusableDeclaration,
                location,
                diagnostic.Message));
        });

        var emitted = candidates.Select(
            static (candidate, cancellationToken) => candidate.Model);
        context.RegisterSourceOutput(emitted, static (productionContext, model) =>
        {
            if (model is not null)
            {
                productionContext.AddSource(
                    model.HintName,
                    SourceText.From(model.SourceText, System.Text.Encoding.UTF8));
            }
        });
    }
}
