// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ResulTus.Analyzers;

/// <summary>
/// Reports <c>RESULT005</c> when <c>ToResult()</c> is called on something that is already a result.
/// </summary>
/// <remarks>
/// <c>ToResult&lt;T&gt;(this T value)</c> is generic over the value, so calling it on a
/// <c>Result&lt;int&gt;</c> produces a <c>Result&lt;Result&lt;int&gt;&gt;</c>. That nesting is
/// almost never intended: it compiles, it looks like a conversion, and the inner failure is now
/// hidden one layer down.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NestedResultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the single diagnostic this analyzer produces.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.NestedResult);

    /// <summary>
    /// Registers the analysis on invocation expressions.
    /// </summary>
    /// <param name="context">The analysis context supplied by the compiler.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.Name != "ToResult" || !ResultSymbols.IsConversionsMethod(method.ReducedFrom ?? method))
        {
            return;
        }

        // An extension method called in static form still reports its declaring class as the
        // receiver type, so the value under conversion is the first parameter in that case.
        // ToResult always declares that parameter, so indexing it cannot fail.
        ITypeSymbol? receiverType = method.ReducedFrom is null
            ? method.Parameters[0].Type
            : method.ReceiverType;

        if (!ResultSymbols.IsResultType(receiverType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.NestedResult,
            invocation.GetLocation()));
    }
}
