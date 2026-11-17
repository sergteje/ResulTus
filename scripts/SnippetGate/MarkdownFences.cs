// Copyright (c) ResulTus. All rights reserved.

namespace SnippetGate;

/// <summary>One fenced block, with the ordinal of its fence in the document.</summary>
internal sealed record Fence(int Ordinal, int OpenerLine, string Info, IReadOnlyList<string> Lines);

/// <summary>
/// Reads fenced blocks out of a markdown document. Both fence forms CommonMark allows are
/// recognised: backticks and tildes, three or more of them, closed by a run of the same character
/// that is at least as long. A four-backtick fence therefore holds three-backtick text, which is
/// how a document shows a fenced example inside an example.
/// The ordinal counts every fence in the document, so a block keeps the number a reader counts
/// when scrolling: if the third fence is a json block, the csharp block after it is block #4.
/// </summary>
internal static class MarkdownFences
{
    internal static IReadOnlyList<Fence> Read(IReadOnlyList<string> lines)
    {
        var fences = new List<Fence>();
        var ordinal = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            if (!TryOpener(lines[index], out var marker, out var length, out var info))
            {
                continue;
            }

            ordinal++;
            var content = new List<string>();
            var cursor = index + 1;

            while (cursor < lines.Count && !IsCloser(lines[cursor], marker, length))
            {
                content.Add(lines[cursor]);
                cursor++;
            }

            fences.Add(new Fence(ordinal, index + 1, info, content));
            index = cursor;
        }

        return fences;
    }

    /// <summary>
    /// An opener is a run of three or more backticks or tildes followed by an optional info string.
    /// A backtick opener whose info string contains a backtick is inline code, not a fence, which is
    /// what keeps a paragraph like <c>```csharp```</c> from swallowing the document.
    /// </summary>
    private static bool TryOpener(string line, out char marker, out int length, out string info)
    {
        marker = '\0';
        length = 0;
        info = string.Empty;

        var cursor = SkipIndent(line);
        if (cursor >= line.Length || (line[cursor] != '`' && line[cursor] != '~'))
        {
            return false;
        }

        var start = cursor;
        while (cursor < line.Length && line[cursor] == line[start])
        {
            cursor++;
        }

        length = cursor - start;
        if (length < 3)
        {
            return false;
        }

        var rest = line[cursor..].Trim();
        if (line[start] == '`' && rest.Contains('`', StringComparison.Ordinal))
        {
            return false;
        }

        marker = line[start];
        info = rest;
        return true;
    }

    /// <summary>A closer is a run of the opener's character, at least as long, followed by whitespace only.</summary>
    private static bool IsCloser(string line, char marker, int length)
    {
        var cursor = SkipIndent(line);
        if (cursor >= line.Length || line[cursor] != marker)
        {
            return false;
        }

        var start = cursor;
        while (cursor < line.Length && line[cursor] == marker)
        {
            cursor++;
        }

        return cursor - start >= length && line[cursor..].Trim().Length == 0;
    }

    private static int SkipIndent(string line)
    {
        var cursor = 0;
        while (cursor < line.Length && (line[cursor] == ' ' || line[cursor] == '\t'))
        {
            cursor++;
        }

        return cursor;
    }
}
