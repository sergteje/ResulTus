// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Collections.Tests;

/// <summary>
/// Tests for the asynchronous collection operations.
/// </summary>
public class AsyncCollectionTests
{
    /// <summary>
    /// GIVEN three tasks that all succeed
    /// WHEN <c>WhenAllAsync()</c>
    /// THEN the result MUST hold the values in enumeration order.
    /// </summary>
    [Fact]
    public async Task WhenAllAsync_AllSuccess_ShouldCollectValues()
    {
        var tasks = new[]
        {
            Task.FromResult(Result<int>.Success(1)),
            Task.FromResult(Result<int>.Success(2)),
            Task.FromResult(Result<int>.Success(3)),
        };

        var combined = await tasks.WhenAllAsync();

        combined.IsSuccess.Should().BeTrue();
        combined.GetValueOrDefault().Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// GIVEN tasks where one fails
    /// WHEN <c>WhenAllAsync()</c>
    /// THEN the first failure in enumeration order MUST surface.
    /// </summary>
    [Fact]
    public async Task WhenAllAsync_OnFailure_ShouldReturnFirstFailure()
    {
        var first = Error.NotFound("first");
        var tasks = new[]
        {
            Task.FromResult(Result<int>.Success(1)),
            Task.FromResult(Result<int>.Failure(first)),
            Task.FromResult(Result<int>.Failure(Error.Unexpected("second"))),
        };

        var combined = await tasks.WhenAllAsync();

        combined.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(first);
    }

    /// <summary>
    /// GIVEN an empty task sequence
    /// WHEN <c>WhenAllAsync()</c>
    /// THEN the result MUST be an empty success.
    /// </summary>
    [Fact]
    public async Task WhenAllAsync_Empty_ShouldBeEmptySuccess()
    {
        var combined = await Array.Empty<Task<Result<int>>>().WhenAllAsync();

        combined.IsSuccess.Should().BeTrue();
        combined.GetValueOrDefault().Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN tasks where one fails
    /// WHEN the unit <c>WhenAllAsync()</c> runs
    /// THEN the first failure MUST surface.
    /// </summary>
    [Fact]
    public async Task UnitWhenAllAsync_OnFailure_ShouldReturnFirstFailure()
    {
        var error = Error.Conflict("dup");
        var tasks = new[]
        {
            Task.FromResult(Result.Success()),
            Task.FromResult(Result.Failure(error)),
        };

        var combined = await tasks.WhenAllAsync();

        combined.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN one already completed task and one that never completes
    /// WHEN <c>WhenAnyAsync()</c>
    /// THEN the completed result MUST be returned.
    /// </summary>
    [Fact]
    public async Task WhenAnyAsync_ShouldReturnFirstCompleted()
    {
        var never = new TaskCompletionSource<Result<int>>();
        var tasks = new[]
        {
            Task.FromResult(Result<int>.Success(7)),
            never.Task,
        };

        var first = await tasks.WhenAnyAsync();

        first.IsSuccess.Should().BeTrue();
        first.GetValueOrDefault().Should().Be(7);
    }

    /// <summary>
    /// GIVEN an empty task sequence
    /// WHEN <c>WhenAnyAsync()</c>
    /// THEN it MUST throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public async Task WhenAnyAsync_Empty_ShouldThrow()
    {
        var act = async () => await Array.Empty<Task<Result<int>>>().WhenAnyAsync();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// GIVEN an already cancelled token
    /// WHEN either async collection method is called
    /// THEN it MUST throw <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public async Task AsyncCollectionMethods_WithCancelledToken_ShouldThrow()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var tasks = new[] { Task.FromResult(Result<int>.Success(1)) };

        var whenAll = async () => await tasks.WhenAllAsync(cts.Token);
        var whenAny = async () => await tasks.WhenAnyAsync(cts.Token);

        await whenAll.Should().ThrowAsync<OperationCanceledException>();
        await whenAny.Should().ThrowAsync<OperationCanceledException>();
    }
}
