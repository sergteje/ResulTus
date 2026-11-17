// Copyright (c) ResulTus. All rights reserved.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SnippetGate;

/// <summary>One scanned document and the blocks that were read out of it.</summary>
internal sealed class DocumentSnippets
{
    internal DocumentSnippets(string relativePath, IReadOnlyList<SnippetBlock> blocks)
    {
        RelativePath = relativePath;
        Blocks = blocks;
    }

    internal string RelativePath { get; }

    internal IReadOnlyList<SnippetBlock> Blocks { get; }

    /// <summary>
    /// The identifier derived from the document path, which names its project, namespace and
    /// snippet class. The readable part alone is not enough: two paths that differ only in
    /// punctuation (`docs/error-handling.md` and `docs/error_handling.md`) reduce to the same
    /// letters, and the second document would overwrite the first and be listed twice in the
    /// solution. The hash of the exact repository-relative path keeps the identity collision-free
    /// and identical on every machine, and stays a valid identifier and file name.
    /// One compilation per document is what lets two documents both declare <c>Product</c> or
    /// <c>AppJsonContext</c>, exactly as two listings in prose do: the C# compiler tolerates the
    /// duplicate names in separate namespaces, but the source generators (System.Text.Json and the
    /// ResulTus error factory) name their generated files after the declared type alone, so two
    /// same-named types in one compilation would collide there.
    /// </summary>
    internal string Identifier =>
        Sanitize(Path.ChangeExtension(RelativePath, null)) + "_" + PathHash(RelativePath);

    internal string Namespace => "SnippetGate.Generated." + Identifier;

    internal string ClassName => "__Snippets_" + Identifier;

    /// <summary>The first six hex characters of the path's SHA-256, lower-case, so it is path-stable.</summary>
    private static string PathHash(string relativePath)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(relativePath));
        return Convert.ToHexString(digest)[..6].ToLowerInvariant();
    }

    private static string Sanitize(string relativePath)
    {
        var builder = new StringBuilder(relativePath.Length);
        foreach (var character in relativePath)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length == 0 || builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        return builder.Length == 0 || char.IsDigit(builder[0]) ? "_" + builder : builder.ToString();
    }
}

/// <summary>Where a generated line came from: the documentation block and the markdown line.</summary>
internal sealed record Origin(int Block, int MarkdownLine);

/// <summary>A generated file plus the map that puts a compiler line back on a documentation line.</summary>
internal sealed class GeneratedDocument
{
    internal GeneratedDocument(string filePath, string documentPath, Dictionary<int, Origin> lineToOrigin)
    {
        FilePath = filePath;
        DocumentPath = documentPath;
        LineToOrigin = lineToOrigin;
    }

    internal string FilePath { get; }

    /// <summary>The repository-relative markdown path this file was generated from.</summary>
    internal string DocumentPath { get; }

    /// <summary>Generated line (1-based) to the documented line that produced it.</summary>
    internal Dictionary<int, Origin> LineToOrigin { get; }
}

/// <summary>
/// Writes the throwaway project tree: one project per document, the checked-in stub harness shared
/// by all of them, and a solution that builds every project in one compiler invocation.
/// </summary>
internal static class GeneratedProject
{
    internal const string SolutionFileName = "SnippetGate.Generated.slnx";

    private const string GlobalUsing = "global using ";

    internal static string ProjectFileName(DocumentSnippets document) => document.Identifier + ".csproj";

    internal static IReadOnlyList<GeneratedDocument> Write(
        string root,
        string directory,
        IReadOnlyList<DocumentSnippets> documents)
    {
        var stubs = Path.Combine(directory, "Stubs");
        CopyStubs(root, stubs);

        var generated = new List<GeneratedDocument>();
        foreach (var document in documents)
        {
            generated.AddRange(WriteDocument(root, directory, document, stubs));
        }

        File.WriteAllText(
            Path.Combine(directory, SolutionFileName),
            SolutionFile(documents),
            new UTF8Encoding(false));

        return generated;
    }

