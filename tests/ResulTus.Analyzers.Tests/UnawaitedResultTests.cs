// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for <c>RESULT007</c>: an asynchronous result produced as a statement and never awaited.
/// </summary>
public class UnawaitedResultTests
{
    private const string UnawaitedSource = """
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

            public static void Unawaited()
            {
                UnitAsync();
                ValueAsync();
                PairAsync();
                UnitValueAsync();
                ValueValueAsync();
                PairValueAsync();
            }
        }
        """;

    private const string HandledSource = """
        using System.Threading.Tasks;
        using ResulTus;

        public sealed class CustomTask<T>
        {
            public CustomTaskAwaiter<T> GetAwaiter() => new();
        }

        public sealed class CustomTaskAwaiter<T> : System.Runtime.CompilerServices.INotifyCompletion
        {
            public bool IsCompleted => true;

            public void OnCompleted(System.Action continuation) => continuation();

            public T GetResult() => default!;
        }

        public static class Subject
        {
            public static Task<Result> UnitAsync() => Task.FromResult(Result.Success());

            public static Task<int> NumberAsync() => Task.FromResult(1);

            public static Task<string> TextAsync() => Task.FromResult("x");

            public static CustomTask<Result> CustomAsync() => new();

            public static void Consume(Task<Result> task)
            {
            }

            public static void ConsumeSync(Result<int> result)
            {
            }

            public static async Task Handled(Result<int> result)
            {
                _ = UnitAsync();
                await UnitAsync();
                var assigned = await UnitAsync();
                Consume(UnitAsync());
                ConsumeSync(Result<int>.Success(1));
                _ = NumberAsync();
                _ = TextAsync();
                CustomAsync();
                Result sync = Result.Success();
                _ = sync;
                var (isSuccess, value, error) = result;
                _ = result.Match(inner => inner, failure => 0);

                if (isSuccess && assigned.IsSuccess)
                {
                    System.Console.WriteLine(value + error.Message);
                }
            }

            public static async Task<Result> Returned() => await UnitAsync();

            public static async Task<bool> Condition() => (await UnitAsync()).IsSuccess;
        }
        """;

    /// <summary>
    /// GIVEN a <c>Task</c> or <c>ValueTask</c> over each result tier, produced as a statement
    /// WHEN <c>RESULT007</c> runs
    /// THEN it MUST report once per dropped awaitable, because nothing awaits it and the outcome is
    /// never observed.
    /// </summary>
    [Fact]
    public async Task UnawaitedAsyncResults_ShouldReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new UnawaitedResultAnalyzer(),
            UnawaitedSource,
            "RESULT007");

        diagnostics.Should().HaveCount(6);
        diagnostics[0].Location.GetLineSpan().StartLinePosition.Line.Should().Be(19);
        diagnostics[5].Location.GetLineSpan().StartLinePosition.Line.Should().Be(24);
    }

    /// <summary>
    /// GIVEN a discard assignment, an awaited or assigned awaitable, a consumed result, an awaitable
    /// of a non-result type, a custom task-like over a result and a synchronous result
    /// WHEN <c>RESULT007</c> runs
    /// THEN it MUST report nothing, because each of those is a deliberate handling or not an
    /// awaitable of a result at all.
    /// </summary>
    [Fact]
    public async Task HandledOrForeignAwaitables_ShouldNotReport()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new UnawaitedResultAnalyzer(),
            HandledSource,
            "RESULT007");

        diagnostics.Should().BeEmpty();
    }
}
