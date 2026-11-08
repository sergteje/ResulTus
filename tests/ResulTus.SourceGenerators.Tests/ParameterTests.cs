// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.SourceGenerators.Tests;

/// <summary>
/// Tests that the generated implementation repeats the declared parameter list exactly, so the two
/// parts of the partial method match and the compiler sees one method rather than two.
/// </summary>
public class ParameterTests
{
    /// <summary>
    /// GIVEN a factory whose parameter is a nullable reference type
    /// WHEN the generated text is read and the factory is invoked
    /// THEN the annotation MUST survive into the generated signature, and the null argument MUST
    /// reach the message, because dropping the annotation would change the method's contract.
    /// </summary>
    [Fact]
    public void NullableReferenceParameter_ShouldKeepItsAnnotation()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class UserErrors
            {
                [ErrorFactory("NAME", ErrorType.Validation, "name {name}")]
                public static partial Error WithName(string? name);
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Text.Should().Contain("global::System.String? name");
        run.InvokeError("Sample.UserErrors", "WithName", (object?)null).Message.Should().Be("name ");
    }

    /// <summary>
    /// GIVEN a factory whose parameter is passed by reference
    /// WHEN the generated text is read
    /// THEN the modifier MUST be reproduced, because a partial implementation whose parameter is
    /// passed differently is a different signature.
    /// </summary>
    /// <param name="modifier">The parameter modifier, as written.</param>
    /// <param name="expected">The text the generated signature must contain.</param>
    [Theory]
    [InlineData("ref int value", "ref global::System.Int32 value")]
    [InlineData("in int value", "in global::System.Int32 value")]
    [InlineData("ref readonly int value", "ref readonly global::System.Int32 value")]
    public void ReferenceParameter_ShouldBeReproduced(string modifier, string expected)
    {
        var source = $$"""
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class UserErrors
            {
                [ErrorFactory("REF", ErrorType.Failure, "value {value}")]
                public static partial Error WithRef({{modifier}});
            }
            """;

        var run = GeneratorHarness.Run(source);

        run.Text.Should().Contain(expected);
        run.ShouldCompile();
    }

    /// <summary>
    /// GIVEN a factory that takes <c>int</c>
    /// WHEN the generated text is read
    /// THEN the type MUST be written fully qualified rather than as the keyword, because the
    /// generated file has no <c>using</c> directives and a user type named <c>Int32</c> in scope
    /// must not be able to capture it.
    /// </summary>
    [Fact]
    public void BuiltInType_ShouldBeFullyQualified()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class UserErrors
            {
                [ErrorFactory("COUNT", ErrorType.Failure, "count {count}")]
                public static partial Error Count(int count);
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Text.Should().Contain("global::System.Int32 count");
        run.Text.Should().NotContain("(int count)");
        run.InvokeError("Sample.UserErrors", "Count", 7).Message.Should().Be("count 7");
    }

    /// <summary>
    /// GIVEN a factory with several parameters
    /// WHEN the generated text is read and the factory is invoked
    /// THEN they MUST be reproduced in declaration order, separated by a comma and a space.
    /// </summary>
    [Fact]
    public void MultipleParameters_ShouldKeepTheirOrder()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class UserErrors
            {
                [ErrorFactory("RANGE", ErrorType.Validation, "{field} must be between {min} and {max}")]
                public static partial Error OutOfRange(string field, int min, int max);
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Text.Should().Contain(
            "(global::System.String field, global::System.Int32 min, global::System.Int32 max)");
        run.InvokeError("Sample.UserErrors", "OutOfRange", "age", 18, 99)
            .Message.Should().Be("age must be between 18 and 99");
    }

    /// <summary>
    /// GIVEN a factory declared on a generic type whose parameter is the type parameter
    /// WHEN the generated text is read
    /// THEN the type parameter MUST be written as itself, because it is in scope inside the
    /// generated part and there is nothing to qualify.
    /// </summary>
    [Fact]
    public void TypeParameter_ShouldBeWrittenAsItself()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors<T>
            {
                [ErrorFactory("VALUE", ErrorType.Unexpected, "value {value}")]
                public static partial Error Unexpected(T value);
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Text.Should().Contain("global::ResulTus.Error Unexpected(T value)");
        run.ShouldCompile();
    }

    /// <summary>
    /// GIVEN a factory with a <c>params</c> parameter
    /// WHEN the generated text is read
    /// THEN the modifier MUST be reproduced, because the two parts of a partial method must have
    /// the same signature and <c>params</c> is part of it.
    /// </summary>
    [Fact]
    public void ParamsParameter_ShouldBeReproduced()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class UserErrors
            {
                [ErrorFactory("MANY", ErrorType.Failure, "many")]
                public static partial Error Many(params string[] values);
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Text.Should().Contain("params global::System.String[] values");
        run.ShouldCompile();
    }

    /// <summary>
    /// GIVEN a factory with a defaulted parameter
    /// WHEN the generated text is read
    /// THEN the default value MUST NOT be repeated, because only the declaring part may carry it.
    /// </summary>
    [Fact]
    public void DefaultValue_ShouldNotBeRepeated()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class UserErrors
            {
                [ErrorFactory("COUNT", ErrorType.Failure, "count {count}")]
                public static partial Error Count(int count = 5);
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Text.Should().NotContain("= 5");
        run.ShouldCompile();
    }
}
