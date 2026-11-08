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
                    => FactoryExtraction.Extract(attributeContext, cancellationToken))
            .Collect();

        context.RegisterSourceOutput(candidates, static (productionContext, all) =>
        {
            foreach (var candidate in all)
            {
                if (candidate.Diagnostic is not null)
                {
                    productionContext.ReportDiagnostic(
                        Diagnostic.Create(
                            UnusableDeclaration,
                            candidate.Diagnostic.Location,
                            candidate.Diagnostic.Message));
                }
            }

            // The pipeline does not promise an order, so it is fixed here: a build must produce the
            // same files in the same order every time.
            foreach (var model in all
                .Where(candidate => candidate.Model is not null)
                .Select(candidate => candidate.Model!)
                .OrderBy(model => model.HintName, System.StringComparer.Ordinal))
            {
                productionContext.AddSource(
                    model.HintName,
                    SourceText.From(model.SourceText, System.Text.Encoding.UTF8));
            }
        });
    }
}
