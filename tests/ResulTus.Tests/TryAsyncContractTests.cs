// Copyright (c) ResulTus. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace ResulTus.Tests;

/// <summary>
/// Tests for the asynchronous capture helpers: a non-cancellation exception becomes a failure that
/// keeps the exception's type, message and <see cref="ErrorType.Unexpected"/> kind, while a
/// cancellation stays control flow and is rethrown instead of being turned into an error.
/// </summary>
public class TryAsyncContractTests
{
    /// <summary>
    /// GIVEN a delegate that completes successfully
    /// WHEN <see cref="Result.TryAsync{T}(System.Func{System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}, System.Threading.CancellationToken)"/>
    /// runs
    /// THEN the result MUST be a success carrying the produced value.
    /// </summary>
    [Fact]
    public async Task TryAsync_OnSuccess_ShouldCarryTheValue()
    {
        var result = await Result.TryAsync(
            _ => Task.FromResult(42),
            TestContext.Current.CancellationToken);

        result.TryGetValue(out var value, out _).Should().BeTrue();
        value.Should().Be(42);
    }

    /// <summary>
    /// GIVEN a delegate that throws a non-cancellation exception
    /// WHEN <c>TryAsync</c> runs
    /// THEN the exception MUST become a failure whose error names the exception type and keeps its
    /// message, instead of escaping to the caller.
    /// </summary>
    [Fact]
    public async Task TryAsync_WhenTheDelegateThrows_ShouldCaptureTheException()
    {
        var result = await Result.TryAsync<int>(
            _ => throw new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken);

        result.TryGetValue(out _, out var error).Should().BeFalse();
        error.Code.Should().Be(typeof(InvalidOperationException).FullName);
        error.Message.Should().Be("boom");
        error.Type.Should().Be(ErrorType.Unexpected);
    }

    /// <summary>
    /// GIVEN a delegate that throws <see cref="OperationCanceledException"/>
    /// WHEN <c>TryAsync</c> runs
    /// THEN the exception MUST be rethrown, because cancellation is control flow and never a
    /// captured failure.
    /// </summary>
    [Fact]
    public async Task TryAsync_WhenTheDelegateIsCancelled_ShouldRethrow()
    {
        var act = () => Result.TryAsync<int>(
            _ => throw new OperationCanceledException(),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// GIVEN an already-cancelled token
    /// WHEN <c>TryAsync</c> runs
    /// THEN it MUST throw before the delegate is invoked, so cancelled work never starts.
    /// </summary>
    [Fact]
    public async Task TryAsync_WithCancelledToken_ShouldThrowWithoutRunningTheDelegate()
    {
        var cancellationToken = new CancellationToken(canceled: true);
        var invoked = 0;

        var act = () => Result.TryAsync(
            _ =>
            {
                invoked++;
                return Task.FromResult(42);
            },
            cancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
        invoked.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a <c>null</c> delegate
    /// WHEN <c>TryAsync</c> runs
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task TryAsync_WithNullDelegate_ShouldThrow()
    {
        var act = () => Result.TryAsync<int>(null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN a unit delegate that completes successfully
    /// WHEN the unit <c>TryAsync</c> runs
    /// THEN the result MUST be a success.
    /// </summary>
    [Fact]
    public async Task TryAsync_Unit_OnSuccess_ShouldSucceed()
    {
        var result = await Result.TryAsync(
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a unit delegate that throws
    /// WHEN the unit <c>TryAsync</c> runs
    /// THEN the failure MUST carry the exception-derived error.
    /// </summary>
    [Fact]
    public async Task TryAsync_Unit_WhenTheDelegateThrows_ShouldCaptureTheException()
    {
        var result = await Result.TryAsync(
            _ => Task.FromException(new InvalidOperationException("boom")),
            TestContext.Current.CancellationToken);

        result.TryGetError(out var error).Should().BeTrue();
        error.Message.Should().Be("boom");
        error.Type.Should().Be(ErrorType.Unexpected);
    }

    /// <summary>
    /// GIVEN a <see cref="ValueTask{TResult}"/> delegate that completes successfully
    /// WHEN <see cref="Result.TryValueTaskAsync{T}(System.Func{System.Threading.CancellationToken, System.Threading.Tasks.ValueTask{T}}, System.Threading.CancellationToken)"/>
    /// runs
    /// THEN the result MUST be a success carrying the produced value.
    /// </summary>
    [Fact]
    public async Task TryValueTaskAsync_OnSuccess_ShouldCarryTheValue()
    {
        var result = await Result.TryValueTaskAsync(
            _ => ValueTask.FromResult(42),
            TestContext.Current.CancellationToken);

        result.TryGetValue(out var value, out _).Should().BeTrue();
        value.Should().Be(42);
    }

    /// <summary>
    /// GIVEN a <see cref="ValueTask{TResult}"/> delegate that throws
    /// WHEN <c>TryValueTaskAsync</c> runs
    /// THEN the exception MUST become a failure with the same projection the task tier uses.
    /// </summary>
    [Fact]
    public async Task TryValueTaskAsync_WhenTheDelegateThrows_ShouldCaptureTheException()
    {
        var result = await Result.TryValueTaskAsync<int>(
            _ => throw new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken);

        result.TryGetValue(out _, out var error).Should().BeFalse();
        error.Code.Should().Be(typeof(InvalidOperationException).FullName);
        error.Message.Should().Be("boom");
        error.Type.Should().Be(ErrorType.Unexpected);
    }

    /// <summary>
    /// GIVEN a <see cref="ValueTask{TResult}"/> delegate that throws
    /// <see cref="OperationCanceledException"/>
    /// WHEN <c>TryValueTaskAsync</c> runs
    /// THEN the exception MUST be rethrown.
    /// </summary>
    [Fact]
    public async Task TryValueTaskAsync_WhenTheDelegateIsCancelled_ShouldRethrow()
    {
        var act = () => Result.TryValueTaskAsync<int>(
            _ => throw new OperationCanceledException(),
            TestContext.Current.CancellationToken).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// GIVEN an already-cancelled token
    /// WHEN <c>TryValueTaskAsync</c> runs
    /// THEN it MUST throw before the delegate is invoked.
    /// </summary>
    [Fact]
    public async Task TryValueTaskAsync_WithCancelledToken_ShouldThrowWithoutRunningTheDelegate()
    {
        var cancellationToken = new CancellationToken(canceled: true);
        var invoked = 0;

        var act = () => Result.TryValueTaskAsync(
            _ =>
            {
                invoked++;
                return ValueTask.FromResult(42);
            },
            cancellationToken).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
        invoked.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a unit <see cref="ValueTask"/> delegate
    /// WHEN <c>TryValueTaskAsync</c> runs on each path
    /// THEN success MUST stay a success and a thrown exception MUST become a failure.
    /// </summary>
    [Fact]
    public async Task TryValueTaskAsync_Unit_ShouldCaptureOrSucceed()
    {
        var succeeded = await Result.TryValueTaskAsync(
            _ => ValueTask.CompletedTask,
            TestContext.Current.CancellationToken);
        var failed = await Result.TryValueTaskAsync(
            _ => ValueTask.FromException(new InvalidOperationException("boom")),
            TestContext.Current.CancellationToken);

        succeeded.IsSuccess.Should().BeTrue();
        failed.TryGetError(out var error).Should().BeTrue();
        error.Message.Should().Be("boom");
    }
}
