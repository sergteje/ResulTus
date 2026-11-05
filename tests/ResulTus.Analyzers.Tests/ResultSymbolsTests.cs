// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for the symbol matcher. Every rule depends on it, so a wrong match either silences a rule
/// or makes it fire on a user type that happens to be called <c>Result</c>.
/// </summary>
public class ResultSymbolsTests
{
    private const string Source = """
        namespace Other
        {
            public readonly struct Result { }
        }

        namespace ResulTus
        {
            public readonly struct NotAResult { }
        }

        public static class Generic<T>
        {
        }
        """;

    private static readonly Compilation Compilation = AnalyzerHarness.CreateCompilation(Source);

    /// <summary>
    /// GIVEN the three public result types
    /// WHEN they are matched
    /// THEN all three MUST be recognised.
    /// </summary>
    [Fact]
    public void IsResultType_ShouldAcceptTheThreeResultTypes()
    {
        ResultSymbols.IsResultType(Compilation.GetTypeByMetadataName("ResulTus.Result")).Should().BeTrue();
        ResultSymbols.IsResultType(Compilation.GetTypeByMetadataName("ResulTus.Result`1")).Should().BeTrue();
        ResultSymbols.IsResultType(Compilation.GetTypeByMetadataName("ResulTus.Result`2")).Should().BeTrue();
    }

    /// <summary>
    /// GIVEN a null type, a type with the right namespace and the wrong name, a type with the right
    /// name and the wrong namespace, an array and a type parameter
    /// WHEN they are matched
    /// THEN none MUST be recognised, so a rule cannot fire on a look-alike.
    /// </summary>
    [Fact]
    public void IsResultType_ShouldRejectEverythingElse()
    {
        var array = Compilation.CreateArrayTypeSymbol(Compilation.GetSpecialType(SpecialType.System_Int32));
        var typeParameter = Compilation.GetTypeByMetadataName("Generic`1")!.TypeParameters[0];

        ResultSymbols.IsResultType(null).Should().BeFalse();
        ResultSymbols.IsResultType(Compilation.GetTypeByMetadataName("ResulTus.NotAResult")).Should().BeFalse();
        ResultSymbols.IsResultType(Compilation.GetTypeByMetadataName("Other.Result")).Should().BeFalse();
        ResultSymbols.IsResultType(array).Should().BeFalse();
        ResultSymbols.IsResultType(typeParameter).Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a method declared by <c>ResultExtensions</c>, one declared elsewhere and null
    /// WHEN they are matched
    /// THEN only the first MUST be recognised.
    /// </summary>
    [Fact]
    public void IsExtensionsMethod_ShouldMatchOnlyResultExtensions()
    {
        var extensions = Compilation.GetTypeByMetadataName("ResulTus.Extensions.ResultExtensions")!;
        var conversions = Compilation.GetTypeByMetadataName("ResulTus.Extensions.ResultConversions")!;

        ResultSymbols.IsExtensionsMethod((IMethodSymbol)extensions.GetMembers("Bind")[0]).Should().BeTrue();
        ResultSymbols.IsExtensionsMethod((IMethodSymbol)conversions.GetMembers("ToResult")[0]).Should().BeFalse();
        ResultSymbols.IsExtensionsMethod(null).Should().BeFalse();
    }

    /// <summary>
    /// GIVEN a method declared by <c>ResultConversions</c>, one declared elsewhere and null
    /// WHEN they are matched
    /// THEN only the first MUST be recognised.
    /// </summary>
    [Fact]
    public void IsConversionsMethod_ShouldMatchOnlyResultConversions()
    {
        var extensions = Compilation.GetTypeByMetadataName("ResulTus.Extensions.ResultExtensions")!;
        var conversions = Compilation.GetTypeByMetadataName("ResulTus.Extensions.ResultConversions")!;

        ResultSymbols.IsConversionsMethod((IMethodSymbol)conversions.GetMembers("ToResult")[0]).Should().BeTrue();
        ResultSymbols.IsConversionsMethod((IMethodSymbol)extensions.GetMembers("Map")[0]).Should().BeFalse();
        ResultSymbols.IsConversionsMethod(null).Should().BeFalse();
    }

    /// <summary>
    /// GIVEN the static <c>Success</c> factory, a sibling factory and null
    /// WHEN they are matched
    /// THEN only <c>Success</c> MUST be recognised.
    /// </summary>
    [Fact]
    public void IsSuccessFactory_ShouldMatchOnlySuccess()
    {
        var valueResult = Compilation.GetTypeByMetadataName("ResulTus.Result`1")!;

        ResultSymbols.IsSuccessFactory((IMethodSymbol)valueResult.GetMembers("Success")[0]).Should().BeTrue();
        ResultSymbols.IsSuccessFactory((IMethodSymbol)valueResult.GetMembers("Failure")[0]).Should().BeFalse();
        ResultSymbols.IsSuccessFactory((IMethodSymbol)valueResult.GetMembers("GetValueOrDefault")[0]).Should().BeFalse();
        ResultSymbols.IsSuccessFactory(null).Should().BeFalse();
    }
}
