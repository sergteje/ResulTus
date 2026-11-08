// Copyright (c) ResulTus. All rights reserved.

using System;

namespace ResulTus.Testing;

/// <summary>
/// The exception thrown when a <see cref="ResultAssertions"/> helper observes a result state that
/// contradicts the assertion.
/// </summary>
/// <remarks>
/// It derives from <see cref="Exception"/> rather than from a test-framework type on purpose: this
/// package has no dependency on xUnit, NUnit, MSTest or any assertion library, so it can be consumed
/// from any of them and from plain console code. Each test framework reports an unhandled exception as
/// a failure, which is exactly the behavior the helpers need.
/// </remarks>
public sealed class ResultAssertionException : Exception
{
    /// <summary>
    /// Initializes a new instance with a default message.
    /// </summary>
    public ResultAssertionException()
        : base("The result did not match the expected outcome.")
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified message.
    /// </summary>
    /// <param name="message">The message that describes the failed assertion.</param>
    public ResultAssertionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the failed assertion.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public ResultAssertionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
