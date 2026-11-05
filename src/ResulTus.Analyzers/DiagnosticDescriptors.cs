// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ResulTus.Analyzers;

/// <summary>
/// The six diagnostics this package ships. A consumer suppresses or escalates a rule by id, so the
/// ids are part of the public contract and a test pins them.
/// </summary>
public static class DiagnosticDescriptors
{
    private const string ReliabilityCategory = "Reliability";
    private const string UsageCategory = "Usage";
    private const string StyleCategory = "Style";

    /// <summary>
    /// Gets the descriptor for a result that is produced and then dropped.
    /// </summary>
    public static DiagnosticDescriptor IgnoredResult { get; } = new(
        id: "RESULT001",
        title: "Result is ignored",
        messageFormat: "The result of this operation is discarded; assign it or check its outcome",
        category: ReliabilityCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the descriptor for a value read that never checks the outcome it came from.
    /// </summary>
    public static DiagnosticDescriptor UnguardedOutcome { get; } = new(
        id: "RESULT002",
        title: "Outcome is not checked",
        messageFormat: "'GetValueOrDefault' returns the default value on failure; check the outcome first",
        category: ReliabilityCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the descriptor for a deconstruction that discards the error.
    /// </summary>
    public static DiagnosticDescriptor DiscardedError { get; } = new(
        id: "RESULT003",
        title: "Deconstruction discards the error",
        messageFormat: "This deconstruction discards the error; capture it so a failure is not lost",
        category: ReliabilityCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the descriptor for a <c>Bind</c> that only wraps its value back into a result.
    /// </summary>
    public static DiagnosticDescriptor RedundantBind { get; } = new(
        id: "RESULT004",
        title: "Bind is a Map",
        messageFormat: "This 'Bind' only wraps the value in a success; use 'Map' instead",
        category: StyleCategory,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the descriptor for converting a result into another result.
    /// </summary>
    public static DiagnosticDescriptor NestedResult { get; } = new(
        id: "RESULT005",
        title: "Result nested in a Result",
        messageFormat: "This conversion wraps a result in another result; it is already a result",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the descriptor for an <c>Ensure</c> guard that cannot depend on the value.
    /// </summary>
    public static DiagnosticDescriptor ConstantEnsure { get; } = new(
        id: "RESULT006",
        title: "Ensure predicate is constant",
        messageFormat: "This 'Ensure' predicate ignores the value and always returns the same result",
        category: ReliabilityCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets every descriptor, in id order.
    /// </summary>
    public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
        IgnoredResult,
        UnguardedOutcome,
        DiscardedError,
        RedundantBind,
        NestedResult,
        ConstantEnsure);
}
