// Copyright (c) ResulTus. All rights reserved.

using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ResulTus.AspNetCore.Tests;

/// <summary>
/// Tests for the registration extension end to end: a handler mapped with <c>WithResultMapping()</c>
/// MUST produce the response the direct conversion produces, and a handler that returns something
/// else MUST keep behaving exactly as it did without the filter.
/// </summary>
public class MappedEndpointTests
{
    /// <summary>
    /// GIVEN a handler that returns a successful unit <see cref="Result"/>
    /// WHEN the route is mapped with <c>WithResultMapping()</c> and invoked
    /// THEN the response MUST be exactly the one the direct <c>ToHttpResult()</c> call produces in
    /// the same application.
    /// </summary>
    [Fact]
    public async Task UnitSuccess_ShouldMatchDirectToHttpResult()
    {
        await using var app = MappedApp.Create(routes =>
            routes.MapGet("/unit", () => Result.Success()).WithResultMapping());

        var mapped = await app.InvokeAsync("/unit");
        var direct = await app.ExecuteAsync(Result.Success().ToHttpResult());

        mapped.Status.Should().Be(direct.Status);
        mapped.Body.Should().Be(direct.Body);
    }

    /// <summary>
    /// GIVEN a handler that returns a failed unit <see cref="Result"/>
    /// WHEN the route is mapped with <c>WithResultMapping()</c> and invoked
    /// THEN the response MUST be exactly the one the direct <c>ToHttpResult()</c> call produces,
    /// ProblemDetails body included.
    /// </summary>
    [Fact]
    public async Task UnitFailure_ShouldMatchDirectToHttpResult()
    {
        await using var app = MappedApp.Create(routes => routes
            .MapGet("/unit", () => Result.Failure(Error.NotFound("missing", "not there")))
            .WithResultMapping());

        var mapped = await app.InvokeAsync("/unit");
        var direct = await app.ExecuteAsync(
            Result.Failure(Error.NotFound("missing", "not there")).ToHttpResult());

        mapped.Status.Should().Be(direct.Status);
        mapped.Body.Should().Be(direct.Body);
    }

    /// <summary>
    /// GIVEN a handler that returns a successful <see cref="Result{TSuccess}"/>
    /// WHEN the route is mapped with <c>WithResultMapping&lt;TSuccess&gt;()</c> and invoked
    /// THEN the response MUST carry the value exactly as the direct <c>ToHttpResult()</c> call does.
    /// </summary>
    [Fact]
    public async Task ValueSuccess_ShouldMatchDirectToHttpResult()
    {
        await using var app = MappedApp.Create(routes =>
            routes.MapGet("/value", () => Result<int>.Success(42)).WithResultMapping<int>());

        var mapped = await app.InvokeAsync("/value");
        var direct = await app.ExecuteAsync(Result<int>.Success(42).ToHttpResult());

        mapped.Status.Should().Be(direct.Status);
        mapped.Body.Should().Be(direct.Body);
    }

    /// <summary>
    /// GIVEN a handler that returns a failed <see cref="Result{TSuccess}"/>
    /// WHEN the route is mapped with <c>WithResultMapping&lt;TSuccess&gt;()</c> and invoked
    /// THEN the response MUST be exactly the one the direct <c>ToHttpResult()</c> call produces.
    /// </summary>
    [Fact]
    public async Task ValueFailure_ShouldMatchDirectToHttpResult()
    {
        await using var app = MappedApp.Create(routes => routes
            .MapGet("/value", () => Result<int>.Failure(Error.Validation("bad", "value too small")))
            .WithResultMapping<int>());

        var mapped = await app.InvokeAsync("/value");
        var direct = await app.ExecuteAsync(
            Result<int>.Failure(Error.Validation("bad", "value too small")).ToHttpResult());

        mapped.Status.Should().Be(direct.Status);
        mapped.Body.Should().Be(direct.Body);
    }

