// Copyright (c) ResulTus. All rights reserved.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ResulTus.Analyzers;

/// <summary>
/// Reads the value a lambda passed as an argument returns.
/// </summary>
internal static class LambdaBody
{
    /// <summary>
    /// Gets the expression a lambda returns, unwrapping a block that only returns one expression.
    /// </summary>
    /// <param name="expression">The argument expression to inspect.</param>
    /// <returns>
    /// The returned expression, or <c>null</c> when <paramref name="expression"/> is not a lambda or
    /// its body does more than return a single expression.
    /// </returns>
    internal static ExpressionSyntax? GetReturnedExpression(ExpressionSyntax expression)
        => expression switch
        {
            SimpleLambdaExpressionSyntax simple => Unwrap(simple.ExpressionBody, simple.Block),
            ParenthesizedLambdaExpressionSyntax parenthesised => Unwrap(parenthesised.ExpressionBody, parenthesised.Block),
            _ => null,
        };

    private static ExpressionSyntax? Unwrap(ExpressionSyntax? expressionBody, BlockSyntax? block)
    {
        if (expressionBody is not null)
        {
            return expressionBody;
        }

        // A block holding a single `return x;` says the same thing as `=> x`.
        return block is { Statements.Count: 1 }
               && block.Statements[0] is ReturnStatementSyntax { Expression: { } returned }
            ? returned
            : null;
    }
}
