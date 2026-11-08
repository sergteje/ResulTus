// Copyright (c) ResulTus. All rights reserved.

using System.Runtime.CompilerServices;

namespace ResulTus.Tests;

/// <summary>
/// Pins the managed size of the core types, which is part of the public contract.
/// </summary>
/// <remarks>
/// These are exact-size assertions, not thresholds: a layout change is a contract change.
/// The expected values follow <c>sizeof(Result&lt;T&gt;) = round8(sizeof(T) + sizeof(Error) + 1)</c>
/// with <c>sizeof(Error)</c> = 24 B. See docs/decisions/001-result-struct-size.md.
/// </remarks>
public class LayoutTests
{
    /// <summary>
    /// GIVEN the <see cref="Error"/> struct
    /// THEN its managed size MUST be 24 bytes (two references plus a <c>byte</c> type tag, padded).
    /// </summary>
    [Fact]
    public void Error_Size_ShouldBe24Bytes()
    {
        Unsafe.SizeOf<Error>().Should().Be(24);
    }

    /// <summary>
    /// GIVEN the <see cref="Result{T}"/> of <see cref="int"/>
    /// THEN its managed size MUST be 32 bytes, so exactly two fit in a 64-byte cache line.
    /// </summary>
    [Fact]
    public void ResultOfInt_Size_ShouldBe32Bytes()
    {
        Unsafe.SizeOf<Result<int>>().Should().Be(32);
    }

    /// <summary>
    /// GIVEN the <see cref="Result{T}"/> of <see cref="long"/>
    /// THEN its managed size MUST be 40 bytes (8-byte payload plus 25 bytes overhead, rounded).
    /// </summary>
    [Fact]
    public void ResultOfLong_Size_ShouldBe40Bytes()
    {
        Unsafe.SizeOf<Result<long>>().Should().Be(40);
    }

    /// <summary>
    /// GIVEN the <see cref="Result{T}"/> of <see cref="Guid"/>
    /// THEN its managed size MUST be 48 bytes (16-byte payload plus 25 bytes overhead, rounded).
    /// </summary>
    [Fact]
    public void ResultOfGuid_Size_ShouldBe48Bytes()
    {
        Unsafe.SizeOf<Result<Guid>>().Should().Be(48);
    }

    /// <summary>
    /// GIVEN the generic <see cref="Result{TSuccess, TError}"/> tier with an 8-byte error type
    /// THEN its managed size MUST be 13 bytes — the zero-allocation tier, well inside one cache line.
    /// </summary>
    /// <remarks>
    /// Measured as 13, not the 16 the size table in docs/decisions/001-result-struct-size.md predicts:
    /// <c>Pack = 1</c> only gets relaxed when the struct holds a reference field, and this instantiation
    /// holds none, so the runtime honors the pack and adds no padding. The tier's claim (fits in 16 bytes,
    /// zero allocation) still holds. This test pins the measured value so a later layout change is visible.
    /// <para>
    /// The aligned 16 B alternative was not taken: it was measured on 2026-10-29 against this layout
    /// (two samples per layout, success and failure, 1 000 and 7 000 elements) and came out 2.5% faster
    /// in one cell and 1.5%–5.0% slower in the other three, with the failure path consistently favoring
    /// the packed stride. That is at or below this machine's resolution, so the smaller stride wins.
    /// </para>
    /// </remarks>
    [Fact]
    public void ResultOfIntAndEightByteError_Size_ShouldBe13Bytes()
    {
        Unsafe.SizeOf<EightByteError>().Should().Be(8);
        Unsafe.SizeOf<Result<int, EightByteError>>().Should().Be(13);
    }

    /// <summary>
    /// GIVEN the failure path of <see cref="Result{T}"/>
    /// WHEN a failure is constructed from a factory-built error
    /// THEN it MUST allocate zero bytes.
    /// </summary>
    /// <remarks>
    /// The error code below is a string literal, which the compiler interns into the shared
    /// intern pool, so building the error performs no allocation and the measurement is
    /// deterministic. A warm-up call runs first so tiered JIT compilation of the generic
    /// instantiation is not counted in the delta.
    /// </remarks>
    [Fact]
    public void FailurePath_ShouldNotAllocate()
    {
        Result<int> warmup = Result<int>.Failure(Error.NotFound("test.not_found"));
        warmup.IsFailure.Should().BeTrue();

        var before = GC.GetAllocatedBytesForCurrentThread();

        var result = Result<int>.Failure(Error.NotFound("test.not_found"));

        var after = GC.GetAllocatedBytesForCurrentThread();

        result.IsFailure.Should().BeTrue();
        (after - before).Should().Be(0);
    }

    /// <summary>
    /// An 8-byte typed error used to measure the generic tier layout.
    /// </summary>
    internal readonly record struct EightByteError(int Code, int Detail);
}
