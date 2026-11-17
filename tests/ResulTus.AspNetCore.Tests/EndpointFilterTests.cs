// Copyright (c) ResulTus. All rights reserved.

using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ResulTus.AspNetCore.Tests;

/// <summary>
/// Tests for the two endpoint filters called in isolation: what they convert MUST be what the direct
/// <c>ToHttpResult()</c> call produces, and every other value MUST come back untouched.
/// </summary>
public class EndpointFilterTests
{
    /// <summary>
    /// GIVEN a handler that returns a successful unit <see cref="Result"/>
    /// WHEN the unit filter converts it and the result is executed
    /// THEN the response MUST be exactly the one the direct <c>ToHttpResult()</c> call produces.
    /// </summary>
    [Fact]
    public async Task UnitSuccess_ShouldMatchDirectToHttpResult()
    {
        var converted = await FilterAsync(new ResultEndpointFilter(), Result.Success());

        var throughFilter = await ExecuteAsync(converted.Should().BeAssignableTo<IResult>().Which);
        var direct = await ExecuteAsync(Result.Success().ToHttpResult());

        throughFilter.Status.Should().Be(direct.Status);
        throughFilter.Body.Should().Be(direct.Body);
    }

    /// <summary>
    /// GIVEN a handler that returns a failed unit <see cref="Result"/>
    /// WHEN the unit filter converts it and the result is executed
    /// THEN the response MUST be exactly the one the direct <c>ToHttpResult()</c> call produces,
    /// status and ProblemDetails body included.
    /// </summary>
    [Fact]
    public async Task UnitFailure_ShouldMatchDirectToHttpResult()
    {
        var error = Error.NotFound("missing", "not there");

        var converted = await FilterAsync(new ResultEndpointFilter(), Result.Failure(error));

        var throughFilter = await ExecuteAsync(converted.Should().BeAssignableTo<IResult>().Which);
        var direct = await ExecuteAsync(Result.Failure(error).ToHttpResult());

        throughFilter.Status.Should().Be(direct.Status);
        throughFilter.Body.Should().Be(direct.Body);
    }

    /// <summary>
    /// GIVEN a handler that returns a successful <see cref="Result{TSuccess}"/>
    /// WHEN the generic filter converts it and the result is executed
    /// THEN the response MUST carry the value exactly as the direct <c>ToHttpResult()</c> call does.
    /// </summary>
    [Fact]
    public async Task ValueSuccess_ShouldMatchDirectToHttpResult()
    {
        var converted = await FilterAsync(new ResultEndpointFilter<int>(), Result<int>.Success(42));

        var throughFilter = await ExecuteAsync(converted.Should().BeAssignableTo<IResult>().Which);
        var direct = await ExecuteAsync(Result<int>.Success(42).ToHttpResult());

        throughFilter.Status.Should().Be(direct.Status);
        throughFilter.Body.Should().Be(direct.Body);
    }

    /// <summary>
    /// GIVEN a handler that returns a failed <see cref="Result{TSuccess}"/>
    /// WHEN the generic filter converts it and the result is executed
    /// THEN the response MUST be exactly the one the direct <c>ToHttpResult()</c> call produces.
    /// </summary>
    [Fact]
    public async Task ValueFailure_ShouldMatchDirectToHttpResult()
    {
        var error = Error.Validation("bad", "value too small");

        var converted = await FilterAsync(new ResultEndpointFilter<int>(), Result<int>.Failure(error));

        var throughFilter = await ExecuteAsync(converted.Should().BeAssignableTo<IResult>().Which);
        var direct = await ExecuteAsync(Result<int>.Failure(error).ToHttpResult());

        throughFilter.Status.Should().Be(direct.Status);
        throughFilter.Body.Should().Be(direct.Body);
    }

    /// <summary>
    /// GIVEN a handler that returns a <see cref="Result{TSuccess}"/> over a success type
    /// WHEN the unit filter sees it
    /// THEN it MUST come back as the very same instance, unconverted. This is why the caller picks
    /// the overload that matches the handler: a filter cannot infer the other tier without reflection.
    /// </summary>
    [Fact]
    public async Task UnitFilter_ValueResult_ShouldPassThroughUntouched()
    {
        object value = Result<int>.Success(42);

        var returned = await FilterAsync(new ResultEndpointFilter(), value);

        returned.Should().BeSameAs(value);
    }

