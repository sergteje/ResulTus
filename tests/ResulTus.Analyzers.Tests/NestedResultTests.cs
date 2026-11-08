// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for <c>RESULT005</c>: converting a result into another result.
/// </summary>
public class NestedResultTests
{
    private const string NestedSource = """
        using System;
        using ResulTus;
        using ResulTus.Extensions;

        public static class Subject
        {
            public static Result<Result<int>> Nested(Result<int> result)
                => result.ToResult();
        }
        """;

    private const string PlainSource = """
        using System;
        using ResulTus;
        using ResulTus.Extensions;

        public static class Subject
        {
            public static Result<int> FromInt(int value)
                => value.ToResult();

            public static Result<string> FromString(string value)
                => value.ToResult();
        }
        """;

    /// <summary>
    /// GIVEN a <c>ToResult()</c> call whose receiver is already a result
    /// WHEN the analyzer runs
    /// THEN it MUST report <c>RESULT005</c>, because the call builds a result nested inside another.
    /// </summary>
    [Fact]
    public async Task ConvertingAResult_ShouldReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new NestedResultAnalyzer(),
            NestedSource,
            "RESULT005");

        diagnostics.Should().ContainSingle();
    }

    /// <summary>
    /// GIVEN a <c>ToResult()</c> call over a plain value
    /// WHEN the analyzer runs
    /// THEN it MUST report nothing, because that is the conversion the method exists for.
    /// </summary>
    [Fact]
    public async Task ConvertingAPlainValue_ShouldNotReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new NestedResultAnalyzer(),
            PlainSource,
            "RESULT005");

        diagnostics.Should().BeEmpty();
    }
}
