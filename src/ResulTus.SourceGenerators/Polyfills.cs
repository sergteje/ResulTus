// Copyright (c) ResulTus. All rights reserved.

namespace System.Runtime.CompilerServices;

/// <summary>
/// Declares the type the compiler requires in order to emit an <c>init</c> accessor, which
/// <c>netstandard2.0</c> does not ship.
/// </summary>
/// <remarks>
/// Nothing references this type by name. It exists only so the records in this assembly — the
/// pipeline models — can be declared at all, and so their value equality keeps working: two models
/// that hold the same text compare equal, which is what lets the incremental pipeline skip the
/// output step.
/// </remarks>
internal static class IsExternalInit
{
}