    /// <summary>
    /// Writes the document's project: one file for the namespace the gate controls, plus one file
    /// per declaration block that brings its own namespace.
    /// </summary>
    private static IReadOnlyList<GeneratedDocument> WriteDocument(
        string root,
        string directory,
        DocumentSnippets document,
        string stubs)
    {
        var projectDirectory = Path.Combine(directory, document.Identifier);
        Directory.CreateDirectory(projectDirectory);

        var declaredNamespaces = document.Blocks
            .SelectMany(block => block.DeclaredNamespaces)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var generated = new List<GeneratedDocument>
        {
            WriteStatementFile(projectDirectory, document, declaredNamespaces),
        };

        foreach (var block in document.Blocks.Where(block => block.DeclaresNamespace))
        {
            generated.Add(WriteNamespaceFile(projectDirectory, document, block, declaredNamespaces));
        }

        File.WriteAllText(
            Path.Combine(projectDirectory, ProjectFileName(document)),
            ProjectFile(root, stubs),
            new UTF8Encoding(false));

        return generated;
    }

    private static GeneratedDocument WriteStatementFile(
        string projectDirectory,
        DocumentSnippets document,
        IReadOnlyList<string> declaredNamespaces)
    {
        var writer = new SourceWriter();

        writer.Write("// <auto-generated/>");
        writer.Write($"// Generated by scripts/SnippetGate from {document.RelativePath}. Do not edit.");
        writer.Write(string.Empty);
        writer.Write("// Ambient state the fragments share across fences: a later block continues the value an");
        writer.Write("// earlier one introduced. Nothing else is supplied from outside the document.");
        WriteUsings(writer, document, declaredNamespaces);

        writer.Write(string.Empty);
        writer.Write("namespace " + document.Namespace + ";");

        var plain = document.Blocks.Where(block => !block.DeclaresNamespace).ToArray();
        if (plain.Any(block => block.Declarations.Count > 0))
        {
            writer.Write(string.Empty);
            writer.Write("// The types the document declares for itself, at namespace level so an attribute-driven");
            writer.Write("// generator sees the same declaration a reader copies into a project.");
            foreach (var entry in plain.SelectMany(block => block.Declarations.Select(line => (block.Ordinal, line))))
            {
                writer.Write(entry.Ordinal, entry.line);
            }
        }

        if (document.Blocks.Any(block => block.DeclaresNamespace))
        {
            writer.Write(string.Empty);
            writer.Write("// A listing that carries its own namespace is compiled in a file of its own, because a");
            writer.Write("// source file may only contain one file-scoped namespace declaration. Those types are");
            writer.Write("// imported above, so the statements below still see them.");
        }

        writer.Write(string.Empty);
        writer.Write("internal static class " + document.ClassName);
        writer.Write("{");

        if (document.Blocks.Any(block => block.Functions.Count > 0))
        {
            writer.Write(string.Empty);
            writer.Write("    // The local functions the document shares between fences, in their member form.");
            foreach (var entry in document.Blocks.SelectMany(block => block.Functions.Select(line => (block.Ordinal, line))))
            {
                writer.Write(entry.Ordinal, entry.line);
            }
        }

        foreach (var block in document.Blocks.Where(block => block.Statements.Count > 0))
        {
            writer.Write(string.Empty);
            writer.Write($"    // block #{block.Ordinal} - {document.RelativePath}:{block.StartLine}");
            var signature = block.NeedsAsync ? "private static async Task" : "private static void";
            writer.Write($"    {signature} __Snippet_{block.Ordinal}()");
            writer.Write("    {");
            foreach (var line in block.Statements)
            {
                writer.Write(block.Ordinal, line);
            }

            writer.Write("    }");
        }

        writer.Write("}");

        var path = Path.Combine(projectDirectory, document.Identifier + ".g.cs");
        File.WriteAllText(path, writer.ToString(), new UTF8Encoding(false));
        return new GeneratedDocument(path, document.RelativePath, writer.LineToOrigin);
    }

