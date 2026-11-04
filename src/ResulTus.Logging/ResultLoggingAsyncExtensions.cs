// Copyright (c) ResulTus. All rights reserved.

using Microsoft.Extensions.Logging;

namespace ResulTus.Logging;

/// <summary>
/// Asynchronous logging extensions for tasks producing a <see cref="Result"/> or
/// <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// These overloads exist so a handler that returns a task is not awaited twice. Each one awaits
/// the task exactly once, logs through the matching synchronous overload, and returns the awaited
/// result, so the task's value survives untouched.
/// </para>
/// <para>
/// The same rules apply as in <see cref="ResultLoggingExtensions"/>: a method writes only for the
/// outcome it describes, the entry is structured, a disabled level costs nothing, and a
/// <c>null</c> task or logger throws <see cref="ArgumentNullException"/>.
/// </para>
/// </remarks>
public static class ResultLoggingAsyncExtensions
{
    /// <summary>
    /// Awaits a task producing a unit result and logs a success at <see cref="LogLevel.Debug"/>.
    /// </summary>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result> LogSuccessAsync(
        this Task<Result> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogSuccess(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a unit result and logs a failure at <see cref="LogLevel.Error"/>.
    /// </summary>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result> LogFailureAsync(
        this Task<Result> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogFailure(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a unit result and logs a failure at <see cref="LogLevel.Error"/>.
    /// </summary>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result> LogErrorAsync(
        this Task<Result> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogError(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a unit result and logs a failure at <see cref="LogLevel.Warning"/>.
    /// </summary>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result> LogWarningAsync(
        this Task<Result> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogWarning(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a unit result and logs a failure at
    /// <see cref="LogLevel.Information"/>.
    /// </summary>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result> LogInformationAsync(
        this Task<Result> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogInformation(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a unit result and logs it once, at the level its outcome implies.
    /// </summary>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result> LogAllAsync(
        this Task<Result> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogAll(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a unit result and logs it once, choosing the failure level from the
    /// error itself.
    /// </summary>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="levelSelector">Maps the error to a level; never invoked on a success.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/>, <paramref name="logger"/> or <paramref name="levelSelector"/> is
    /// <c>null</c>.
    /// </exception>
    public static async Task<Result> LogAllAsync(
        this Task<Result> task,
        ILogger logger,
        Func<Error, LogLevel> levelSelector,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(levelSelector);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogAll(logger, levelSelector, message);
    }

    /// <summary>
    /// Awaits a task producing a generic result and logs a success at
    /// <see cref="LogLevel.Debug"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result<T>> LogSuccessAsync<T>(
        this Task<Result<T>> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogSuccess(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a generic result and logs a failure at
    /// <see cref="LogLevel.Error"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result<T>> LogFailureAsync<T>(
        this Task<Result<T>> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogFailure(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a generic result and logs a failure at
    /// <see cref="LogLevel.Error"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result<T>> LogErrorAsync<T>(
        this Task<Result<T>> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogError(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a generic result and logs a failure at
    /// <see cref="LogLevel.Warning"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result<T>> LogWarningAsync<T>(
        this Task<Result<T>> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogWarning(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a generic result and logs a failure at
    /// <see cref="LogLevel.Information"/>.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result<T>> LogInformationAsync<T>(
        this Task<Result<T>> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogInformation(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a generic result and logs it once, at the level its outcome implies.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result<T>> LogAllAsync<T>(
        this Task<Result<T>> task,
        ILogger logger,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogAll(logger, message);
    }

    /// <summary>
    /// Awaits a task producing a generic result and logs it once, choosing the failure level from
    /// the error itself.
    /// </summary>
    /// <typeparam name="T">The success value's type.</typeparam>
    /// <param name="task">The task to await and log.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="levelSelector">Maps the error to a level; never invoked on a success.</param>
    /// <param name="message">An optional message that replaces the default rendered text.</param>
    /// <returns>The awaited result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/>, <paramref name="logger"/> or <paramref name="levelSelector"/> is
    /// <c>null</c>.
    /// </exception>
    public static async Task<Result<T>> LogAllAsync<T>(
        this Task<Result<T>> task,
        ILogger logger,
        Func<Error, LogLevel> levelSelector,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(levelSelector);
        ArgumentNullException.ThrowIfNull(task);

        return (await task.ConfigureAwait(false)).LogAll(logger, levelSelector, message);
    }
}
