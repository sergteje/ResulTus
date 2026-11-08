// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ResulTus.AspNetCore.Tests;

/// <summary>
/// Tests for the default <see cref="ErrorType"/> to HTTP status mapping and for the
/// ProblemDetails document produced on the failure path.
/// </summary>
public class ErrorTypeStatusMappingTests
{
    /// <summary>
    /// The expected status for every defined <see cref="ErrorType"/>. Written out here
    /// deliberately instead of reusing the production table, so the test proves the mapping
    /// rather than echoing it.
    /// </summary>
    private static readonly Dictionary<ErrorType, int> Expected = new()
    {
        [ErrorType.None] = 500,
        [ErrorType.Failure] = 500,
        [ErrorType.Validation] = 400,
        [ErrorType.NotFound] = 404,
        [ErrorType.Conflict] = 409,
        [ErrorType.Unauthorized] = 401,
        [ErrorType.Forbidden] = 403,
        [ErrorType.Timeout] = 504,
        [ErrorType.Cancelled] = 499,
        [ErrorType.Unexpected] = 500,
    };

    /// <summary>
    /// Provides every defined <see cref="ErrorType"/> with its expected status.
    /// </summary>
    /// <returns>One row per enum value.</returns>
    public static IEnumerable<object[]> AllMappings()
    {
        foreach (var pair in Expected)
        {
            yield return new object[] { pair.Key, pair.Value };
        }
    }

    /// <summary>
    /// GIVEN every defined <see cref="ErrorType"/>
    /// WHEN the default mapping is asked for its status
    /// THEN it MUST return the documented status, proving the mapping is total over the enum.
    /// </summary>
    /// <param name="type">The error type under test.</param>
    /// <param name="expected">The expected HTTP status.</param>
    [Theory]
    [MemberData(nameof(AllMappings))]
    public void GetStatusCode_ShouldMapEveryErrorType(ErrorType type, int expected)
    {
        ResultHttpStatus.GetStatusCode(type).Should().Be(expected);
    }

    /// <summary>
    /// GIVEN a failed result of every <see cref="ErrorType"/>
    /// WHEN it is converted to an <see cref="IResult"/> and executed
    /// THEN the response status MUST equal the mapped status.
    /// </summary>
    /// <param name="type">The error type under test.</param>
    /// <param name="expected">The expected HTTP status.</param>
    [Theory]
    [MemberData(nameof(AllMappings))]
    public async Task ToHttpResult_OnFailure_ShouldUseTheMappedStatus(ErrorType type, int expected)
    {
        var error = CreateError(type);

        var context = await HttpResponseProbe.ExecuteAsync(Result.Failure(error).ToHttpResult());

        context.Response.StatusCode.Should().Be(expected);
    }

    /// <summary>
    /// GIVEN a failed result carrying a message
    /// WHEN it is converted to an <see cref="IResult"/> and executed
    /// THEN the body MUST be a ProblemDetails document whose status, title, detail and code
    /// describe the error.
    /// </summary>
    [Fact]
    public async Task ToHttpResult_OnFailure_ShouldProduceProblemDetails()
    {
        var error = Error.NotFound("missing", "not there");

        var context = await HttpResponseProbe.ExecuteAsync(Result.Failure(error).ToHttpResult());
        using var body = await HttpResponseProbe.ReadJsonAsync(context);

        body.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        body.RootElement.GetProperty("title").GetString().Should().Be("Not Found");
        body.RootElement.GetProperty("detail").GetString().Should().Be("not there");
        body.RootElement.GetProperty("code").GetString().Should().Be("missing");
    }

    /// <summary>
    /// GIVEN a failed result whose error carries no message
    /// WHEN it is converted and executed
    /// THEN the ProblemDetails MUST omit the <c>detail</c> member entirely, and MUST NOT
    /// substitute an empty string.
    /// </summary>
    [Fact]
    public async Task ToHttpResult_OnFailureWithoutMessage_ShouldOmitDetail()
    {
        var error = Error.Conflict("duplicate");

        var context = await HttpResponseProbe.ExecuteAsync(Result.Failure(error).ToHttpResult());
        using var body = await HttpResponseProbe.ReadJsonAsync(context);

        body.RootElement.TryGetProperty("detail", out _).Should().BeFalse();
        body.RootElement.GetProperty("code").GetString().Should().Be("duplicate");
    }

    /// <summary>
    /// GIVEN a failed result whose mapped status is a standard HTTP status
    /// WHEN it is converted and executed
    /// THEN the ProblemDetails MUST carry the RFC 9110 section URI the framework supplies as its
    /// <c>type</c>. This asserts the framework default is preserved rather than overridden with an
    /// invented URI scheme.
    /// </summary>
    [Fact]
    public async Task ToHttpResult_OnFailure_ShouldLetTheFrameworkSupplyProblemType()
    {
        var context = await HttpResponseProbe.ExecuteAsync(Result.Failure(Error.NotFound("missing")).ToHttpResult());
        using var body = await HttpResponseProbe.ReadJsonAsync(context);

        body.RootElement.GetProperty("type").GetString()
            .Should().StartWith("https://tools.ietf.org/html/rfc9110#section-");
    }

    /// <summary>
    /// GIVEN a failed result whose mapped status is not a standard HTTP status (499, the mapped
    /// status for <see cref="ErrorType.Cancelled"/>)
    /// WHEN it is converted and executed
    /// THEN the ProblemDetails MUST omit <c>type</c> entirely, because there is no RFC 9110
    /// section for it and writing a fabricated URI would be worse than writing nothing.
    /// </summary>
    [Fact]
    public async Task ToHttpResult_OnNonStandardStatus_ShouldOmitProblemType()
    {
        var context = await HttpResponseProbe.ExecuteAsync(Result.Failure(Error.Cancelled("cancelled")).ToHttpResult());
        using var body = await HttpResponseProbe.ReadJsonAsync(context);

        body.RootElement.TryGetProperty("type", out _).Should().BeFalse();
        body.RootElement.GetProperty("title").GetString().Should().Be("Client Closed Request");
        body.RootElement.GetProperty("status").GetInt32().Should().Be(499);
    }

    /// <summary>
    /// Creates a representative error for the given type, so every type can be exercised.
    /// </summary>
    /// <param name="type">The error type to build.</param>
    /// <returns>An error of that type.</returns>
    private static Error CreateError(ErrorType type) => type switch
    {
        ErrorType.None => Error.None,
        ErrorType.Failure => Error.Failure("failure"),
        ErrorType.Validation => Error.Validation("validation"),
        ErrorType.NotFound => Error.NotFound("missing"),
        ErrorType.Conflict => Error.Conflict("duplicate"),
        ErrorType.Unauthorized => Error.Unauthorized("auth"),
        ErrorType.Forbidden => Error.Forbidden("forbidden"),
        ErrorType.Timeout => Error.Timeout("slow"),
        ErrorType.Cancelled => Error.Cancelled("cancelled"),
        ErrorType.Unexpected => Error.Unexpected("unexpected"),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}

/// <summary>
/// Executes an <see cref="IResult"/> against a <see cref="DefaultHttpContext"/> and exposes the
/// resulting status code and body, so the real response can be asserted without a test server.
/// </summary>
internal static class HttpResponseProbe
{
    /// <summary>
    /// The services the framework's result types resolve at execution time. Without them,
    /// <c>ProblemHttpResult</c> and the JSON writers fail on a bare <see cref="DefaultHttpContext"/>.
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
        var context = new DefaultHttpContext
        {
            RequestServices = Services,
        };
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
        return await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
    }
}
