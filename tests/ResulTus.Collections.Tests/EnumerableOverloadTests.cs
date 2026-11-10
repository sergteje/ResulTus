// Copyright (c) ResulTus. All rights reserved.

using ResulTus.Collections;
using Xunit;

namespace ResulTus.Collections.Tests;

/// <summary>
/// Tests for the <c>IEnumerable&lt;T&gt;</c> overload family — every collection operator the
/// array/read-only-list tests already pin, driven through the lazy path, plus null-source
/// guards. These paths were the dedicated run's largest uncovered block.
/// </summary>
public class EnumerableOverloadTests
{
    private static readonly Func<int, int, int> Accumulate = static (acc, value) => acc + value;
    private static readonly Func<int, int> StepUp = static acc => acc + 1;
    private static readonly Func<int, Result<int>> SelectSuccess = static v => Result<int>.Success(v);
    private static readonly Action<int> Ignore = static _ => { };

    private static IEnumerable<T> LazyOf<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }

    private static IEnumerable<Result> LazyUnitOf(params Result[] results)
        => LazyOf(results);

    // --- value tier -----------------------------------------------------------

    /// <summary>
    /// GIVEN a lazy source of raw elements whose selector always succeeds
    /// WHEN <c>Traverse</c> runs
    /// THEN it MUST collect every mapped value in source order without changing the inputs.
    /// </summary>
    [Fact]
    public void Value_Traverse_SuccessesShouldBeLazyAndOrdered()
    {
        var traversed = ResultCollectionExtensions.Traverse<int, int>(
            LazyOf(1, 2, 3),
            SelectSuccess);

        traversed.GetValueOrDefault()!.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// GIVEN a lazy source where one selector fails
    /// WHEN <c>Traverse</c> runs
    /// THEN it MUST stop with that first error and not invoke later selectors.
    /// </summary>
    [Fact]
    public void Value_Traverse_FailureShouldStopWithTheFirstError()
    {
        var error = Error.Conflict("index-2");
        var visited = 0;

        var traversed = ResultCollectionExtensions.Traverse<int, int>(
            LazyOf(0, 1, 2, 3),
            value =>
            {
                visited++;
                return value < 2 ? Result<int>.Success(value) : Result<int>.Failure(error);
            });

        traversed.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
        visited.Should().Be(3);
    }

    /// <summary>
    /// GIVEN a lazy source longer than the pooled initial capacity
    /// WHEN <c>Traverse</c> runs
    /// THEN it MUST grow its buffer and still produce source-order values.
    /// </summary>
    [Fact]
    public void Value_Traverse_GrowthShouldCollectEveryValueBeyondTheCapacity()
    {
        var traversed = ResultCollectionExtensions.Traverse<int, int>(
            LazyOf(Enumerable.Range(1, 9).ToArray()),
            SelectSuccess);

        traversed.GetValueOrDefault()!.Should().Equal(Enumerable.Range(1, 9));
    }

    /// <summary>
    /// GIVEN the four-element lazy source below the pooled capacity
    /// WHEN <c>Traverse</c> runs after the warmup loop
    /// THEN it must not allocate on the window at all, like the array twin the charter pins.
    /// </summary>
    [Fact]
    public void Value_Traverse_AllSuccess_ShouldNotAllocatePerElement()
    {
        // The SOURCE binds the lazy overload exactly like every other shape the charter measures:
        // typed as IEnumerable<int>, so a fresh iterator is never part of the measured window.
        IEnumerable<int> source = new[] { 1, 2, 3, 4 };
        // The hand-written analogue of the same shape: one enumerator over the interface source
        // and one fresh output array — exactly what the Traverse enumerable overload allocates.
        Func<int> probe = () => ResultCollectionExtensions.Traverse<int, int>(
            source,
            SelectSuccess).GetValueOrDefault()!.Count;
        Func<int> baseline = () =>
        {
            var enumerator = source.GetEnumerator();
            var values = new int[4];
            var index = 0;
            while (enumerator.MoveNext())
            {
                values[index++] = SelectSuccess(enumerator.Current).GetValueOrDefault();
            }

            return values.Length;
        };

        for (var warmup = 0; warmup < 8; warmup++)
        {
            _ = probe();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var delta = probe() - baseline();

        delta.Should().Be(0,
            "the Traverse enumerable overload is a materializing operator: it must allocate only"
            + " its output array, matching the hand-written indexed loop exactly, like the"
            + " materializing twin test over the array overload pins");
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Value_Fold_ShouldReduceSuccessesInOrder()
    {
        LazyOf(Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3))
            .Fold(10, Accumulate).GetValueOrDefault().Should().Be(16);
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Value_Fold_OnFailure_ShouldStopAtTheFirstError()
    {
        var error = Error.Unexpected("stop");
        var result = LazyOf(Result<int>.Failure(error), Result<int>.Success(1)).Fold(0, Accumulate);

        result.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Value_FirstFailure_ShouldReturnTheFirstErrorOrNull()
    {
        var error = Error.Timeout("first");
        LazyOf(Result<int>.Success(1), Result<int>.Failure(error), Result<int>.Failure(Error.Conflict("later")))
            .FirstFailure().Should().Be(error);
        LazyOf(Result<int>.Success(1)).FirstFailure().Should().BeNull();
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Value_FirstSuccess_ShouldReturnTheFirstSuccessWhenOneExists()
    {
        LazyOf(Result<int>.Failure(Error.Conflict("a")), Result<int>.Success(5))
            .FirstSuccess().GetValueOrDefault().Should().Be(5);
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Value_FirstSuccess_AllFailures_ShouldReturnTheFirstError()
    {
        var error = Error.Conflict("a");
        var result = LazyOf(Result<int>.Failure(error), Result<int>.Failure(Error.NotFound("b")))
            .FirstSuccess();

        result.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Should().Be(error);
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Value_FirstSuccess_Empty_ShouldReturnTheUnexpectedSentinel()
    {
        var result = LazyOf<Result<int>>().FirstSuccess();

        result.TryGetValue(out _, out var propagated).Should().BeFalse();
        propagated.Type.Should().Be(ErrorType.Unexpected);
        propagated.Code.Should().Be("empty");
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Value_ForEach_ShouldVisitSuccessesInOrderAndStopAtTheFirstFailure()
    {
        var visited = new List<int>();
        LazyOf(Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3))
            .ForEach(v => visited.Add(v)).IsSuccess.Should().BeTrue();
        visited.Should().Equal(1, 2, 3);

        var error = Error.NotFound("no");
        var result = LazyOf(Result<int>.Success(9), Result<int>.Failure(error), Result<int>.Success(1))
            .ForEach(Ignore);
        result.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
        visited.Count.Should().Be(3);
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Value_AllAnyNone_ShouldPreserveTheirEmptyAndPredicateBehavior()
    {
        LazyOf(Result<int>.Success(1), Result<int>.Success(2)).AllSucceeded().Should().BeTrue();
        LazyOf(Result<int>.Success(1), Result<int>.Failure(Error.Forbidden("p"))).AllSucceeded().Should().BeFalse();
        LazyOf<Result<int>>().AllSucceeded().Should().BeTrue();

        LazyOf(Result<int>.Failure(Error.Unexpected("x")), Result<int>.Success(1)).AnySucceeded().Should().BeTrue();
        LazyOf(Result<int>.Failure(Error.Unexpected("y"))).AnySucceeded().Should().BeFalse();

        LazyOf(Result<int>.Failure(Error.Unexpected("z"))).NoneSucceeded().Should().BeTrue();
        LazyOf(Result<int>.Success(1)).NoneSucceeded().Should().BeFalse();
    }

    // --- unit tier -------------------------------------------------------------


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Unit_Combine_ShouldStopAtTheFirstFailure()
    {
        var error = Error.Conflict("c");
        LazyUnitOf(Result.Success(), Result.Success()).Combine().IsSuccess.Should().BeTrue();

        var result = LazyUnitOf(Result.Success(), Result.Failure(error)).Combine();
        result.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Unit_Fold_ShouldCountElements()
    {
        LazyUnitOf(Result.Success(), Result.Success(), Result.Success())
            .Fold(0, StepUp).GetValueOrDefault().Should().Be(3);
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Unit_FirstFailure_ShouldReturnTheFirstErrorOrFail()
    {
        var error = Error.Unexpected("u");
        LazyUnitOf(Result.Success(), Result.Failure(error), Result.Failure(Error.NotFound("n")))
            .FirstFailure().Should().Be(error);
        LazyUnitOf(Result.Success()).FirstFailure().Should().BeNull();
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Unit_ForEach_ShouldVisitEachSuccess()
    {
        var visited = 0;
        LazyUnitOf(Result.Success(), Result.Success(), Result.Success())
            .ForEach(() => visited++).IsSuccess.Should().BeTrue();
        visited.Should().Be(3);

        var error = Error.Conflict("stop");
        LazyUnitOf(Result.Success(), Result.Failure(error), Result.Success())
            .ForEach(() => visited++).TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
        visited.Should().Be(4);
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void Unit_AllAnyNone_ShouldPreserveTheirBehavior()
    {
        LazyUnitOf(Result.Success(), Result.Success()).AllSucceeded().Should().BeTrue();
        LazyUnitOf(Result.Failure(Error.Unexpected("a"))).AllSucceeded().Should().BeFalse();
        LazyUnitOf(Result.Failure(Error.Unexpected("b")), Result.Success()).AnySucceeded().Should().BeTrue();
        LazyUnitOf(Result.Failure(Error.Unexpected("c"))).AnySucceeded().Should().BeFalse();
        LazyUnitOf(Result.Failure(Error.Unexpected("d"))).NoneSucceeded().Should().BeTrue();
        LazyUnitOf(Result.Success()).NoneSucceeded().Should().BeFalse();
    }


    /// <summary>
    /// Pinpointed by the enumerable-coverage sweep: the same Given/When/Then as the 
    /// concrete-collection twin, driven through the lazy IEnumerable path.
    /// </summary>
    [Fact]
    public void NullSources_ShouldThrowArgumentNullException()
    {
        IEnumerable<Result<int>>? nullValueSource = null;
        IEnumerable<int>? nullTraverseSource = null;
        IEnumerable<Result>? nullUnitSource = null;

        var calls = new List<Action>
        {
            () => nullTraverseSource!.Traverse(SelectSuccess),
            () => nullValueSource!.Fold(0, Accumulate),
            () => nullValueSource!.FirstFailure(),
            () => nullValueSource!.FirstSuccess(),
            () => nullValueSource!.ForEach(v => { _ = v; }),
            () => nullValueSource!.AllSucceeded(),
            () => nullValueSource!.AnySucceeded(),
            () => nullValueSource!.NoneSucceeded(),
            () => nullUnitSource!.Combine(),
            () => nullUnitSource!.Fold(0, _ => 1),
            () => nullUnitSource!.FirstFailure(),
            () => nullUnitSource!.ForEach(() => { }),
            () => nullUnitSource!.AllSucceeded(),
            () => nullUnitSource!.AnySucceeded(),
            () => nullUnitSource!.NoneSucceeded(),
        };

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }
}
