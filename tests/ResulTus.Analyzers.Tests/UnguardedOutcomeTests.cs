// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for <c>RESULT002</c>: a default value read from a result whose outcome is never consulted.
/// </summary>
public class UnguardedOutcomeTests
{
    private const string UnguardedSource = """
        using System;
        using ResulTus;

        public static class Subject
        {
            public static int ExpressionBody(Result<int> result)
                => result.GetValueOrDefault();

            public static string BlockBody(Result<string> result)
            {
                var value = result.GetValueOrDefault();
                return value;
            }
        }
        """;

    private const string GuardedSource = """
        using System;
        using ResulTus;

        public static class Subject
        {
            public static int WithSuccessCheck(Result<int> result)
                => result.IsSuccess ? result.GetValueOrDefault() : 0;

            public static int WithFailureCheck(Result<int> result)
                => result.IsFailure ? 0 : result.GetValueOrDefault();

            public static int WithTryGetValue(Result<int> result)
                => result.TryGetValue(out var value) ? value : 0;

            public static int WithMatch(Result<int> result)
                => result.Match(value => value, _ => 0);

            public static int WithGetValueOr(Result<int> result)
                => result.GetValueOr(_ => 0);

            public static int WithFallbackArgument(Result<int> result)
                => result.GetValueOrDefault(0);

            public static int WithDeconstruction(Result<int> result)
            {
                var (ok, value, error) = result;
                return ok ? value : result.GetValueOrDefault();
            }
        }
        """;

    /// <summary>
    /// GIVEN a <c>GetValueOrDefault()</c> call with no fallback in a member that never checks the
    /// outcome
    /// WHEN the analyzer runs
    /// THEN it MUST report <c>RESULT002</c> for each call, because the default value is returned in
    /// silence on failure.
    /// </summary>
    [Fact]
    public async Task UnguardedCalls_ShouldReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new UnguardedOutcomeAnalyzer(),
            UnguardedSource,
            "RESULT002");

        diagnostics.Should().HaveCount(2);
    }

    /// <summary>
    /// GIVEN a member that checks the outcome before or alongside the default read, or that supplies
    /// a fallback argument
    /// WHEN the analyzer runs
    /// THEN it MUST report nothing, because the caller is not reading a silent default.
    /// </summary>
    [Fact]
    public async Task GuardedCalls_ShouldNotReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new UnguardedOutcomeAnalyzer(),
            GuardedSource,
            "RESULT002");

        diagnostics.Should().BeEmpty();
    }
}
