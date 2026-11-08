// Copyright (c) ResulTus. All rights reserved.

using Microsoft.AspNetCore.Mvc;

namespace ResulTus.AspNetCore.Tests;

/// <summary>
/// Tests for the custom status selector, which lets a consumer replace the default mapping.
/// </summary>
public class CustomStatusSelectorTests
{
    /// <summary>
    /// GIVEN a failed unit <see cref="Result"/> and a selector that always returns 418
    /// WHEN it is converted to an <see cref="Microsoft.AspNetCore.Http.IResult"/> and executed
    /// THEN the response status MUST be 418, proving the selector wins over the default mapping.
    /// </summary>
    [Fact]
    public async Task UnitResult_ToHttpResult_ShouldHonourTheSelector()
    {
        var context = await HttpResponseProbe.ExecuteAsync(
            Result.Failure(Error.NotFound("missing")).ToHttpResult(_ => 418));

        context.Response.StatusCode.Should().Be(418);
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result{T}"/> and a selector that always returns 422
    /// WHEN it is converted and executed
    /// THEN the response status MUST be 422.
    /// </summary>
    [Fact]
    public async Task ValueResult_ToHttpResult_ShouldHonourTheSelector()
    {
        var context = await HttpResponseProbe.ExecuteAsync(
            Result<int>.Failure(Error.Validation("bad")).ToHttpResult(_ => 422));

        context.Response.StatusCode.Should().Be(422);
    }

    /// <summary>
    /// GIVEN a failed unit <see cref="Result"/> and a selector returning 418
    /// WHEN it is converted to an MVC action result
    /// THEN the object result status MUST be 418.
    /// </summary>
    [Fact]
    public void UnitResult_ToActionResult_ShouldHonourTheSelector()
    {
        var actionResult = Result.Failure(Error.NotFound("missing")).ToActionResult(_ => 418);

        actionResult.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(418);
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result{T}"/> and a selector returning 422
    /// WHEN it is converted to an MVC action result
    /// THEN the inner object result status MUST be 422.
    /// </summary>
    [Fact]
    public void ValueResult_ToActionResult_ShouldHonourTheSelector()
    {
        var actionResult = Result<int>.Failure(Error.Validation("bad")).ToActionResult(_ => 422);

        actionResult.Result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(422);
    }

    /// <summary>
    /// GIVEN a selector that inspects the error
    /// WHEN the result is converted
    /// THEN the selector MUST receive the whole <see cref="Error"/>, not just its type, so
    /// consumers can map on the code as well.
    /// </summary>
    [Fact]
    public async Task Selector_ShouldReceiveTheWholeError()
    {
        var observed = default(Error);
        var invoked = false;
        var error = Error.Conflict("duplicate", "already exists");

        await HttpResponseProbe.ExecuteAsync(
            Result.Failure(error).ToHttpResult(e =>
            {
                observed = e;
                invoked = true;
                return 409;
            }));

        invoked.Should().BeTrue();
        observed.Should().Be(error);
        observed.Code.Should().Be("duplicate");
        observed.Message.Should().Be("already exists");
        observed.Type.Should().Be(ErrorType.Conflict);
    }

    /// <summary>
    /// GIVEN a selector that returns a non-standard status
    /// WHEN the result is converted and executed
    /// THEN the ProblemDetails MUST report that status and MUST omit <c>type</c>, since there is
    /// no RFC section to point at.
    /// </summary>
    [Fact]
    public async Task Selector_WithNonStandardStatus_ShouldProduceConsistentProblemDetails()
    {
        var context = await HttpResponseProbe.ExecuteAsync(
            Result.Failure(Error.NotFound("missing")).ToHttpResult(_ => 599));
        using var body = await HttpResponseProbe.ReadJsonAsync(context);

        body.RootElement.GetProperty("status").GetInt32().Should().Be(599);
        body.RootElement.TryGetProperty("type", out _).Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a <c>null</c> selector
    /// WHEN any of the four conversion overloads is called
    /// THEN it MUST throw <see cref="ArgumentNullException"/> instead of silently falling back
    /// to the default mapping.
    /// </summary>
    [Fact]
    public void NullSelector_ShouldThrow()
    {
        var unit = Result.Success();
        var value = Result<int>.Success(1);

        var fromUnitHttp = () => unit.ToHttpResult(null!);
        var fromValueHttp = () => value.ToHttpResult(null!);
        var fromUnitMvc = () => unit.ToActionResult(null!);
        var fromValueMvc = () => value.ToActionResult(null!);

        fromUnitHttp.Should().Throw<ArgumentNullException>();
        fromValueHttp.Should().Throw<ArgumentNullException>();
        fromUnitMvc.Should().Throw<ArgumentNullException>();
        fromValueMvc.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN a successful result and a custom selector
    /// WHEN it is converted
    /// THEN the selector MUST NOT be invoked, because there is no error to map.
    /// </summary>
    [Fact]
    public async Task Selector_ShouldNotBeInvokedOnSuccess()
    {
        var invoked = false;

        var context = await HttpResponseProbe.ExecuteAsync(
            Result.Success().ToHttpResult(_ =>
            {
                invoked = true;
                return 418;
            }));

        invoked.Should().BeFalse();
        context.Response.StatusCode.Should().Be(200);
    }
}
