// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ResulTus.Analyzers;

/// <summary>
/// Reports <c>RESULT001</c> when a result is produced and then dropped as a statement.
/// </summary>
/// <remarks>
/// A dropped result is the failure mode the whole pattern exists to prevent: the operation reports
/// its outcome and nobody reads it. Assigning it, discarding it with <c>_ =</c>, returning it or
/// passing it to another call are all deliberate, so none of those report.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IgnoredResultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the single diagnostic this analyzer produces.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.IgnoredResult);

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

        // `_ = GetValue();` is the documented way to say "I am dropping this on purpose".
        if (statement.Expression is AssignmentExpressionSyntax)
        {
            return;
        }

        var type = context.SemanticModel
            .GetTypeInfo(statement.Expression, context.CancellationToken)
            .Type;

        if (!ResultSymbols.IsResultType(type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.IgnoredResult,
            statement.Expression.GetLocation()));
    }
}