    /// <summary>
    /// Writes one declaration block verbatim, with no generated namespace in front of it. The file
    /// carries the document's usings so the listing still resolves the packages it references.
    /// </summary>
    private static GeneratedDocument WriteNamespaceFile(
        string projectDirectory,
        DocumentSnippets document,
        SnippetBlock block,
        IReadOnlyList<string> declaredNamespaces)
    {
        var writer = new SourceWriter();

        writer.Write("// <auto-generated/>");
        writer.Write($"// Generated by scripts/SnippetGate from {document.RelativePath}, block #{block.Ordinal}.");
        writer.Write("// The listing declares its own namespace, so it is compiled as its own file: a source file");
        writer.Write("// may only contain one file-scoped namespace declaration, and the statement file already");
        writer.Write("// has the generated one. The statements import the namespaces declared here.");
        WriteUsings(writer, document, declaredNamespaces);

        writer.Write(string.Empty);
        foreach (var line in block.Declarations)
        {
            writer.Write(block.Ordinal, line);
        }

        var path = Path.Combine(
            projectDirectory,
            string.Create(CultureInfo.InvariantCulture, $"{document.Identifier}.ns{block.Ordinal}.g.cs"));
        File.WriteAllText(path, writer.ToString(), new UTF8Encoding(false));
        return new GeneratedDocument(path, document.RelativePath, writer.LineToOrigin);
    }

