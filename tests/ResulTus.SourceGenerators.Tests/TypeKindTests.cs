// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.SourceGenerators.Tests;

/// <summary>
/// Tests that the generated part merges with every type kind the attribute accepts, and with the
/// nesting and generic shapes a real domain type uses.
/// </summary>
public class TypeKindTests
{
    /// <summary>
    /// GIVEN a <c>[ResultErrors]</c> type declared as a class, a static class, a struct, a record or
    /// a record struct
    /// WHEN the generator runs
    /// THEN the generated part MUST merge with it and the factory MUST run, because the generator
    /// has no reason to care which kind it is completing.
    /// </summary>
    /// <param name="declaration">The type declaration, without the name.</param>
    [Theory]
    [InlineData("public partial class")]
    [InlineData("public static partial class")]
    [InlineData("public partial struct")]
    [InlineData("public partial record")]
    [InlineData("public partial record struct")]
    public void EveryTypeKind_ShouldCompileAndRun(string declaration)
    {
        var source = $$"""
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            {{declaration}} Errors
            {
                [ErrorFactory("KIND", ErrorType.Failure, "boom")]
                public static partial Error Fail();
            }
            """;

        var error = GeneratorHarness.Run(source).InvokeError("Sample.Errors", "Fail");

        error.Code.Should().Be("KIND");
        error.Type.Should().Be(ErrorType.Failure);
        error.Message.Should().Be("boom");
    }

    /// <summary>
    /// GIVEN a <c>[ResultErrors]</c> type nested inside another partial type
    /// WHEN the generator runs
    /// THEN the generated part MUST reopen the whole nesting, because a nested type cannot be
    /// declared outside its container.
    /// </summary>
    [Fact]
    public void NestedType_ShouldReopenItsContainers()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            public static partial class Outer
            {
                [ResultErrors]
                public static partial class Inner
                {
                    [ErrorFactory("INNER", ErrorType.Conflict, "inner")]
                    public static partial Error Conflict();
                }
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Text.Should().Contain("partial class Outer");
        run.Text.Should().Contain("partial class Inner");
        run.InvokeError("Sample.Outer+Inner", "Conflict").Code.Should().Be("INNER");
    }

    /// <summary>
    /// GIVEN a generic <c>[ResultErrors]</c> type whose factory takes a type parameter
    /// WHEN the generator runs
    /// THEN the generated part MUST repeat the type parameter list, because a partial declaration
    /// without it would be a different type.
    /// </summary>
    [Fact]
    public void GenericType_ShouldRepeatItsTypeParameters()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors<T>
            {
                [ErrorFactory("GENERIC", ErrorType.Unexpected, "value {value}")]
                public static partial Error Unexpected(T value);
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Text.Should().Contain("partial class Errors<T>");
        run.ShouldCompile();
    }

    /// <summary>
    /// GIVEN a type in the global namespace
    /// WHEN the generator runs
    /// THEN the generated file MUST NOT wrap itself in a namespace block, because that would put
    /// the second part somewhere the first one is not.
    /// </summary>
    [Fact]
    public void GlobalNamespace_ShouldNotEmitANamespaceBlock()
    {
        const string Source = """
            using System;
            using ResulTus;

            [ResultErrors]
            public static partial class GlobalErrors
            {
                [ErrorFactory("GLOBAL", ErrorType.Failure, "global")]
                public static partial Error Fail();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Text.Should().NotContain("namespace");
        run.InvokeError("GlobalErrors", "Fail").Code.Should().Be("GLOBAL");
    }
}
