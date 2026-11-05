// Copyright (c) ResulTus. All rights reserved.

using Microsoft.CodeAnalysis;

namespace ResulTus.Analyzers;

/// <summary>
/// Recognises the library's own types and extension methods inside a compilation.
/// </summary>
/// <remarks>
/// Matching is done on metadata names and the containing namespace rather than on display strings,
/// so a type forwarded, aliased or nested in a generic still resolves and a user type that happens
/// to be called <c>Result</c> in another namespace does not.
/// </remarks>
internal static class ResultSymbols
{
    /// <summary>The namespace every public ResulTus type lives in.</summary>
    internal const string Namespace = "ResulTus";

    /// <summary>The metadata name of <c>ResulTus.Result</c>.</summary>
    internal const string UnitResultMetadataName = "Result";

    /// <summary>The metadata name of <c>ResulTus.Result&lt;T&gt;</c>.</summary>
    internal const string ValueResultMetadataName = "Result`1";

    /// <summary>The metadata name of <c>ResulTus.Result&lt;TSuccess, TError&gt;</c>.</summary>
    internal const string PairResultMetadataName = "Result`2";

    /// <summary>The display name of the extension class holding the value-result operators.</summary>
    internal const string ExtensionsTypeName = "ResulTus.Extensions.ResultExtensions";

    /// <summary>The display name of the extension class holding the conversions.</summary>
    internal const string ConversionsTypeName = "ResulTus.Extensions.ResultConversions";

    /// <summary>
    /// Determines whether <paramref name="type"/> is one of the three result types.
    /// </summary>
    /// <param name="type">The type to test; may be <c>null</c>.</param>
    /// <returns><c>true</c> when the type is <c>Result</c>, <c>Result&lt;T&gt;</c> or <c>Result&lt;TSuccess, TError&gt;</c>.</returns>
    internal static bool IsResultType(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        var definition = named.OriginalDefinition;

        // A named type always has a containing namespace; a top-level type's is the global one.
        // The null-forgiving operator says so, instead of hiding an impossible branch from coverage.
        if (definition.ContainingNamespace!.ToDisplayString() != Namespace)
        {
            return false;
        }

        return definition.MetadataName is
            UnitResultMetadataName or
            ValueResultMetadataName or
            PairResultMetadataName;
    }

    /// <summary>
    /// Determines whether <paramref name="method"/> is declared by <c>ResultExtensions</c>.
    /// </summary>
    /// <param name="method">The method to test; may be <c>null</c>.</param>
    /// <returns><c>true</c> when the method's containing type is the extensions class.</returns>
    internal static bool IsExtensionsMethod(IMethodSymbol? method)
        // A method symbol always has a containing type, so only the method itself can be null.
        => method is not null && method.ContainingType!.ToDisplayString() == ExtensionsTypeName;

    /// <summary>
    /// Determines whether <paramref name="method"/> is declared by <c>ResultConversions</c>.
    /// </summary>
    /// <param name="method">The method to test; may be <c>null</c>.</param>
    /// <returns><c>true</c> when the method's containing type is the conversions class.</returns>
    internal static bool IsConversionsMethod(IMethodSymbol? method)
        // Same invariant as IsExtensionsMethod: only the method itself can be null.
        => method is not null && method.ContainingType!.ToDisplayString() == ConversionsTypeName;

    /// <summary>
    /// Determines whether <paramref name="symbol"/> is the static <c>Success</c> factory of a result type.
    /// </summary>
    /// <param name="symbol">The symbol to test; may be <c>null</c>.</param>
    /// <returns><c>true</c> for <c>Result&lt;T&gt;.Success</c> and <c>Result&lt;TSuccess, TError&gt;.Success</c>.</returns>
    internal static bool IsSuccessFactory(IMethodSymbol? symbol)
        => symbol is { IsStatic: true, Name: "Success" } && IsResultType(symbol.ContainingType);
}
