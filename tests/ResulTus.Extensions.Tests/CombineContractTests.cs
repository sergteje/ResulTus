// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Extensions.Tests;

/// <summary>
/// Contract tests for the binary <c>Combine</c> over two value-bearing results: tuple shape,
/// argument order, failure precedence and the zero-allocation floor.
/// </summary>
public class CombineContractTests
{
    private static Result<(int First, string Second)> _tupleSink;
    private static Result<(int First, string Second)> _firstFailureSink;
    private static Result<(int First, string Second)> _secondFailureSink;

    /// <summary>
    /// GIVEN two successful results
    /// WHEN they are combined
    /// THEN the tuple keeps argument order and exposes the named elements.
    /// </summary>
    [Fact]
    public void Combine_ShouldExposeNamedTupleElementsInArgumentOrder()
    {
        Result<(int First, string Second)> combined =
            Result<int>.Success(7).Combine(Result<string>.Success("seven"));

        combined.IsSuccess.Should().BeTrue();

        // Named-element access is a compile-time contract: these lines do not compile if the
        // returned tuple loses its element names.
        var tuple = combined.GetValueOrDefault();
        tuple.First.Should().Be(7);
        tuple.Second.Should().Be("seven");
    }

    /// <summary>
    /// GIVEN a string first and an int second
    /// WHEN they are combined
    /// THEN the returned element types follow the argument order.
    /// </summary>
    [Fact]
    public void Combine_ShouldFollowArgumentOrderForDistinctTypes()
    {
        Result<(string First, int Second)> combined =
            Result<string>.Success("seven").Combine(Result<int>.Success(7));

        combined.IsSuccess.Should().BeTrue();
        var tuple = combined.GetValueOrDefault();
        tuple.First.Should().Be("seven");
        tuple.Second.Should().Be(7);
    }

    /// <summary>
    /// GIVEN two failures with different errors
    /// WHEN they are combined
    /// THEN the first error in argument order wins.
    /// </summary>
    [Fact]
    public void Combine_ShouldKeepTheFirstErrorWhenBothFail()
    {
        var firstError = Error.Conflict("first");
        var secondError = Error.NotFound("second");

        var combined = Result<int>.Failure(firstError).Combine(Result<string>.Failure(secondError));

        combined.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(firstError);
        propagated.Should().NotBe(secondError);
    }

    /// <summary>
    /// GIVEN a successful first result and a failed second
    /// WHEN they are combined
    /// THEN the second error is returned.
    /// </summary>
    [Fact]
    public void Combine_ShouldReturnTheSecondErrorOnlyWhenTheFirstSucceeded()
    {
        var secondError = Error.Timeout("second");

        var combined = Result<int>.Success(7).Combine(Result<string>.Failure(secondError));

        combined.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(secondError);
    }

    /// <summary>
    /// GIVEN default results, which are failures carrying <see cref="Error.None"/>
    /// WHEN they are combined
    /// THEN the first error is returned and nothing throws.
    /// </summary>
    [Fact]
    public void Combine_ShouldTreatDefaultResultsAsFailures()
    {
        var combined = default(Result<int>).Combine(default(Result<string>));

        combined.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(Error.None);
    }

    /// <summary>
    /// GIVEN a success whose reference value is <c>null</c>
    /// WHEN it is combined with a successful second
    /// THEN the success is preserved and the null element survives.
    /// </summary>
    [Fact]
    public void Combine_ShouldPreserveANullSuccessValue()
    {
        var combined = Result<string?>.Success(null).Combine(Result<string>.Success("seven"));

        combined.IsSuccess.Should().BeTrue();
        combined.GetValueOrDefault().First.Should().BeNull();
    }

    /// <summary>
    /// GIVEN a successful combination
    /// WHEN it is repeated over a warm loop
    /// THEN it allocates nothing on the heap.
    /// </summary>
    [Fact]
    public void Combine_ShouldNotAllocateOnSuccess()
    {
        var first = Result<int>.Success(7);
        var second = Result<string>.Success("seven");

        Measure(() =>
        {
            _tupleSink = first.Combine(second);
            return 0;
        }).Should().Be(0);
    }

    /// <summary>
    /// GIVEN a failed first result
    /// WHEN the combination is repeated over a warm loop
    /// THEN it allocates nothing on the heap.
    /// </summary>
    [Fact]
    public void Combine_ShouldNotAllocateWhenTheFirstFails()
    {
        var first = Result<int>.Failure(Error.Conflict("first"));
        var second = Result<string>.Success("seven");

        Measure(() =>
        {
            _firstFailureSink = first.Combine(second);
            return 0;
        }).Should().Be(0);
    }

    /// <summary>
    /// GIVEN a failed second result
    /// WHEN the combination is repeated over a warm loop
    /// THEN it allocates nothing on the heap.
    /// </summary>
    [Fact]
    public void Combine_ShouldNotAllocateWhenTheSecondFails()
    {
        var first = Result<int>.Success(7);
        var second = Result<string>.Failure(Error.Timeout("second"));

        Measure(() =>
        {
            _secondFailureSink = first.Combine(second);
            return 0;
        }).Should().Be(0);
    }

    private static long Measure(Func<int> operation, int iterations = 10_000)
    {
        _ = operation();
        _ = operation();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var index = 0; index < iterations; index++)
        {
            checksum += operation();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(checksum);
        GC.KeepAlive(_tupleSink);
        GC.KeepAlive(_firstFailureSink);
        GC.KeepAlive(_secondFailureSink);
        return allocated;
    }
}
