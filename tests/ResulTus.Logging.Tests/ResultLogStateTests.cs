// Copyright (c) ResulTus. All rights reserved.

using System.Collections;

namespace ResulTus.Logging.Tests;

/// <summary>
/// Tests for the structured state itself, so the field contract is pinned directly instead of only
/// through a logger that happens to implement the same interface.
/// </summary>
public class ResultLogStateTests
{
    /// <summary>
    /// GIVEN a successful outcome
    /// WHEN the state is read
    /// THEN it MUST expose exactly <c>ResultType</c> and <c>IsSuccess</c>, and no error field.
    /// </summary>
    [Fact]
    public void Success_ShouldExposeOnlyTheOutcomeFields()
    {
        var state = new ResultLogState("Int32", true, default, null);

        state.Count.Should().Be(2);
        state.Should().Equal(
            new KeyValuePair<string, object?>("ResultType", "Int32"),
            new KeyValuePair<string, object?>("IsSuccess", true));
    }

    /// <summary>
    /// GIVEN a failure whose error carries a message
    /// WHEN the state is read
    /// THEN it MUST expose the error's type, code and message in that order.
    /// </summary>
    [Fact]
    public void FailureWithMessage_ShouldExposeTheErrorFields()
    {
        var state = new ResultLogState("Result", false, Error.NotFound("missing", "gone"), null);

        state.Count.Should().Be(5);
        state.Should().Equal(
            new KeyValuePair<string, object?>("ResultType", "Result"),
            new KeyValuePair<string, object?>("IsSuccess", false),
            new KeyValuePair<string, object?>("ErrorType", nameof(ErrorType.NotFound)),
            new KeyValuePair<string, object?>("ErrorCode", "missing"),
            new KeyValuePair<string, object?>("ErrorMessage", "gone"));
    }

    /// <summary>
    /// GIVEN a failure whose error carries no message
    /// WHEN the state is read
    /// THEN <c>ErrorMessage</c> MUST be absent rather than an empty string.
    /// </summary>
    [Fact]
    public void FailureWithoutMessage_ShouldOmitTheMessageField()
    {
        var state = new ResultLogState("Result", false, Error.Conflict("duplicate"), null);

        state.Count.Should().Be(4);
        state.Select(pair => pair.Key).Should().NotContain("ErrorMessage");
    }

    /// <summary>
    /// GIVEN a state with fewer fields than the requested index
    /// WHEN an out-of-range index is read
    /// THEN it MUST throw <see cref="ArgumentOutOfRangeException"/> instead of returning a bogus
    /// field, because a provider walking the state must never see a made-up value.
    /// </summary>
    [Fact]
    public void Indexer_OutOfRange_ShouldThrow()
    {
        var success = new ResultLogState("Result", true, default, null);
        var withoutMessage = new ResultLogState("Result", false, Error.Conflict("duplicate"), null);

        var successRead = () => success[2];
        var messageRead = () => withoutMessage[4];

        successRead.Should().Throw<ArgumentOutOfRangeException>();
        messageRead.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// GIVEN a state
    /// WHEN it is enumerated through the non-generic <see cref="IEnumerable"/>
    /// THEN it MUST yield the same fields, so a legacy consumer sees the same contract.
    /// </summary>
    [Fact]
    public void NonGenericEnumeration_ShouldYieldTheSameFields()
    {
        var state = new ResultLogState("Result", false, Error.Timeout("slow"), null);
        var items = new List<KeyValuePair<string, object?>>();

        foreach (var item in (IEnumerable)state)
        {
            items.Add((KeyValuePair<string, object?>)item!);
        }

        items.Should().Equal(state);
    }

    /// <summary>
    /// GIVEN a caller-supplied message
    /// WHEN the rendered text is read
    /// THEN it MUST be that message, so a call site can say what it was attempting.
    /// </summary>
    [Fact]
    public void CustomMessage_ShouldWin()
    {
        var state = new ResultLogState("Result", false, Error.NotFound("missing"), "looking up order 7");

        state.Message.Should().Be("looking up order 7");
    }

    /// <summary>
    /// GIVEN no caller-supplied message
    /// WHEN the rendered text is read
    /// THEN it MUST describe the outcome, including the error code and its message when present.
    /// </summary>
    [Fact]
    public void DefaultMessage_ShouldDescribeTheOutcome()
    {
        var success = new ResultLogState("Result", true, default, null);
        var withoutMessage = new ResultLogState("Result", false, Error.Conflict("duplicate"), null);
        var withMessage = new ResultLogState("Result", false, Error.NotFound("missing", "gone"), null);

        success.Message.Should().Be("Result succeeded.");
        withoutMessage.Message.Should().Be("Result failed: duplicate");
        withMessage.Message.Should().Be("Result failed: missing (gone)");
    }
}
