// Copyright (c) ResulTus. All rights reserved.

using Microsoft.Extensions.Logging;

namespace ResulTus.Logging.Tests;

/// <summary>
/// An <see cref="ILogger"/> that records every entry it receives, including the structured state,
/// so tests can assert fields instead of parsing a formatted string.
/// </summary>
public sealed class RecordingLogger : ILogger
{
    /// <summary>
    /// The entries recorded so far, in order.
    /// </summary>
    public List<LogEntry> Entries { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the logger reports levels as enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => Enabled;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (state is IReadOnlyList<KeyValuePair<string, object?>> structured)
        {
            foreach (var pair in structured)
            {
                fields[pair.Key] = pair.Value;
            }
        }

        Entries.Add(new LogEntry(logLevel, formatter(state, exception), fields));
    }
}

/// <summary>
/// One captured log entry.
/// </summary>
/// <param name="Level">The level the entry was written at.</param>
/// <param name="Message">The rendered message.</param>
/// <param name="Fields">The structured fields carried by the state.</param>
public readonly record struct LogEntry(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Fields);

/// <summary>
/// Tests for the synchronous logging extensions.
/// </summary>
public class ResultLoggingTests
{
    /// <summary>
    /// GIVEN a successful unit result
    /// WHEN it is logged with <c>LogSuccess</c>
    /// THEN one entry MUST be written at <c>Debug</c> carrying the structured outcome.
    /// </summary>
    [Fact]
    public void LogSuccess_OnSuccess_ShouldWriteDebug()
    {
        var logger = new RecordingLogger();

        var returned = Result.Success().LogSuccess(logger);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Debug);
        logger.Entries[0].Fields["IsSuccess"].Should().Be(true);
        logger.Entries[0].Fields["ResultType"].Should().Be("Result");
        returned.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed unit result
    /// WHEN it is logged with <c>LogSuccess</c>
    /// THEN nothing MUST be written, because the outcome does not match.
    /// </summary>
    [Fact]
    public void LogSuccess_OnFailure_ShouldWriteNothing()
    {
        var logger = new RecordingLogger();

        Result.Failure(Error.NotFound("missing")).LogSuccess(logger);

        logger.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a failed unit result
    /// WHEN it is logged with <c>LogFailure</c>
    /// THEN one entry MUST be written at <c>Error</c> carrying the error's type, code and message.
    /// </summary>
    [Fact]
    public void LogFailure_OnFailure_ShouldWriteErrorWithTheErrorFields()
    {
        var logger = new RecordingLogger();

        Result.Failure(Error.NotFound("missing", "not there")).LogFailure(logger);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Error);
        logger.Entries[0].Fields["IsSuccess"].Should().Be(false);
        logger.Entries[0].Fields["ErrorType"].Should().Be(ErrorType.NotFound.ToString());
        logger.Entries[0].Fields["ErrorCode"].Should().Be("missing");
        logger.Entries[0].Fields["ErrorMessage"].Should().Be("not there");
        logger.Entries[0].Message.Should().Contain("missing");
    }

