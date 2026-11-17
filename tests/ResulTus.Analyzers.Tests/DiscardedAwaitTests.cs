// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for <c>RESULT008</c>: an awaitable awaited as a whole statement, discarding the result it
/// produces.
/// </summary>
public class DiscardedAwaitTests
{
    private const string DiscardedSource = """
        using System.Threading.Tasks;
        using ResulTus;

        public static class Subject
        {
            public static Task<Result> UnitAsync() => Task.FromResult(Result.Success());

            public static Task<Result<int>> ValueAsync() => Task.FromResult(Result<int>.Success(1));

            public static Task<Result<int, string>> PairAsync() => Task.FromResult(Result<int, string>.Success(1));

            public static ValueTask<Result> UnitValueAsync() => new(Result.Success());

            public static ValueTask<Result<int>> ValueValueAsync() => new(Result<int>.Success(1));

            public static ValueTask<Result<int, string>> PairValueAsync() => new(Result<int, string>.Success(1));

            public static async Task Discarded()
            {
                await UnitAsync();
                await ValueAsync();
                await PairAsync();
                await UnitValueAsync();
                await ValueValueAsync();
                await PairValueAsync();
            }
        }
        """;

    private const string HandledSource = """
        using System.Threading.Tasks;
        using ResulTus;

        public static class Subject
        {
            public static Task<Result> UnitAsync() => Task.FromResult(Result.Success());

            public static Task<Result<int>> ValueAsync() => Task.FromResult(Result<int>.Success(1));

            public static Task<int> NumberAsync() => Task.FromResult(1);

            public static Task<string> TextAsync() => Task.FromResult("x");

            public static void Consume(Result<int> result)
            {
            }

            public static void ConsumeSync(Result<int> result)
            {
            }

            public static async Task Handled()
            {
                _ = await UnitAsync();
                var assigned = await ValueAsync();
                Consume(await ValueAsync());
                ConsumeSync(Result<int>.Success(1));
                await NumberAsync();
                await TextAsync();
                Result sync = Result.Success();
                _ = sync;

                if (assigned.IsSuccess)
                {
                    System.Console.WriteLine(assigned.ToString());
                }
            }

            public static async Task<Result<int>> Returned() => await ValueAsync();

            public static async Task<bool> Condition() => (await ValueAsync()).IsSuccess;
        }
        """;

    /// <summary>
    /// GIVEN a <c>Task</c> or <c>ValueTask</c> over each result tier, awaited as a whole statement
    /// WHEN <c>RESULT008</c> runs
    /// THEN it MUST report once per discarded result, because the failure is lost after the fact.
    /// </summary>
    [Fact]
    public async Task DiscardedAwaits_ShouldReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new DiscardedAwaitAnalyzer(),
            DiscardedSource,
            "RESULT008");

        diagnostics.Should().HaveCount(6);
        diagnostics[0].Location.GetLineSpan().StartLinePosition.Line.Should().Be(19);
        diagnostics[5].Location.GetLineSpan().StartLinePosition.Line.Should().Be(24);
    }

    /// <summary>
    /// GIVEN a discard assignment, an awaited value that is assigned, consumed, returned or tested,
    /// an await of a non-result type and a synchronous result
    /// WHEN <c>RESULT008</c> runs
    /// THEN it MUST report nothing, because every one of those observes the result or never had one.
    /// </summary>
    [Fact]
    public async Task ObservedOrForeignAwaits_ShouldNotReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new DiscardedAwaitAnalyzer(),
            HandledSource,
            "RESULT008");

        diagnostics.Should().BeEmpty();
    }
}
