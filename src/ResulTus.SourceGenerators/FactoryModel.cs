// Copyright (c) ResulTus. All rights reserved.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ResulTus.SourceGenerators;

/// <summary>
/// One type in the chain that encloses a generated factory, already rendered as a declaration.
/// </summary>
/// <param name="Name">The type's name, without its type parameter list.</param>
/// <param name="Declaration">
/// The declaration header, for example <c>public static partial class UserErrors</c> or
/// <c>internal partial record struct Envelope&lt;T&gt;</c>.
/// </param>
internal sealed record ContainingType(string Name, string Declaration);

/// <summary>
/// One generated factory, already rendered as a complete file.
/// </summary>
/// <param name="HintName">The name the file is added under.</param>
/// <param name="SourceText">The file's contents.</param>
/// <remarks>
/// The model holds finished text rather than a tree of symbols on purpose: the incremental pipeline
/// compares models to decide whether to re-run the output step, and strings compare structurally.
/// A model built from symbol references would compare by reference and defeat the caching.
/// </remarks>
internal sealed record FactoryModel(string HintName, string SourceText);

/// <summary>
/// The parts of a factory that are read from the declaration and written into the generated file.
/// </summary>
/// <param name="Namespace">The containing namespace, or <c>null</c> for the global one.</param>
/// <param name="ContainingTypes">The type chain, outermost first, including the declaring type.</param>
/// <param name="Accessibility">The method's declared accessibility, with its trailing space.</param>
/// <param name="MethodName">The method's name.</param>
/// <param name="Parameters">The rendered parameter list, without the parentheses.</param>
/// <param name="Code">The error code from the attribute.</param>
/// <param name="Type">
/// The name of the <c>ResulTus.ErrorType</c> member from the attribute, which is also the name of
/// the <c>ResulTus.Error</c> factory the generated body calls. It is a name rather than an enum
/// value because this assembly targets <c>netstandard2.0</c> and never references the runtime
/// library it generates code for.
/// </param>
/// <param name="MessageTemplate">The message template from the attribute, or <c>null</c>.</param>
internal sealed record FactoryShape(
    string? Namespace,
    ImmutableArray<ContainingType> ContainingTypes,
    string Accessibility,
    string MethodName,
    string Parameters,
    string Code,
    string Type,
    string? MessageTemplate);

/// <summary>
/// A declaration the generator cannot complete, with the reason and the place to point at.
/// </summary>
/// <param name="Message">The reason, which names the offending member.</param>
/// <param name="Location">Where the diagnostic is reported.</param>
internal sealed record FactoryDiagnostic(string Message, Location Location);

/// <summary>
/// The outcome of reading one <c>[ErrorFactory]</c> declaration.
/// </summary>
/// <param name="Model">The file to emit, or <c>null</c> when the declaration is unusable.</param>
/// <param name="Diagnostic">The reason the declaration is unusable, or <c>null</c> when it is fine.</param>
/// <remarks>
/// The model is finished text, so the pipeline's cached value compares by content and the output
/// step is skipped when nothing changed.
/// </remarks>
internal sealed record FactoryCandidate(FactoryModel? Model, FactoryDiagnostic? Diagnostic)
{
    /// <summary>
    /// Creates the candidate for a declaration that can be completed.
    /// </summary>
    /// <param name="model">The rendered file.</param>
    /// <returns>The candidate.</returns>
    internal static FactoryCandidate Generated(FactoryModel model) => new(model, null);

    /// <summary>
    /// Creates the candidate for a declaration that cannot be completed.
    /// </summary>
    /// <param name="message">The reason, which names the offending member.</param>
    /// <param name="location">Where to report it.</param>
    /// <returns>The candidate.</returns>
    internal static FactoryCandidate Unusable(string message, Location location)
        => new(null, new FactoryDiagnostic(message, location));

    /// <summary>
    /// Creates the candidate for a target the generator does not handle at all.
    /// </summary>
    /// <returns>A candidate that produces nothing and reports nothing.</returns>
    internal static FactoryCandidate Ignored() => new(null, null);
}
