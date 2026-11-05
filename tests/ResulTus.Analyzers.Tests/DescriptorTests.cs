// Copyright (c) ResulTus. All rights reserved.

namespace ResulTus.Analyzers.Tests;

/// <summary>
/// Tests for the diagnostic descriptors themselves. The ids are the package's public contract:
/// a consumer suppresses a rule by id, so a typo has to fail the build here rather than ship.
/// </summary>
public class DescriptorTests
{
    /// <summary>
    /// GIVEN the descriptor set
    /// WHEN the ids are read in order
    /// THEN they MUST be exactly the six documented rules.
    /// </summary>
    [Fact]
    public void All_ShouldBeTheSixDocumentedRules()
    {
        DiagnosticDescriptors.All
            .Select(descriptor => descriptor.Id)
            .Should()
            .Equal("RESULT001", "RESULT002", "RESULT003", "RESULT004", "RESULT005", "RESULT006");
    }

    /// <summary>
    /// GIVEN the descriptor set
    /// WHEN the default severities are read
    /// THEN they MUST match the documented table, so a rule cannot silently change from a warning
    /// to noise.
    /// </summary>
    [Fact]
    public void DefaultSeverities_ShouldMatchTheDocumentedTable()
    {
        DiagnosticDescriptors.IgnoredResult.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        DiagnosticDescriptors.UnguardedOutcome.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        DiagnosticDescriptors.DiscardedError.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        DiagnosticDescriptors.RedundantBind.DefaultSeverity.Should().Be(DiagnosticSeverity.Info);
        DiagnosticDescriptors.NestedResult.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        DiagnosticDescriptors.ConstantEnsure.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
    }

    /// <summary>
    /// GIVEN every descriptor
    /// WHEN its metadata is read
    /// THEN it MUST carry a title, a message format, a category and be enabled by default, because
    /// an analyzer with an empty category or a disabled default is invisible to the user.
    /// </summary>
    [Fact]
    public void EveryDescriptor_ShouldCarryCompleteMetadata()
    {
        foreach (var descriptor in DiagnosticDescriptors.All)
        {
            descriptor.Title.ToString().Should().NotBeNullOrWhiteSpace();
            descriptor.MessageFormat.ToString().Should().NotBeNullOrWhiteSpace();
            descriptor.Category.Should().NotBeNullOrWhiteSpace();
            descriptor.IsEnabledByDefault.Should().BeTrue();
        }
    }
}
