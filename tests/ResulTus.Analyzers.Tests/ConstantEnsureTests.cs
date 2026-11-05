// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for <c>RESULT006</c>: an <c>Ensure</c> whose predicate ignores the value and returns a
/// constant, which makes the guard dead code.
/// </summary>
public class ConstantEnsureTests
{
    private const string ConstantSource = """
        using System;
        using ResulTus;
        using ResulTus.Extensions;

        public static class Subject
        {
            public static Result<int> AlwaysPasses(Result<int> result)
                => result.Ensure(_ => true, Error.Failure("never"));

            public static Result<int> NeverPasses(Result<int> result)
                => result.Ensure(value => false, Error.Failure("always"));
        }
        """;

    private const string RealSource = """
        using System;
        using ResulTus;
        using ResulTus.Extensions;

        public static class Subject
        {
            public static Result<int> Real(Result<int> result)
                => result.Ensure(value => value > 0, Error.Failure("negative"));

            public static Result<int> Captured(Result<int> result, int limit)
                => result.Ensure(value => value < limit, Error.Failure("over"));
        }
        """;

    /// <summary>
    /// GIVEN an <c>Ensure</c> whose predicate returns a constant
    /// WHEN the analyzer runs
    /// THEN it MUST report <c>RESULT006</c> once per call, because the guard can never depend on
    /// the value.
    /// </summary>
    [Fact]
    public async Task ConstantPredicate_ShouldReportTwice()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new ConstantEnsureAnalyzer(),
            ConstantSource,
            "RESULT006");

        diagnostics.Should().HaveCount(2);
        diagnostics.Select(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line)
            .OrderBy(line => line)
            .Should()
            .Equal(7, 10);
    }

    /// <summary>
    /// GIVEN an <c>Ensure</c> whose predicate uses the value
    /// WHEN the analyzer runs
    /// THEN it MUST report nothing, because the guard is doing its job.
    /// </summary>
    [Fact]
    public async Task RealPredicate_ShouldNotReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new ConstantEnsureAnalyzer(),
            RealSource,
            "RESULT006");

        diagnostics.Should().BeEmpty();
    }
}
