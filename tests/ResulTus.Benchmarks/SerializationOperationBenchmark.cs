// Copyright (c) ResulTus. All rights reserved.

#pragma warning disable IL2026, IL3050 // This benchmark intentionally measures the reflection-based converter registration path; AOT coverage is a separate test.

using System.Text.Json;
using BenchmarkDotNet.Attributes;
using ResulTus;
using ResulTus.Serialization;

namespace ResulTus.Benchmarks;

/// <summary>
/// Measures System.Text.Json serialization and deserialization for the ResulTus types.
/// </summary>
/// <remarks>
/// The array rows use 1,000 results so converter lookup costs inside the element loop are visible.
/// Each operation stores its result in a dedicated sink; <see cref="Cleanup"/> tolerates the other
/// sinks remaining at their defaults because BenchmarkDotNet runs each benchmark case separately.
/// </remarks>
[MemoryDiagnoser]
public class SerializationOperationBenchmark
{
    private const int ArrayLength = 1_000;

    private JsonSerializerOptions _options = null!;
    private Result<int> _success;
    private Result<int> _failure;
    private Result<int>[] _results = [];
    private string _successJson = string.Empty;
    private string _failureJson = string.Empty;
    private string _resultsJson = string.Empty;
    private string _errorJson = string.Empty;
    private string _errorTypeJson = string.Empty;
    private Error _error;

    private string? _sinkSerializeSuccess;
    private string? _sinkSerializeFailure;
    private string? _sinkSerializeResults;
    private string? _sinkSerializeError;
    private string? _sinkSerializeErrorType;
    private Result<int>? _sinkDeserializeSuccess;
    private Result<int>? _sinkDeserializeFailure;
    private Result<int>[]? _sinkDeserializeResults;
    private Error? _sinkDeserializeError;
    private ErrorType? _sinkDeserializeErrorType;
    private int _checksum;

    /// <summary>
    /// Prepares serializer options and the input JSON outside the measured operations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _options = new JsonSerializerOptions().AddResulTusConverters();
        _success = Result<int>.Success(42);
        _failure = Result<int>.Failure(Error.NotFound("item.missing", "Item was not found."));
        _error = Error.Conflict("item.exists", "The item already exists.");
        _results = new Result<int>[ArrayLength];

        for (var index = 0; index < _results.Length; index++)
        {
            _results[index] = Result<int>.Success(index);
        }

        _successJson = JsonSerializer.Serialize(_success, _options);
        _failureJson = JsonSerializer.Serialize(_failure, _options);
        _resultsJson = JsonSerializer.Serialize(_results, _options);
        _errorJson = JsonSerializer.Serialize(_error, _options);
        _errorTypeJson = JsonSerializer.Serialize(ErrorType.NotFound, _options);
    }

    /// <summary>
    /// Reads every sink so the stores made by the benchmark methods cannot be removed.
    /// </summary>
    /// <remarks>
    /// Exactly one benchmark method has executed in each BenchmarkDotNet process, so every other
    /// sink is still at its default value when cleanup runs.
    /// </remarks>
    [GlobalCleanup]
    public void Cleanup()
    {
        _checksum += _sinkSerializeSuccess?.Length ?? 0;
        _checksum += _sinkSerializeFailure?.Length ?? 0;
        _checksum += _sinkSerializeResults?.Length ?? 0;
        _checksum += _sinkSerializeError?.Length ?? 0;
        _checksum += _sinkSerializeErrorType?.Length ?? 0;
        _checksum += _sinkDeserializeSuccess?.IsSuccess == true ? 1 : 0;
        _checksum += _sinkDeserializeFailure?.IsFailure == true ? 1 : 0;
        _checksum += _sinkDeserializeResults?.Length ?? 0;
        _checksum += _sinkDeserializeError?.Code.Length ?? 0;
        _checksum += _sinkDeserializeErrorType.HasValue ? (int)_sinkDeserializeErrorType.Value : 0;
    }

    /// <summary>
    /// Serializes a successful generic result.
    /// </summary>
    [Benchmark]
    public void SerializeResultSuccess()
        => _sinkSerializeSuccess = JsonSerializer.Serialize(_success, _options);

    /// <summary>
    /// Serializes a failed generic result.
    /// </summary>
    [Benchmark]
    public void SerializeResultFailure()
        => _sinkSerializeFailure = JsonSerializer.Serialize(_failure, _options);

    /// <summary>
    /// Deserializes a successful generic result.
    /// </summary>
    [Benchmark]
    public void DeserializeResultSuccess()
        => _sinkDeserializeSuccess = JsonSerializer.Deserialize<Result<int>>(_successJson, _options);

    /// <summary>
    /// Deserializes a failed generic result.
    /// </summary>
    [Benchmark]
    public void DeserializeResultFailure()
        => _sinkDeserializeFailure = JsonSerializer.Deserialize<Result<int>>(_failureJson, _options);

    /// <summary>
    /// Serializes an array of one thousand successful generic results.
    /// </summary>
    [Benchmark]
    public void SerializeResultArray()
        => _sinkSerializeResults = JsonSerializer.Serialize(_results, _options);

    /// <summary>
    /// Deserializes an array of one thousand successful generic results.
    /// </summary>
    [Benchmark]
    public void DeserializeResultArray()
        => _sinkDeserializeResults = JsonSerializer.Deserialize<Result<int>[]>(_resultsJson, _options);

    /// <summary>
    /// Serializes a structured error.
    /// </summary>
    [Benchmark]
    public void SerializeError()
        => _sinkSerializeError = JsonSerializer.Serialize(_error, _options);

    /// <summary>
    /// Deserializes a structured error.
    /// </summary>
    [Benchmark]
    public void DeserializeError()
        => _sinkDeserializeError = JsonSerializer.Deserialize<Error>(_errorJson, _options);

    /// <summary>
    /// Serializes an error category.
    /// </summary>
    [Benchmark]
    public void SerializeErrorType()
        => _sinkSerializeErrorType = JsonSerializer.Serialize(ErrorType.NotFound, _options);

    /// <summary>
    /// Deserializes an error category.
    /// </summary>
    [Benchmark]
    public void DeserializeErrorType()
        => _sinkDeserializeErrorType = JsonSerializer.Deserialize<ErrorType>(_errorTypeJson, _options);
}

#pragma warning restore IL2026, IL3050
