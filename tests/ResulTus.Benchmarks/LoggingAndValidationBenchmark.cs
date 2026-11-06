// Copyright (c) ResulTus. All rights reserved.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using ResulTus;
using ResulTus.Logging;
using ResulTus.Validation;

namespace ResulTus.Benchmarks;

/// <summary>
/// Measures failure logging when the configured log level is enabled or disabled.
/// </summary>
[MemoryDiagnoser]
public class LoggingOperationBenchmark
{
    private Result<int> _failure;
    private CapturingLogger _logger = null!;
    private Result<int> _sinkResult;
    private int _checksum;

    /// <summary>
    /// Gets or sets whether the logger enables the error level.
    /// </summary>
    [Params(true, false)]
    public bool Enabled { get; set; }

    /// <summary>
    /// Creates the failed result and capturing logger outside the measured operation.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _failure = Result<int>.Failure(Error.NotFound("item.missing", "Item was not found."));
        _logger = new CapturingLogger(Enabled);
    }

    /// <summary>
    /// Reads the captured event and result so the logging work remains observable.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _checksum += _logger.CallCount;
        _checksum += _logger.CapturedState is null ? 0 : 1;
        _checksum += _sinkResult.IsFailure ? 1 : 0;
    }

    /// <summary>
    /// Logs one failed result with the configured logger level.
    /// </summary>
    [Benchmark]
    public void LogFailure()
        => _sinkResult = _failure.LogFailure(_logger);

    private sealed class CapturingLogger(bool enabled) : ILogger
    {
        public int CallCount { get; private set; }

        public object? CapturedState { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => enabled;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            CallCount++;
            CapturedState = state;
        }
    }
}

/// <summary>
/// Measures materializing validation conversions for a populated issue list.
/// </summary>
/// <remarks>
/// Each operation processes 1,000 issues across ten fields; the input model is constructed in
/// <see cref="Setup"/> and is not part of the measured work.
/// </remarks>
[MemoryDiagnoser]
public class ValidationOperationBenchmark
{
    private const int IssueCount = 1_000;

    private ValidationErrors _errors = null!;
    private IReadOnlyDictionary<string, string[]>? _sinkDictionary;
    private Error _sinkError;
    private int _checksum;

    /// <summary>
    /// Creates the populated issue list outside the measured operations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var issues = new ValidationIssue[IssueCount];
        for (var index = 0; index < issues.Length; index++)
        {
            issues[index] = new ValidationIssue(
                $"field-{index % 10}",
                $"Issue {index} is invalid.",
                $"VALIDATION_{index % 10}");
        }

        _errors = new ValidationErrors(issues);
    }

    /// <summary>
    /// Reads the output sinks so the allocations cannot be removed.
    /// </summary>
    /// <remarks>
    /// BenchmarkDotNet calls cleanup for each operation in a separate process, so the dictionary
    /// sink remains null for the <see cref="ToError"/> case.
    /// </remarks>
    [GlobalCleanup]
    public void Cleanup()
    {
        _checksum += _sinkDictionary?.Count ?? 0;
        _checksum += _sinkError.Message?.Length ?? 0;
    }

    /// <summary>
    /// Groups the issue messages by field into a dictionary.
    /// </summary>
    [Benchmark]
    public void ToDictionary()
        => _sinkDictionary = _errors.ToDictionary();

    /// <summary>
    /// Collapses all issues into one validation error.
    /// </summary>
    [Benchmark]
    public void ToError()
        => _sinkError = _errors.ToError();
}
