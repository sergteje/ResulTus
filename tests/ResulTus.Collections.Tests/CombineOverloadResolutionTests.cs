// Copyright (c) ResulTus. All rights reserved.

using ResulTus.Extensions;

namespace ResulTus.Collections.Tests;

/// <summary>
/// Binding regressions around the element-wise <c>Combine</c> overloads: adding the binary
/// <c>Result&lt;TFirst&gt;</c> overload must not move any existing call site.
/// </summary>
public class CombineOverloadResolutionTests
{
    /// <summary>
    /// GIVEN an <see cref="IEnumerable{T}"/> of unit results
    /// WHEN it is combined
    /// THEN it binds to the sequence overload.
    /// </summary>
    [Fact]
    public void IEnumerableResult_ShouldStillBindToTheSequenceOverload()
    {
        IEnumerable<Result> source = new List<Result> { Result.Success(), Result.Success() };

        Result combined = source.Combine();

        combined.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a <see cref="Result"/> array
    /// WHEN it is combined
    /// THEN it binds to the array overload and reports the first failure.
    /// </summary>
    [Fact]
    public void ResultArray_ShouldStillBindToTheArrayOverload()
    {
        var error = Error.Conflict("array");
        Result[] source = { Result.Success(), Result.Failure(error) };

        Result combined = source.Combine();

        combined.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN an <see cref="IReadOnlyList{T}"/> of unit results
    /// WHEN it is combined
    /// THEN it binds to the read-only-list overload and reports the first failure.
    /// </summary>
    [Fact]
    public void ReadOnlyList_ShouldStillBindToTheReadOnlyListOverload()
    {
        var error = Error.NotFound("list");
        IReadOnlyList<Result> source = new List<Result> { Result.Failure(error), Result.Success() };

        Result combined = source.Combine();

        combined.TryGetError(out var propagated).Should().BeTrue();
        propagated.Should().Be(error);
    }

    /// <summary>
    /// GIVEN an empty array
    /// WHEN it is combined
    /// THEN it still returns success.
    /// </summary>
    [Fact]
    public void EmptySequence_ShouldStillReturnSuccess()
    {
        Result[] source = Array.Empty<Result>();

        Result combined = source.Combine();

        combined.IsSuccess.Should().BeTrue();
    }
}
