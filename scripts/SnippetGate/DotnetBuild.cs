// Copyright (c) ResulTus. All rights reserved.

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SnippetGate;

/// <summary>One compiler line, reduced to the fields the gate reports back.</summary>
internal sealed record CompilerDiagnostic(string File, int Line, int Column, string Severity, string Code, string Message);

internal sealed record BuildResult(int ExitCode, string Output, IReadOnlyList<CompilerDiagnostic> Diagnostics);

/// <summary>Runs the compiler over the generated project and reads its output.</summary>
internal static class DotnetBuild
{
    private static readonly Regex Located = new(
        @"^\s*(?<file>.+?)\((?<line>\d+),(?<column>\d+)\): (?<severity>error|warning) (?<code>[A-Za-z]+[0-9]+): (?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Unlocated = new(
        @"^\s*(?<file>.+?) : (?<severity>error|warning) (?<code>[A-Za-z]+[0-9]+): (?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ProjectSuffix = new(@"\s+\[[^\]]*\]\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Builds the generated solution. Release matches the CI invocation, and quiet verbosity keeps
    /// the output to diagnostics so the report is the same on every host.
    /// </summary>
    internal static BuildResult Run(string target)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(target)!,
        };

        start.ArgumentList.Add("build");
        start.ArgumentList.Add(target);
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("Release");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-v:quiet");
        // A gate that leaves build nodes alive would also leave handles on the temporary project,
        // and the gate deletes that project when it succeeds.
        start.ArgumentList.Add("-nodeReuse:false");

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start 'dotnet build'. Is the .NET SDK on PATH?");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var output = standardOutput.Result + standardError.Result;
        return new BuildResult(process.ExitCode, output, Parse(output));
    }

    private static IReadOnlyList<CompilerDiagnostic> Parse(string output)
    {
        var diagnostics = new List<CompilerDiagnostic>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var located = Located.Match(line);
            if (located.Success)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    located.Groups["file"].Value.Trim(),
                    int.Parse(located.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    int.Parse(located.Groups["column"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    located.Groups["severity"].Value,
                    located.Groups["code"].Value,
                    ProjectSuffix.Replace(located.Groups["message"].Value, string.Empty)));
                continue;
            }

            var unlocated = Unlocated.Match(line);
            if (unlocated.Success)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    unlocated.Groups["file"].Value.Trim(),
                    0,
                    0,
                    unlocated.Groups["severity"].Value,
                    unlocated.Groups["code"].Value,
                    ProjectSuffix.Replace(unlocated.Groups["message"].Value, string.Empty)));
            }
        }

        return diagnostics;
    }
}
