// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Diagnostics;

/// <summary>
/// A unit result together with how long the operation that produced it took.
/// </summary>
/// <remarks>
/// A value type on purpose: measuring an operation must not allocate a wrapper, or the
/// instrumentation would cost more than the thing it measures.
/// </remarks>
/// <param name="Result">The result the operation returned, unchanged.</param>
/// <param name="Elapsed">How long the operation took.</param>
public readonly record struct TimedUnitResult(Result Result, TimeSpan Elapsed);

/// <summary>
/// A generic result together with how long the operation that produced it took.
/// </summary>
/// <remarks>
/// A value type on purpose: measuring an operation must not allocate a wrapper, or the
/// instrumentation would cost more than the thing it measures.
/// </remarks>
/// <typeparam name="T">The success value's type.</typeparam>
/// <param name="Result">The result the operation returned, unchanged.</param>
/// <param name="Elapsed">How long the operation took.</param>
public readonly record struct TimedResult<T>(Result<T> Result, TimeSpan Elapsed);
