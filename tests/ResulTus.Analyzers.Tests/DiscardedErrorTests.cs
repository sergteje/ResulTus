// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for <c>RESULT003</c>: a deconstruction that throws the error away.
/// </summary>
public class DiscardedErrorTests
{
    private const string DiscardedSource = """
        using System;
        using ResulTus;

        public static class Subject
        {
            public static void Discards(Result unit, Result<int> value, Result<bool, string> pair)
            {
                var (unitOk, _) = unit;
                var (valueOk, valueValue, _) = value;
                (valueOk, valueValue, _) = value;
                var (pairOk, pairValue, _) = pair;
            }
        }
        """;

    private const string CapturedSource = """
        using System;
        using ResulTus;

        public static class Subject
        {
            public static void Captures(Result unit, Result<int> value, Result<bool, string> pair)
            {
                var (unitOk, unitError) = unit;
                var (valueOk, valueValue, valueError) = value;
                var (pairOk, pairValue, pairError) = pair;
            }

            public static void UnrelatedTuple()
            {
                var (first, _) = (1, 2);
            }
        }
        """;

    /// <summary>
    /// GIVEN a deconstruction of any of the three result types that discards the last element
    /// WHEN the analyzer runs
    /// THEN it MUST report <c>RESULT003</c> for both the declaration and the assignment form,
    /// because the error is the last element in every result type.
    /// </summary>
    [Fact]
    public async Task DiscardedError_ShouldReportForEveryResultTypeAndForm()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new DiscardedErrorAnalyzer(),
            DiscardedSource,
            "RESULT003");

        diagnostics.Should().HaveCount(4);
    }

    /// <summary>
    /// GIVEN a deconstruction that captures the error, and a plain tuple that discards an element
    /// WHEN the analyzer runs
    /// THEN it MUST report nothing, because no result loses its error.
    /// </summary>
    [Fact]
    public async Task CapturedError_ShouldNotReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new DiscardedErrorAnalyzer(),
            CapturedSource,
            "RESULT003");

        diagnostics.Should().BeEmpty();
    }
}
