// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ResulTus.Analyzers;

/// <summary>
/// Reports <c>RESULT003</c> when a deconstruction of a result discards the error.
/// </summary>
/// <remarks>
/// Every result type puts the error last — <c>Result</c> is <c>(bool, Error)</c>, <c>Result&lt;T&gt;</c>
/// is <c>(bool, T?, Error)</c> and <c>Result&lt;TSuccess, TError&gt;</c> is
/// <c>(bool, TSuccess?, TError?)</c> — so discarding the last element is always discarding the error.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedErrorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the single diagnostic this analyzer produces.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.DiscardedError);

    /// <summary>
    /// Registers the analysis on deconstruction assignments.
    /// </summary>
    /// <param name="context">The analysis context supplied by the compiler.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeDeconstruction, OperationKind.DeconstructionAssignment);
    }

    private static void AnalyzeDeconstruction(OperationAnalysisContext context)
    {
        var operation = (IDeconstructionAssignmentOperation)context.Operation;

        if (!ResultSymbols.IsResultType(operation.Value.Type))
        {
            return;
        }

        var target = FindTargetTuple(operation.Target);

        if (target is null)
        {
            return;
        }

        if (target.Elements[target.Elements.Length - 1] is not IDiscardOperation)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DiscardedError,
            operation.Syntax.GetLocation()));
    }

    /// <summary>
    /// Finds the tuple that holds the deconstruction targets.
    /// </summary>
    /// <param name="target">The assignment target.</param>
    /// <returns>The target tuple, or <c>null</c> when the target is not a tuple.</returns>
    /// <remarks>
    /// The two syntax forms do not produce the same shape: <c>(a, b, _) = result</c> has the tuple as
    /// the target itself, while <c>var (a, b, _) = result</c> wraps it in a declaration expression.
    /// Looking through the children covers both without depending on the wrapper's name.
    /// </remarks>
    private static ITupleOperation? FindTargetTuple(IOperation target)
        => target is ITupleOperation tuple
            ? tuple
            : target.ChildOperations.OfType<ITupleOperation>().FirstOrDefault();
}
