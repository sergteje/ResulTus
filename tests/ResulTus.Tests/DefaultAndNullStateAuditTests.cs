// Copyright (c) ResulTus. All rights reserved.

using ResulTus;

namespace ResulTus.Tests;

/// <summary>
/// Quality-audit probes (S2/S3 of odd/tasks/quality-audit.md): the default struct
/// state and the null-success state must be coherent, documented contracts — not
/// accidents. A failing probe here is an audit finding.
/// </summary>
/// <remarks>
/// The null-success state is deliberate: <c>Success(T)</c> is one erased signature
/// shared by <c>T</c> and <c>T?</c>, so the runtime cannot reject null for the
/// non-nullable annotation without also rejecting the sanctioned
/// <c>Result&lt;string?&gt;.Success(null)</c> (pinned by the serialization and
/// Combine suites). These probes pin the state so future changes cannot silently
/// break either side of it.
/// </remarks>
public class DefaultAndNullStateAuditTests
{
    /// <summary>
    /// GIVEN <c>default(Result&lt;int&gt;)</c>
    /// THEN it MUST be a failure carrying <see cref="Error.None"/> — never a success.
    /// </summary>
    [Fact]
    public void DefaultValue_IsAFailureCarryingNone()
    {
        Result<int> result = default;

        result.IsSuccess.Should().BeFalse("the default byte flag is 0");
        result.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN <c>default(Result&lt;int&gt;)</c>
    /// THEN every accessor MUST behave like an ordinary failure: the error is
    /// <see cref="Error.None"/>, the value contract yields the fallback, and
    /// <see cref="Result{T}.GetValueOr(Func{Error, T})"/> receives that error.
    /// </summary>
    [Fact]
    public void DefaultValue_Accessors_BehaveLikeAnOrdinaryFailure()
    {
        Result<int> result = default;

        result.TryGetValue(out _, out var error).Should().BeFalse();
        error.Should().Be(Error.None, "the default Error compares equal to None by design");

        result.GetValueOrDefault(42).Should().Be(42);
        result.GetValueOrDefault().Should().Be(0);

        Error? received = null;
        var recovered = result.GetValueOr(e => { received = e; return -1; });
        recovered.Should().Be(-1);
        received.Should().Be(Error.None);
    }

    /// <summary>
    /// GIVEN <c>default(Result&lt;int&gt;)</c>
    /// THEN <see cref="Result{T}.Match{TResult}(Func{T, TResult}, Func{Error, TResult})"/>
    /// MUST route to the failure branch with <see cref="Error.None"/>.
    /// </summary>
    [Fact]
    public void DefaultValue_Match_RoutesToFailureWithNone()
    {
        Result<int> result = default;

        var routed = result.Match(value => $"success:{value}", error => $"failure:{error.Code}:{error.Type}");
        routed.Should().Be("failure::None");
    }

    /// <summary>
    /// GIVEN two default results
    /// THEN they MUST be equal, and a default MUST equal an explicit
    /// <c>Failure(Error.None)</c> — both are the same value: a failure with no error.
    /// </summary>
    [Fact]
    public void DefaultValue_EqualsAnExplicitFailureOfNone()
    {
        Result<int> first = default;
        Result<int> second = default;

        first.Should().Be(second);
        first.Should().Be(Result<int>.Failure(Error.None));
    }

    /// <summary>
    /// GIVEN <c>default(Result)</c> (the unit tier)
    /// THEN it MUST be a failure whose error is <see cref="Error.None"/>.
    /// </summary>
    [Fact]
    public void DefaultUnitResult_IsAFailureCarryingNone()
    {
        Result result = default;

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();

        var routed = result.Match(() => "success", error => $"failure:{error.Code}:{error.Type}");
        routed.Should().Be("failure::None");
    }

    /// <summary>
    /// GIVEN <c>default(Result&lt;string, string&gt;)</c> (the typed tier)
    /// THEN it MUST be a failure whose deconstructed members are both default.
    /// </summary>
    [Fact]
    public void DefaultTypedResult_IsAFailureWithDefaultMembers()
    {
        Result<string, string> result = default;

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();

        result.Deconstruct(out var isSuccess, out var value, out var error);
        isSuccess.Should().BeFalse();
        value.Should().BeNull();
        error.Should().BeNull();

        var routed = result.Match(v => $"success:{v}", e => $"failure:{e ?? "null"}");
        routed.Should().Be("failure:null");
    }

    /// <summary>
    /// GIVEN a failure result
    /// THEN the <c>out</c> value of <c>TryGetValue</c> MUST be <c>default(T)</c>
    /// even when the caller ignores the returned bool — a caller that reads the
    /// value without the bool at worst observes a default, never garbage.
    /// </summary>
    [Fact]
    public void Failure_TryGetValue_OutValueIsDefaultNotGarbage()
    {
        var result = Result<int>.Failure(Error.NotFound("E1", "gone"));
        var (isSuccess, value, error) = result;

        isSuccess.Should().BeFalse();
        value.Should().Be(0);
        error.Code.Should().Be("E1");
    }

    /// <summary>
    /// GIVEN <c>Result&lt;string?&gt;.Success(null)</c> — the sanctioned null success
    /// THEN the accessors MUST report it as a success carrying null, and
    /// <see cref="Result{T}.Match{TResult}(Func{T, TResult}, Func{Error, TResult})"/>
    /// MUST hand null to the success branch.
    /// </summary>
    [Fact]
    public void NullSuccess_IsARealSuccessCarryingNull()
    {
        Result<string?> result = Result<string?>.Success(null);

        result.IsSuccess.Should().BeTrue("null is a representable success for a nullable T");
        result.IsFailure.Should().BeFalse();

        var tryGetValue = result.TryGetValue(out var value, out var error);
        tryGetValue.Should().BeTrue();
        value.Should().BeNull();
        error.Should().Be(Error.None);

        result.GetValueOrDefault("fallback").Should().BeNull("the success value IS null");

        var routed = result.Match(v => $"success:{v ?? "null"}", _ => "failure");
        routed.Should().Be("success:null");
    }

    /// <summary>
    /// GIVEN the implicit conversion from a null reference
    /// THEN the runtime cannot distinguish it from <c>Success(null)</c> — the
    /// nullability annotation carries the intent, the erased signature carries
    /// the state. Pinned so a future guard cannot silently break the nullable case.
    /// </summary>
    [Fact]
    public void ImplicitConversionFromNullReference_ProducesTheSameNullSuccess()
    {
        string? nullValue = null;
        Result<string?> result = nullValue;

        result.IsSuccess.Should().BeTrue();
        result.TryGetValue(out var value).Should().BeTrue();
        value.Should().BeNull();
    }
}
