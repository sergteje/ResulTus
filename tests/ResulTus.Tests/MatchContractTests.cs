// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Tests;

/// <summary>
/// Tests for the <c>Match</c> callback contract on every result tier: a null callback is a caller
/// error and must fail the same way regardless of which branch the result would take.
/// </summary>
public class MatchContractTests
{
    private static readonly Error Sample = Error.Unexpected("test", "boom");

    /// <summary>
    /// GIVEN a unit <see cref="Result"/>, successful or failed
    /// WHEN either callback is <c>null</c>
    /// THEN <see cref="Result.Match{TResult}"/> MUST throw <see cref="ArgumentNullException"/>
    /// before any branch runs.
    /// </summary>
    [Fact]
    public void UnitMatch_WithNullCallback_ShouldThrow_RegardlessOfState()
    {
        var success = Result.Success();
        var failure = Result.Failure(Sample);

        Action nullSuccessOnSuccess = () => success.Match<int>(null!, _ => 1);
        Action nullFailureOnSuccess = () => success.Match<int>(() => 1, null!);
        Action nullSuccessOnFailure = () => failure.Match<int>(null!, _ => 1);
        Action nullFailureOnFailure = () => failure.Match<int>(() => 1, null!);

        nullSuccessOnSuccess.Should().Throw<ArgumentNullException>();
        nullFailureOnSuccess.Should().Throw<ArgumentNullException>();
        nullSuccessOnFailure.Should().Throw<ArgumentNullException>();
        nullFailureOnFailure.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN a value-bearing <see cref="Result{T}"/>, successful or failed
    /// WHEN either callback is <c>null</c>
    /// THEN <see cref="Result{T}.Match{TResult}"/> MUST throw <see cref="ArgumentNullException"/>
    /// before any branch runs.
    /// </summary>
    [Fact]
    public void SingleGenericMatch_WithNullCallback_ShouldThrow_RegardlessOfState()
    {
        var success = Result<int>.Success(1);
        var failure = Result<int>.Failure(Sample);

        Action nullSuccessOnSuccess = () => success.Match(null!, _ => 1);
        Action nullFailureOnSuccess = () => success.Match(value => value, null!);
        Action nullSuccessOnFailure = () => failure.Match(null!, _ => 1);
        Action nullFailureOnFailure = () => failure.Match(value => value, null!);

        nullSuccessOnSuccess.Should().Throw<ArgumentNullException>();
        nullFailureOnSuccess.Should().Throw<ArgumentNullException>();
        nullSuccessOnFailure.Should().Throw<ArgumentNullException>();
        nullFailureOnFailure.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN a typed <see cref="Result{TSuccess, TError}"/>, successful or failed
    /// WHEN either callback is <c>null</c>
    /// THEN <see cref="Result{TSuccess, TError}.Match{TResult}"/> MUST throw
    /// <see cref="ArgumentNullException"/> before any branch runs.
    /// </summary>
    [Fact]
    public void TypedMatch_WithNullCallback_ShouldThrow_RegardlessOfState()
    {
        var success = Result<int, Error>.Success(1);
        var failure = Result<int, Error>.Failure(Sample);

        Action nullSuccessOnSuccess = () => success.Match(null!, _ => 1);
        Action nullFailureOnSuccess = () => success.Match(value => value, null!);
        Action nullSuccessOnFailure = () => failure.Match(null!, _ => 1);
        Action nullFailureOnFailure = () => failure.Match(value => value, null!);

        nullSuccessOnSuccess.Should().Throw<ArgumentNullException>();
        nullFailureOnSuccess.Should().Throw<ArgumentNullException>();
        nullSuccessOnFailure.Should().Throw<ArgumentNullException>();
        nullFailureOnFailure.Should().Throw<ArgumentNullException>();
    }
}
