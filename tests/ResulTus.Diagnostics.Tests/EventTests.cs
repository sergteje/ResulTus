// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Diagnostics.Tests;

/// <summary>
/// Tests for the <see cref="System.Diagnostics.DiagnosticListener"/> events a measured operation
/// publishes.
/// </summary>
[Collection(DiagnosticsCollection.Name)]
public class EventTests
{
    private const string StartedEvent = "ResultOperationStarted";
    private const string CompletedEvent = "ResultOperationCompleted";

    /// <summary>
    /// GIVEN a subscribed listener
    /// WHEN a unit operation is measured
    /// THEN it MUST see the started event before the operation and the completed event after it.
    /// </summary>
    [Fact]
    public void Measure_ShouldEmitStartedThenCompleted()
    {
        using var observer = new EventObserver();
        Func<Result> operation = () => Result.Success();

        operation.Measure("DoWork");

        observer.Events.Select(entry => entry.Key).Should().Equal(StartedEvent, CompletedEvent);
        observer.Events[0].Value.Should().BeOfType<ResultOperationStarted>()
            .Which.OperationName.Should().Be("DoWork");
    }

    /// <summary>
    /// GIVEN a failed operation
    /// WHEN it is measured
    /// THEN the completed payload MUST carry the error's type and code.
    /// </summary>
    [Fact]
    public void CompletedPayload_OnFailure_ShouldDescribeTheError()
    {
        using var observer = new EventObserver();
        Func<Result> operation = () => Result.Failure(Error.NotFound("missing", "gone"));

        operation.Measure("DoWork");

        var completed = observer.Events[1].Value
            .Should().BeOfType<ResultOperationCompleted>().Subject;

        completed.OperationName.Should().Be("DoWork");
        completed.IsSuccess.Should().BeFalse();
        completed.ErrorType.Should().Be(ErrorType.NotFound);
        completed.ErrorCode.Should().Be("missing");
        completed.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    /// <summary>
    /// GIVEN a successful operation
    /// WHEN it is measured
    /// THEN the completed payload MUST carry <see cref="ErrorType.None"/> and no code, so a
    /// subscriber can branch on the type without a nullable.
    /// </summary>
    [Fact]
    public void CompletedPayload_OnSuccess_ShouldCarryNoError()
    {
        using var observer = new EventObserver();
        Func<Result<int>> operation = () => Result<int>.Success(7);

        operation.Measure("DoWork");

        var completed = observer.Events[1].Value
            .Should().BeOfType<ResultOperationCompleted>().Subject;

        completed.IsSuccess.Should().BeTrue();
        completed.ErrorType.Should().Be(ErrorType.None);
        completed.ErrorCode.Should().BeNull();
    }

    /// <summary>
    /// GIVEN a subscribed listener
    /// WHEN an asynchronous operation is measured
    /// THEN it MUST see the same two events in the same order.
    /// </summary>
    [Fact]
    public async Task MeasureAsync_ShouldEmitStartedThenCompleted()
    {
        using var observer = new EventObserver();
        Func<Task<Result>> operation = () => Task.FromResult(Result.Success());

        await operation.MeasureAsync("DoWork");

        observer.Events.Select(entry => entry.Key).Should().Equal(StartedEvent, CompletedEvent);
    }

    /// <summary>
    /// GIVEN an operation that throws
    /// WHEN it is measured
    /// THEN the completed event MUST still be published as a failure, so a subscriber never sees
    /// a started event without its pair.
    /// </summary>
    [Fact]
    public void Measure_WhenTheOperationThrows_ShouldEmitCompleted()
    {
        using var observer = new EventObserver();
        Func<Result> operation = () => throw new InvalidOperationException("boom");

        var act = () => operation.Measure("DoWork");

        act.Should().Throw<InvalidOperationException>();
        observer.Events.Select(entry => entry.Key).Should().Equal(StartedEvent, CompletedEvent);

        var completed = observer.Events[1].Value
            .Should().BeOfType<ResultOperationCompleted>().Subject;

        completed.IsSuccess.Should().BeFalse();
        completed.ErrorType.Should().Be(ErrorType.Unexpected);
    }

    /// <summary>
    /// GIVEN an asynchronous operation that throws
    /// WHEN it is measured
    /// THEN the completed event MUST still be published as a failure.
    /// </summary>
    [Fact]
    public async Task MeasureAsync_WhenTheOperationThrows_ShouldEmitCompleted()
    {
        using var observer = new EventObserver();
        Func<Task<Result>> operation = () => throw new InvalidOperationException("boom");

        var act = () => operation.MeasureAsync("DoWork");

        await act.Should().ThrowAsync<InvalidOperationException>();
        observer.Events.Select(entry => entry.Key).Should().Equal(StartedEvent, CompletedEvent);
    }
}
