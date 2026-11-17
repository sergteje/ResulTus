// Copyright (c) ResulTus. All rights reserved.

using System.Text;
using System.Text.RegularExpressions;

namespace SnippetGate;

/// <summary>One line of documented code, with the markdown line it came from.</summary>
internal sealed record SourceLine(string Text, int MarkdownLine);

/// <summary>
/// A single ```csharp fence, split into the four things the generated project needs: the using
/// directives the document as a whole relies on, the declarations that belong at namespace level,
/// the local functions the document shares between fences, and the statements that fit a method
/// body.
/// </summary>
internal sealed class SnippetBlock
{
    private static readonly Regex UsingDirective = new(
        @"^[ \t]*(?:global[ \t]+)?using[ \t]+(?:static[ \t]+)?[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*(?:[ \t]*=[ \t]*[A-Za-z_][A-Za-z0-9_.]*)?[ \t]*;[ \t]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TypeDeclaration = new(
        @"^(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|ref|unsafe|file|new)[ \t]+)*(?:namespace|class|struct|record|interface|enum|delegate)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // A local function the whole document shares. It is emitted as a static method of the snippet
    // class, which is the member form of exactly the same declaration.
    private static readonly Regex LocalFunction = new(
        @"^static[ \t]+[A-Za-z_]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Await = new(@"\bawait\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<SourceLine> body = [];

    private SnippetBlock(int ordinal, int startLine)
    {
        Ordinal = ordinal;
        StartLine = startLine;
    }

    /// <summary>The fence ordinal inside the document, which is also the block's readable number.</summary>
    internal int Ordinal { get; }

    /// <summary>The markdown line of the first code line, for diagnostics about the block itself.</summary>
    internal int StartLine { get; }

    internal List<SourceLine> Usings { get; } = [];

    /// <summary>Types and namespaces, emitted verbatim at namespace level.</summary>
    internal List<SourceLine> Declarations { get; } = [];

    /// <summary>
    /// Local functions the document shares between fences. Inside the snippet class the very same
    /// text is a static method, which is what makes <c>Parse("42")</c> resolve from a later block.
    /// </summary>
    internal List<SourceLine> Functions { get; } = [];

    /// <summary>Statements wrapped in one method.</summary>
    internal List<SourceLine> Statements { get; } = [];

    internal bool NeedsAsync => Statements.Any(line => Await.IsMatch(line.Text));

    /// <summary>True when the block contributes nothing at all: not one using, declaration or statement.</summary>
    internal bool IsEmpty =>
        Usings.Count == 0 && Declarations.Count == 0 && Functions.Count == 0 && Statements.Count == 0;

    internal static SnippetBlock Parse(Fence fence)
    {
        var block = new SnippetBlock(fence.Ordinal, fence.OpenerLine + 1);
        block.body.AddRange(Numbered(fence));
        block.Split();
        return block;
    }

    private void Split()
    {
        var index = HoistUsings();

        while (index < body.Count)
        {
            var line = body[index];
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                Statements.Add(line);
                index++;
                continue;
            }

            if (StartsAttributeRun(index, out var afterAttributes))
            {
                if (afterAttributes < body.Count
                    && IsDeclarationStart(CSharpLines.Code(body[afterAttributes].Text).Trim()))
                {
                    index = CaptureMember(index);
                    continue;
                }

                // Attributes stacked on a statement belong with the statements; nothing in the
                // documentation does this, but guessing a member here would invent a type.
                Statements.AddRange(body.GetRange(index, afterAttributes - index));
                index = afterAttributes;
                continue;
            }

            if (IsDeclarationStart(CSharpLines.Code(line.Text).Trim()))
            {
                index = CaptureMember(index);
                continue;
            }

            Statements.Add(line);
            index++;
        }

        TrimEdges(Statements);
    }

    /// <summary>
    /// Removes the using directives that open the block and returns them, so the generated file
    /// carries one deduplicated list per document instead of one per fence.
    /// </summary>
    private int HoistUsings()
    {
        var index = 0;
        while (index < body.Count && string.IsNullOrWhiteSpace(body[index].Text))
        {
            index++;
        }

        while (index < body.Count && UsingDirective.IsMatch(body[index].Text))
        {
            Usings.Add(new SourceLine(body[index].Text.Trim(), body[index].MarkdownLine));
            index++;
        }

        return index;
    }

    /// <summary>
    /// Finds the next non-blank line after a run of attribute lines, so an attributed declaration
    /// is captured whole instead of leaving its attributes behind.
    /// </summary>
    private bool StartsAttributeRun(int index, out int afterAttributes)
    {
        var cursor = index;
        while (cursor < body.Count && IsAttributeLine(CSharpLines.Code(body[cursor].Text)))
        {
            cursor++;
        }

        if (cursor == index)
        {
            afterAttributes = index;
            return false;
        }

        while (cursor < body.Count && string.IsNullOrWhiteSpace(body[cursor].Text))
        {
            cursor++;
        }

        afterAttributes = cursor;
        return true;
    }

    /// <summary>
    /// An attribute line is a bracketed name, not the bare <c>[</c> that opens a collection
    /// expression, which is a statement.
    /// </summary>
    private static bool IsAttributeLine(string code)
    {
        var trimmed = code.TrimStart();
        return trimmed.Length > 2
            && trimmed[0] == '['
            && char.IsLetter(trimmed[1])
            && trimmed.Contains(']', StringComparison.Ordinal);
    }

    /// <summary>
    /// Copies one declaration - a type, a namespace or a local function - verbatim into the list
    /// its kind belongs to, and returns the index of the line after it.
    /// </summary>
    private int CaptureMember(int start)
    {
        var target = IsTypeDeclarationAt(start) ? Declarations : Functions;
        var depth = 0;
        var inBlockComment = false;

        for (var index = start; index < body.Count; index++)
        {
            var code = CSharpLines.Code(body[index].Text, ref inBlockComment);
            target.Add(body[index]);
            depth += CSharpLines.BraceDelta(code);

            var trimmed = code.TrimEnd();
            if (depth == 0 && (trimmed.EndsWith(';') || trimmed.EndsWith('}')))
            {
                return index + 1;
            }
        }

        return body.Count;
    }

    /// <summary>Looks past the attributes of a member to decide what kind of member it is.</summary>
    private bool IsTypeDeclarationAt(int start)
    {
        for (var index = start; index < body.Count; index++)
        {
            var code = CSharpLines.Code(body[index].Text);
            if (string.IsNullOrWhiteSpace(code) || IsAttributeLine(code))
            {
                continue;
            }

            return TypeDeclaration.IsMatch(code.Trim());
        }

        return false;
    }

    private static bool IsDeclarationStart(string code) =>
        TypeDeclaration.IsMatch(code) || LocalFunction.IsMatch(code);

    /// <summary>
    /// Removes the indentation a fence may carry when it sits inside a list item, so a top-level
    /// declaration starts at column zero and is recognised as one. Line numbers survive it.
    /// </summary>
    private static IReadOnlyList<SourceLine> Numbered(Fence fence)
    {
        var indent = fence.Lines
            .Where(line => line.Trim().Length > 0)
            .Select(LeadingSpaces)
            .DefaultIfEmpty(0)
            .Min();

        return fence.Lines
            .Select((line, offset) => new SourceLine(
                line.Length >= indent ? line[indent..] : line.TrimStart(),
                fence.OpenerLine + 1 + offset))
            .ToArray();
    }

    private static int LeadingSpaces(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static void TrimEdges(List<SourceLine> lines)
    {
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0].Text))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1].Text))
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }
}

