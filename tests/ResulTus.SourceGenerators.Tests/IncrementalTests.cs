// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.SourceGenerators.Tests;

/// <summary>
/// Tests that the pipeline stays opt-in and produces the same bytes every time it runs.
/// </summary>
public class IncrementalTests
{
    /// <summary>
    /// GIVEN a compilation that uses neither attribute
    /// WHEN the generator runs
    /// THEN it MUST produce no file at all, because nothing opted in.
    /// </summary>
    [Fact]
    public void WithoutAttributes_ShouldProduceNothing()
    {
        const string Source = """
            namespace Sample;

            public static class Plain
            {
                public static int Value() => 1;
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Sources.Should().BeEmpty();
        run.Diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a type marked <c>[ResultErrors]</c> with no annotated method
    /// WHEN the generator runs
    /// THEN it MUST produce no file, because the marker alone declares nothing to implement.
    /// </summary>
    [Fact]
    public void MarkerWithoutFactories_ShouldProduceNothing()
    {
        const string Source = """
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors
            {
                public static Error Fail() => Error.Failure("CODE");
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Sources.Should().BeEmpty();
        run.Diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN two overloads of the same factory name
    /// WHEN the generator runs
    /// THEN it MUST produce one file per overload with distinct hint names, because the hint name
    /// carries a hash of the full signature rather than only the method name.
    /// </summary>
    [Fact]
    public void Overloads_ShouldProduceDistinctFiles()
    {
        const string Source = """
            using System;
            using ResulTus;

            namespace Sample;

            [ResultErrors]
            public static partial class Errors
            {
                [ErrorFactory("BY_ID", ErrorType.NotFound, "id {id}")]
                public static partial Error Missing(Guid id);

                [ErrorFactory("BY_CODE", ErrorType.NotFound, "code {code}")]
                public static partial Error Missing(string code);
            }
            """;

        var run = GeneratorHarness.Run(Source);

        run.Sources.Should().HaveCount(2);
        run.Sources.Select(source => source.HintName).Should().OnlyHaveUniqueItems();
        run.ShouldCompile();
    }

    /// <summary>
    /// GIVEN the same source
    /// WHEN the generator runs twice
    /// THEN the hint names and the text MUST be identical, because a hint name derived from a
    /// per-process hash would change on every run and break the build.
    /// </summary>
    [Fact]
    public void RunningTwice_ShouldProduceTheSameFiles()
    {
        const string Source = """
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

        var first = GeneratorHarness.Run(Source);
        var second = GeneratorHarness.Run(Source);

        second.Sources.Select(source => source.HintName)
            .Should().Equal(first.Sources.Select(source => source.HintName));
        second.Text.Should().Be(first.Text);
    }
}
