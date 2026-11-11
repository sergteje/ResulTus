// Copyright (c) ResulTus. All rights reserved.

using ResulTus;
using ResulTus.Extensions;

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Quality-audit probes (S4 of odd/tasks/quality-audit.md): the functional operators must
/// respect the composition laws — functor identity, functor composition, monad
/// associativity, short-circuiting, and idempotence of the state-preserving operators.
/// Equality on the result structs makes every law directly assertable. A failing probe
/// is an audit finding.
/// </summary>
public class CompositionLawAuditTests
{
    private static readonly Error Boom = Error.Failure("E-BOOM", "boom");

    /// <summary>
    /// GIVEN any result
    /// THEN <c>Map(identity)</c> MUST return the same value (functor identity),
    /// on success and on failure.
    /// </summary>
    [Fact]
    public void Map_Identity()
    {
        var success = Result<int>.Success(41);
        success.Map(Identity).Should().Be(success);

        var failure = Result<int>.Failure(Boom);
        failure.Map(Identity).Should().Be(failure);
    }

    /// <summary>
    /// GIVEN any result and two mappings
    /// THEN <c>Map(f).Map(g)</c> MUST equal <c>Map(g ∘ f)</c> (functor composition).
    /// </summary>
    [Fact]
    public void Map_Composition()
    {
        var success = Result<int>.Success(41);
        success.Map(Double).Map(ToString).Should().Be(success.Map(x => ToString(Double(x))));

        var failure = Result<int>.Failure(Boom);
        failure.Map(Double).Map(ToString).Should().Be(failure.Map(x => ToString(Double(x))));
    }

    /// <summary>
    /// GIVEN any result and two binders
    /// THEN <c>Bind(f).Bind(g)</c> MUST equal <c>Bind(x => f(x).Bind(g))</c>
    /// (monad associativity) — and the binders MUST never run on a failure.
    /// </summary>
    [Fact]
    public void Bind_Associativity_And_ShortCircuit()
    {
        var success = Result<int>.Success(41);
        success.Bind(BindDouble).Bind(BindToString)
            .Should().Be(success.Bind(x => BindDouble(x).Bind(BindToString)));

        var binderRan = false;
        var failure = Result<int>.Failure(Boom);
        var chained = failure.Bind(_ => { binderRan = true; return Result<string>.Success("ran"); });
        chained.IsSuccess.Should().BeFalse("the failure short-circuits the chain");
        chained.TryGetValue(out _, out var chainedError).Should().BeFalse();
        chainedError.Should().Be(Boom, "the original error is preserved through the chain");
        binderRan.Should().BeFalse("a failure short-circuits the binder");
    }

    /// <summary>
    /// GIVEN a binder chain where the first binder fails
    /// THEN the failure MUST win over the second binder's would-be success —
    /// the chain value is the FIRST failure, and the second binder never runs.
    /// </summary>
    [Fact]
    public void Bind_FirstFailureWins()
    {
        var secondRan = false;
        var chained = Result<int>.Success(41)
            .Bind(_ => Result<string>.Failure(Boom))
            .Bind(_ => { secondRan = true; return Result<string>.Success("never"); });

        secondRan.Should().BeFalse();
        chained.Should().Be(Result<string>.Failure(Boom));
    }

    /// <summary>
    /// GIVEN any result
    /// THEN <c>MapError(identity)</c> MUST return the same value on both states.
    /// </summary>
    [Fact]
    public void MapError_Identity()
    {
        var success = Result<int>.Success(41);
        success.MapError(Identity).Should().Be(success);

        var failure = Result<int>.Failure(Boom);
        failure.MapError(Identity).Should().Be(failure);
    }