    /// <summary>
    /// GIVEN a successful result
    /// WHEN it is logged with <c>LogFailure</c>
    /// THEN nothing MUST be written.
    /// </summary>
    [Fact]
    public void LogFailure_OnSuccess_ShouldWriteNothing()
    {
        var logger = new RecordingLogger();

        Result.Success().LogFailure(logger);

        logger.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a failed result whose error carries no message
    /// WHEN it is logged
    /// THEN the <c>ErrorMessage</c> field MUST be absent rather than an empty string, so a provider
    /// can tell "no message" from "an empty message".
    /// </summary>
    [Fact]
    public void LogFailure_WithoutMessage_ShouldOmitTheField()
    {
        var logger = new RecordingLogger();

        Result.Failure(Error.Conflict("duplicate")).LogFailure(logger);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Fields.Should().NotContainKey("ErrorMessage");
        logger.Entries[0].Fields["ErrorCode"].Should().Be("duplicate");
    }

    /// <summary>
    /// GIVEN a successful and a failed result
    /// WHEN both are logged with <c>LogAll</c>
    /// THEN each MUST produce exactly one entry, at the level its outcome implies.
    /// </summary>
    [Fact]
    public void LogAll_ShouldWriteOncePerOutcome()
    {
        var logger = new RecordingLogger();

        Result.Success().LogAll(logger);
        Result.Failure(Error.Failure("boom")).LogAll(logger);

        logger.Entries.Should().HaveCount(2);
        logger.Entries[0].Level.Should().Be(LogLevel.Debug);
        logger.Entries[1].Level.Should().Be(LogLevel.Error);
    }

    /// <summary>
    /// GIVEN a selector that maps <see cref="ErrorType.NotFound"/> to <c>Warning</c>
    /// WHEN a not-found failure is logged with <c>LogAll(selector)</c>
    /// THEN the entry MUST use the selector's level, not the default.
    /// </summary>
    [Fact]
    public void LogAll_WithSelector_ShouldUseTheSelectedLevel()
    {
        var logger = new RecordingLogger();

        Result.Failure(Error.NotFound("missing")).LogAll(
            logger,
            error => error.Type == ErrorType.NotFound ? LogLevel.Warning : LogLevel.Error);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Warning);
    }