    /// <summary>
    /// Emits the document's hoisted directives in the order the compiler demands: a global using
    /// must precede every non-global one (CS8915), so the harness import sits between them.
    /// </summary>
    private static void WriteUsings(
        SourceWriter writer,
        DocumentSnippets document,
        IReadOnlyList<string> declaredNamespaces)
    {
        var hoisted = document.Blocks
            .SelectMany(block => block.Usings.Select(usingLine => (block.Ordinal, Line: usingLine)))
            .GroupBy(entry => entry.Line.Text, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        foreach (var entry in hoisted
            .Where(entry => entry.Line.Text.StartsWith(GlobalUsing, StringComparison.Ordinal))
            .OrderBy(entry => entry.Line.Text, StringComparer.Ordinal))
        {
            writer.Write(entry.Ordinal, entry.Line);
        }

        writer.Write("using static global::__SnippetGlobals;");

        var rest = hoisted
            .Where(entry => !entry.Line.Text.StartsWith(GlobalUsing, StringComparison.Ordinal))
            .OrderBy(entry => entry.Line.Text, StringComparer.Ordinal)
            .ToArray();

        foreach (var entry in rest)
        {
            writer.Write(entry.Ordinal, entry.Line);
        }

        // A declared namespace has no line of its own in the document: the gate adds the import so
        // the statements see the types the listing just declared.
        foreach (var name in declaredNamespaces)
        {
            var directive = "using " + name + ";";
            if (!rest.Any(entry => string.Equals(entry.Line.Text.Trim(), directive, StringComparison.Ordinal)))
            {
                writer.Write(directive);
            }
        }
    }

    private static string ProjectFile(string root, string stubs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        builder.AppendLine();
        builder.AppendLine("  <!-- Written by scripts/SnippetGate into a temporary directory. Documentation fragments are");
        builder.AppendLine("       compiled, never run: warnings a fragment cannot avoid (an unused using, an unused local)");
        builder.AppendLine("       are switched off, and a compile error is the only thing that fails. -->");
        builder.AppendLine("  <PropertyGroup>");
        builder.AppendLine("    <TargetFramework>net10.0</TargetFramework>");
        builder.AppendLine("    <OutputType>Library</OutputType>");
        builder.AppendLine("    <Nullable>enable</Nullable>");
        builder.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        builder.AppendLine("    <LangVersion>latest</LangVersion>");
        builder.AppendLine("    <IsPackable>false</IsPackable>");
        builder.AppendLine("    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>");
        builder.AppendLine("    <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>");
        builder.AppendLine("    <GenerateDocumentationFile>false</GenerateDocumentationFile>");
        builder.AppendLine("    <NoWarn>CS8019;CS0168;CS0219;CS8321</NoWarn>");
        builder.AppendLine("  </PropertyGroup>");
        builder.AppendLine();
        builder.AppendLine("  <ItemGroup>");
        builder.AppendLine("    <!-- The ASP.NET Core snippets are real Minimal API and MVC code. -->");
        builder.AppendLine("    <FrameworkReference Include=\"Microsoft.AspNetCore.App\" />");
        builder.AppendLine("  </ItemGroup>");
        builder.AppendLine();
        builder.AppendLine("  <ItemGroup>");
        builder.AppendLine("    <!-- One copy of the stub harness on disk, compiled into every document assembly. -->");
        builder.Append("    <Compile Include=\"")
            .Append(Xml(Path.GetFullPath(stubs).Replace('\\', '/')))
            .AppendLine("/*.cs\" Link=\"Stubs/%(Filename)%(Extension)\" />");
        builder.AppendLine("  </ItemGroup>");
        builder.AppendLine();
        builder.AppendLine("  <ItemGroup>");
        foreach (var project in Repository.SourceProjects(root))
        {
            if (Path.GetFileName(project) == "ResulTus.SourceGenerators.csproj")
            {
                builder.AppendLine("    <!-- A Roslyn component, not a reference: [ResultErrors] and [ErrorFactory] generate");
                builder.AppendLine("         the partial implementations the source-generator snippets call. -->");
                builder.AppendLine(
                    $"    <ProjectReference Include=\"{Xml(project)}\" OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\" />");
            }
            else
            {
                builder.AppendLine($"    <ProjectReference Include=\"{Xml(project)}\" />");
            }
        }

        builder.AppendLine("  </ItemGroup>");
        builder.AppendLine();
        builder.AppendLine("</Project>");
        return builder.ToString();
    }

    private static string SolutionFile(IReadOnlyList<DocumentSnippets> documents)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<Solution>");
        builder.AppendLine("  <Folder Name=\"/documents/\">");
        foreach (var document in documents)
        {
            builder.AppendLine(
                $"    <Project Path=\"{Xml(document.Identifier + "/" + ProjectFileName(document))}\" />");
        }

        builder.AppendLine("  </Folder>");
        builder.AppendLine("</Solution>");
        return builder.ToString();
    }

    /// <summary>
    /// Escapes a value that becomes an XML attribute. A checkout below a directory named <c>R&amp;D</c>
    /// or containing a quote would otherwise produce a project file no parser accepts. Ampersand
    /// goes first, or the entities it introduces would be escaped twice.
    /// </summary>
    private static string Xml(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&apos;", StringComparison.Ordinal);

    private static void CopyStubs(string root, string target)
    {
        Directory.CreateDirectory(target);

        var source = Path.Combine(root, "scripts", "SnippetGate", "Stubs");
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (var stub in Directory.EnumerateFiles(source, "*.cs").OrderBy(path => path, StringComparer.Ordinal))
        {
            File.Copy(stub, Path.Combine(target, Path.GetFileName(stub)), overwrite: true);
        }
    }

    /// <summary>Builds a source file while recording which documented line owns every line.</summary>
    private sealed class SourceWriter
    {
        private readonly StringBuilder builder = new();
        private int line;

        internal Dictionary<int, Origin> LineToOrigin { get; } = [];

        internal void Write(string text)
        {
            builder.Append(text).Append('\n');
            line++;
        }

        internal void Write(int block, SourceLine source)
        {
            builder.Append(source.Text).Append('\n');
            line++;
            LineToOrigin[line] = new Origin(block, source.MarkdownLine);
        }

        public override string ToString() => builder.ToString();
    }
}
