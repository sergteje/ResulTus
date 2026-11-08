// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for <c>RESULT004</c>: a <c>Bind</c> whose lambda only wraps the mapped value back into a
/// result, which is what <c>Map</c> already does.
/// </summary>
public class RedundantBindTests
{
    private const string RedundantSource = """
        using System;
        using ResulTus;
        using ResulTus.Extensions;

        public static class Subject
        {
            public static Result<string> Redundant(Result<int> result)
                => result.Bind(value => Result<string>.Success(value.ToString()));
        }
        """;

    private const string LegitimateSource = """
        using System;
        using ResulTus;
        using ResulTus.Extensions;

        public static class Subject
        {
            public static Result<string> Real(Result<int> result)
                => result.Bind(value => Compute(value));

            public static Result<string> Mapped(Result<int> result)
                => result.Map(value => value.ToString());

            private static Result<string> Compute(int value) => Result<string>.Success(value.ToString());
        }
        """;

    /// <summary>
    /// GIVEN a <c>Bind</c> whose lambda wraps its result in <c>Result&lt;string&gt;.Success</c>
    /// WHEN the analyzer runs
    /// THEN it MUST report <c>RESULT004</c>, because <c>Map</c> expresses the same thing.
    /// </summary>
    [Fact]
    public async Task BindWrappingSuccess_ShouldReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new RedundantBindAnalyzer(),
            RedundantSource,
            "RESULT004");

        diagnostics.Should().ContainSingle();
    }

    /// <summary>
    /// GIVEN a <c>Bind</c> that calls another result-producing method, and a <c>Map</c>
    /// WHEN the analyzer runs
    /// THEN it MUST report nothing, because neither can be simplified.
    /// </summary>
    [Fact]
    public async Task LegitimateBindAndMap_ShouldNotReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new RedundantBindAnalyzer(),
            LegitimateSource,
            "RESULT004");

        diagnostics.Should().BeEmpty();
    }
}
