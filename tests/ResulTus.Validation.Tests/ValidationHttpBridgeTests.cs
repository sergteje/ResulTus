// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ResulTus.Validation.Tests;

/// <summary>
/// Tests for the bridge that renders a <see cref="Result{TSuccess,TError}"/> of
/// <see cref="ValidationErrors"/> as the RFC 9457 <c>errors</c> extension.
/// </summary>
public class ValidationHttpBridgeTests
{
    /// <summary>
    /// GIVEN a failed validation result
    /// WHEN it is converted to an <see cref="IResult"/> and executed
    /// THEN the response MUST be <c>400 Bad Request</c> with a ProblemDetails body whose
    /// <c>errors</c> member maps each field to its messages.
    /// </summary>
    [Fact]
    public async Task Failure_ToHttpResult_ShouldBe400WithErrorsMap()
    {
        var errors = new ValidationErrors(
        [
            new ValidationIssue("Name", "Name is required."),
            new ValidationIssue("Age", "Age must be 18 or older."),
        ]);

        var context = await Probe.ExecuteAsync(Result<Customer, ValidationErrors>.Failure(errors).ToHttpResult());
        using var body = await Probe.ReadJsonAsync(context);

        context.Response.StatusCode.Should().Be(400);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        body.RootElement.GetProperty("title").GetString().Should().Be("Bad Request");

        var map = body.RootElement.GetProperty("errors");
        map.GetProperty("Name")[0].GetString().Should().Be("Name is required.");
        map.GetProperty("Age")[0].GetString().Should().Be("Age must be 18 or older.");
    }

    /// <summary>
    /// GIVEN two failures on the same field
    /// WHEN the result is executed
    /// THEN both messages MUST appear under that field, in order, proving nothing is dropped.
    /// </summary>
    [Fact]
    public async Task Failure_WithRepeatedField_ShouldKeepEveryMessage()
    {
        var errors = new ValidationErrors(
        [
            new ValidationIssue("Name", "first"),
            new ValidationIssue("Name", "second"),
        ]);

        var context = await Probe.ExecuteAsync(Result<Customer, ValidationErrors>.Failure(errors).ToHttpResult());
        using var body = await Probe.ReadJsonAsync(context);

        var messages = body.RootElement.GetProperty("errors").GetProperty("Name");
        messages.GetArrayLength().Should().Be(2);
        messages[0].GetString().Should().Be("first");
        messages[1].GetString().Should().Be("second");
    }

    /// <summary>
    /// GIVEN an object-level failure
    /// WHEN the result is executed
    /// THEN it MUST land under the empty-string key, which is the convention ASP.NET Core's
    /// <c>ModelState</c> uses.
    /// </summary>
    [Fact]
    public async Task ObjectLevelFailure_ShouldUseTheEmptyKey()
    {
        var errors = new ValidationErrors([new ValidationIssue(string.Empty, "the object is invalid")]);

        var context = await Probe.ExecuteAsync(Result<Customer, ValidationErrors>.Failure(errors).ToHttpResult());
        using var body = await Probe.ReadJsonAsync(context);

        body.RootElement.GetProperty("errors").GetProperty(string.Empty)[0].GetString()
            .Should().Be("the object is invalid");
    }

    /// <summary>
    /// GIVEN a successful validation result
    /// WHEN it is converted and executed
    /// THEN the response MUST be <c>200 OK</c> carrying the value, and MUST NOT carry an
    /// <c>errors</c> member.
    /// </summary>
    [Fact]
    public async Task Success_ToHttpResult_ShouldBe200WithTheValue()
    {
        var customer = new Customer { Name = "Ada", Age = 36 };

        var context = await Probe.ExecuteAsync(Result<Customer, ValidationErrors>.Success(customer).ToHttpResult());
        using var body = await Probe.ReadJsonAsync(context);

        context.Response.StatusCode.Should().Be(200);
        body.RootElement.GetProperty("name").GetString().Should().Be("Ada");
        body.RootElement.GetProperty("age").GetInt32().Should().Be(36);
    }

    /// <summary>
    /// GIVEN a failed validation result
    /// WHEN it is converted to an MVC action result
    /// THEN it MUST be an <see cref="ObjectResult"/> whose status is 400 and whose value is the
    /// same ProblemDetails document.
    /// </summary>
    [Fact]
    public void Failure_ToActionResult_ShouldBeObjectResultWithErrors()
    {
        var errors = new ValidationErrors([new ValidationIssue("Name", "Name is required.")]);

        var actionResult = Result<Customer, ValidationErrors>.Failure(errors).ToActionResult();

        var objectResult = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);

        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(400);
        problem.Title.Should().Be("Bad Request");

        var map = problem.Extensions["errors"].Should().BeAssignableTo<IReadOnlyDictionary<string, string[]>>().Subject;
        map["Name"].Should().Equal("Name is required.");
    }

    /// <summary>
    /// GIVEN a successful validation result
    /// WHEN it is converted to an MVC action result
    /// THEN it MUST carry the value and expose no error.
    /// </summary>
    [Fact]
    public void Success_ToActionResult_ShouldCarryTheValue()
    {
        var customer = new Customer { Name = "Ada", Age = 36 };

        var actionResult = Result<Customer, ValidationErrors>.Success(customer).ToActionResult();

        actionResult.Value.Should().BeSameAs(customer);
        actionResult.Result.Should().BeNull();
    }

    /// <summary>
    /// Executes an <see cref="IResult"/> against a <see cref="DefaultHttpContext"/> and exposes the
    /// response, so the real status code and body can be asserted without a test server.
    /// </summary>
    private static class Probe
    {
        /// <summary>
        /// The services the framework's result types resolve at execution time.
        /// </summary>
        private static readonly IServiceProvider Services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .ConfigureHttpJsonOptions(_ => { })
            .BuildServiceProvider();

        /// <summary>
        /// Executes <paramref name="result"/> against a fresh <see cref="DefaultHttpContext"/>.
        /// </summary>
        /// <param name="result">The result to execute.</param>
        /// <returns>The context the result wrote to.</returns>
        internal static async Task<HttpContext> ExecuteAsync(IResult result)
        {
            var context = new DefaultHttpContext { RequestServices = Services };
            context.Response.Body = new MemoryStream();

            await result.ExecuteAsync(context);

            return context;
        }

        /// <summary>
        /// Reads the response body of <paramref name="context"/> as a JSON document.
        /// </summary>
        /// <param name="context">The context whose response body to read.</param>
        /// <returns>The parsed JSON document.</returns>
        internal static async Task<JsonDocument> ReadJsonAsync(HttpContext context)
        {
            context.Response.Body.Position = 0;
            return await JsonDocument.ParseAsync(
                context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        }
    }
}
