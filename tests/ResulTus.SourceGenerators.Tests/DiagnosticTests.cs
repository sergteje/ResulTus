// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.SourceGenerators.Tests;

/// <summary>
/// Tests for <c>RESULTGEN001</c>: a declaration the generator cannot complete is reported, with the
/// offending member named, instead of silently producing nothing.
/// </summary>
/// <remarks>
/// These tests do not assert that the output compiles. Every one of these declarations is broken on
/// its own — a partial method with no implementation is <c>CS8795</c> — and the point of the
/// diagnostic is to explain why the generator did not fix it.
/// </remarks>
public class DiagnosticTests
{
    /// <summary>
    /// GIVEN an annotated method whose type is not marked <c>[ResultErrors]</c>
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the type and produce no file, because the
    /// attribute on the type is the opt-in.
    /// </summary>
    [Fact]
    public void TypeWithoutTheMarker_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [Obsolete("still not opted in")]
            public static partial class NotOptedIn
            {
                [ErrorFactory("CODE", ErrorType.Failure, "message")]
                public static partial Error Fail();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "NotOptedIn");
    }

    /// <summary>
    /// GIVEN an annotated method on a type that is not <c>partial</c>
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the type, because the generated part has
    /// nowhere to merge.
    /// </summary>
    [Fact]
    public void TypeThatIsNotPartial_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static class Solid
            {
                [ErrorFactory("CODE", ErrorType.Failure, "message")]
                public static partial Error Fail();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Solid");
    }

    /// <summary>
    /// GIVEN an annotated method on a <c>file</c>-scoped type
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the type, because a file-local type cannot be
    /// reopened from the generated file.
    /// </summary>
    [Fact]
    public void FileLocalType_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            file static partial class Hidden
            {
                [ErrorFactory("CODE", ErrorType.Failure, "message")]
                public static partial Error Fail();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Hidden");
    }

    /// <summary>
    /// GIVEN an annotated method that is not <c>partial</c>
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the method, because the generator can only
    /// complete a partial declaration.
    /// </summary>
    [Fact]
    public void MethodThatIsNotPartial_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors
            {
                [ErrorFactory("CODE", ErrorType.Failure, "message")]
                public static Error Fail() => Error.Failure("CODE");
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Fail");
    }

    /// <summary>
    /// GIVEN an annotated partial method that already has an implementation
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the method, because a second implementation
    /// would be a duplicate.
    /// </summary>
    [Fact]
    public void MethodThatAlreadyHasABody_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors
            {
                public static partial Error Fail();

                [ErrorFactory("CODE", ErrorType.Failure, "message")]
                public static partial Error Fail() => Error.Failure("CODE");
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Fail");
    }

    /// <summary>
    /// GIVEN an annotated method that is not <c>static</c>
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the method, because the generated body calls
    /// a static factory and an instance factory has no meaning.
    /// </summary>
    [Fact]
    public void MethodThatIsNotStatic_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public partial class Errors
            {
                [ErrorFactory("CODE", ErrorType.Failure, "message")]
                public partial Error Fail();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Fail");
    }

    /// <summary>
    /// GIVEN an annotated method that does not return <see cref="Error"/>
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the method and the type it returns instead.
    /// </summary>
    [Fact]
    public void MethodWithAnotherReturnType_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors
            {
                [ErrorFactory("CODE", ErrorType.Failure, "message")]
                public static partial string Fail();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Fail");
    }

    /// <summary>
    /// GIVEN a factory that declares <c>ErrorType.None</c>
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the method, because a factory that produces
    /// "no error" is not a factory and <c>Error.None</c> is a property, not a method.
    /// </summary>
    [Fact]
    public void NoneCategory_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors
            {
                [ErrorFactory("CODE", ErrorType.None, "message")]
                public static partial Error Fail();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Fail");
    }

    /// <summary>
    /// GIVEN a factory with an <c>out</c> parameter
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the method, because the generated body
    /// returns an <see cref="Error"/> and cannot assign the caller's variable. Assigning a default
    /// would hand the caller a value the declaration never produced.
    /// </summary>
    [Fact]
    public void OutParameter_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors
            {
                [ErrorFactory("CODE", ErrorType.Failure, "message")]
                public static partial Error Fail(out int value);
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Fail");
    }

    /// <summary>
    /// GIVEN a factory annotated with a <c>null</c> code
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the method, because an error without a code
    /// cannot be identified at the call site.
    /// </summary>
    [Fact]
    public void NullCode_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors
            {
                [ErrorFactory(null!, ErrorType.Failure, "message")]
                public static partial Error Fail();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Fail");
    }

    /// <summary>
    /// GIVEN a factory whose <c>[ErrorFactory]</c> names a category that is not a member of
    /// <c>ErrorType</c>
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the method, because there is no
    /// <c>Error</c> factory to call for a value that does not exist.
    /// </summary>
    [Fact]
    public void UnknownCategory_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors
            {
                [ErrorFactory("CODE", (ErrorType)99, "message")]
                public static partial Error Fail();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Fail");
    }

    /// <summary>
    /// GIVEN an annotated method whose <c>[ErrorFactory]</c> does not supply the three arguments
    /// WHEN the generator runs
    /// THEN it MUST report <c>RESULTGEN001</c> naming the method instead of indexing past the end
    /// of an unbound attribute, because a half-written annotation is the most common mistake.
    /// </summary>
    [Fact]
    public void IncompleteAttribute_ShouldBeReported()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors
            {
                [ErrorFactory("CODE")]
                public static partial Error Fail();
            }
            """;

        var run = GeneratorHarness.Run(Source);

        AssertGenerationError(run, "Fail");
    }

    private static void AssertGenerationError(GeneratorRun run, string offendingMember)
    {
        var diagnostic = run.Diagnostics.Single(item => item.Id == "RESULTGEN001");

        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain(offendingMember);
        diagnostic.Location.Should().NotBe(Location.None);

        run.Sources.Should().BeEmpty();
    }
}