    /// <summary>
    /// GIVEN a selector that would throw
    /// WHEN the result is a success
    /// THEN the selector MUST NOT be invoked, because there is no error to classify.
    /// </summary>
    [Fact]
    public void LogAll_WithSelector_ShouldNotInvokeItOnSuccess()
    {
        var logger = new RecordingLogger();
        var invoked = false;

        Result.Success().LogAll(logger, _ =>
        {
            invoked = true;
            return LogLevel.Error;
        });

        invoked.Should().BeFalse();
        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Debug);
    }

    /// <summary>
    /// GIVEN a selector that maps <see cref="ErrorType.Conflict"/> to <c>Warning</c>
    /// WHEN a failed generic result is logged with <c>LogAll(selector)</c>
    /// THEN the entry MUST use the selector's level and keep the generic type's name.
    /// </summary>
    [Fact]
    public void GenericLogAll_WithSelector_ShouldUseTheSelectedLevel()
    {
        var logger = new RecordingLogger();

        Result<string>.Failure(Error.Conflict("duplicate"))
            .LogAll(logger, _ => LogLevel.Warning);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Warning);
        logger.Entries[0].Fields["ResultType"].Should().Be("String");
    }

    /// <summary>
    /// GIVEN a selector
    /// WHEN a successful generic result is logged with <c>LogAll(selector)</c>
    /// THEN the selector MUST NOT be invoked and the entry MUST use the success level.
    /// </summary>
    [Fact]
    public void GenericLogAll_WithSelector_ShouldNotInvokeItOnSuccess()
    {
        var logger = new RecordingLogger();
        var invoked = false;

        Result<int>.Success(7).LogAll(logger, _ =>
        {
            invoked = true;
            return LogLevel.Critical;
        });

        invoked.Should().BeFalse();
        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Debug);
    }

    /// <summary>
    /// GIVEN a failed generic result
    /// WHEN it is logged with each generic level-specific method
    /// THEN each MUST write at its own level.
    /// </summary>
    [Fact]
    public void GenericLevelSpecificMethods_ShouldWriteAtTheirLevel()
    {
        var logger = new RecordingLogger();
        var failed = Result<string>.Failure(Error.Validation("bad"));

        failed.LogError(logger);
        failed.LogWarning(logger);
        failed.LogInformation(logger);

        logger.Entries.Select(entry => entry.Level).Should().Equal(
            LogLevel.Error, LogLevel.Warning, LogLevel.Information);
    }

    /// <summary>
    /// GIVEN a failed result
    /// WHEN it is logged with each level-specific method
    /// THEN each MUST write at its own level and nowhere else.
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Information)]
    public void LevelSpecificMethods_ShouldWriteAtTheirLevel(LogLevel expected)
    {
        var logger = new RecordingLogger();
        var result = Result.Failure(Error.Validation("bad"));

        _ = expected switch
        {
            LogLevel.Warning => result.LogWarning(logger),
            LogLevel.Information => result.LogInformation(logger),
            _ => result.LogError(logger),
        };

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(expected);
    }

    /// <summary>
    /// GIVEN a level-specific method and a successful result
    /// WHEN it is called
    /// THEN nothing MUST be written.
    /// </summary>
    [Fact]
    public void LevelSpecificMethods_OnSuccess_ShouldWriteNothing()
    {
        var logger = new RecordingLogger();

        Result.Success().LogError(logger);
        Result.Success().LogWarning(logger);
        Result.Success().LogInformation(logger);

        logger.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a successful generic result
    /// WHEN it is logged
    /// THEN the value MUST survive untouched and the entry MUST name the generic argument.
    /// </summary>
    [Fact]
    public void GenericResult_ShouldKeepItsValueAndNameItsType()
    {
        var logger = new RecordingLogger();

        var returned = Result<int>.Success(42).LogSuccess(logger);

        returned.IsSuccess.Should().BeTrue();
        returned.GetValueOrDefault().Should().Be(42);
        logger.Entries[0].Fields["ResultType"].Should().Be("Int32");
    }

    /// <summary>
    /// GIVEN a failed generic result
    /// WHEN it is logged
    /// THEN the error fields MUST be present and the failure value MUST be untouched.
    /// </summary>
    [Fact]
    public void GenericFailure_ShouldCarryTheErrorFields()
    {
        var logger = new RecordingLogger();

        var returned = Result<string>.Failure(Error.Forbidden("denied", "not yours")).LogFailure(logger);

        returned.IsFailure.Should().BeTrue();
        returned.TryGetValue(out _, out var error).Should().BeFalse();
        error.Code.Should().Be("denied");
        logger.Entries[0].Fields["ErrorType"].Should().Be(ErrorType.Forbidden.ToString());
        logger.Entries[0].Fields["ErrorMessage"].Should().Be("not yours");
    }

    /// <summary>
    /// GIVEN a custom message
    /// WHEN the result is logged
    /// THEN the message MUST be the one supplied, while the structured fields stay intact.
    /// </summary>
    [Fact]
    public void CustomMessage_ShouldReplaceTheDefaultText()
    {
        var logger = new RecordingLogger();

        Result.Failure(Error.NotFound("missing")).LogFailure(logger, "looking up the order");

        logger.Entries[0].Message.Should().Be("looking up the order");
        logger.Entries[0].Fields["ErrorCode"].Should().Be("missing");
    }

    /// <summary>
    /// GIVEN a <c>null</c> logger
    /// WHEN any entry point is called
    /// THEN it MUST throw <see cref="ArgumentNullException"/> instead of silently dropping the line.
    /// </summary>
    [Fact]
    public void NullLogger_ShouldThrow()
    {
        var unit = Result.Success();
        var value = Result<int>.Success(1);

        var calls = new List<Action>
        {
            () => unit.LogSuccess(null!),
            () => unit.LogFailure(null!),
            () => unit.LogAll(null!),
            () => unit.LogAll(null!, _ => LogLevel.Error),
            () => unit.LogError(null!),
            () => unit.LogWarning(null!),
            () => unit.LogInformation(null!),
            () => value.LogSuccess(null!),
            () => value.LogFailure(null!),
            () => value.LogAll(null!),
            () => value.LogAll(null!, _ => LogLevel.Error),
            () => value.LogError(null!),
            () => value.LogWarning(null!),
            () => value.LogInformation(null!),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }
}
