// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Tests;

/// <summary>
/// Tests for capturing synchronous exceptions as results: <see cref="Error.FromException"/> and
/// <see cref="Result.Try(System.Action)"/> with its overloads.
/// </summary>
public class ExceptionCaptureTests
{
    private static Result<int> _valueSink;
    private static Result _unitSink;

    /// <summary>
    /// GIVEN a delegate that returns a value
    /// WHEN <c>Try</c> runs
    /// THEN it MUST return a success holding that value.
    /// </summary>
    [Fact]
    public void Try_WithValue_OnSuccess_ShouldReturnTheValue()
    {
        var result = Result.Try(static () => 42);

        result.IsSuccess.Should().BeTrue();
        result.GetValueOrDefault().Should().Be(42);
    }

    /// <summary>
    /// GIVEN a delegate that throws
    /// WHEN <c>Try</c> runs
    /// THEN it MUST return an unexpected error whose code is the exception type and whose message is the exception message.
    /// </summary>
    [Fact]
    public void Try_WithValue_OnException_ShouldReturnAnUnexpectedError()
    {
        var result = Result.Try<int>(static () => throw new InvalidOperationException("boom"));

        result.TryGetValue(out _, out var error).Should().BeFalse();
        error.Type.Should().Be(ErrorType.Unexpected);
        error.Code.Should().Be(typeof(InvalidOperationException).FullName);
        error.Message.Should().Be("boom");
    }

