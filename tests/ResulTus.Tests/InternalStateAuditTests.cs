// Copyright (c) ResulTus. All rights reserved.

using ResulTus;

namespace ResulTus.Tests;

/// <summary>
/// Quality-audit probes (S1 of odd/tasks/quality-audit.md): the record semantics of the
/// three result tiers — equality, hashing, state isolation. A failing probe is an
/// audit finding.
/// </summary>
public class InternalStateAuditTests
{
    /// <summary>
    /// GIVEN a success and a failure whose carried fields would compare equal
    /// THEN the state flag MUST separate them — a success is never equal to a
    /// failure, whatever the fields contain.
    /// </summary>
    [Fact]
    public void SuccessIsNeverEqualToAFailure_EvenWithEqualPayloads()
    {
        Result<int>.Success(0).Should().NotBe(Result<int>.Failure(Error.None));
        Result.Success().Should().NotBe(default(Result));
        Result<string, string>.Success("x").Should().NotBe(Result<string, string>.Failure("x"));
    }

    /// <summary>
    /// GIVEN two results of the same state
    /// THEN equality MUST follow the carried payload only: same value, same
    /// error code/type, same state — equal; anything different — not equal.
    /// </summary>
    [Fact]
    public void EqualityFollowsThePayloadWithinTheSameState()
    {
        Result<int>.Success(41).Should().Be(Result<int>.Success(41));
        Result<int>.Success(41).Should().NotBe(Result<int>.Success(42));
        Result<int>.Failure(Error.Failure("E1")).Should().Be(Result<int>.Failure(Error.Failure("E1")));
        Result<int>.Failure(Error.Failure("E1")).Should().NotBe(Result<int>.Failure(Error.Failure("E2")));
        Result<int>.Failure(Error.Failure("E1")).Should().NotBe(Result<int>.Failure(Error.NotFound("E1")));
    }

    /// <summary>
    /// GIVEN equal results
    /// THEN their hash codes MUST be equal (the record hash contract), and two
    /// results differing only by state MUST be usable as distinct dictionary keys.
    /// </summary>
    [Fact]
    public void GetHashCodeMatchesEquality()
    {
        var success = Result<int>.Success(41);
        var sameSuccess = Result<int>.Success(41);
        var failure = Result<int>.Failure(Error.None);

        success.GetHashCode().Should().Be(sameSuccess.GetHashCode());
        new Dictionary<Result<int>, string> { [success] = "a", [failure] = "b" }
            .Should().ContainKey(sameSuccess);
    }

    /// <summary>
    /// GIVEN <c>Result&lt;Error&gt;</c> — an Error used as the success payload
    /// THEN the tier MUST keep the payload state and the error state distinct:
    /// <c>Success(e)</c> is not <c>Failure(e)</c>, and success preserves the
    /// carried error object as a plain value.
    /// </summary>
    [Fact]
    public void ResultOfError_KeepsPayloadAndErrorStatesDistinct()
    {
        var error = Error.Failure("E1", "boom");
        Result<Error> asPayload = Result<Error>.Success(error);
        Result<Error> asFailure = Result<Error>.Failure(error);

        asPayload.Should().NotBe(asFailure);
        asPayload.TryGetValue(out var value).Should().BeTrue();
        value.Should().Be(error);

        asPayload.Match(v => v.Code, _ => "failure").Should().Be("E1");
        asFailure.Match(_ => "success", e => e.Code).Should().Be("E1");
    }

    /// <summary>
    /// GIVEN the unit tier's two constructors
    /// THEN <c>Result.Success()</c> MUST differ from <c>default</c> and from
    /// <c>Failure(Error.None)</c> only by the state flag — and the two failures
    /// MUST be equal, mirroring the generic tier.
    /// </summary>
    [Fact]
    public void UnitTier_MirrorsTheGenericStateContract()
    {
        Result.Success().Should().NotBe(default(Result));
        Result.Failure(Error.None).Should().Be(default(Result));
        Result.Failure(Error.Failure("E1")).Should().NotBe(default(Result));
    }

    /// <summary>
    /// GIVEN a success result
    /// THEN the error out member of <c>TryGetValue</c>/<c>Deconstruct</c> MUST be
    /// <see cref="Error.None"/> — a success never reports a stale error, and the
    /// carried value is the one given.
    /// </summary>
    [Fact]
    public void Success_NeverReportsAStaleError()
    {
        var (isSuccess, value, error) = Result<int>.Success(7);

        isSuccess.Should().BeTrue();
        value.Should().Be(7);
        error.Should().Be(Error.None);
    }

    /// <summary>
    /// GIVEN the same error carried by two failures of the typed tier
    /// THEN typed-tier equality MUST follow the error value's own equality
    /// contract (value semantics, not reference identity).
    /// </summary>
    [Fact]
    public void TypedTier_ErrorComparisonUsesValueSemantics()
    {
        var first = Result<string, Error>.Failure(Error.Failure("E1"));
        var second = Result<string, Error>.Failure(Error.Failure("E1"));

        first.Should().Be(second, "Error is a record struct with value equality");
        first.Should().NotBe(Result<string, Error>.Success("E1-carrier"));
    }
}
