// Copyright (c) ResulTus. All rights reserved.

using Microsoft.Extensions.Logging;

namespace ResulTus.Logging;

/// <summary>
/// Logging extensions for <see cref="Result"/>, <see cref="Result{T}"/> and their task-returning
/// forms.
/// </summary>
/// <remarks>
/// <para>
/// Every method returns the result it was given, so a log call composes inside a chain without
/// changing the chain's value. A method that does not describe the actual outcome writes nothing:
/// <c>LogSuccess</c> on a failure is a no-op, and so is <c>LogFailure</c> on a success. Nothing is
/// written twice.
/// </para>
/// <para>
/// The entry is structured. The state handed to the logger exposes <c>ResultType</c> and
/// <c>IsSuccess</c> always, plus <c>ErrorType</c>, <c>ErrorCode</c> and <c>ErrorMessage</c> on
/// failure, so a provider reads named fields instead of parsing the rendered text.
/// </para>
/// <para>
/// Every method checks whether its level is enabled before building anything, so a disabled level
/// — the default <c>Debug</c> on success, for instance — allocates nothing.
/// </para>
/// <para>
/// The rendered message is a convenience for text sinks and does not name the operation. Pass
/// <c>message</c> to say what was being attempted; the structured fields are unaffected by it.
/// </para>
/// </remarks>
public static class ResultLoggingExtensions
{
    /// <summary>
    /// Logs a successful unit result at <see cref="LogLevel.Debug"/>.
    /// </summary>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result LogSuccess(this Result result, ILogger logger, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return result.IsSuccess ? LogUnit(logger, result, LogLevel.Debug, message) : result;
    }

    /// <summary>
    /// Logs a failed unit result at <see cref="LogLevel.Error"/>.
    /// </summary>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result LogFailure(this Result result, ILogger logger, string? message = null)
        => result.LogAt(logger, LogLevel.Error, message);

    /// <summary>
    /// Logs a failed unit result at <see cref="LogLevel.Error"/>.
    /// </summary>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result LogError(this Result result, ILogger logger, string? message = null)
        => result.LogAt(logger, LogLevel.Error, message);

    /// <summary>
    /// Logs a failed unit result at <see cref="LogLevel.Warning"/>.
    /// </summary>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result LogWarning(this Result result, ILogger logger, string? message = null)
        => result.LogAt(logger, LogLevel.Warning, message);

    /// <summary>
    /// Logs a failed unit result at <see cref="LogLevel.Information"/>.
    /// </summary>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result LogInformation(this Result result, ILogger logger, string? message = null)
        => result.LogAt(logger, LogLevel.Information, message);

    /// <summary>
    /// Logs a unit result once, at the level its outcome implies: <see cref="LogLevel.Debug"/> on
    /// success, <see cref="LogLevel.Error"/> on failure.
    /// </summary>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result LogAll(this Result result, ILogger logger, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return LogUnit(
            logger,
            result,
            result.IsSuccess ? LogLevel.Debug : LogLevel.Error,
            message);
    }

    /// <summary>
    /// Logs a unit result once, choosing the failure level from the error itself.
    /// </summary>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="levelSelector">
    /// Maps the error to a level. It is never invoked on a success, because there is no error to
    /// classify.
    /// </param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> or <paramref name="levelSelector"/> is <c>null</c>.
    /// </exception>
    public static Result LogAll(
        this Result result,
        ILogger logger,
        Func<Error, LogLevel> levelSelector,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(levelSelector);

        if (result.IsSuccess)
        {
            return LogUnit(logger, result, LogLevel.Debug, message);
        }

        result.TryGetError(out var error);
        return LogUnit(logger, result, levelSelector(error), message);
    }

    /// <summary>
    /// Logs a successful generic result at <see cref="LogLevel.Debug"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result<T> LogSuccess<T>(this Result<T> result, ILogger logger, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return result.IsSuccess ? LogGeneric(logger, result, LogLevel.Debug, message) : result;
    }

    /// <summary>
    /// Logs a failed generic result at <see cref="LogLevel.Error"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result<T> LogFailure<T>(this Result<T> result, ILogger logger, string? message = null)
        => result.LogAt(logger, LogLevel.Error, message);

    /// <summary>
    /// Logs a failed generic result at <see cref="LogLevel.Error"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result<T> LogError<T>(this Result<T> result, ILogger logger, string? message = null)
        => result.LogAt(logger, LogLevel.Error, message);

    /// <summary>
    /// Logs a failed generic result at <see cref="LogLevel.Warning"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result<T> LogWarning<T>(this Result<T> result, ILogger logger, string? message = null)
        => result.LogAt(logger, LogLevel.Warning, message);

    /// <summary>
    /// Logs a failed generic result at <see cref="LogLevel.Information"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result<T> LogInformation<T>(this Result<T> result, ILogger logger, string? message = null)
        => result.LogAt(logger, LogLevel.Information, message);

    /// <summary>
    /// Logs a generic result once, at the level its outcome implies: <see cref="LogLevel.Debug"/>
    /// on success, <see cref="LogLevel.Error"/> on failure.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
    public static Result<T> LogAll<T>(this Result<T> result, ILogger logger, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return LogGeneric(
            logger,
            result,
            result.IsSuccess ? LogLevel.Debug : LogLevel.Error,
            message);
    }

    /// <summary>
    /// Logs a generic result once, choosing the failure level from the error itself.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="result">The result to log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="levelSelector">
    /// Maps the error to a level. It is never invoked on a success, because there is no error to
    /// classify.
    /// </param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The same <paramref name="result"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> or <paramref name="levelSelector"/> is <c>null</c>.
    /// </exception>
    public static Result<T> LogAll<T>(
        this Result<T> result,
        ILogger logger,
        Func<Error, LogLevel> levelSelector,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(levelSelector);

        if (result.IsSuccess)
        {
            return LogGeneric(logger, result, LogLevel.Debug, message);
        }

        result.TryGetValue(out _, out var error);
        return LogGeneric(logger, result, levelSelector(error), message);
    }

    private static Result LogAt(this Result result, ILogger logger, LogLevel level, string? message)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return result.IsSuccess ? result : LogUnit(logger, result, level, message);
    }

    private static Result<T> LogAt<T>(
        this Result<T> result,
        ILogger logger,
        LogLevel level,
        string? message)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return result.IsSuccess ? result : LogGeneric(logger, result, level, message);
    }

    private static Result LogUnit(ILogger logger, Result result, LogLevel level, string? message)
    {
        result.TryGetError(out var error);
        Write(logger, level, result.IsSuccess, error, "Result", message);
        return result;
    }

    private static Result<T> LogGeneric<T>(
        ILogger logger,
        Result<T> result,
        LogLevel level,
        string? message)
    {
        result.TryGetValue(out _, out var error);
        Write(logger, level, result.IsSuccess, error, typeof(T).Name, message);
        return result;
    }

    private static void Write(
        ILogger logger,
        LogLevel level,
        bool isSuccess,
        Error error,
        string resultType,
        string? message)
    {
        if (!logger.IsEnabled(level))
        {
            return;
        }

        logger.Log(
            level,
            default,
            new ResultLogState(resultType, isSuccess, error, message),
            null,
            static (state, _) => state.Message);
    }
}
