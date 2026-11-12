// Copyright (c) ResulTus. All rights reserved.

using System.Text.Json;

namespace ResulTus.Tests;

/// <summary>
/// Pins the DocFX site contract realized for S4: a local tool manifest, a metadata
/// scope covering the eleven shipped libraries, and a CI job that builds the site.
/// The build itself runs in the <c>docs</c> CI job, not here (network + tool restore).
/// </summary>
public class DocfxSiteConfigTests
{
    /// <summary>
    /// The eleven shipped libraries whose metadata scope the DocFX site must cover.
    /// Named here so a drift failure reports exactly which project leaked in or out.
    /// </summary>
    private static readonly string[] ShippedLibraries =
    [
        "ResulTus",
        "ResulTus.AspNetCore",
        "ResulTus.Collections",
        "ResulTus.Diagnostics",
        "ResulTus.Extensions",
        "ResulTus.HttpClient",
        "ResulTus.Logging",
        "ResulTus.Serialization",
        "ResulTus.Testing",
        "ResulTus.Validation",
        "ResulTus.Validation.FluentValidation",
    ];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ResulTus.slnx")))
            directory = directory.Parent;
        if (directory is null)
        {
            throw new InvalidOperationException(
                "The DocFX contract tests walk up from the test assembly to ResulTus.slnx and "
                + "found nothing; they require the test output to live inside the repository tree "
                + $"(started from '{AppContext.BaseDirectory}').");
        }

        return directory.FullName;
    }

    private static string ReadRepoFile(params string[] relativeSegments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. relativeSegments]));

    /// <summary>
    /// GIVEN the committed tool manifest
    /// THEN docfx MUST be pinned to 2.81.0 and expose the <c>docfx</c> command.
    /// </summary>
    [Fact]
    public void ToolManifest_ShouldPinDocfx2810()
    {
        using var manifest = JsonDocument.Parse(ReadRepoFile(".config", "dotnet-tools.json"));
        var docfx = manifest.RootElement.GetProperty("tools").GetProperty("docfx");
        docfx.GetProperty("version").GetString().Should().Be("2.81.0");
        docfx.GetProperty("commands").EnumerateArray().Select(c => c.GetString())
            .Should().Contain("docfx");
    }

    /// <summary>
    /// GIVEN docs/docfx.json
    /// THEN metadata MUST pin net10.0, exclude only existing src projects, and the
    ///      resulting metadata scope MUST be exactly the eleven shipped libraries —
    ///      so a new or renamed project cannot leak into (or vanish from) the site.
    /// </summary>
    [Fact]
    public void DocfxMetadata_ShouldCoverExactlyTheShippedLibraries()
    {
        using var config = JsonDocument.Parse(ReadRepoFile("docs", "docfx.json"));
        var metadata = config.RootElement.GetProperty("metadata")[0];
        metadata.GetProperty("properties").GetProperty("TargetFramework").GetString().Should().Be("net10.0");

        var excluded = metadata.GetProperty("src")[0].GetProperty("exclude")
            .EnumerateArray().Select(e => e.GetString()!)
            .Select(glob => glob.Replace("**/", string.Empty).Replace("/**", string.Empty))
            .Order()
            .ToArray();

        var allSrcProjects = Directory
            .GetFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Order()
            .ToArray();

        // Single source of truth: the shipped-library list above plus the actual src tree
        // define what must be excluded, so the config globs cannot drift from the test's
        // expectation — a mismatch fails naming both sides.
        var expectedExcluded = allSrcProjects
            .Except(ShippedLibraries, StringComparer.Ordinal)
            .ToArray();

        excluded.Should().NotBeEmpty();
        excluded.Should().BeEquivalentTo(expectedExcluded,
            "the docfx exclusion globs must name exactly the src projects that are not shipped libraries");

        var scope = allSrcProjects.Where(name => !excluded.Contains(name)).Order().ToArray();
        scope.Should().BeEquivalentTo(ShippedLibraries,
            "the metadata scope must be exactly the shipped libraries; anything else leaked in or out");
    }

    /// <summary>
    /// GIVEN docs/.gitignore
    /// THEN the generated <c>_site/</c> and <c>api/</c> trees MUST stay untracked.
    /// </summary>
    [Fact]
    public void DocfxBuild_ShouldIgnoreGeneratedOutputs()
    {
        var ignored = ReadRepoFile("docs", ".gitignore").Split('\n').Select(l => l.Trim()).ToArray();
        ignored.Should().Contain("_site/");
        ignored.Should().Contain("api/");
        File.Exists(Path.Combine(RepositoryRoot(), "docs", "index.md")).Should().BeTrue();
    }

    /// <summary>
    /// GIVEN the CI workflow
    /// THEN the <c>docs</c> job itself MUST restore the local tool and build docs/docfx.json
    ///      (anchored inside that job's block, not anywhere in the file), with the three
    ///      pre-existing jobs and the read-only top-level permission intact.
    /// </summary>
    [Fact]
    public void CiWorkflow_ShouldBuildDocfxSiteInDocsJob()
    {
        var workflow = ReadRepoFile(".github", "workflows", "ci.yml").Replace("\r\n", "\n");

        workflow.Should().Contain("  build-and-test:");
        workflow.Should().Contain("  coverage-gate:");
        workflow.Should().Contain("  aot-smoke:");

        var docsJob = ExtractBlock(workflow, "  docs:", "  ");
        docsJob.Should().Contain("dotnet tool restore",
            "the docs job must restore the committed local tool manifest");
        docsJob.Should().Contain("dotnet tool run docfx -- docs/docfx.json",
            "the docs job must build the site");

        var permissions = ExtractBlock(workflow, "permissions:", string.Empty);
        permissions.Should().Contain("contents: read");
    }

    /// <summary>
    /// Returns the lines of one YAML block: from its declaration line to the next line at the
    /// declaration's own indentation level, so assertions cannot pass on text that merely
    /// appears somewhere else in the file.
    /// </summary>
    private static string ExtractBlock(string workflow, string declaration, string siblingIndent)
    {
        var lines = workflow.Split('\n');
        var start = Array.FindIndex(lines, l => l == declaration.TrimEnd(':') + ":"
            || l == declaration);
        if (start < 0)
        {
            throw new InvalidOperationException($"Block '{declaration}' was not found in the workflow.");
        }

        var block = new List<string> { lines[start] };
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith(siblingIndent, StringComparison.Ordinal)
                && !lines[i].StartsWith(siblingIndent + " ", StringComparison.Ordinal))
            {
                break;
            }

            block.Add(lines[i]);
        }

        return string.Join('\n', block);
    }
}
