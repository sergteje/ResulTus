// Copyright (c) ResulTus. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;

namespace ResulTus.Testing.Tests;

/// <summary>
/// Tests for the framework-agnostic assertion helpers in <see cref="ResultAssertions"/>.
/// </summary>
public class ResultAssertionsTests
{
    /// <summary>
    /// GIVEN a successful unit result
    /// WHEN <c>ShouldSucceed</c> runs
    /// THEN it MUST return the receiver without throwing.
    /// </summary>
    [Fact]
    public void UnitShouldSucceed_OnSuccess_ShouldReturnTheReceiver()
    {
        var returned = Result.Success().ShouldSucceed();

        returned.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a failed unit result
    /// WHEN <c>ShouldSucceed</c> runs
    /// THEN it MUST throw with the error code, type and message in the text.
    /// </summary>
    [Fact]
    public void UnitShouldSucceed_OnFailure_ShouldThrowWithTheErrorDetails()
    {
        Action act = () => _ = Result.Failure(Error.NotFound("user.missing", "no such user")).ShouldSucceed();

        act.Should().Throw<ResultAssertionException>()
            .Which.Message.Should().Contain("user.missing")
            .And.Contain("NotFound")
            .And.Contain("no such user");
    }

    /// <summary>
    /// GIVEN a failed unit result
    /// WHEN <c>ShouldFail</c> runs
    /// THEN it MUST return the error it carries.
    /// </summary>
    [Fact]
    public void UnitShouldFail_OnFailure_ShouldReturnTheError()
    {
        var expected = Error.Timeout("slow");

        var returned = Result.Failure(expected).ShouldFail();

        returned.Should().Be(expected);
    }

    /// <summary>
    /// GIVEN a successful unit result
    /// WHEN <c>ShouldFail</c> runs
    /// THEN it MUST throw.
    /// </summary>
    [Fact]
    public void UnitShouldFail_OnSuccess_ShouldThrow()
    {
        Action act = () => _ = Result.Success().ShouldFail();

        act.Should().Throw<ResultAssertionException>();
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN <c>ShouldSucceed</c> runs
    /// THEN it MUST return the success value.
    /// </summary>
    [Fact]
    public void ShouldSucceed_OnSuccess_ShouldReturnTheValue()
    {
        Result<int> result = 42;

        result.ShouldSucceed().Should().Be(42);
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>ShouldSucceed</c> runs
    /// THEN it MUST throw with the error details in the text.
    /// </summary>
    [Fact]
    public void ShouldSucceed_OnFailure_ShouldThrowWithTheErrorDetails()
    {
        var result = Result<int>.Failure(Error.Conflict("duplicate", "already there"));

        Action act = () => _ = result.ShouldSucceed();

        act.Should().Throw<ResultAssertionException>()
            .Which.Message.Should().Contain("duplicate")
            .And.Contain("Conflict")
            .And.Contain("already there");
    }

    /// <summary>
    /// GIVEN a failed <c>Result&lt;int&gt;</c>
    /// WHEN <c>ShouldFail</c> runs
    /// THEN it MUST return the error it carries.
    /// </summary>
    [Fact]
    public void ShouldFail_OnFailure_ShouldReturnTheError()
    {
        var expected = Error.Unauthorized("denied");
        var result = Result<int>.Failure(expected);

        result.ShouldFail().Should().Be(expected);
    }

    /// <summary>
    /// GIVEN a successful <c>Result&lt;int&gt;</c>
    /// WHEN <c>ShouldFail</c> runs
    /// THEN it MUST throw without putting the success value in the message.
    /// </summary>
    [Fact]
    public void ShouldFail_OnSuccess_ShouldThrowWithoutLeakingTheValue()
    {
        Result<string> result = "super-secret-token";

        Action act = () => _ = result.ShouldFail();

        act.Should().Throw<ResultAssertionException>()
            .Which.Message.Should().NotContain("super-secret-token");
    }

    /// <summary>
    /// GIVEN a successful typed result
    /// WHEN <c>ShouldSucceed</c> runs
    /// THEN it MUST return the success value.
    /// </summary>
    [Fact]
    public void TypedShouldSucceed_OnSuccess_ShouldReturnTheValue()
    {
        var result = Result<int, DomainError>.Success(42);

        result.ShouldSucceed().Should().Be(42);
    }

    /// <summary>
    /// GIVEN a failed typed result
    /// WHEN <c>ShouldSucceed</c> runs
    /// THEN it MUST throw with the typed error rendered in the text.
    /// </summary>
    [Fact]
    public void TypedShouldSucceed_OnFailure_ShouldThrowWithTheTypedError()
    {
        var result = Result<int, DomainError>.Failure(new DomainError(404, "missing"));

        Action act = () => _ = result.ShouldSucceed();

        act.Should().Throw<ResultAssertionException>()
            .Which.Message.Should().Contain("404")
            .And.Contain("missing");
    }

    /// <summary>
    /// GIVEN a failed typed result
    /// WHEN <c>ShouldFail</c> runs
    /// THEN it MUST return the exact typed error value.
    /// </summary>
    [Fact]
    public void TypedShouldFail_OnFailure_ShouldReturnTheExactTypedError()
    {
        var expected = new DomainError(500, "boom");
        var result = Result<int, DomainError>.Failure(expected);

        result.ShouldFail().Should().Be(expected);
    }

    /// <summary>
    /// GIVEN a successful typed result
    /// WHEN <c>ShouldFail</c> runs
    /// THEN it MUST throw.
    /// </summary>
    [Fact]
    public void TypedShouldFail_OnSuccess_ShouldThrow()
    {
        Action act = () => _ = Result<string, DomainError>.Success("value").ShouldFail();

        act.Should().Throw<ResultAssertionException>();
    }

    /// <summary>
    /// GIVEN <c>default(Result)</c>
    /// WHEN <c>ShouldFail</c> runs
    /// THEN it MUST return <see cref="Error.None"/>, because the default is a failure.
    /// </summary>
    [Fact]
    public void DefaultUnitResult_ShouldBeAFailureCarryingNone()
    {
        var returned = default(Result).ShouldFail();

        returned.Should().Be(Error.None);
        returned.Type.Should().Be(ErrorType.None);
    }

    /// <summary>
    /// GIVEN any unexpected state
    /// WHEN an assertion fails
    /// THEN the thrown exception MUST be a <see cref="ResultAssertionException"/> with a message.
    /// </summary>
    [Fact]
    public void AssertionFailure_ShouldBeAResultAssertionException()
    {
        Action act = () => _ = Result.Success().ShouldFail();

        act.Should().Throw<ResultAssertionException>()
            .Which.Message.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// GIVEN the shipped testing assembly
    /// THEN it MUST implement the Testing framework-independent standard; the repository
    /// multi-targets libraries on net8.0/net9.0/net10.0, so the test runner loads whichever
    /// target the consumer's graph selects — every one of them must answer the same name.
    /// </summary>
    [Fact]
    public void Package_ShouldTargetNet8()
    {
        var attribute = typeof(ResultAssertions).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        attribute.Should().NotBeNull();
        attribute!.FrameworkName.Should().Be(".NETCoreApp,Version=v" + Environment.Version.Major + ".0");
    }

    /// <summary>
    /// GIVEN the shipped testing assembly
    /// THEN it MUST NOT depend on a test framework, because it is consumed from any of them.
    /// </summary>
    [Fact]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Reading the reference list of a test assembly in a plain unit test, not consuming it in trimmed code.")]
    public void Package_ShouldNotReferenceATestFramework()
    {
        var referenced = typeof(ResultAssertions).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        referenced.Should().NotContain(name =>
            name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("FluentAssertions", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("MSTest", StringComparison.OrdinalIgnoreCase));
    }

    private readonly record struct DomainError(int Code, string Message);
}
