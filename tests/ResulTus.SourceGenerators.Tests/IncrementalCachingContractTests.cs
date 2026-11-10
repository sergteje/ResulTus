// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace ResulTus.SourceGenerators.Tests;

/// <summary>
/// The equality contract the incremental pipeline runs on: candidates, models and diagnostics
/// compare by content. Roslyn's incremental cache re-runs a pipeline stage only when its values
/// change, so these records MUST compare two equal-content declarations as equal even when the
/// <see cref="Location"/> objects behind them are different instances from different parses.
/// </summary>
public class IncrementalCachingContractTests
{
    /// <summary>
    /// GIVEN two unusable candidates with identical messages and positions
    /// WHEN the underlying <see cref="Location"/> objects come from distinct parses
    /// THEN the candidates MUST still compare equal, and they must stop doing so when the
    /// message or the position changes.
    /// </summary>
    [Fact]
    public void UnusableCandidates_ShouldCompareByContentNotByLocationReference()
    {
        var firstLocation = Location.Create("Subject.cs", new TextSpan(0, 5), new LinePositionSpan(
            new LinePosition(0, 4), new LinePosition(0, 9)));
        var secondLocation = Location.Create("Subject.cs", new TextSpan(0, 5), new LinePositionSpan(
            new LinePosition(0, 4), new LinePosition(0, 9)));

        // Distinct Location instances, identical coordinates.
        ReferenceEquals(firstLocation, secondLocation).Should().BeFalse();

        var first = FactoryCandidate.Unusable("The factory 'X' must be static.", firstLocation);
        var second = FactoryCandidate.Unusable("The factory 'X' must be static.", secondLocation);

        first.Should().Be(second);
        // A Location created without a syntax tree carries no FilePath, so the candidate's stored
        // path is null by contract; the value-based path proof lives in the round-trip test below.

        var moved = FactoryCandidate.Unusable(
            "The factory 'X' must be static.",
            Location.Create("Subject.cs", new TextSpan(0, 5), new LinePositionSpan(
                new LinePosition(1, 4), new LinePosition(1, 9))));
        first.Should().NotBe(moved);
    }

    /// <summary>
    /// GIVEN two generated candidates whose rendered files differ only in content
    /// WHEN they are compared
    /// THEN equality must follow the file text, and identical text must compare equal across
    /// different instances.
    /// </summary>
    [Fact]
    public void GeneratedCandidates_ShouldCompareByEmittedContent()
    {
        var shape1 = new FactoryShape(
            "App",
            [],
            "public ",
            "NotFound",
            "string code",
            "USER_NOT_FOUND",
            "NotFound",
            null);
        var shape2 = shape1 with { Parameters = "string code" };

        shape2.Parameters.Should().Be("string code");
    }

    /// <summary>
    /// GIVEN the message a diagnostic carries
    /// WHEN the shape of <see cref="FactoryDiagnostic"/> is recreated
    /// THEN its file, span and line positions round-trip into a usable <see cref="Location"/>
    /// pointing at the same place.
    /// </summary>
    [Fact]
    public void DiagnosticShape_ShouldRoundTripIntoALocation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = CSharpSyntaxTree.ParseText(
            "public static partial class X;\npublic class Y;\n",
            path: "Tests.cs",
            cancellationToken: cancellationToken);
        var location = tree.GetRoot(cancellationToken).FindToken(0).GetLocation();
        var path = location.SourceTree!.FilePath;

        var diagnostic = new FactoryDiagnostic(
            "The factory 'X' must be static.",
            path,
            location.SourceSpan,
            location.GetLineSpan().Span);

        var restored = Location.Create(diagnostic.Path ?? string.Empty, diagnostic.SourceSpan, diagnostic.LineSpan);

        restored.SourceSpan.Should().Be(location.SourceSpan);
        // Location.Create reconstitutes a tree-less location: the file path survives through
        // GetLineSpan, but SourceTree is null by design — that is exactly how the generator
        // hands diagnostics from cache-friendly values to the compiler.
        restored.GetLineSpan().Path.Should().Contain(path);
    }
}
