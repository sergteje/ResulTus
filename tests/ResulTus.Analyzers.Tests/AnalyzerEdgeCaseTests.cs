// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Edge cases that the happy-path tests of each rule do not reach: forms the analyzer must stay
/// silent on, and code that does not compile at all.
/// </summary>
public class AnalyzerEdgeCaseTests
{
    /// <summary>
    /// GIVEN a <c>Bind</c> whose argument is a method group or a conditional expression
    /// WHEN <c>RESULT004</c> runs
    /// THEN it MUST report nothing, because neither wraps a single success factory it can see.
    /// </summary>
    [Fact]
    public async Task RedundantBind_ShouldNotReportWhenTheLambdaCannotBeRead()
    {
        const string Source = """
            using System;
            using ResulTus;
            using ResulTus.Extensions;

            public static class Subject
            {
                public static Result<string> ViaMethodGroup(Result<int> result)
                    => result.Bind(Compute);

                public static Result<string> ViaConditional(Result<int> result)
                    => result.Bind(value => value > 0
                        ? Result<string>.Success(value.ToString())
                        : Result<string>.Success("none"));

                private static Result<string> Compute(int value) => Result<string>.Success(value.ToString());
            }
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new RedundantBindAnalyzer(),
            Source,
            "RESULT004");

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN an <c>Ensure</c> whose predicate is a method group, a <c>default</c> literal, or a call
    /// that is not an <c>Ensure</c> at all
    /// WHEN <c>RESULT006</c> runs
    /// THEN it MUST report nothing, because none of them is a constant guard it can see.
    /// </summary>
    [Fact]
    public async Task ConstantEnsure_ShouldNotReportWhenThePredicateCannotBeRead()
    {
        const string Source = """
            using System;
            using ResulTus;
            using ResulTus.Extensions;

            public static class Subject
            {
                public static Result<int> ViaMethodGroup(Result<int> result)
                    => result.Ensure(IsPositive, Error.Failure("negative"));

                public static Result<int> ViaDefault(Result<int> result)
                    => result.Ensure(_ => default, Error.Failure("never"));

                public static Result<int> ViaMap(Result<int> result)
                    => result.Map(value => value + 1);

                public static int ViaAnotherTwoArgumentCall(int value)
                    => Math.Max(value, 0);

                public static int ViaAnotherMethodCalledEnsure(int value)
                    => Ensure(value, 0);

                private static bool IsPositive(int value) => value > 0;

                private static int Ensure(int left, int right) => left + right;
            }
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new ConstantEnsureAnalyzer(),
            Source,
            "RESULT006");

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN an <c>Ensure</c> written with a block body that returns a constant
    /// WHEN <c>RESULT006</c> runs
    /// THEN it MUST still report, because the block says exactly what the expression form says.
    /// </summary>
    [Fact]
    public async Task ConstantEnsure_ShouldReadABlockBody()
    {
        const string Source = """
            using System;
            using ResulTus;
            using ResulTus.Extensions;

            public static class Subject
            {
                public static Result<int> Block(Result<int> result)
                    => result.Ensure(value => { return true; }, Error.Failure("never"));
            }
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new ConstantEnsureAnalyzer(),
            Source,
            "RESULT006");

        diagnostics.Should().ContainSingle();
    }

    /// <summary>
    /// GIVEN a <c>ToResult</c> call written in static form over a result
    /// WHEN <c>RESULT005</c> runs
    /// THEN it MUST report, because the nesting does not depend on the call syntax.
    /// </summary>
    [Fact]
    public async Task NestedResult_ShouldReportTheStaticForm()
    {
        const string Source = """
            using System;
            using ResulTus;
            using ResulTus.Extensions;

            public static class Subject
            {
                public static Result<Result<int>> Nested(Result<int> result)
                    => ResultConversions.ToResult(result);
            }
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new NestedResultAnalyzer(),
            Source,
            "RESULT005");

