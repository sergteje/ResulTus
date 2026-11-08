// Copyright (c) ResulTus. All rights reserved.

using System.Collections;

namespace ResulTus.Logging;

/// <summary>
/// The structured state handed to the logger for one <see cref="Result"/> outcome.
/// </summary>
/// <remarks>
/// <para>
/// A log entry is not a formatted string. The state implements
/// <see cref="IReadOnlyList{T}"/> of <see cref="KeyValuePair{TKey,TValue}"/>, which is the shape
/// <c>Microsoft.Extensions.Logging</c> providers understand: each element is a named field, so a
/// provider reads <c>ErrorCode</c> instead of parsing it back out of the rendered text.
/// </para>
/// <para>
/// <c>ResultType</c> and <c>IsSuccess</c> are always present. <c>ErrorType</c>, <c>ErrorCode</c>
/// and <c>ErrorMessage</c> are present only on a failure, and <c>ErrorMessage</c> is omitted
/// entirely when the error carries no message, so a provider can tell "no message" apart from
/// "an empty message".
/// </para>
/// </remarks>
internal sealed class ResultLogState : IReadOnlyList<KeyValuePair<string, object?>>
{
    private readonly string _resultType;
    private readonly bool _isSuccess;
    private readonly Error _error;
    private readonly string? _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultLogState"/> class.
    /// </summary>
    /// <param name="resultType">The value of the <c>ResultType</c> field.</param>
    /// <param name="isSuccess">Whether the logged result represents a success.</param>
    /// <param name="error">The result's error; ignored when <paramref name="isSuccess"/> is <c>true</c>.</param>
    /// <param name="message">A caller-supplied message that replaces the rendered default.</param>
    internal ResultLogState(string resultType, bool isSuccess, Error error, string? message)
    {
        _resultType = resultType;
        _isSuccess = isSuccess;
        _error = error;
        _message = message;
    }

    /// <inheritdoc />
    public int Count => _isSuccess
        ? 2
        : _error.Message is null ? 4 : 5;

    /// <inheritdoc />
    public KeyValuePair<string, object?> this[int index] => index switch
    {
        0 => new KeyValuePair<string, object?>("ResultType", _resultType),
        1 => new KeyValuePair<string, object?>("IsSuccess", _isSuccess),
        2 when !_isSuccess => new KeyValuePair<string, object?>("ErrorType", _error.Type.ToString()),
        3 when !_isSuccess => new KeyValuePair<string, object?>("ErrorCode", _error.Code),
        4 when !_isSuccess && _error.Message is not null
            => new KeyValuePair<string, object?>("ErrorMessage", _error.Message),
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    /// <summary>
    /// Gets the text rendered for a plain-text sink, using the caller's message when one was given.
    /// </summary>
    internal string Message => _message ?? BuildDefaultMessage();

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private string BuildDefaultMessage()
    {
        if (_isSuccess)
        {
            return "Result succeeded.";
        }

        return _error.Message is null
            ? $"Result failed: {_error.Code}"
            : $"Result failed: {_error.Code} ({_error.Message})";
    }
}
