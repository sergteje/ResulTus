// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ResulTus.AspNetCore.Tests;

/// <summary>
/// Tests for the success paths of both adapters, and for the guarantee that nothing is lost
/// on the way to the response.
/// </summary>
public class SuccessPathTests
{
    /// <summary>
    /// GIVEN a successful unit <see cref="Result"/>
    /// WHEN it is converted to an <see cref="IResult"/> and executed
    /// THEN the response MUST be <c>200 OK</c> with an empty body.
    /// </summary>
    [Fact]
    public async Task UnitSuccess_ToHttpResult_ShouldBe200WithNoBody()
    {
        var context = await HttpResponseProbe.ExecuteAsync(Result.Success().ToHttpResult());

        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Length.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result{T}"/>
    /// WHEN it is converted to an <see cref="IResult"/> and executed
    /// THEN the response MUST be <c>200 OK</c> and the body MUST be the value as JSON.
    /// </summary>
    [Fact]
    public async Task ValueSuccess_ToHttpResult_ShouldBe200WithTheValue()
    {
        var context = await HttpResponseProbe.ExecuteAsync(Result<int>.Success(42).ToHttpResult());
        using var body = await HttpResponseProbe.ReadJsonAsync(context);

        context.Response.StatusCode.Should().Be(200);
        body.RootElement.GetInt32().Should().Be(42);
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result{T}"/> holding <c>null</c>
    /// WHEN it is converted and executed
    /// THEN the response MUST be <c>200 OK</c> with an empty body. ASP.NET Core's <c>Ok&lt;T&gt;</c>
    /// writes nothing when the value is null; the operation still succeeded, there is simply
    /// nothing to serialize. Forcing a literal <c>null</c> body would mean a custom <see cref="IResult"/>
    /// and losing the response metadata the framework emits for OpenAPI.
    /// </summary>
    [Fact]
    public async Task NullValueSuccess_ToHttpResult_ShouldBe200WithEmptyBody()
    {
        var context = await HttpResponseProbe.ExecuteAsync(Result<string?>.Success(null).ToHttpResult());

        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Length.Should().Be(0);
    }

    /// <summary>
    /// GIVEN a successful unit <see cref="Result"/>
    /// WHEN it is converted to an MVC action result
    /// THEN it MUST be an <see cref="OkResult"/>, which is a 200 with no body.
    /// </summary>
    [Fact]
    public void UnitSuccess_ToActionResult_ShouldBeOkResult()
    {
        var actionResult = Result.Success().ToActionResult();

        var ok = actionResult.Should().BeOfType<OkResult>().Subject;
        ok.StatusCode.Should().Be(200);
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result{T}"/>
    /// WHEN it is converted to an MVC action result
    /// THEN it MUST carry the value as an <see cref="OkObjectResult"/>.
    /// </summary>
    [Fact]
    public void ValueSuccess_ToActionResult_ShouldCarryTheValue()
    {
        var actionResult = Result<int>.Success(42).ToActionResult();

        actionResult.Value.Should().Be(42);
        actionResult.Result.Should().BeNull();
    }

    /// <summary>
    /// GIVEN a failed unit <see cref="Result"/>
    /// WHEN it is converted to an MVC action result
    /// THEN it MUST be an <see cref="ObjectResult"/> carrying the mapped status and the
    /// ProblemDetails document.
    /// </summary>
    [Fact]
    public void UnitFailure_ToActionResult_ShouldBeObjectResultWithProblemDetails()
    {
        var error = Error.NotFound("missing", "not there");

        var actionResult = Result.Failure(error).ToActionResult();

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);

        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Not Found");
        problem.Detail.Should().Be("not there");
        problem.Extensions["code"].Should().Be("missing");
    }

    /// <summary>
    /// GIVEN a failed <see cref="Result{T}"/>
    /// WHEN it is converted to an MVC action result
    /// THEN its inner result MUST be an <see cref="ObjectResult"/> with the mapped status and
    /// ProblemDetails, and it MUST NOT expose a value.
    /// </summary>
    [Fact]
    public void ValueFailure_ToActionResult_ShouldWrapObjectResult()
    {
        var error = Error.Validation("bad", "value too small");

        var actionResult = Result<int>.Failure(error).ToActionResult();

        actionResult.Value.Should().Be(0);
        var objectResult = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);

        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("value too small");
        problem.Extensions["code"].Should().Be("bad");
    }

    /// <summary>
    /// GIVEN a failed result whose error carries no message
    /// WHEN it is converted to an MVC action result
    /// THEN the ProblemDetails <c>Detail</c> MUST stay <c>null</c>, not become an empty string.
    /// </summary>
    [Fact]
    public void FailureWithoutMessage_ToActionResult_ShouldKeepDetailNull()
    {
        var actionResult = Result.Failure(Error.Conflict("duplicate")).ToActionResult();

        var problem = actionResult.Should().BeOfType<ObjectResult>().Subject
            .Value.Should().BeOfType<ProblemDetails>().Subject;

        problem.Detail.Should().BeNull();
    }

    /// <summary>
    /// GIVEN a successful <see cref="Result{T}"/> over a reference type
    /// WHEN it is converted and executed
    /// THEN the value MUST survive the trip untouched.
    /// </summary>
    [Fact]
    public async Task ReferenceTypeValue_ShouldSurviveUntouched()
    {
        var context = await HttpResponseProbe.ExecuteAsync(Result<string>.Success("ok").ToHttpResult());
        using var body = await HttpResponseProbe.ReadJsonAsync(context);

        body.RootElement.GetString().Should().Be("ok");
    }
}