    /// <summary>
    /// GIVEN a unit delegate that completes
    /// WHEN <c>Try</c> runs
    /// THEN it MUST return a successful unit result.
    /// </summary>
    [Fact]
    public void Try_WithAction_OnSuccess_ShouldReturnSuccess()
    {
        var invoked = false;

        var result = Result.Try(() => invoked = true);

        result.IsSuccess.Should().BeTrue();
        invoked.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a unit delegate that throws
    /// WHEN <c>Try</c> runs
    /// THEN it MUST return a failed unit result carrying the unexpected error.
    /// </summary>
    [Fact]
    public void Try_WithAction_OnException_ShouldReturnAnUnexpectedError()
    {
        var result = Result.Try(static () => throw new FormatException("bad format"));

        result.IsSuccess.Should().BeFalse();
        result.TryGetError(out var error).Should().BeTrue();
        error.Type.Should().Be(ErrorType.Unexpected);
        error.Code.Should().Be(typeof(FormatException).FullName);
        error.Message.Should().Be("bad format");
    }

    /// <summary>
    /// GIVEN a delegate that throws
    /// WHEN <c>Try</c> runs with a mapper
    /// THEN the mapper MUST translate the exception in both overloads.
    /// </summary>
    [Fact]
    public void Try_WithMapper_OnException_ShouldUseTheMappedError()
    {
        var captured = new List<Exception>();

        var withValue = Result.Try<int>(
            static () => throw new InvalidOperationException("boom"),
            exception =>
            {
                captured.Add(exception);
                return Error.Conflict("mapped", exception.Message);
            });
        var withUnit = Result.Try(
            static () => throw new InvalidOperationException("boom"),
            exception =>
            {
                captured.Add(exception);
                return Error.Conflict("mapped", exception.Message);
            });

        withValue.TryGetValue(out _, out var valueError).Should().BeFalse();
        valueError.Type.Should().Be(ErrorType.Conflict);
        valueError.Code.Should().Be("mapped");
        valueError.Message.Should().Be("boom");

        withUnit.IsFailure.Should().BeTrue();
        withUnit.TryGetError(out var unitError).Should().BeTrue();
        unitError.Code.Should().Be("mapped");

        captured.Should().HaveCount(2);
        captured.Should().AllBeOfType<InvalidOperationException>();
    }

    /// <summary>
    /// GIVEN a delegate that completes
    /// WHEN <c>Try</c> runs with a mapper
    /// THEN the mapper MUST NOT run.
    /// </summary>
    [Fact]
    public void Try_WithMapper_OnSuccess_ShouldNotInvokeTheMapper()
    {
        var invoked = false;

        var withValue = Result.Try(static () => 42, _ =>
        {
            invoked = true;
            return Error.Conflict("mapped");
        });
        var withUnit = Result.Try(static () => { }, _ =>
        {
            invoked = true;
            return Error.Conflict("mapped");
        });

        invoked.Should().BeFalse();
        withValue.GetValueOrDefault().Should().Be(42);
        withUnit.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a delegate that throws <see cref="OperationCanceledException"/>
    /// WHEN <c>Try</c> runs, with or without a mapper
    /// THEN the exception MUST propagate instead of becoming a failure.
    /// </summary>
    [Fact]
    public void Try_ShouldRethrowOperationCanceledException()
    {
        Action[] calls =
        [
            () => { _ = Result.Try<int>(static () => throw new OperationCanceledException("stop")); },
            () => { _valueSink = Result.Try<int>(static () => throw new OperationCanceledException("stop"), _ => Error.Cancelled("cancelled")); },
            () => { _unitSink = Result.Try(static () => throw new OperationCanceledException("stop")); },
            () => { _unitSink = Result.Try(static () => throw new OperationCanceledException("stop"), _ => Error.Cancelled("cancelled")); },
        ];

        foreach (var call in calls)
        {
            call.Should().Throw<OperationCanceledException>();
        }
    }

    /// <summary>
    /// GIVEN a delegate that throws a cancellation type derived from <see cref="OperationCanceledException"/>
    /// WHEN <c>Try</c> runs
    /// THEN it MUST propagate as well, so .NET cancellation control flow is preserved.
    /// </summary>
    [Fact]
    public void Try_ShouldRethrowDerivedCancellation()
    {
        Action[] calls =
        [
            () => { _ = Result.Try<int>(static () => throw new TaskCanceledException("stop")); },
            () => { _unitSink = Result.Try(static () => throw new TaskCanceledException("stop")); },
        ];

        foreach (var call in calls)
        {
            call.Should().Throw<TaskCanceledException>();
        }
    }

    /// <summary>
    /// GIVEN a <c>null</c> delegate or mapper
    /// WHEN any <c>Try</c> overload runs
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Try_WithNullDelegate_ShouldThrow()
    {
        Action[] calls =
        [
            () => Result.Try((Action)null!),
            () => Result.Try((Func<int>)null!),
            () => Result.Try((Action)null!, _ => Error.None),
            () => Result.Try((Func<int>)null!, _ => Error.None),
            () => Result.Try(static () => { }, null!),
            () => Result.Try(static () => 1, null!),
        ];

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// GIVEN an exception
    /// WHEN <c>Error.FromException()</c> runs
    /// THEN the error MUST be unexpected, coded by the exception type and messaged by the exception.
    /// </summary>
    [Fact]
    public void FromException_ShouldMapTypeCodeAndMessage()
    {
        var exception = new InvalidOperationException("boom");

        var error = Error.FromException(exception);

        error.Type.Should().Be(ErrorType.Unexpected);
        error.Code.Should().Be(typeof(InvalidOperationException).FullName);
        error.Message.Should().Be("boom");
    }

    /// <summary>
    /// GIVEN a project-specific exception
    /// WHEN <c>Error.FromException()</c> runs
    /// THEN the code MUST follow the runtime type, not a fixed name.
    /// </summary>
    [Fact]
    public void FromException_ShouldUseTheRuntimeTypeOfACustomException()
    {
        var error = Error.FromException(new DomainException("domain failure"));

        error.Code.Should().Be(typeof(DomainException).FullName);
        error.Message.Should().Be("domain failure");
    }

    /// <summary>
    /// GIVEN a <c>null</c> exception
    /// WHEN <c>Error.FromException()</c> runs
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void FromException_WithNull_ShouldThrow()
    {
        Action call = () => Error.FromException(null!);

        call.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN a delegate that completes
    /// WHEN <c>Try</c> runs over a warm loop
    /// THEN it MUST allocate nothing on the heap.
    /// </summary>
    /// <remarks>
    /// Only the success path is pinned: capturing an exception necessarily touches the exception's
    /// message string, so a failure-path 0 B claim would be false.
    /// </remarks>
    [Fact]
    public void Try_OnSuccess_ShouldNotAllocate()
    {
        Measure(() =>
        {
            _valueSink = Result.Try(static () => 42);
            _unitSink = Result.Try(static () => { });
            return 0;
        }).Should().Be(0);
    }

    private static long Measure(Func<int> operation, int iterations = 10_000)
    {
        _ = operation();
        _ = operation();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            checksum += operation();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(checksum);
        GC.KeepAlive(_valueSink);
        GC.KeepAlive(_unitSink);
        return allocated;
    }

    private sealed class DomainException : Exception
    {
        public DomainException(string message)
            : base(message)
        {
        }
    }
}
