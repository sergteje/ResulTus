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
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ResulTus.slnx")))
            directory = directory.Parent;
        directory.Should().NotBeNull("the test must run inside the repository");
        return directory!.FullName;
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
    /// THEN metadata MUST pin net10.0, exclude both netstandard2.0 Roslyn projects,
    ///      and the remaining src projects MUST be exactly eleven.
    /// </summary>
    [Fact]
    public void DocfxMetadata_ShouldPinNet10AndExcludeRoslynProjects()
    {
        using var config = JsonDocument.Parse(ReadRepoFile("docs", "docfx.json"));
        var metadata = config.RootElement.GetProperty("metadata")[0];
        metadata.GetProperty("properties").GetProperty("TargetFramework").GetString().Should().Be("net10.0");

        var exclude = metadata.GetProperty("src")[0].GetProperty("exclude")
            .EnumerateArray().Select(e => e.GetString()).ToArray();
        exclude.Should().Contain("**/ResulTus.Analyzers/**");
        exclude.Should().Contain("**/ResulTus.SourceGenerators/**");

        var srcProjects = Directory
            .GetFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not "ResulTus.Analyzers" and not "ResulTus.SourceGenerators")
            .ToArray();
        srcProjects.Should().HaveCount(11);
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
    /// THEN a <c>docs</c> job MUST restore the local tool and build docs/docfx.json,
    ///      with the three pre-existing jobs and the read-only permission intact.
    /// </summary>
    [Fact]
    public void CiWorkflow_ShouldBuildDocfxSiteInDocsJob()
    {
        var workflow = ReadRepoFile(".github", "workflows", "ci.yml").Replace("\r\n", "\n");
        workflow.Should().Contain("permissions:\n  contents: read");
        workflow.Should().Contain("  build-and-test:");
        workflow.Should().Contain("  coverage-gate:");
        workflow.Should().Contain("  aot-smoke:");
        workflow.Should().Contain("  docs:");
        workflow.Should().Contain("run: dotnet tool restore");
        workflow.Should().Contain("run: dotnet tool run docfx -- docs/docfx.json");
    }
}
