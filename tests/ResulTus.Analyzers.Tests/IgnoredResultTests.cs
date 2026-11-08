// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for <c>RESULT001</c>: a result that is produced and then dropped.
/// </summary>
public class IgnoredResultTests
{
    private const string IgnoredSource = """
        using System;
        using ResulTus;

        public static class Subject
        {
            public static Result DoWork() => Result.Success();

            public static Result<int> GetValue() => Result<int>.Success(1);

            public static Result<int, string> GetPair() => Result<int, string>.Success(1);

            public static void Ignored()
            {
                DoWork();
            }
        }
        """;

    private const string HandledSource = """
        using System;
        using ResulTus;
        using ResulTus.Extensions;

        public static class Subject
        {
            public static Result DoWork() => Result.Success();

            public static Result<int> GetValue() => Result<int>.Success(1);

            public static Result<int, string> GetPair() => Result<int, string>.Success(1);

            public static void Handled()
            {
                var stored = DoWork();
                _ = GetValue();
                Consume(GetPair());
                _ = GetValue().MapError(error => error);
                System.Console.WriteLine(stored.IsSuccess);
            }

            public static void Consume(Result<int, string> value)
            {
            }
        }
        """;

    /// <summary>
    /// GIVEN a unit result produced and discarded as a statement
    /// WHEN the analyzer runs
    /// THEN it MUST report <c>RESULT001</c> on that statement.
    /// </summary>
    [Fact]
    public async Task IgnoredUnitResult_ShouldReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new IgnoredResultAnalyzer(),
            IgnoredSource,
            "RESULT001");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Location.GetLineSpan().StartLinePosition.Line.Should().Be(13);
    }

    /// <summary>
    /// GIVEN results that are stored, explicitly discarded, passed as an argument or chained
    /// WHEN the analyzer runs
    /// THEN it MUST report nothing, because each of those is a deliberate handling.
    /// </summary>
    [Fact]
    public async Task HandledResults_ShouldNotReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new IgnoredResultAnalyzer(),
            HandledSource,
            "RESULT001");

        diagnostics.Should().BeEmpty();
    }
}
