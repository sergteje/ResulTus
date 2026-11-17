// Copyright (c) ResulTus. All rights reserved.

namespace SnippetGate;

/// <summary>
/// Drives the gate: scan, generate, compile, map the compiler back onto the prose.
/// </summary>
internal static class SnippetGateRunner
{
    internal static int Run()
    {
        string root;
        try
        {
            root = Repository.Locate();
        }
        catch (InvalidOperationException error)
        {
            Console.Error.WriteLine(error.Message);
            return 2;
        }

        var documents = Load(root);
        var blocks = documents.SelectMany(document => document.Blocks.Select(block => (document, block))).ToArray();

        var skipped = blocks.Where(entry => entry.block.IsEmpty).ToArray();
        if (skipped.Length > 0)
        {
            foreach (var (document, block) in skipped)
            {
                Console.Error.WriteLine(
                    $"{document.RelativePath} block #{block.Ordinal} (line {block.StartLine}): a ```csharp fence with no"
                    + " content is neither compiled nor excluded. Remove the fence or give it code.");
            }

            return 1;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "resultus-snippet-gate",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        var generated = GeneratedProject.Write(root, directory, documents);
        var solution = Path.Combine(directory, GeneratedProject.SolutionFileName);
        var build = DotnetBuild.Run(solution);

        var failures = build.Diagnostics.Where(diagnostic => diagnostic.Severity == "error").ToArray();
        if (build.ExitCode == 0 && failures.Length == 0)
        {
            // There is no exclusion path: a fence that is not C# is never counted as a C# block, and an
            // empty C# fence already failed above. Saying "none excluded" states the design instead of
            // printing a computed zero that a reader could mistake for a lenient count.
            Console.WriteLine(
                $"snippet gate: {documents.Count} documents, {blocks.Length} blocks compiled, none excluded");
            Delete(directory);
            return 0;
        }

        Report(generated, directory, build, blocks.Length);
        return 1;
    }

    /// <summary>
    /// Removes the scratch project. Best effort: a build node that still holds a handle is a
    /// reason to warn, never a reason to fail a gate that just proved every snippet compiles.
    /// </summary>
    private static void Delete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException error)
        {
            Console.Error.WriteLine($"warning: could not delete {directory}: {error.Message}");
        }
        catch (UnauthorizedAccessException error)
        {
            Console.Error.WriteLine($"warning: could not delete {directory}: {error.Message}");
        }
    }

    private static IReadOnlyList<DocumentSnippets> Load(string root)
    {
        var documents = new List<DocumentSnippets>();

        foreach (var relativePath in Repository.Documents(root))
        {
            var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var fences = MarkdownFences.Read(File.ReadAllLines(fullPath));

            var blocks = fences
                .Where(fence => string.Equals(fence.Info, "csharp", StringComparison.Ordinal))
                .Select(SnippetBlock.Parse)
                .ToArray();

            if (blocks.Length == 0)
            {
                continue;
            }

            documents.Add(new DocumentSnippets(relativePath, blocks));
        }

        return documents;
    }

    private static void Report(
        IReadOnlyList<GeneratedDocument> generated,
        string directory,
        BuildResult build,
        int blockCount)
    {
        var byPath = generated.ToDictionary(
            document => Path.GetFullPath(document.FilePath),
            StringComparer.OrdinalIgnoreCase);
        var byRelativePath = generated.ToDictionary(
            document => Path.GetFileName(document.FilePath),
            StringComparer.OrdinalIgnoreCase);
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var diagnostic in build.Diagnostics.Where(diagnostic => diagnostic.Severity == "error"))
        {
            var document = Resolve(diagnostic.File, byPath, byRelativePath);
            var location = document is not null && document.LineToOrigin.TryGetValue(diagnostic.Line, out var origin)
                ? $"{document.DocumentPath}:{origin.MarkdownLine} (block #{origin.Block})"
                : document is not null
                    ? $"{document.DocumentPath} (generated wrapper)"
                    : StubOrTool(diagnostic.File, directory);

            var line = $"{location}: {diagnostic.Code}: {diagnostic.Message}";
            if (reported.Add(line))
            {
                Console.Error.WriteLine(line);
            }
        }

        if (reported.Count == 0)
        {
            Console.Error.WriteLine("the generated project did not compile, and the compiler reported no diagnostic:");
            foreach (var line in build.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).TakeLast(20))
            {
                Console.Error.WriteLine("    " + line);
            }
        }

        Console.Error.WriteLine(
            $"snippet gate: FAILED - {reported.Count} error(s) while compiling {blockCount} blocks");
        Console.Error.WriteLine($"generated project kept at: {directory}");
    }

    /// <summary>
    /// Maps a diagnostic path back to the document it came from. The compiler reports absolute
    /// paths for the generated files, but a path-case difference or a relative path from MSBuild
    /// shows up on some hosts, so the file name is the fallback key.
    /// </summary>
    private static GeneratedDocument? Resolve(
        string file,
        IReadOnlyDictionary<string, GeneratedDocument> byPath,
        IReadOnlyDictionary<string, GeneratedDocument> byRelativePath)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(file);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (byPath.TryGetValue(full, out var document))
        {
            return document;
        }

        return byRelativePath.TryGetValue(Path.GetFileName(file), out var fallback) ? fallback : null;
    }

    /// <summary>
    /// Names a diagnostic that did not land on a document: the checked-in stub harness when the
    /// path is inside it, and the generated wrapper (a missing brace, a project-level failure)
    /// otherwise.
    /// </summary>
    private static string StubOrTool(string file, string directory)
    {
        var stubs = Path.Combine(directory, "Stubs") + Path.DirectorySeparatorChar;
        return file.StartsWith(stubs, StringComparison.OrdinalIgnoreCase)
            ? "scripts/SnippetGate/Stubs/" + Path.GetFileName(file)
            : "generated project";
    }
}