    /// <summary>
    /// GIVEN a handler that already returns an <see cref="IResult"/> of its own
    /// WHEN the route is mapped with the filter and invoked
    /// THEN the response MUST be identical to the same route mapped without the filter, because the
    /// filter returns a value it does not recognize untouched.
    /// </summary>
    [Fact]
    public async Task HandlerReturningIResult_ShouldBehaveAsWithoutTheFilter()
    {
        await using var app = MappedApp.Create(routes =>
        {
            routes.MapGet("/with-filter", () => Results.Ok("ok")).WithResultMapping();
            routes.MapGet("/without-filter", () => Results.Ok("ok"));
        });

        var withFilter = await app.InvokeAsync("/with-filter");
        var withoutFilter = await app.InvokeAsync("/without-filter");

        withFilter.Status.Should().Be(withoutFilter.Status);
        withFilter.Body.Should().Be(withoutFilter.Body);
    }

    /// <summary>
    /// GIVEN a handler that returns a plain DTO instead of a result
    /// WHEN the route is mapped with the filter and invoked
    /// THEN the response MUST be identical to the same route mapped without the filter, so the
    /// framework's own serialization still applies.
    /// </summary>
    [Fact]
    public async Task HandlerReturningDto_ShouldBehaveAsWithoutTheFilter()
    {
        await using var app = MappedApp.Create(routes =>
        {
            routes.MapGet("/with-filter", () => new Product("widget")).WithResultMapping();
            routes.MapGet("/without-filter", () => new Product("widget"));
        });

        var withFilter = await app.InvokeAsync("/with-filter");
        var withoutFilter = await app.InvokeAsync("/without-filter");

        withFilter.Status.Should().Be(withoutFilter.Status);
        withFilter.Body.Should().Be(withoutFilter.Body);
    }
}

/// <summary>
/// A plain DTO a handler can return instead of a result, used to prove the filter leaves a
/// response the framework serializes on its own untouched.
/// </summary>
/// <param name="Name">The product name.</param>
public sealed record Product(string Name);

/// <summary>
/// Hosts a Minimal API route set without a test server: it builds the endpoint pipeline the
/// framework builds, then invokes a route's request delegate against a
/// <see cref="DefaultHttpContext"/>, filters included, which is enough to observe the response.
/// </summary>
internal sealed class MappedApp : IAsyncDisposable
{
    private readonly WebApplication _app;

    private MappedApp(WebApplication app) => _app = app;

    /// <summary>
    /// Builds an application and maps its routes, without starting the server.
    /// </summary>
    /// <param name="map">Maps the routes the test exercises.</param>
    /// <returns>The application, ready to be invoked.</returns>
    internal static MappedApp Create(Action<WebApplication> map)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(_ => { });

        var app = builder.Build();
        map(app);

        return new MappedApp(app);
    }

    /// <summary>
    /// Invokes the route mapped at <paramref name="path"/>, filters included.
    /// </summary>
    /// <param name="path">The literal route pattern the test mapped.</param>
    /// <returns>The status code and body the request delegate wrote.</returns>
    internal async Task<EndpointResponse> InvokeAsync(string path)
    {
        var endpoint = ((IEndpointRouteBuilder)_app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => string.Equals(candidate.RoutePattern.RawText, path, StringComparison.Ordinal));

        var context = new DefaultHttpContext { RequestServices = _app.Services };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);

        await endpoint.RequestDelegate!(context);

        return await ReadAsync(context);
    }

    /// <summary>
    /// Executes a result directly, with the application's services, so the comparison against the
    /// mapped route sees the same serialization settings a real request would.
    /// </summary>
    /// <param name="result">The result to execute.</param>
    /// <returns>The status code and body the result wrote.</returns>
    internal async Task<EndpointResponse> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext { RequestServices = _app.Services };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        return await ReadAsync(context);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static async Task<EndpointResponse> ReadAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);

        return new EndpointResponse(
            context.Response.StatusCode,
            await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }
}

/// <summary>What a request wrote to the response.</summary>
/// <param name="Status">The status code.</param>
/// <param name="Body">The body, read as UTF-8 text.</param>
internal readonly record struct EndpointResponse(int Status, string Body);