    /// <summary>
    /// GIVEN a handler that returns a <see cref="Result{TSuccess}"/> over a different success type
    /// WHEN a generic filter closed over another <c>TSuccess</c> sees it
    /// THEN it MUST come back as the very same instance, unconverted.
    /// </summary>
    [Fact]
    public async Task ValueFilter_OtherSuccessType_ShouldPassThroughUntouched()
    {
        object value = Result<string>.Success("other");

        var returned = await FilterAsync(new ResultEndpointFilter<int>(), value);

        returned.Should().BeSameAs(value);
    }

    /// <summary>
    /// GIVEN a handler that returns something that is not a result at all
    /// WHEN either filter sees it
    /// THEN it MUST come back as the very same instance, so a handler returning a DTO, a string or an
    /// <see cref="IResult"/> behaves exactly as it did without the filter.
    /// </summary>
    [Fact]
    public async Task BothFilters_NonResultValue_ShouldPassThroughUntouched()
    {
        object dto = new Product("widget");
        object text = "plain";
        var endpointResult = Results.Ok("ok");

        (await FilterAsync(new ResultEndpointFilter(), dto)).Should().BeSameAs(dto);
        (await FilterAsync(new ResultEndpointFilter(), text)).Should().BeSameAs(text);
        (await FilterAsync(new ResultEndpointFilter(), endpointResult)).Should().BeSameAs(endpointResult);
        (await FilterAsync(new ResultEndpointFilter<int>(), dto)).Should().BeSameAs(dto);
    }

    /// <summary>
    /// GIVEN a null invocation context or a null next delegate
    /// WHEN the unit filter is invoked directly
    /// THEN it MUST throw <see cref="ArgumentNullException"/> naming the argument, instead of failing
    /// later with a null reference.
    /// </summary>
    [Fact]
    public async Task UnitFilter_WithNullArgument_ShouldThrowArgumentNullException()
    {
        var filter = new ResultEndpointFilter();
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), []);

        var nullContext = () => filter.InvokeAsync(null!, _ => ValueTask.FromResult<object?>(null)).AsTask();
        var nullNext = () => filter.InvokeAsync(context, null!).AsTask();

        await nullContext.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
        await nullNext.Should().ThrowAsync<ArgumentNullException>().WithParameterName("next");
    }

    /// <summary>
    /// GIVEN a null invocation context or a null next delegate
    /// WHEN the generic filter is invoked directly
    /// THEN it MUST throw <see cref="ArgumentNullException"/> naming the argument, exactly as the
    /// unit filter does.
    /// </summary>
    [Fact]
    public async Task ValueFilter_WithNullArgument_ShouldThrowArgumentNullException()
    {
        var filter = new ResultEndpointFilter<int>();
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), []);

        var nullContext = () => filter.InvokeAsync(null!, _ => ValueTask.FromResult<object?>(null)).AsTask();
        var nullNext = () => filter.InvokeAsync(context, null!).AsTask();

        await nullContext.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
        await nullNext.Should().ThrowAsync<ArgumentNullException>().WithParameterName("next");
    }

    /// <summary>
    /// GIVEN no route builder at all
    /// WHEN the unit registration is called with <c>null</c>
    /// THEN it MUST throw <see cref="ArgumentNullException"/> naming the builder.
    /// </summary>
    [Fact]
    public void WithResultMapping_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        RouteHandlerBuilder? builder = null;
        var register = () => builder!.WithResultMapping();

        register.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    /// <summary>
    /// GIVEN no route builder at all
    /// WHEN the generic registration is called with <c>null</c>
    /// THEN it MUST throw <see cref="ArgumentNullException"/> naming the builder.
    /// </summary>
    [Fact]
    public void WithResultMappingOfTSuccess_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        RouteHandlerBuilder? builder = null;
        var register = () => builder!.WithResultMapping<int>();

        register.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    /// <summary>
    /// Runs <paramref name="filter"/> with a next delegate that returns <paramref name="handlerReturn"/>.
    /// </summary>
    /// <param name="filter">The filter under test.</param>
    /// <param name="handlerReturn">The value the handler would have returned.</param>
    /// <returns>What the filter returned.</returns>
    private static async Task<object?> FilterAsync(IEndpointFilter filter, object? handlerReturn)
    {
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), []);

        return await filter.InvokeAsync(context, _ => ValueTask.FromResult(handlerReturn));
    }

    /// <summary>
    /// Executes <paramref name="result"/> and reads back the status and the body it wrote.
    /// </summary>
    /// <param name="result">The result to execute.</param>
    /// <returns>The status code and the body as text.</returns>
    private static async Task<(int Status, string Body)> ExecuteAsync(IResult result)
    {
        var context = await HttpResponseProbe.ExecuteAsync(result);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);

        return (context.Response.StatusCode, await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }
}
