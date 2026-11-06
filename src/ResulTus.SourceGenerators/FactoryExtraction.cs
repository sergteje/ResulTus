// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ResulTus.SourceGenerators;

/// <summary>
/// Reads a <c>[ErrorFactory]</c> method declaration and turns it into the file the generator emits,
/// or into the reason it cannot be emitted.
/// </summary>
/// <remarks>
/// Every rejection is a separate statement rather than part of one condition, because each one has
/// its own message: the point of <c>RESULTGEN001</c> is to say which member is wrong and why, not
/// only that something is.
/// </remarks>
internal static class FactoryExtraction
{
    private const string ResultErrorsAttribute = "global::ResulTus.ResultErrorsAttribute";

    private const string ErrorType = "global::ResulTus.Error";

    private const string NoneMember = "None";

    /// <summary>
    /// The format used for every type written into the generated file: fully qualified, with
    /// nullable reference annotations kept. Special types are not collapsed to keywords, so
    /// <c>int</c> is written <c>global::System.Int32</c> and cannot be shadowed by a user type
    /// named <c>Int32</c> in the file being generated.
    /// </summary>
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Extracts the factory declared by the annotated method.
    /// </summary>
    /// <param name="context">The context Roslyn supplies for the annotated method.</param>
    /// <param name="cancellationToken">The token that cancels the extraction.</param>
    /// <returns>
    /// The generated file, or the reason the declaration is unusable, or nothing when the target is
    /// not a method the generator handles.
    /// </returns>
    internal static FactoryCandidate Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not IMethodSymbol method)
        {
            return FactoryCandidate.Ignored();
        }

        // A method always has a containing type, and the attribute is only valid on a class or a
        // struct, so this is the type the generated part has to reopen.
        var containingType = method.ContainingType!;

        var typeName = containingType.ToDisplayString();

        if (!IsOptedIn(containingType))
        {
            return FactoryCandidate.Unusable(
                $"The type '{typeName}' is not marked with [ResultErrors], so the factory '{method.Name}' cannot be generated. Add [ResultErrors] to the type.",
                FirstLocation(containingType));
        }

        var chain = new List<ContainingType>();

        for (var current = containingType; current is not null; current = current.ContainingType)
        {
            // A file-local type cannot be reopened from another file, so no second part can merge
            // with it and nothing can be generated.
            if (current.IsFileLocal)
            {
                return FactoryCandidate.Unusable(
                    $"The type '{current.ToDisplayString()}' is file-local, so the factory '{method.Name}' cannot be generated. Remove the 'file' modifier.",
                    FirstLocation(current));
            }

            var declaration = GetDeclaration(current, cancellationToken);

            if (!IsPartial(declaration))
            {
                return FactoryCandidate.Unusable(
                    $"The type '{current.ToDisplayString()}' must be declared partial, because the generated factory is a second part of it.",
                    FirstLocation(current));
            }

            chain.Add(new ContainingType(current.Name, RenderDeclaration(declaration)));
        }

        chain.Reverse();

        // A partial method that already has a body is either the implementation part itself or a
        // definition whose implementation the user wrote; either way there is nothing to add.
        if (method.PartialDefinitionPart is not null || method.PartialImplementationPart is not null)
        {
            return FactoryCandidate.Unusable(
                $"The factory '{method.Name}' already has a body, so the generator has nothing to add. Remove the body or the [ErrorFactory] attribute.",
                FirstLocation(method));
        }

        if (!method.IsPartialDefinition)
        {
            return FactoryCandidate.Unusable(
                $"The method '{method.Name}' must be declared partial, because the generator supplies its body.",
                FirstLocation(method));
        }

        if (!method.IsStatic)
        {
            return FactoryCandidate.Unusable(
                $"The factory '{method.Name}' must be static.",
                FirstLocation(method));
        }

        var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (returnType != ErrorType)
        {
            return FactoryCandidate.Unusable(
                $"The factory '{method.Name}' must return ResulTus.Error, but returns '{method.ReturnType.ToDisplayString()}'.",
                FirstLocation(method));
        }

        if (HasOutParameter(method))
        {
            return FactoryCandidate.Unusable(
                $"The factory '{method.Name}' has an 'out' parameter. The generated body returns an Error and cannot assign it, and assigning a default would hand the caller a value the factory never produced. Return the value instead.",
                FirstLocation(method));
        }

        var attribute = context.Attributes[0];

        if (attribute.ConstructorArguments.Length != 3
            || attribute.ConstructorArguments[0].Value is not string code)
        {
            return FactoryCandidate.Unusable(
                $"The factory '{method.Name}' must be annotated with [ErrorFactory(code, type, message)].",
                FirstLocation(method));
        }

        var typeConstant = attribute.ConstructorArguments[1];
        var typeValue = Convert.ToInt32(typeConstant.Value, CultureInfo.InvariantCulture);
        var typeMember = ResolveEnumMemberName((INamedTypeSymbol)typeConstant.Type!, typeValue);

        if (typeMember is null)
        {
            return FactoryCandidate.Unusable(
                $"The factory '{method.Name}' declares an [ErrorFactory] type that is not a member of ResulTus.ErrorType.",
                FirstLocation(method));
        }

        // A factory that produces "no error" is not a factory, and ResulTus.Error exposes None as a
        // property rather than as a factory, so the generated call would not even compile.
        if (typeMember == NoneMember)
        {
            return FactoryCandidate.Unusable(
                $"The factory '{method.Name}' declares ErrorType.None, which is the absence of an error. Use one of the other ErrorType values.",
                FirstLocation(method));
        }

        var shape = new FactoryShape(
            RenderNamespace(containingType.ContainingNamespace!),
            chain.ToImmutableArray(),
            RenderAccessibility(method.DeclaredAccessibility),
            method.Name,
            RenderParameters(method),
            code,
            typeMember,
            attribute.ConstructorArguments[2].Value as string);

        return FactoryCandidate.Generated(FactoryEmitter.Emit(shape));
    }

    private static bool HasOutParameter(IMethodSymbol method)
    {
        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind == RefKind.Out)
            {
                return true;
            }
        }

        return false;
    }

    private static Location FirstLocation(ISymbol symbol) => symbol.Locations[0];

    private static bool IsOptedIn(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == ResultErrorsAttribute)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the one declaration of <paramref name="type"/> that is in source.
    /// </summary>
    /// <param name="type">A type declared in the compilation.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The declaration.</returns>
    /// <remarks>
    /// A type that declares a method is declared in source, so it always has a syntax reference, and
    /// <c>[ResultErrors]</c> is only valid on a class or a struct — which is what makes the cast to
    /// <see cref="TypeDeclarationSyntax"/> safe.
    /// </remarks>
    private static TypeDeclarationSyntax GetDeclaration(
        INamedTypeSymbol type,
        CancellationToken cancellationToken)
        => (TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);

    private static bool IsPartial(TypeDeclarationSyntax declaration)
    {
        foreach (var modifier in declaration.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Rebuilds the type's declaration header from its own source, so the generated part repeats
    /// exactly the modifiers the user wrote instead of a hand-maintained approximation of them.
    /// </summary>
    /// <param name="declaration">The type's declaration.</param>
    /// <returns>The header, without a body.</returns>
    private static string RenderDeclaration(TypeDeclarationSyntax declaration)
    {
        var builder = new StringBuilder();

        foreach (var modifier in declaration.Modifiers)
        {
            builder.Append(modifier.Text);
            builder.Append(' ');
        }

        builder.Append(GetKeyword(declaration));
        builder.Append(' ');
        builder.Append(declaration.Identifier.Text);

        if (declaration.TypeParameterList is not null)
        {
            builder.Append(declaration.TypeParameterList.ToString());
        }

        return builder.ToString();
    }

    private static string GetKeyword(TypeDeclarationSyntax declaration)
    {
        switch (declaration)
        {
            case RecordDeclarationSyntax record:
                return record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                    ? "record struct"
                    : "record";

            case StructDeclarationSyntax:
                return "struct";

            default:
                return "class";
        }
    }

    private static string? RenderNamespace(INamespaceSymbol containingNamespace)
    {
        if (containingNamespace.IsGlobalNamespace)
        {
            return null;
        }

        var parts = new List<string>();

        for (var current = containingNamespace; !current.IsGlobalNamespace; current = current.ContainingNamespace!)
        {
            parts.Add(current.Name);
        }

        parts.Reverse();

        return string.Join(".", parts);
    }

    private static string RenderAccessibility(Accessibility accessibility)
    {
        switch (accessibility)
        {
            case Accessibility.Public:
                return "public ";

            case Accessibility.Internal:
                return "internal ";

            case Accessibility.Protected:
                return "protected ";

            case Accessibility.ProtectedOrInternal:
                return "protected internal ";

            case Accessibility.ProtectedAndInternal:
                return "private protected ";

            default:
                return "private ";
        }
    }

    private static string RenderParameters(IMethodSymbol method)
    {
        var builder = new StringBuilder();

        for (var index = 0; index < method.Parameters.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            var parameter = method.Parameters[index];

            if (parameter.IsParams)
            {
                // params is part of the signature, so the implementation must repeat it. It cannot
                // be combined with a ref kind, so the two are exclusive.
                builder.Append("params ");
            }
            else
            {
                switch (parameter.RefKind)
                {
                    case RefKind.Ref:
                        builder.Append("ref ");
                        break;

                    case RefKind.In:
                        builder.Append("in ");
                        break;

                    case RefKind.RefReadOnlyParameter:
                        builder.Append("ref readonly ");
                        break;
                }
            }

            builder.Append(parameter.Type.ToDisplayString(TypeFormat));
            builder.Append(' ');
            builder.Append(parameter.Name);
        }

        return builder.ToString();
    }

    private static string? ResolveEnumMemberName(INamedTypeSymbol type, int value)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol field
                && field.HasConstantValue
                && field.ConstantValue is not null
                && Convert.ToInt32(field.ConstantValue, CultureInfo.InvariantCulture) == value)
            {
                return field.Name;
            }
        }

        return null;
    }
}
