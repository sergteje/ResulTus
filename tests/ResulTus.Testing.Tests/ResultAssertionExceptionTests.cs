// Copyright (c) ResulTus. All rights reserved.

using System;
using Xunit;

namespace ResulTus.Testing.Tests;

/// <summary>
/// Tests for <see cref="ResultAssertionException"/>'s constructors and message surface —
/// the members which a test framework renders verbatim when an assertion fails.
/// </summary>
public class ResultAssertionExceptionTests
{
    /// <summary>
    /// GIVEN the parameterless constructor
    /// WHEN the exception is created
    /// THEN it MUST carry the default message.
    /// </summary>
    [Fact]
    public void ParameterlessConstructor_ShouldCarryTheDefaultMessage()
    {
        var exception = new ResultAssertionException();

        exception.Message.Should().Be("The result did not match the expected outcome.");
    }

    /// <summary>
    /// GIVEN a constructor with a message
    /// WHEN the exception is created
    /// THEN it MUST carry that message verbatim.
    /// </summary>
    [Fact]
    public void MessageConstructor_ShouldCarryTheGivenMessage()
    {
        var exception = new ResultAssertionException("custom failure");

        exception.Message.Should().Be("custom failure");
    }

    /// <summary>
    /// GIVEN a constructor with a message and an inner exception
    /// WHEN the exception is created
    /// THEN it MUST expose the message, the inner exception, and a ToString containing both.
    /// </summary>
    [Fact]
    public void MessageAndInnerConstructor_ShouldExposeTheExceptionChain()
    {
        var inner = new InvalidOperationException("why");
        var exception = new ResultAssertionException("custom failure", inner);

        exception.Message.Should().Be("custom failure");
        exception.InnerException.Should().BeSameAs(inner);
        exception.ToString().Should().Contain("custom failure").And.Contain("why");
    }
}