/// <summary>
/// Reads C# the way the splitter needs it: the line with string, character and comment content
/// removed, so a brace inside a message or a semicolon inside a comment never decides structure.
/// </summary>
internal static class CSharpLines
{
    internal static string Code(string line, ref bool inBlockComment)
    {
        var code = new StringBuilder(line.Length);
        var index = 0;

        while (index < line.Length)
        {
            if (inBlockComment)
            {
                var end = line.IndexOf("*/", index, StringComparison.Ordinal);
                if (end < 0)
                {
                    return code.ToString();
                }

                inBlockComment = false;
                index = end + 2;
                continue;
            }

            var current = line[index];

            if (current == '/' && index + 1 < line.Length && line[index + 1] == '/')
            {
                break;
            }

            if (current == '/' && index + 1 < line.Length && line[index + 1] == '*')
            {
                inBlockComment = true;
                index += 2;
                continue;
            }

            if (TryStringStart(line, index, out var quote, out var verbatim))
            {
                index = SkipString(line, quote, verbatim);
                continue;
            }

            if (current == '\'')
            {
                index = SkipCharLiteral(line, index);
                continue;
            }

            code.Append(current);
            index++;
        }

        return code.ToString();
    }

    internal static string Code(string line)
    {
        var inBlockComment = false;
        return Code(line, ref inBlockComment);
    }

    /// <summary>Brace nesting contributed by one line of code.</summary>
    internal static int BraceDelta(string code)
    {
        var delta = 0;
        foreach (var character in code)
        {
            delta += character switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0,
            };
        }

        return delta;
    }

    private static bool TryStringStart(string line, int index, out int quote, out bool verbatim)
    {
        quote = index;
        verbatim = false;

        var cursor = index;
        while (cursor < line.Length && cursor - index < 2 && (line[cursor] == '$' || line[cursor] == '@'))
        {
            if (line[cursor] == '@')
            {
                verbatim = true;
            }

            cursor++;
        }

        if (cursor < line.Length && line[cursor] == '"')
        {
            quote = cursor;
            return true;
        }

        quote = index;
        verbatim = false;
        return false;
    }

    private static int SkipString(string line, int quote, bool verbatim)
    {
        var cursor = quote + 1;
        while (cursor < line.Length)
        {
            if (verbatim)
            {
                if (line[cursor] == '"')
                {
                    if (cursor + 1 < line.Length && line[cursor + 1] == '"')
                    {
                        cursor += 2;
                        continue;
                    }

                    return cursor + 1;
                }
            }
            else
            {
                if (line[cursor] == '\\')
                {
                    cursor += 2;
                    continue;
                }

                if (line[cursor] == '"')
                {
                    return cursor + 1;
                }
            }

            cursor++;
        }

        // An unterminated string ends with the line; the compiler will say so if that is wrong.
        return line.Length;
    }

    private static int SkipCharLiteral(string line, int index)
    {
        var cursor = index + 1;
        while (cursor < line.Length)
        {
            if (line[cursor] == '\\')
            {
                cursor += 2;
                continue;
            }

            if (line[cursor] == '\'')
            {
                return cursor + 1;
            }

            cursor++;
        }

        return line.Length;
    }
}
