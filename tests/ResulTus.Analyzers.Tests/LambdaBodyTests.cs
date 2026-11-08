// Copyright (c) ResulTus. All rights reserved.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for the lambda reader shared by <c>RESULT004</c> and <c>RESULT006</c>.
/// </summary>
public class LambdaBodyTests
{
    /// <summary>
    /// GIVEN a lambda written as an expression or as a block with a single return
    /// WHEN its returned expression is read
    /// THEN the expression MUST be found, because both forms say the same thing.
    /// </summary>
    /// <param name="text">The argument source.</param>
    [Theory]
    [InlineData("value => value")]
    [InlineData("(value) => value")]
    [InlineData("value => { return value; }")]
    public void GetReturnedExpression_ShouldReadTheReturnedExpression(string text)
    {
        LambdaBody.GetReturnedExpression(ParseArgument(text)).Should().NotBeNull();
    }

    /// <summary>
    /// GIVEN a method group, a block with no return, a block with two returns and a block with an
    /// expression statement
    /// WHEN its returned expression is read
    /// THEN nothing MUST be found, because there is no single expression to read.
    /// </summary>
    /// <param name="text">The argument source.</param>
    [Theory]
    [InlineData("Compute")]
    [InlineData("value => { }")]
    [InlineData("value => { return; }")]
    [InlineData("value => { value.ToString(); }")]
    [InlineData("value => { return value; return value; }")]
    public void GetReturnedExpression_ShouldRejectWhatItCannotRead(string text)
    {
        LambdaBody.GetReturnedExpression(ParseArgument(text)).Should().BeNull();
    }

    /// <summary>
    /// GIVEN a lambda node that carries neither an expression body nor a block
    /// WHEN its returned expression is read
    /// THEN nothing MUST be found, because there is nothing to read.
    /// </summary>
    [Fact]
    public void GetReturnedExpression_ShouldRejectALambdaWithNoBody()
    {
        var lambda = SyntaxFactory.SimpleLambdaExpression(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier("value")));

        LambdaBody.GetReturnedExpression(lambda).Should().BeNull();
    }

    private static ExpressionSyntax ParseArgument(string text)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var tree = CSharpSyntaxTree.ParseText(
            $"class C {{ void M() {{ F({text}); }} }}",
            cancellationToken: cancellationToken);

        return tree.GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<ArgumentSyntax>()
            .Single()
            .Expression;
    }
}
