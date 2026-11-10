// The API-compatibility contract, per shipped assembly: the current surface resolved from the
// builds this run loads MUST equal the checked-in snapshot line by line. A public-member change
// (added, removed, re-signed, re-named) fails the suite until it is deliberately reviewed and
// the snapshot regenerated per the versioning strategy in `odd/specs/versioning.md`.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ResulTus.ApiSurface;
using Xunit;

namespace ResulTus.ApiCompat.Tests;

public class ApiCompatTests
{
    public static IEnumerable<object[]> SnapshotNames()
        => Directory.GetFiles(
                Path.Combine(AppContext.BaseDirectory, "PublicApi"), "*.txt")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(static name => new object[] { name! });

    [Theory]
    [MemberData(nameof(SnapshotNames))]
    public void PublicSurface_ShouldMatchTheSnapshot(string assemblyName)
    {
        var snapshotFile = Path.Combine(AppContext.BaseDirectory, "PublicApi", $"{assemblyName}.txt");
        var snapshotLines = File.ReadAllLines(snapshotFile)
            .Where(static line => line.Length > 0)
            .ToArray();

        var currentLines = Surface.Lines(ShippedAssemblies.Resolve(assemblyName));

        var missing = currentLines.Except(snapshotLines).ToArray();
        var unexpected = snapshotLines.Except(currentLines).ToArray();

        if (missing.Length == 0 && unexpected.Length == 0)
        {
            return;
        }

        var report = string.Join(
            Environment.NewLine,
            missing.Select(static line => $"    + {line}")
                .Concat(unexpected.Select(static line => $"    - {line}")));

        Assert.Fail(string.Concat(
            $"the public surface of {assemblyName} changed; review it against the versioning"
            + " strategy (odd/specs/versioning.md), regenerate the snapshot"
            + " (dotnet run --project scripts/ApiSnapshot -- --write), and include the snapshot"
            + " diff in the change's commit",
            Environment.NewLine,
            report));
    }
}
