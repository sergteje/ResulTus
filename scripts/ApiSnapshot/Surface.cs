// Copyright (c) ResulTus. All rights reserved.

// The public-surface extraction shared by the ApiSnapshot tool (which writes the contract
// snapshots) and the API-compatibility tests (which re-derive it and diff against them).
// Deliberately reflection-only: no Roslyn, no source rewriting — the loaded assemblies ARE
// the contract.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ResulTus.ApiSurface;

/// <summary>
/// The shipped assemblies whose public members form the contract.
/// </summary>
/// <remarks>
/// The analyzers and the source generator are NOT in the list by design: their public surface
/// is the attribute and metadata contract, exercised by their own test projects in-process
/// against Roslyn, which a managed reflection script would load out of context.
/// </remarks>
public static class ShippedAssemblies
{
    public static readonly string[] Names =
    [
        "ResulTus",
        "ResulTus.Extensions",
        "ResulTus.Collections",
        "ResulTus.Serialization",
        "ResulTus.AspNetCore",
        "ResulTus.HttpClient",
        "ResulTus.Logging",
        "ResulTus.Diagnostics",
        "ResulTus.Validation",
        "ResulTus.Validation.FluentValidation",
        "ResulTus.Testing",
    ];

    /// <summary>
    /// Resolves every shipped assembly through the currently loaded types: the tool and the test
    /// project reference them, so one touch per assembly is enough to locate it.
    /// </summary>
    public static Assembly Resolve(string name) => name switch
    {
        "ResulTus" => typeof(ResulTus.Error).Assembly,
        "ResulTus.Extensions" => typeof(Extensions.ResultExtensions).Assembly,
        "ResulTus.Collections" => typeof(Collections.ResultCollectionExtensions).Assembly,
        "ResulTus.Serialization" => typeof(Serialization.UnitResultJsonConverter).Assembly,
        "ResulTus.AspNetCore" => typeof(AspNetCore.ResultHttpStatus).Assembly,
        "ResulTus.HttpClient" => typeof(HttpClient.HttpClientError).Assembly,
        "ResulTus.Logging" => typeof(Logging.ResultLoggingExtensions).Assembly,
        "ResulTus.Diagnostics" => typeof(Diagnostics.ResultDiagnostics).Assembly,
        "ResulTus.Validation" => typeof(Validation.ValidationErrors).Assembly,
        "ResulTus.Validation.FluentValidation" => typeof(Validation.FluentValidation.FluentValidationExtensions).Assembly,
        "ResulTus.Testing" => typeof(Testing.ResultAssertions).Assembly,
        _ => throw new InvalidOperationException($"Unknown shipped assembly '{name}'."),
    };
}

/// <summary>
/// Builds the sorted, deterministic public-surface lines of one assembly.
/// </summary>
public static class Surface
{
    /// <summary>
    /// Every public line of <paramref name="assembly"/>, sorted: one <c>T:</c> line per public
    /// type and its nested public types, and one line per member, sorted within each type.
    /// </summary>
    public static IReadOnlyList<string> Lines(Assembly assembly)
    {
        var lines = new List<string>();

        foreach (var type in PublicTypes(assembly).OrderBy(GetLine, StringComparer.Ordinal))
        {
            lines.Add(GetLine(type));

            foreach (var memberLine in MemberLines(type))
            {
                lines.Add(memberLine);
            }
        }

        return lines;
    }

    private static IEnumerable<Type> PublicTypes(Assembly assembly)
    {
        void Collect(Type source, List<Type> into)
        {
            into.Add(source);
            foreach (var nested in source.GetNestedTypes(BindingFlags.Public))
            {
                Collect(nested, into);
            }
        }

        var types = new List<Type>();
        foreach (var type in assembly.GetExportedTypes())
        {
            Collect(type, types);
        }

        return types;
    }

    private static string GetLine(Type type)
        => $"T: {Clean(type)}";

    private static IEnumerable<string> MemberLines(Type type)
    {
        const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        var owner = Clean(type);

        foreach (var constructor in type.GetConstructors(Flags))
        {
            yield return $"M: {owner}.ctor({RenderParameters(constructor)})";
        }

        foreach (var field in type.GetFields(Flags))
        {
            yield return $"F: {Clean(field.FieldType)} {owner}.{field.Name}";
        }

        foreach (var @event in type.GetEvents(Flags))
        {
            yield return $"E: {owner}.{@event.Name}";
        }

        foreach (var property in type.GetProperties(Flags))
        {
            var getter = property.GetMethod is not null ? "get; " : string.Empty;
            var setter = property.SetMethod is not null ? "set; " : string.Empty;
            yield return $"P: {Clean(property.PropertyType)} {owner}.{property.Name} {{ {getter}{setter}}}";
        }

        foreach (var method in type.GetMethods(Flags))
        {
            if (method.IsSpecialName && method.Name.StartsWith("op_", StringComparison.Ordinal))
            {
                yield return $"O: {owner}.{method.Name}({RenderParameters(method)}) -> {Clean(method.ReturnType)}";
                continue;
            }

            if (method.IsSpecialName)
            {
                continue;
            }

            yield return $"M: {owner}.{method.Name}({RenderParameters(method)}) -> {Clean(method.ReturnType)}";
        }
    }

    /// <summary>
    /// Renders a type's identity: arity stripped into angle brackets, nested types dotted, value
    /// and reference layers dissolved so the same member yields one spelling per surface.
    /// </summary>
    private static string Clean(Type type)
    {
        if (type.IsByRef || type.IsPointer)
        {
            type = type.GetElementType()!;
        }

        var name = type.FullName ?? type.Name;
        var arity = name.IndexOf('`');
        if (arity < 0)
        {
            return name;
        }

        var baseName = name[..arity];
        if (!type.IsGenericType)
        {
            return baseName;
        }

        return baseName + "<" + string.Join(", ", type.GetGenericArguments().Select(Clean)) + ">";
    }

    private static string RenderParameters(MethodBase method)
    {
        var parameters = method.GetParameters();
        return parameters.Length == 0
            ? string.Empty
            : string.Join(
                ", ",
                parameters.Select(parameter =>
                {
                    var direction = parameter.ParameterType.IsByRef
                        ? (parameter.IsOut ? "out " : "ref ")
                        : string.Empty;
                    return $"{direction}{Clean(parameter.ParameterType)} {parameter.Name}";
                }));
    }
}