    /// <summary>
    /// GIVEN a success
    /// THEN every state-preserving operator MUST leave it unchanged AND skip the
    /// failure branch: <c>Recover</c> must not invoke its fallback,
    /// <c>OnFailure</c> must not run.
    /// </summary>
    [Fact]
    public void Success_IsUntouchedByRecoveryAndFailureBranches()
    {
        var success = Result<int>.Success(41);
        var recoveryRan = false;
        var onFailureRan = false;

        success.Recover(_ => { recoveryRan = true; return -1; }).Should().Be(success);
        success.Recover(_ => { recoveryRan = true; return Result<int>.Success(-1); }).Should().Be(success);
        success.OnFailure(_ => onFailureRan = true).Should().Be(success);

        recoveryRan.Should().BeFalse();
        onFailureRan.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a failure
    /// THEN <c>Recover</c> MUST turn it into the recovered success, and a second
    /// <c>Recover</c> MUST be the identity on that success (recovery idempotence
    /// through the state change).
    /// </summary>
    [Fact]
    public void Recover_TurnsFailureIntoSuccess_AndThenIsIdempotent()
    {
        var failure = Result<int>.Failure(Boom);
        var recovered = failure.Recover(_ => 7);

        recovered.Should().Be(Result<int>.Success(7));
        recovered.Recover(_ => -1).Should().Be(recovered);
    }

    /// <summary>
    /// GIVEN a failure
    /// THEN <c>Recover</c> with a result-producing fallback MUST propagate that
    /// fallback's own failure (recovery is not forced to succeed).
    /// </summary>
    [Fact]
    public void Recover_PropagatesTheFallbacksOwnFailure()
    {
        var other = Error.NotFound("E-OTHER");
        var recovered = Result<int>.Failure(Boom).Recover(_ => Result<int>.Failure(other));

        recovered.Should().Be(Result<int>.Failure(other));
    }

    /// <summary>
    /// GIVEN any result
    /// THEN <c>Ensure</c> MUST be idempotent for a predicate that holds, MUST not
    /// touch a failure (its predicate never runs), and MUST replace a failing
    /// predicate's success with the built error exactly once.
    /// </summary>
    [Fact]
    public void Ensure_Idempotence_And_FailureShortCircuit()
    {
        var failure = Result<int>.Failure(Boom);
        var predicateRan = false;
        failure.Ensure(_ => { predicateRan = true; return true; }, () => Error.None)
            .Should().Be(failure);
        predicateRan.Should().BeFalse("Ensure never evaluates the predicate on a failure");

        var success = Result<int>.Success(41);
        success.Ensure(_ => true, () => Error.None)
            .Ensure(_ => true, () => Error.None)
            .Should().Be(success);

        var enforced = success.Ensure(_ => false, () => Error.NotFound("E-ENS"));
        enforced.Should().Be(Result<int>.Failure(Error.NotFound("E-ENS")));
    }

    /// <summary>
    /// GIVEN any result
    /// THEN <c>Tap</c>/<c>OnSuccess</c>/<c>OnFailure</c> MUST return the original
    /// value unchanged (side effects only) — and <c>Switch</c> MUST agree with the
    /// result state on which branch runs.
    /// </summary>
    [Fact]
    public void SideEffectOperators_ReturnTheOriginalValue()
    {
        var success = Result<int>.Success(41);
        var seen = new List<string>();

        success.Tap(v => seen.Add($"success:{v}")).Should().Be(success);
        success.OnSuccess(v => seen.Add("onsuccess")).Should().Be(success);
        success.OnFailure(_ => seen.Add("onfailure")).Should().Be(success);
        success.Switch(v => seen.Add($"switch:{v}"), _ => seen.Add("switch:failure"));

        seen.Should().BeEquivalentTo(["success:41", "onsuccess", "switch:41"], options => options.WithStrictOrdering());
    }

    /// <summary>
    /// GIVEN the typed tier
    /// THEN its <c>Map</c> MUST satisfy functor identity and composition, and its
    /// <c>Bind</c> MUST short-circuit a failure without running the binder.
    /// </summary>
    [Fact]
    public void TypedTier_RespectsTheSameLaws()
    {
        var success = Result<int, Error>.Success(41);
        success.Map(Identity).Should().Be(success);
        success.Map(Double).Map(ToString).Should().Be(success.Map(x => ToString(Double(x))));

        var binderRan = false;
        var failure = Result<int, Error>.Failure(Boom);
        var chained = failure.Bind(_ => { binderRan = true; return Result<string, Error>.Success("ran"); });
        chained.IsFailure.Should().BeTrue("the failure short-circuits the chain");
        chained.Deconstruct(out _, out _, out var chainedError);
        chainedError.Should().Be(Boom, "the original error is preserved through the chain");
        binderRan.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN the unit tier
    /// THEN its <c>Map</c> and <c>Bind</c> MUST short-circuit a failure, and a
    /// success MUST flow through untouched by <c>MapError</c>.
    /// </summary>
    [Fact]
    public void UnitTier_RespectsTheSameLaws()
    {
        var failure = Result.Failure(Boom);
        var ran = false;
        var mapped = failure.Map(() => { ran = true; return 1; });
        mapped.IsSuccess.Should().BeFalse("the failure short-circuits the map");
        mapped.TryGetValue(out _, out var mappedError).Should().BeFalse();
        mappedError.Should().Be(Boom, "the original error is preserved through the map");
        ran.Should().BeFalse();

        var success = Result.Success();
        success.MapError(Identity).Should().Be(success);
        success.Bind(() => Result<string>.Success("v")).Should().Be(Result<string>.Success("v"));
    }

    private static T Identity<T>(T value) => value;
    private static int Double(int value) => value * 2;
    private static string ToString(int value) => value.ToString();
    private static Result<int> BindDouble(int value) => Result<int>.Success(value * 2);
    private static Result<string> BindToString(int value) => Result<string>.Success(value.ToString());
}
