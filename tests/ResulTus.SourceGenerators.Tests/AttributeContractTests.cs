// Copyright (c) ResulTus. All rights reserved.

using System.Reflection;

namespace ResulTus.SourceGenerators.Tests;

/// <summary>
/// Tests for the two attributes the generator reads. They are public API in the core assembly, so
/// their usage surface is a contract: widening it silently would let a consumer annotate something
/// the generator never looks at.
/// </summary>
public class AttributeContractTests
{
    /// <summary>
    /// GIVEN <c>[ResultErrors]</c>
    /// WHEN its usage is read
    /// THEN it MUST apply to classes and structs only, and MUST NOT be inherited or repeatable,
    /// because a derived type that silently opted in would generate factories it never declared.
    /// </summary>
    [Fact]
    public void ResultErrors_ShouldTargetOnlyTypesItCanGenerateFor()
    {
        var usage = typeof(ResultErrorsAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>()!;

        usage.ValidOn.Should().Be(AttributeTargets.Class | AttributeTargets.Struct);
        usage.Inherited.Should().BeFalse();
        usage.AllowMultiple.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN <c>[ErrorFactory]</c>
    /// WHEN its usage is read
    /// THEN it MUST apply to methods only, and MUST NOT be inherited or repeatable, because two
    /// code values for one method have no meaning.
    /// </summary>
    [Fact]
    public void ErrorFactory_ShouldTargetOnlyMethods()
    {
        var usage = typeof(ErrorFactoryAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>()!;

        usage.ValidOn.Should().Be(AttributeTargets.Method);
        usage.Inherited.Should().BeFalse();
        usage.AllowMultiple.Should().BeFalse();
    }

    /// <summary>
    /// GIVEN an <c>[ErrorFactory]</c> instance
    /// WHEN its values are read
    /// THEN they MUST be the three values it was constructed with, unchanged.
    /// </summary>
    [Fact]
    public void ErrorFactory_ShouldExposeTheValuesItWasGiven()
    {
        var attribute = new ErrorFactoryAttribute("USER_NOT_FOUND", ErrorType.NotFound, "User {id} not found");

        attribute.Code.Should().Be("USER_NOT_FOUND");
        attribute.Type.Should().Be(ErrorType.NotFound);
        attribute.Message.Should().Be("User {id} not found");
    }

    /// <summary>
    /// GIVEN an <c>[ErrorFactory]</c> constructed without a message
    /// WHEN its message is read
    /// THEN it MUST stay <c>null</c>, because an error with no message is not an error with an
    /// empty one, and the two are distinct on <see cref="Error.Message"/>.
    /// </summary>
    [Fact]
    public void ErrorFactory_ShouldKeepAMissingMessageNull()
    {
        var attribute = new ErrorFactoryAttribute("USER_NOT_FOUND", ErrorType.NotFound, null);

        attribute.Message.Should().BeNull();
    }

    /// <summary>
    /// GIVEN a <c>null</c> code
    /// WHEN an <c>[ErrorFactory]</c> is constructed
    /// THEN it MUST throw, because an error without a code cannot be identified at the call site.
    /// </summary>
    [Fact]
    public void ErrorFactory_ShouldRejectANullCode()
    {
        var construct = () => new ErrorFactoryAttribute(null!, ErrorType.Failure, "message");

        construct.Should().Throw<ArgumentNullException>().WithParameterName("code");
    }
}
