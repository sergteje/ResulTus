// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ResulTus;
using ResulTus.AspNetCore;
using ResulTus.Validation;
using Xunit;

namespace ResulTus.AspNetCore.Tests;

/// <summary>
/// Tests for the validation bridge into RFC 9457 responses, covering
/// <see cref="ValidationResultHttpExtensions"/> and <c>ValidationProblemDetails</c> together These
/// classes were left uncovered in the aspnetcore module's dedicated measurements because their
/// behavior lives fully here — and fully covers both the fluent API and the errors contract.
/// </summary>
public class ValidationHttpResultTests
{
    /// <summary>
    /// GIVEN a fluent success carrying a payload
    /// WHEN <c>ToHttpResult</c> runs and executes
    /// THEN the response MUST be 200 OK holding the payload as its JSON body.
    /// </summary>
    [Fact]
    public async Task Success_ToHttpResult_ShouldBeOkWithThePayloadAsJson()
    {
        var result = Result<string, ValidationErrors>.Success("payload");

        var context = await HttpResponseProbe.ExecuteAsync(result.ToHttpResult());
        context.Response.StatusCode.Should().Be(200);

        using var body = await HttpResponseProbe.ReadJsonAsync(context);
        body.RootElement.GetString().Should().Be("payload");
    }

    /// <summary>
    /// GIVEN a fluent failure carrying two issues over one field plus an object-level problem
    /// WHEN <c>ToHttpResult</c> runs and executes
    /// THEN the response MUST be a 400 problem document whose <c>errors</c> member maps every
    /// field to every message, including the object-level empty-key entry.
    /// </summary>
    [Fact]
    public async Task Failure_ToHttpResult_ShouldProduceA400WithTheErrorsMap()
    {
        var failures = new ValidationErrors(
        [
            new ValidationIssue("Name", "required"),
            new ValidationIssue("Name", "too short"),
            new ValidationIssue("", "object-level problem"),
        ]);
        var result = Result<string, ValidationErrors>.Failure(failures);

        var context = await HttpResponseProbe.ExecuteAsync(result.ToHttpResult());
        context.Response.StatusCode.Should().Be(400);

        using var body = await HttpResponseProbe.ReadJsonAsync(context);
        var root = body.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("title").GetString().Should().Be("Bad Request");
        var errors = root.GetProperty("errors");
        errors.GetProperty("Name").GetArrayLength().Should().Be(2);
        errors.GetProperty("Name").EnumerateArray()
            .Select(m => m.GetString())
            .Should().Equal("required", "too short");
        errors.GetProperty("").GetArrayLength().Should().Be(1);
    }

    /// <summary>
    /// GIVEN a fluent failure
    /// WHEN <c>ToActionResult</c> runs
    /// THEN it MUST be a 400 ObjectResult carrying the problem document with the errors map.
    /// </summary>
    [Fact]
    public void Failure_ToActionResult_ShouldBe400ObjectResultWithTheErrorsMap()
    {
        var failures = new ValidationErrors([new ValidationIssue("Age", "negative")]);
        var result = Result<int, ValidationErrors>.Failure(failures);

        var objectResult = result.ToActionResult().Result.Should().BeOfType<ObjectResult>().Which;

        objectResult.StatusCode.Should().Be(400);
        var problem = objectResult.Value;
        problem.Should().NotBeNull();
        problem.Should().BeOfType<ProblemDetails>();
        ((ProblemDetails)problem!).Extensions.Should().ContainKey("errors");
    }

    // The ProblemDetails carry the validation state twice: as an extension map and with the
    // title, mapping the fluent failure to the ActionResult contract.

    /// <summary>
    /// GIVEN a fluent failure
    /// WHEN the result is deconstructed
    /// THEN it MUST expose the issues, a null payload and a false success flag.
    /// </summary>
    [Fact]
    public void Failure_Result_Deconstruct_ShouldExposeTheIssues()
    {        var failures = new ValidationErrors([new ValidationIssue("Name", "required")]);
        var result = Result<string, ValidationErrors>.Failure(failures);

        result.Deconstruct(out var isSuccess, out var value, out var errors);
        isSuccess.Should().BeFalse();
        value.Should().BeNull();
        errors!.HasErrors.Should().BeTrue();
    }
}
