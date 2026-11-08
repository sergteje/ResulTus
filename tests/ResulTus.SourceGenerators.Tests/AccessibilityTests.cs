// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.SourceGenerators.Tests;

/// <summary>
/// Tests that the generated implementation repeats the declared accessibility of the method.
/// </summary>
/// <remarks>
/// Accessibility is part of a partial method's signature: a part that widens or narrows it is a
/// different method, and the compiler refuses the pair.
/// </remarks>
public class AccessibilityTests
{
    /// <summary>
    /// GIVEN a factory declared with any accessibility
    /// WHEN the generated text is read
    /// THEN the generated part MUST declare the same one, so the two parts are the same method.
    /// </summary>
    /// <param name="modifier">The declared accessibility, as written.</param>
    /// <param name="expected">The text the generated signature must start with.</param>
    [Theory]
    [InlineData("public", "public ")]
    [InlineData("internal", "internal ")]
    [InlineData("private", "private ")]
    [InlineData("protected", "protected ")]
    [InlineData("protected internal", "protected internal ")]
    [InlineData("private protected", "private protected ")]
    public void DeclaredAccessibility_ShouldBeReproduced(string modifier, string expected)
    {
        var source = $$"""
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public partial class Errors
            {
                [ErrorFactory("CODE", ErrorType.Failure, "boom")]
                {{modifier}} static partial Error Fail();
            }
            """;

        var run = GeneratorHarness.Run(source);

        run.Text.Should().Contain(expected + "static partial global::ResulTus.Error Fail()");
        run.ShouldCompile();
    }
}
