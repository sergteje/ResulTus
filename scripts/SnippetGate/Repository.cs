// Copyright (c) ResulTus. All rights reserved.

namespace SnippetGate;

/// <summary>
/// Locates the repository and the exact set of documents the gate compiles.
/// </summary>
internal static class Repository
{
    private const string RootMarker = "ResulTus.slnx";

    /// <summary>
    /// Walks up from the current directory to the repository root. The gate is invoked as
    /// <c>dotnet run --project scripts/SnippetGate</c> from the root, but resolving the marker
    /// keeps it correct from any subdirectory and from CI.
    /// </summary>
    internal static string Locate()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RootMarker)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"SnippetGate must run inside the ResulTus repository: {RootMarker} not found above {Directory.GetCurrentDirectory()}.");
    }

    /// <summary>
    /// The scanned set, in a deterministic order: the two root readmes, every markdown file under
    /// docs (recursive) and one readme per package. CHANGELOG.md, CONTRIBUTING.md, AGENTS.md,
    /// SECURITY.md and odd/ are deliberately outside it: they describe process, not the API.
    /// </summary>
    internal static IReadOnlyList<string> Documents(string root)
    {
        var documents = new List<string>();

        foreach (var name in new[] { "README.md", "README.es.md" })
        {
            if (File.Exists(Path.Combine(root, name)))
            {
                documents.Add(name);
            }
        }

        AddMarkdownFiles(Path.Combine(root, "docs"), SearchOption.AllDirectories, root, documents);

        foreach (var package in SortedDirectories(Path.Combine(root, "src")))
        {
            var readme = Path.Combine(package, "README.md");
            if (File.Exists(readme))
            {
                documents.Add(Relative(root, readme));
            }
        }

        return documents.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Every shipped project, sorted, as absolute forward-slashed paths for MSBuild.</summary>
    internal static IReadOnlyList<string> SourceProjects(string root) =>
        SortedDirectories(Path.Combine(root, "src"))
            .Select(package => Path.Combine(package, Path.GetFileName(package) + ".csproj"))
            .Where(File.Exists)
            .Select(project => Path.GetFullPath(project).Replace('\\', '/'))
            .ToArray();

    internal static string Relative(string root, string fullPath) =>
        Path.GetRelativePath(root, fullPath).Replace('\\', '/');

    private static void AddMarkdownFiles(string directory, SearchOption search, string root, List<string> documents)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        documents.AddRange(Directory
            .EnumerateFiles(directory, "*.md", search)
            .Select(file => Relative(root, file))
            .OrderBy(file => file, StringComparer.Ordinal));
    }

    private static IEnumerable<string> SortedDirectories(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateDirectories(directory).OrderBy(path => path, StringComparer.Ordinal)
            : [];
}
