// Copyright (c) ResulTus. All rights reserved.

using Microsoft.Extensions.Logging;

namespace ResulTus.Logging.Tests;

/// <summary>
/// Tests for the asynchronous overloads and for the guarantee that a disabled level costs nothing.
/// </summary>
public class AsyncLoggingTests
{
    /// <summary>
    /// GIVEN a task producing a successful unit result
    /// WHEN it is logged asynchronously
    /// THEN the awaited result MUST be returned untouched and one entry MUST be written.
    /// </summary>
    [Fact]
    public async Task UnitTask_ShouldLogAndReturnTheResult()
    {
        var logger = new RecordingLogger();

        var returned = await Task.FromResult(Result.Success()).LogSuccessAsync(logger);

        returned.IsSuccess.Should().BeTrue();
        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Debug);
    }

    /// <summary>
    /// GIVEN a task producing a failed generic result
    /// WHEN it is logged asynchronously
    /// THEN one entry MUST be written at <c>Error</c> with the error fields, and the failure MUST
    /// survive untouched.
    /// </summary>
    [Fact]
    public async Task GenericTask_ShouldLogTheFailure()
    {
        var logger = new RecordingLogger();

        var returned = await Task.FromResult(Result<int>.Failure(Error.NotFound("missing", "gone")))
            .LogFailureAsync(logger);

        returned.IsFailure.Should().BeTrue();
        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Error);
        logger.Entries[0].Fields["ErrorCode"].Should().Be("missing");
        logger.Entries[0].Fields["ErrorMessage"].Should().Be("gone");
    }

    /// <summary>
    /// GIVEN a task producing a successful generic result
    /// WHEN it is logged with <c>LogAllAsync</c>
    /// THEN exactly one entry MUST be written, at the success level.
    /// </summary>
    [Fact]
    public async Task LogAllAsync_ShouldWriteOncePerOutcome()
    {
        var logger = new RecordingLogger();

        await Task.FromResult(Result<string>.Success("ok")).LogAllAsync(logger);
        await Task.FromResult(Result<string>.Failure(Error.Failure("boom"))).LogAllAsync(logger);

        logger.Entries.Should().HaveCount(2);
        logger.Entries[0].Level.Should().Be(LogLevel.Debug);
        logger.Entries[1].Level.Should().Be(LogLevel.Error);
    }

    /// <summary>
    /// GIVEN a selector
    /// WHEN a failed task is logged with <c>LogAllAsync(selector)</c>
    /// THEN the entry MUST use the selector's level.
    /// </summary>
    [Fact]
    public async Task LogAllAsync_WithSelector_ShouldUseTheSelectedLevel()
    {
        var logger = new RecordingLogger();

        await Task.FromResult(Result.Failure(Error.Conflict("duplicate")))
            .LogAllAsync(logger, _ => LogLevel.Critical);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Critical);
    }

    /// <summary>
    /// GIVEN the level-specific asynchronous methods
    /// WHEN a failure is logged with each
    /// THEN each MUST write at its own level.
    /// </summary>
    [Fact]
    public async Task AsyncLevelSpecificMethods_ShouldWriteAtTheirLevel()
    {
        var logger = new RecordingLogger();
        var failed = Task.FromResult(Result.Failure(Error.Validation("bad")));

        await failed.LogErrorAsync(logger);
        await failed.LogWarningAsync(logger);
        await failed.LogInformationAsync(logger);

        logger.Entries.Select(e => e.Level).Should().Equal(
            LogLevel.Error, LogLevel.Warning, LogLevel.Information);
    }

    /// <summary>
    /// GIVEN a logger that reports every level as disabled
    /// WHEN a result is logged
    /// THEN the logger's <c>Log</c> MUST never be reached, so no state object is built and the call
    /// costs nothing beyond the level check.
    /// </summary>
    [Fact]
    public void DisabledLevel_ShouldNotReachTheLogger()
    {
        var logger = new RecordingLogger { Enabled = false };

        Result.Failure(Error.NotFound("missing")).LogFailure(logger);
        Result.Success().LogSuccess(logger);

        logger.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a selector
    /// WHEN a failed generic task is logged with <c>LogAllAsync(selector)</c>
    /// THEN the entry MUST use the selector's level.
    /// </summary>
    [Fact]
    public async Task GenericLogAllAsync_WithSelector_ShouldUseTheSelectedLevel()
    {
        var logger = new RecordingLogger();

        await Task.FromResult(Result<Guid>.Failure(Error.Timeout("slow")))
            .LogAllAsync(logger, _ => LogLevel.Critical);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Critical);
        logger.Entries[0].Fields["ErrorType"].Should().Be(ErrorType.Timeout.ToString());
    }

    /// <summary>
    /// GIVEN every asynchronous overload
    /// WHEN each is called once
    /// THEN each MUST reach its result, write only when its outcome matches, and return the
    /// awaited result untouched.
    /// </summary>
    [Fact]
    public async Task EveryAsyncOverload_ShouldReachItsResult()
    {
        var logger = new RecordingLogger();
        var unitSuccess = Task.FromResult(Result.Success());
        var unitFailure = Task.FromResult(Result.Failure(Error.Failure("boom")));
        var valueSuccess = Task.FromResult(Result<int>.Success(1));
        var valueFailure = Task.FromResult(Result<int>.Failure(Error.Validation("bad")));

        (await unitSuccess.LogFailureAsync(logger)).IsSuccess.Should().BeTrue();
        (await unitFailure.LogAllAsync(logger)).IsFailure.Should().BeTrue();
        (await valueSuccess.LogSuccessAsync(logger)).GetValueOrDefault().Should().Be(1);
        (await valueFailure.LogErrorAsync(logger)).IsFailure.Should().BeTrue();
        (await valueFailure.LogWarningAsync(logger)).IsFailure.Should().BeTrue();
        (await valueFailure.LogInformationAsync(logger)).IsFailure.Should().BeTrue();

        logger.Entries.Select(entry => entry.Level).Should().Equal(
            LogLevel.Error, LogLevel.Debug, LogLevel.Error, LogLevel.Warning, LogLevel.Information);
    }

    /// <summary>
    /// GIVEN a <c>null</c> task
    /// WHEN an asynchronous overload is called
    /// THEN it MUST throw <see cref="ArgumentNullException"/> rather than a
    /// <see cref="NullReferenceException"/> from the await.
    /// </summary>
    [Fact]
    public async Task NullTask_ShouldThrow()
    {
        var logger = new RecordingLogger();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ((Task<Result>)null!).LogSuccessAsync(logger));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ((Task<Result<int>>)null!).LogAllAsync(logger, _ => LogLevel.Error));
    }

    /// <summary>
    /// GIVEN a <c>null</c> logger
    /// WHEN any asynchronous entry point is called
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task NullLogger_ShouldThrow()
    {
        var unit = Task.FromResult(Result.Success());
        var value = Task.FromResult(Result<int>.Success(1));

        var calls = new List<Func<Task>>
        {
            () => unit.LogSuccessAsync(null!),
            () => unit.LogFailureAsync(null!),
            () => unit.LogAllAsync(null!),
            () => unit.LogAllAsync(null!, _ => LogLevel.Error),
            () => unit.LogErrorAsync(null!),
            () => unit.LogWarningAsync(null!),
            () => unit.LogInformationAsync(null!),
            () => value.LogSuccessAsync(null!),
            () => value.LogFailureAsync(null!),
            () => value.LogAllAsync(null!),
            () => value.LogAllAsync(null!, _ => LogLevel.Error),
            () => value.LogErrorAsync(null!),
            () => value.LogWarningAsync(null!),
            () => value.LogInformationAsync(null!),
        };

        foreach (var call in calls)
        {
            await call.Should().ThrowAsync<ArgumentNullException>();
        }
    }
}
