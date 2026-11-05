// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ResulTus.Analyzers;

/// <summary>
/// Reports <c>RESULT006</c> when an <c>Ensure</c> predicate ignores the value and returns a constant.
/// </summary>
/// <remarks>
/// A guard that always returns the same answer cannot depend on the value it is guarding. Written
/// as <c>Ensure(_ =&gt; true, error)</c> it is dead code, and as <c>Ensure(_ =&gt; false, error)</c> it
/// fails unconditionally, which is a different statement than the one it looks like.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstantEnsureAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the single diagnostic this analyzer produces.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.ConstantEnsure);

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

        // Ensure(receiver, predicate, error): the predicate is the second argument written, first
        // in the argument list because the receiver is the first one.
        if (invocation.ArgumentList.Arguments.Count != 2)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.Name != "Ensure" || !ResultSymbols.IsExtensionsMethod(method.ReducedFrom ?? method))
        {
            return;
        }

        var body = LambdaBody.GetReturnedExpression(invocation.ArgumentList.Arguments[0].Expression);

        if (body is null)
        {
            return;
        }

        if (body is not LiteralExpressionSyntax literal)
        {
            return;
        }

        if (literal.Kind() is not (SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ConstantEnsure,
            invocation.GetLocation()));
    }
}
