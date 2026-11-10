// Copyright (c) ResulTus. All rights reserved.

using System;
using ResulTus;
using Xunit;

namespace ResulTus.Tests;

/// <summary>
/// Tests for the public surface of <see cref="ErrorFactoryAttribute"/>: the constructor's
/// contract, its three getters, and the reject-A-null-code rule the generator depends on.
/// </summary>
public class ErrorFactoryAttributeTests
{
    /// <summary>
    /// GIVEN a code, a type and a message template
    /// WHEN the attribute is constructed
    /// THEN it MUST expose all three values verbatim.
    /// </summary>
    [Fact]
    public void Constructor_ShouldExposeAllThreeValues()
    {
        var attribute = new ErrorFactoryAttribute("USER_NOT_FOUND", ErrorType.NotFound, "User {id} not found");

        attribute.Code.Should().Be("USER_NOT_FOUND");
        attribute.Type.Should().Be(ErrorType.NotFound);
        attribute.Message.Should().Be("User {id} not found");
    }

    /// <summary>
    /// GIVEN a null message template
    /// WHEN the attribute is constructed
    /// THEN it MUST keep the null, because a generated error may have no message.
    /// </summary>
    [Fact]
    public void Constructor_WithNullMessage_ShouldKeepItNull()
    {
        var attribute = new ErrorFactoryAttribute("E_X", ErrorType.Failure, null);

        attribute.Message.Should().BeNull();
        attribute.Code.Should().Be("E_X");
        attribute.Type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// GIVEN a null code
    /// WHEN the attribute is constructed
    /// THEN it MUST throw <see cref="ArgumentNullException"/>, because every generated error
    /// carries a non-null code.
    /// </summary>
    [Fact]
    public void Constructor_WithNullCode_ShouldThrow()
    {
        var code = null as string;
        Assert.Throws<ArgumentNullException>(() => new ErrorFactoryAttribute(code!, ErrorType.Failure, null));
    }
}
