// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ResulTus.Analyzers;

/// <summary>
/// Reports <c>RESULT002</c> when a default value is read from a result whose outcome is never checked.
/// </summary>
/// <remarks>
/// <para>
/// <c>GetValueOrDefault()</c> has an optional parameter, so a call that writes no argument silently
/// returns <c>default</c> when the result is a failure. Nothing throws and nothing is logged; the
/// caller keeps going with a value that means "no value".
/// </para>
/// <para>
/// The check is deliberately coarse: it looks for a guard on the same receiver text anywhere in the
/// enclosing member. A guard that lives in another method, or a result known to be successful from
/// construction, is not modelled. A control-flow analysis would be a much larger machine for the
/// same finding, and the finding is a warning, not a proof.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnguardedOutcomeAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> GuardNames = ImmutableHashSet.Create(
        "IsSuccess",
        "IsFailure",
        "TryGetValue",
        "Match",
        "GetValueOr",
        "Deconstruct");

    /// <summary>
    /// Gets the single diagnostic this analyzer produces.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.UnguardedOutcome);

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

        // `GetValueOrDefault(T defaultValue = default!)` has an optional parameter, so the call that
        // reads a silent default is the one that writes no argument at all.
        if (invocation.ArgumentList.Arguments.Count != 0)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.Name != "GetValueOrDefault" || !ResultSymbols.IsResultType(method.ContainingType))
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return;
        }

        var receiverText = access.Expression.ToString();
        var member = FindEnclosingMember(invocation);

        if (member is null)
        {
            return;
        }

        if (IsOutcomeChecked(member, receiverText, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.UnguardedOutcome,
            invocation.GetLocation()));
    }

    private static SyntaxNode? FindEnclosingMember(SyntaxNode node)
        => node.Ancestors().FirstOrDefault(ancestor => ancestor is MemberDeclarationSyntax);

    private static bool IsOutcomeChecked(
        SyntaxNode member,
        string receiverText,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var node in member.DescendantNodes())
        {
            switch (node)
            {
                case MemberAccessExpressionSyntax access
                    when GuardNames.Contains(access.Name.Identifier.ValueText)
                         && access.Expression.ToString() == receiverText:
                    return true;

                // Both `var (ok, value, error) = result` and `(ok, value, error) = result` are
                // deconstruction assignments, and both count as reading the outcome.
                case AssignmentExpressionSyntax assignment
                    when assignment.Right.ToString() == receiverText
                         && semanticModel.GetOperation(assignment, cancellationToken) is IDeconstructionAssignmentOperation:
                    return true;
            }
        }

        return false;
    }
}
