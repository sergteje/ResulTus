// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Tests;

/// <summary>
/// Tests for <see cref="ErrorType"/> enum.
/// </summary>
public class ErrorTypeTests
{
    /// <summary>
    /// GIVEN <c>default(ErrorType)</c>
    /// WHEN evaluating
    /// THEN it MUST equal <c>ErrorType.None</c>.
    /// </summary>
    [Fact]
    public void Default_ShouldBeNone()
    {
        default(ErrorType).Should().Be(ErrorType.None);
    }

    /// <summary>
    /// GIVEN every <c>ErrorType</c> value
    /// WHEN compared by underlying integer
    /// THEN each MUST be unique.
    /// </summary>
    [Fact]
    public void AllValues_ShouldBeDistinct()
    {
        var values = Enum.GetValues<ErrorType>();
        values.Select(e => (byte)e).Should().OnlyHaveUniqueItems();
    }
}
