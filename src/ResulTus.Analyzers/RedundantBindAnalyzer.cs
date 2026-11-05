// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ResulTus.Analyzers;

/// <summary>
/// Reports <c>RESULT004</c> when a <c>Bind</c> only wraps its value back into a success.
/// </summary>
/// <remarks>
/// <c>Bind(x =&gt; Result&lt;T&gt;.Success(f(x)))</c> is exactly <c>Map(f)</c>. The lambda adds a
/// result construction that carries no failure, so the extra step buys nothing but noise.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantBindAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the single diagnostic this analyzer produces.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.RedundantBind);

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

        var arguments = invocation.ArgumentList.Arguments;

        // Reduced form has the lambda as its only argument; the static form has the receiver first.
        if (arguments.Count == 0)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        // `ReducedFrom` is the declaration behind a call written in extension-method form, and it is
        // the symbol that still carries the declaring type.
        if (method.Name != "Bind" || !ResultSymbols.IsExtensionsMethod(method.ReducedFrom ?? method))
        {
            return;
        }

        var body = LambdaBody.GetReturnedExpression(arguments[arguments.Count - 1].Expression);

        if (body is null)
        {
            return;
        }

        if (body is not InvocationExpressionSyntax factoryInvocation)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(factoryInvocation, context.CancellationToken).Symbol is not IMethodSymbol factory)
        {
            return;
        }

        if (!ResultSymbols.IsSuccessFactory(factory))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.RedundantBind,
            invocation.GetLocation()));
    }
}
