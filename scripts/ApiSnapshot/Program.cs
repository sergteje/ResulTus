// Copyright (c) ResulTus. All rights reserved.

// The ApiSnapshot console program: --write emits the public-surface snapshot files, --check
// diffs the current surface against them. One tool, no argument-fu: the contract snapshots
// under tests/ResulTus.ApiCompat.Tests/PublicApi are the input of the contract tests and this
// is the only thing allowed to author them.

using System;
using ResulTus.ApiSurface;

if (args.Length is not 1 || (args[0] != "--write" && args[0] != "--check"))
{
    Console.Error.WriteLine("Usage: dotnet run --project scripts/ApiSnapshot -- (--write | --check)");
    return 2;
}

const string SnapshotFolder = "tests/ResulTus.ApiCompat.Tests/PublicApi";

var failures = 0;
foreach (var name in ShippedAssemblies.Names)
{
    var lines = Surface.Lines(ShippedAssemblies.Resolve(name));
    var content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
    var file = System.IO.Path.Combine(SnapshotFolder, $"{name}.txt");

    if (args[0] == "--write")
    {
        System.IO.File.WriteAllText(file, content, System.Text.Encoding.UTF8);
        Console.WriteLine($"written: {file} ({lines.Count} lines)");
        continue;
    }

    var contains = System.IO.File.Exists(file)
        ? System.IO.File.ReadAllText(file)
        : string.Empty;
    var expected = contains
        .Split(
            ["\r\n", "\n"],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(static line => line)
        .ToArray();
    var actual = lines
        .Select(static line => line.Trim())
        .Where(static line => line.Length > 0)
        .ToArray();

    var missing = actual.Except(expected).ToArray();
    var extra = expected.Except(actual).ToArray();
    if (missing.Length == 0 && extra.Length == 0)
    {
        Console.WriteLine($"[check] {name}: matches the snapshot");
        continue;
    }

    Console.Error.WriteLine($"[check] {name}: the public surface changed");
    foreach (var line in missing)
    {
        Console.Error.WriteLine($"    + {line}");
    }

    foreach (var line in extra)
    {
        Console.Error.WriteLine($"    - {line}");
    }

    Console.Error.WriteLine(
        "    review the change per the versioning strategy; if it is intended, re-run this tool"
        + " with --write and include the snapshot diff in the same commit as the API change");
    failures++;
}

return failures;
