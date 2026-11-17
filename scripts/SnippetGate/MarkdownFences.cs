// Copyright (c) ResulTus. All rights reserved.

using System.Text.RegularExpressions;

namespace SnippetGate;

/// <summary>One fenced block, with the ordinal of its fence in the document.</summary>
internal sealed record Fence(int Ordinal, int OpenerLine, string Info, IReadOnlyList<string> Lines);

/// <summary>
/// Reads fenced blocks out of a markdown document. The ordinal counts every fence in the document,
/// so a block keeps the number a reader counts when scrolling: if the third fence is a ```json,
/// the csharp block after it is block #4.
/// </summary>
internal static class MarkdownFences
{
    // A fence opener carries an optional info string and nothing else on the line. An inline
    // ```fragment``` inside a paragraph therefore never opens a block.
    private static readonly Regex Opener = new(
        @"^[ \t]*```(?<info>[A-Za-z0-9_+.\-]*)[ \t]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Closer = new(
        @"^[ \t]*```[ \t]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static IReadOnlyList<Fence> Read(IReadOnlyList<string> lines)
    {
        var fences = new List<Fence>();
        var ordinal = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            var opener = Opener.Match(lines[index]);
            if (!opener.Success)
            {
                continue;
            }

            ordinal++;
            var content = new List<string>();
            var cursor = index + 1;

            while (cursor < lines.Count && !Closer.IsMatch(lines[cursor]))
            {
                content.Add(lines[cursor]);
                cursor++;
            }

            fences.Add(new Fence(ordinal, index + 1, opener.Groups["info"].Value, content));
            index = cursor;
        }

        return fences;
    }
}
