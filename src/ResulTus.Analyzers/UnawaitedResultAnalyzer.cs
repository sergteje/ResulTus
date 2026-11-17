// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ResulTus.Analyzers;

/// <summary>
/// Reports <c>RESULT007</c> when a <c>Task&lt;Result&gt;</c>, <c>Task&lt;Result&lt;T&gt;&gt;</c>,
/// <c>Task&lt;Result&lt;TSuccess, TError&gt;&gt;</c> or the matching <c>ValueTask</c> is produced as
/// an expression statement and never awaited or assigned.
/// </summary>
/// <remarks>
/// <para>
/// A fire-and-forget task over a result loses the failure twice: nobody reads the outcome, and an
/// exception thrown inside it surfaces only as an unobserved task exception. The compiler's own
/// warning (<c>CS4014</c>) covers an unawaited awaitable inside an asynchronous method, says nothing
/// about the lost outcome, and stays silent in a synchronous method.
/// </para>
/// <para>
/// Assigning the task, discarding it with <c>_ =</c>, awaiting it, returning it or passing it to
/// another call are all deliberate, so none of those report. An awaited statement is
/// <c>RESULT008</c>'s finding, not this one's.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnawaitedResultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the single diagnostic this analyzer produces.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.UnawaitedResult);

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

        // `_ = SomethingAsync();` is the documented acknowledgement, and `await SomethingAsync();`
        // already unwraps the result, so RESULT008 owns that statement.
        if (statement.Expression is AssignmentExpressionSyntax or AwaitExpressionSyntax)
        {
            return;
        }

        var type = context.SemanticModel
            .GetTypeInfo(statement.Expression, context.CancellationToken)
            .Type;

        if (!ResultSymbols.IsAwaitableResultType(type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.UnawaitedResult,
            statement.Expression.GetLocation()));
    }
}