        diagnostics.Should().ContainSingle();
    }

    /// <summary>
    /// GIVEN the other conversions the library exposes
    /// WHEN <c>RESULT005</c> runs
    /// THEN it MUST report nothing, because only <c>ToResult</c> can nest a result.
    /// </summary>
    [Fact]
    public async Task NestedResult_ShouldIgnoreTheOtherConversions()
    {
        const string Source = """
            using System;
            using ResulTus;
            using ResulTus.Extensions;

            public static class Subject
            {
                public static int? Unwrapped(Result<int> result)
                    => result.ToNullable();

                public static Exception Thrown(Error error)
                    => error.ToException();
            }
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new NestedResultAnalyzer(),
            Source,
            "RESULT005");

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a default read from a type that is not a result
    /// WHEN <c>RESULT002</c> runs
    /// THEN it MUST report nothing, because the rule is about results, not about every default.
    /// </summary>
    [Fact]
    public async Task UnguardedOutcome_ShouldIgnoreTheDefaultOfOtherTypes()
    {
        const string Source = """
            using System;
            using ResulTus;

            public static class Subject
            {
                public static int FromNullable(int? value)
                    => value.GetValueOrDefault();
            }
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new UnguardedOutcomeAnalyzer(),
            Source,
            "RESULT002");

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a result copied into a second variable and then read as a default
    /// WHEN <c>RESULT002</c> runs
    /// THEN it MUST report once, because copying the result is not checking its outcome.
    /// </summary>
    [Fact]
    public async Task UnguardedOutcome_ShouldNotTreatACopyAsAGuard()
    {
        const string Source = """
            using System;
            using ResulTus;

            public static class Subject
            {
                public static int Copied(Result<int> result)
                {
                    var other = result;
                    other = result;
                    return other.GetValueOrDefault();
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new UnguardedOutcomeAnalyzer(),
            Source,
            "RESULT002");

        diagnostics.Should().ContainSingle();
    }

    /// <summary>
    /// GIVEN a <c>Bind</c> written in static form whose lambda only wraps the value in a success
    /// WHEN <c>RESULT004</c> runs
    /// THEN it MUST still report, because the call syntax does not change what the lambda does.
    /// </summary>
    [Fact]
    public async Task RedundantBind_ShouldReportTheStaticForm()
    {
        const string Source = """
            using System;
            using ResulTus;
            using ResulTus.Extensions;

            public static class Subject
            {
                public static Result<string> Static(Result<int> result)
                    => ResultExtensions.Bind(result, value => Result<string>.Success(value.ToString()));
            }
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new RedundantBindAnalyzer(),
            Source,
            "RESULT004");

        diagnostics.Should().ContainSingle();
    }

    /// <summary>
    /// GIVEN a <c>GetValueOrDefault</c> call in a top-level statement, which is a member of the
    /// compilation unit and belongs to no enclosing method
    /// WHEN <c>RESULT002</c> runs
    /// THEN it MUST report, because that statement is the whole scope and it checks nothing.
    /// </summary>
    [Fact]
    public async Task UnguardedOutcome_ShouldReportInTopLevelStatements()
    {
        const string Source = """
            using System;
            using ResulTus;

            Result<int> result = Result<int>.Success(1);
            int value = result.GetValueOrDefault();
            Console.WriteLine(value);
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new UnguardedOutcomeAnalyzer(),
            Source,
            "RESULT002",
            OutputKind.ConsoleApplication);

        diagnostics.Should().ContainSingle();
    }

    /// <summary>
    /// GIVEN a zero-argument call to a method on a result that is not <c>GetValueOrDefault</c>
    /// WHEN <c>RESULT002</c> runs
    /// THEN it MUST report nothing, because the rule is about one method, not about every call.
    /// </summary>
    [Fact]
    public async Task UnguardedOutcome_ShouldIgnoreOtherZeroArgumentCalls()
    {
        const string Source = """
            using System;
            using ResulTus;

            public static class Subject
            {
                public static string Named(Result<int> result)
                    => result.ToString() ?? string.Empty;
            }
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new UnguardedOutcomeAnalyzer(),
            Source,
            "RESULT002");

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN an unqualified <c>GetValueOrDefault</c> call from inside a result type, which has no
    /// receiver expression to compare a guard against
    /// WHEN <c>RESULT002</c> runs
    /// THEN it MUST report nothing, because the rule cannot name the receiver it would need to
    /// search for.
    /// </summary>
    /// <remarks>
    /// The source declares its own <c>ResulTus.Result&lt;T&gt;</c>, which shadows the referenced one.
    /// That is how an unqualified call inside a result type becomes reachable from a test: the
    /// library's own instance methods are written exactly this way.
    /// </remarks>
    [Fact]
    public async Task UnguardedOutcome_ShouldIgnoreCallsWithoutAReceiver()
    {
        const string Source = """
            namespace ResulTus
            {
                public readonly struct Result<T>
                {
                    public T Read() => GetValueOrDefault();

                    public T GetValueOrDefault() => default!;

                    public bool IsSuccess => true;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.AnalyzeRuleAsync(
            new UnguardedOutcomeAnalyzer(),
            Source,
            "RESULT002");

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN source that does not compile
    /// WHEN every rule runs over it
    /// THEN none MUST crash, because a consumer's editor runs them on code that is half-typed.
    /// </summary>
    [Fact]
    public async Task EveryRule_ShouldSurviveSourceThatDoesNotCompile()
    {
        const string Source = """
            using System;
            using ResulTus;
            using ResulTus.Extensions;

            public static class Subject
            {
                public static void Broken(Result<int> result)
                {
                    int notAMethod = 0;

                    result.UndefinedCall();
                    Unknown(1, 2, 3);
                    notAMethod();
                    notAMethod(1);
                    notAMethod(1, 2);
                }
            }
            """;

        DiagnosticAnalyzer[] analyzers =
        [
            new IgnoredResultAnalyzer(),
            new UnguardedOutcomeAnalyzer(),
            new DiscardedErrorAnalyzer(),
            new RedundantBindAnalyzer(),
            new NestedResultAnalyzer(),
            new ConstantEnsureAnalyzer(),
        ];

        foreach (var analyzer in analyzers)
        {
            var diagnostics = await AnalyzerHarness.AnalyzeBrokenSourceAsync(analyzer, Source);

            diagnostics.Should().NotContain(
                diagnostic => diagnostic.Id.StartsWith("AD", StringComparison.Ordinal),
                "an analyzer crash is reported as an AD diagnostic");
        }
    }
}
