// Copyright (c) ResulTus. All rights reserved.

using System.Threading.Tasks;
using Xunit;

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// False-positive battery for every rule: code that a name-based search would
/// mistake for result misuse, but that the analyzers must NOT flag, because
/// their gates bind to the real <c>ResulTus</c> symbols and semantics.
/// </summary>
/// <remarks>
/// Each case compiles (the harness asserts that) and runs the six rules, and
/// every rule must stay silent. A lookalike type in a user's own namespace, a
/// third-party <c>Bind</c>, a nullable value type's <c>GetValueOrDefault</c>, a
/// deconstruction of an ordinary tuple, an identity bind through someone else's
/// extension, a nested lookalike, and a predicate that reads real state all sit
/// inside this file. If a rule starts firing on any of these, the analyzer has
/// grown an unjustified complaint and the suite fails.
/// </remarks>
public class AnalyzerFalsePositiveTests
{
    /// <summary>
    /// RESULT001 must not flag a user's own type named <c>Result</c> that lives
    /// in another namespace and is freely discarded.
    /// </summary>
    [Fact]
    public async Task OtherNamespaceResultType_Discarded_ShouldStaySilent()
    {
        const string source = """
            using System;

            namespace Acme.Internal
            {
                public sealed record Result
                {
                    private Result() { }
                    public static Result Success() => new();
                }
            }

            public static class Subject
            {
                public static Acme.Internal.Result DoWork() => Acme.Internal.Result.Success();

                public static void Ignored()
                {
                    DoWork();
                    _ = DoWork();
                }
            }
            """;

        await AssertAllRulesSilent(source);
    }

    /// <summary>
    /// RESULT002 must not flag <see cref="System.Nullable{T}"/>'s own
    /// <c>GetValueOrDefault</c>, which no result ever needs guarding.
    /// </summary>
    [Fact]
    public async Task NullableGetValueOrDefault_ShouldStaySilent()
    {
        const string source = """
            using System;

            public static class Subject
            {
                public static int Read(int? maybe)
                {
                    return maybe.GetValueOrDefault();
                }
            }
            """;

        await AssertAllRulesSilent(source);
    }

    /// <summary>
    /// RESULT003 must not flag ordinary tuple deconstruction that discards a
    /// component, including a leading `_`.
    /// </summary>
    [Fact]
    public async Task OrdinaryTupleDiscard_ShouldStaySilent()
    {
        const string source = """
            using System;

            public static class Subject
            {
                public static (bool ok, int payload) Pair(int value) => (value > 0, value);

                public static void Handle()
                {
                    var (ok, _) = Pair(3);
                    Console.WriteLine(ok);
                }
            }
            """;

        await AssertAllRulesSilent(source);
    }

    /// <summary>
    /// RESULT004 must not flag an identity bind through a third-party extension
    /// on the consumer's own type, written exactly like the complaint shape.
    /// </summary>
    [Fact]
    public async Task ThirdPartyIdentityBind_ShouldStaySilent()
    {
        const string source = """
            using System;

            public sealed class MyResult<TValue>
            {
                public TValue Value { get; }
                public MyResult(TValue value) => Value = value;
            }

            public static class MyExtensions
            {
                public static MyResult<TOut> Bind<TValue, TOut>(
                    this MyResult<TValue> source,
                    Func<TValue, TOut> bind) => new(bind(source.Value));
            }

            public static class Subject
            {
                public static void Handle(MyResult<int> source)
                {
                    var same = source.Bind(v => v);
                    Console.WriteLine(same.Value);
                }
            }
            """;

        await AssertAllRulesSilent(source);
    }

    /// <summary>
    /// RESULT005 must not flag a nesting of the consumer's own result-shaped
    /// generics, which is not a ResulTus result.
    /// </summary>
    [Fact]
    public async Task OtherNamespaceNestedResult_ShouldStaySilent()
    {
        const string source = """
            using System;

            namespace Acme.Internal
            {
                public sealed record Result
                {
                    private Result() { }
                    public static Result Success() => new();
                }

                public sealed record Result<T>
                {
                    public Result(T value) { }
                }

                public sealed record Tiny
                {
                    public Result? Inner { get; init; }
                }
            }

            public static class Subject
            {
                public static Acme.Internal.Result<Acme.Internal.Tiny> WrapTiny() =>
                    new(new Acme.Internal.Tiny());
            }
            """;

        await AssertAllRulesSilent(source);
    }

    /// <summary>
    /// RESULT006 must not flag an <c>Ensure</c>-shaped predicate that reads real
    /// state, even when the state's value is always true in practice.
    /// </summary>
    [Fact]
    public async Task StatefulPredicateEnsure_ShouldStaySilent()
    {
        const string source = """
            using System;
            using ResulTus;
            using ResulTus.Extensions;

            public sealed class Subject
            {
                public bool FeatureFlag { get; set; } = true;

                public Result<int> Guard(int value)
                {
                    return Result<int>.Success(value).Ensure(_ => FeatureFlag, Error.Conflict("disabled"));
                }
            }
            """;

        await AssertAllRulesSilent(source);
    }

    private static async Task AssertAllRulesSilent(string source)
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(
            new IgnoredResultAnalyzer(),
            source);
        diagnostics = diagnostics.AddRange(
            await AnalyzerHarness.AnalyzeRuleAsync(new UnguardedOutcomeAnalyzer(), source, "RESULT002"));

        var all = ImmutableCollectionsMarshalOr(diagnostics);

        // RESULT003 through RESULT006 run their own analyzers over the same source.
        var discarded = await AnalyzerHarness.AnalyzeRuleAsync(new DiscardedErrorAnalyzer(), source, "RESULT003");
        var redundant = await AnalyzerHarness.AnalyzeRuleAsync(new RedundantBindAnalyzer(), source, "RESULT004");
        var nested = await AnalyzerHarness.AnalyzeRuleAsync(new NestedResultAnalyzer(), source, "RESULT005");
        var constant = await AnalyzerHarness.AnalyzeRuleAsync(new ConstantEnsureAnalyzer(), source, "RESULT006");

        all = all.AddRange(discarded).AddRange(redundant).AddRange(nested).AddRange(constant);

        all.Should().BeEmpty(
            "every analyzer binds to ResulTus symbols; a lookalike must never reach a complaint");
    }

    private static System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> ImmutableCollectionsMarshalOr(
        System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
        => diagnostics;
}
