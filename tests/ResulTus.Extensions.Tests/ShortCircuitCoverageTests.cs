// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Coverage for the short-circuit paths that the per-method tests do not reach.
/// </summary>
public class ShortCircuitCoverageTests
{
    /// <summary>
    /// GIVEN a failed <c>Task&lt;Result&lt;int&gt;&gt;</c>
    /// WHEN <c>EnsureAsync</c> is called
    /// THEN the predicate MUST NOT run and the original error MUST survive.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_OnFailure_ShouldNotRunPredicate()
    {
        var error = Error.Conflict("dup");
        var source = Task.FromResult(Result<int>.Failure(error));
        var invoked = 0;

        var ensured = await source.EnsureAsync((_, _) =>
        {
            invoked++;
            return ValueTask.FromResult(true);
        }, Error.Validation("unused"));

        invoked.Should().Be(0);
        ensured.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a failed <c>Task&lt;Result&gt;</c>
    /// WHEN <c>EnsureAsync</c> is called
    /// THEN the predicate MUST NOT run.
    /// </summary>
    [Fact]
    public async Task UnitEnsureAsync_OnFailure_ShouldNotRunPredicate()
    {
        var error = Error.Conflict("dup");
        var source = Task.FromResult(Result.Failure(error));
        var invoked = 0;

        var ensured = await source.EnsureAsync(_ =>
        {
            invoked++;
            return ValueTask.FromResult(true);
        }, Error.Validation("unused"));

        invoked.Should().Be(0);
        ensured.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a failed <c>Task&lt;Result&gt;</c>
    /// WHEN <c>BindAsync</c> is called
    /// THEN the binder MUST NOT run.
    /// </summary>
    [Fact]
    public async Task UnitBindAsync_OnFailure_ShouldShortCircuit()
    {
        var error = Error.Timeout("slow");
        var source = Task.FromResult(Result.Failure(error));
        var invoked = 0;

        var bound = await source.BindAsync(_ =>
        {
            invoked++;
            return ValueTask.FromResult(Result<string>.Success("never"));
        });

        invoked.Should().Be(0);
        bound.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a successful <c>Task&lt;Result&gt;</c>
    /// WHEN <c>RecoverAsync</c> is called
    /// THEN the recovery MUST NOT run.
    /// </summary>
    [Fact]
    public async Task UnitRecoverAsync_OnSuccess_ShouldNotRun()
    {
        var source = Task.FromResult(Result.Success());
        var invoked = 0;

        var recovered = await source.RecoverAsync((_, _) =>
        {
            invoked++;
            return ValueTask.FromResult(Result.Failure(Error.Unexpected("never")));
        });

        invoked.Should().Be(0);
        recovered.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result"/>
    /// WHEN <c>Ensure</c> is called
    /// THEN the predicate MUST NOT run.
    /// </summary>
    [Fact]
    public void UnitEnsure_OnFailure_ShouldNotRunPredicate()
    {
        var error = Error.Conflict("dup");
        var result = Result.Failure(error);
        var invoked = 0;

        var ensured = result.Ensure(() =>
        {
            invoked++;
            return true;
        }, Error.Validation("unused"));

        invoked.Should().Be(0);
        ensured.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result"/>
    /// WHEN <c>MapError</c> is called
    /// THEN the mapper MUST NOT run and the success MUST survive.
    /// </summary>
    [Fact]
    public void UnitMapError_OnSuccess_ShouldKeepResult()
    {
        var result = Result.Success();
        var invoked = 0;

        var mapped = result.MapError(e =>
        {
            invoked++;
            return e;
        });

        invoked.Should().Be(0);
        mapped.IsSuccess.Should().BeTrue();
    }
}
