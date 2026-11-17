// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ResulTus.Analyzers;

/// <summary>
/// Reports <c>RESULT008</c> when an <c>await</c> used as a whole expression statement produces a
/// <c>Result</c> and throws it away.
/// </summary>
/// <remarks>
/// <para>
/// <c>await SomethingAsync();</c> waits for the operation and then drops its outcome, which is the
/// same failure <c>RESULT001</c> describes for the synchronous tier, after the work is already done:
/// a failure has been returned and nobody looks at it.
/// </para>
/// <para>
/// Reading the awaited value — assigning it, returning it, passing it as an argument, using it in a
/// condition or discarding it with <c>_ = await</c> — is deliberate, so none of those report. An
/// awaitable dropped without an <c>await</c> is <c>RESULT007</c>'s finding.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedAwaitAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the single diagnostic this analyzer produces.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.DiscardedAwait);

    /// <summary>
    /// Registers the analysis on expression statements.
    /// </summary>
    /// <param name="context">The analysis context supplied by the compiler.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
    }

    private static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
    {
        var statement = (ExpressionStatementSyntax)context.Node;

        if (statement.Expression is not AwaitExpressionSyntax awaited)
        {
            return;
        }

        // The type of an await expression is what the awaitable produces, so a `Task<Result>` and a
        // custom awaitable that yields a Result look the same here, which is what the rule wants.
        var type = context.SemanticModel
            .GetTypeInfo(awaited, context.CancellationToken)
            .Type;

        if (!ResultSymbols.IsResultType(type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DiscardedAwait,
            awaited.GetLocation()));
    }
}
