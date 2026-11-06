// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.SourceGenerators.Tests;

/// <summary>
/// Tests for the generated factory body: what the generator writes, and what the resulting
/// <see cref="Error"/> actually holds at run time.
/// </summary>
public class ErrorFactoryGenerationTests
{
    private const string NotFoundSource = """
        using System;
        using ResulTus;

        namespace Sample;

        [ResultErrors]
        public static partial class UserErrors
        {
            [ErrorFactory("USER_NOT_FOUND", ErrorType.NotFound, "User {id} not found")]
            public static partial Error NotFound(Guid id);
        }
        """;

    /// <summary>
    /// GIVEN a type marked <c>[ResultErrors]</c> with one annotated partial method
    /// WHEN the generator runs
    /// THEN it MUST produce exactly one file, and the completed compilation MUST compile, because
    /// the generated part is what turns the declaration into a method.
    /// </summary>
    [Fact]
    public void DeclaredFactory_ShouldProduceOneCompilingFile()
    {
        var run = GeneratorHarness.Run(NotFoundSource);

        run.Sources.Should().ContainSingle();
        run.ShouldCompile();
    }

    /// <summary>
    /// GIVEN the same declaration
    /// WHEN the generated text is read
    /// THEN it MUST repeat the declared signature and delegate to the matching
    /// <see cref="Error"/> factory, because that is the whole generated body.
    /// </summary>
    [Fact]
    public void GeneratedSource_ShouldRepeatTheSignatureAndDelegate()
    {
        var run = GeneratorHarness.Run(NotFoundSource);

        run.Text.Should().Contain(
            "public static partial global::ResulTus.Error NotFound(global::System.Guid id)");
        run.Text.Should().Contain(
            "=> global::ResulTus.Error.NotFound(\"USER_NOT_FOUND\", $\"User {id} not found\");");
    }

    /// <summary>
    /// GIVEN the same declaration
    /// WHEN the generated factory is invoked
    /// THEN the returned error MUST carry the declared code, the declared category and the message
    /// with the placeholder filled from the argument.
    /// </summary>
    [Fact]
    public void GeneratedFactory_ShouldProduceTheDeclaredError()
    {
        var id = Guid.NewGuid();

        var error = GeneratorHarness.Run(NotFoundSource).InvokeError("Sample.UserErrors", "NotFound", id);

        error.Code.Should().Be("USER_NOT_FOUND");
        error.Type.Should().Be(ErrorType.NotFound);
        error.Message.Should().Be($"User {id} not found");
    }

    /// <summary>
    /// GIVEN a factory declared with a <c>null</c> template
    /// WHEN it is invoked
    /// THEN the error's message MUST be <c>null</c>, because an error with no message is not an
    /// error with an empty one.
    /// </summary>
    [Fact]
    public void NullTemplate_ShouldProduceAnErrorWithoutMessage()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class UserErrors
            {
                [ErrorFactory("SILENT", ErrorType.Failure, null)]
                public static partial Error Silent();
            }
            """;

        var error = GeneratorHarness.Run(Source).InvokeError("Sample.UserErrors", "Silent");

        error.Code.Should().Be("SILENT");
        error.Type.Should().Be(ErrorType.Failure);
        error.Message.Should().BeNull();
    }

    /// <summary>
    /// GIVEN a factory declared with an empty template
    /// WHEN it is invoked
    /// THEN the error's message MUST be empty rather than <c>null</c>, because the declaration
    /// asked for a message and supplied an empty one.
    /// </summary>
    [Fact]
    public void EmptyTemplate_ShouldProduceAnEmptyMessage()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class UserErrors
            {
                [ErrorFactory("EMPTY", ErrorType.Failure, "")]
                public static partial Error Empty();
            }
            """;

        var error = GeneratorHarness.Run(Source).InvokeError("Sample.UserErrors", "Empty");

        error.Message.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a factory declared with no parameters
    /// WHEN the generated text is read
    /// THEN the signature MUST still be valid and the body MUST pass no arguments to the message,
    /// because a template without placeholders has nothing to fill.
    /// </summary>
    [Fact]
    public void ParameterlessFactory_ShouldGenerateACallWithoutArguments()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class UserErrors
            {
                [ErrorFactory("BARE", ErrorType.Unexpected, "nothing to fill")]
                public static partial Error Bare();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Text.Should().Contain("public static partial global::ResulTus.Error Bare()");
        run.Text.Should().Contain("$\"nothing to fill\"");
        run.InvokeError("Sample.UserErrors", "Bare").Message.Should().Be("nothing to fill");
    }
}
