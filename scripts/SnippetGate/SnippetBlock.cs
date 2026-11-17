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

    // A namespace declaration, in the file-scoped form (`namespace A.B;`) or the block form
    // (`namespace A.B {`). A listing that carries one has to be emitted outside the generated
    // namespace, or the file ends up with two.
    private static readonly Regex NamespaceDeclaration = new(
        @"^namespace[ \t]+(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)[ \t]*(?:;|\{)[ \t]*$",
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
    /// The namespaces this block's declarations establish. A block that carries one is written to
    /// its own file so a file-scoped namespace never meets the generated one, and the document's
    /// statements import it.
    /// </summary>
    internal List<string> DeclaredNamespaces { get; } = [];

    /// <summary>
    /// Local functions the document shares between fences. Inside the snippet class the very same
    /// text is a static method, which is what makes <c>Parse("42")</c> resolve from a later block.
    /// </summary>
    internal List<SourceLine> Functions { get; } = [];

    /// <summary>Statements wrapped in one method.</summary>
    internal List<SourceLine> Statements { get; } = [];

    internal bool NeedsAsync => Statements.Any(line => Await.IsMatch(line.Text));

    /// <summary>True when this block's declarations carry their own namespace declaration.</summary>
    internal bool DeclaresNamespace => DeclaredNamespaces.Count > 0;

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
    /// Removes the using directives that open the block, so the generated file carries one
    /// deduplicated list per document instead of one per fence. Blank lines and comment lines
    /// between directives are trivia: a listing that separates two usings with a blank line or
    /// explains one with a comment still opens with directives, and skipping that trivia here is
    /// what keeps the second directive out of the generated method body. Those trivia lines are
    /// dropped, and each directive keeps the markdown line it came from.
    /// </summary>
    private int HoistUsings()
    {
        var probe = 0;
        while (probe < body.Count && IsTrivia(body[probe]))
        {
            probe++;
        }

        if (probe >= body.Count || !UsingDirective.IsMatch(body[probe].Text))
        {
            return 0;
        }

        var index = 0;
        while (index < body.Count)
        {
            if (UsingDirective.IsMatch(body[index].Text))
            {
                Usings.Add(new SourceLine(body[index].Text.Trim(), body[index].MarkdownLine));
                index++;
                continue;
            }

            if (!IsTrivia(body[index]))
            {
                break;
            }

            index++;
        }

        return index;
    }

    private static bool IsTrivia(SourceLine line) =>
        string.IsNullOrWhiteSpace(line.Text)
        || line.Text.TrimStart().StartsWith("//", StringComparison.Ordinal);

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
    /// its kind belongs to, and returns the index of the line after it. Raw string literals are
    /// tracked across lines, so a brace that is only text inside a payload never ends the member.
    /// </summary>
    private int CaptureMember(int start)
    {
        var isDeclaration = IsTypeDeclarationAt(start);
        var captured = new List<SourceLine>();
        var state = default(ScanState);
        var depth = 0;

        for (var index = start; index < body.Count; index++)
        {
            var code = CSharpLines.Code(body[index].Text, ref state);
            captured.Add(body[index]);
            depth += CSharpLines.BraceDelta(code);

            var trimmed = code.TrimEnd();
            if (depth == 0 && (trimmed.EndsWith(';') || trimmed.EndsWith('}')))
            {
                break;
            }
        }

        if (isDeclaration)
        {
            Declarations.AddRange(captured);

            var declared = NamespaceDeclaredBy(captured);
            if (declared is not null)
            {
                DeclaredNamespaces.Add(declared);
            }
        }
        else
        {
            Functions.AddRange(captured);
        }

        return Math.Min(start + captured.Count, body.Count);
    }

    /// <summary>The namespace a declaration span opens, or <see langword="null"/> when it opens none.</summary>
    private static string? NamespaceDeclaredBy(IReadOnlyList<SourceLine> span)
    {
        foreach (var line in span)
        {
            var code = CSharpLines.Code(line.Text).Trim();
            if (code.Length == 0 || IsAttributeLine(code))
            {
                continue;
            }

            var declaration = NamespaceDeclaration.Match(code);
            return declaration.Success ? declaration.Groups["name"].Value : null;
        }

        return null;
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
/// Scanner state that survives a line break: a block comment or a raw string literal can span
/// lines, and both must keep their content out of the brace count.
/// </summary>
internal struct ScanState
{
    /// <summary>True while inside a <c>/* ... */</c> comment.</summary>
    public bool InBlockComment;

    /// <summary>The length of the opening quote run while inside a raw string literal, otherwise 0.</summary>
    public int RawQuotes;
}

/// <summary>
/// Reads C# the way the splitter needs it: the line with string, character and comment content
/// removed, so a brace inside a message, a payload or a comment never decides structure.
/// </summary>
internal static class CSharpLines
{
    internal static string Code(string line, ref ScanState state)
    {
        var code = new StringBuilder(line.Length);
        var index = 0;

        while (index < line.Length)
        {
            if (state.RawQuotes > 0)
            {
                index = SkipRawString(line, index, ref state);
                continue;
            }

            if (state.InBlockComment)
            {
                var end = line.IndexOf("*/", index, StringComparison.Ordinal);
                if (end < 0)
                {
                    return code.ToString();
                }

                state.InBlockComment = false;
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
                state.InBlockComment = true;
                index += 2;
                continue;
            }

            if (current == '\'')
            {
                index = SkipCharLiteral(line, index);
                continue;
            }

            var prefixStart = index;
            var verbatim = false;
            while (index < line.Length
                && index - prefixStart < 2
                && (line[index] == '$' || line[index] == '@'))
            {
                verbatim |= line[index] == '@';
                index++;
            }

            if (index < line.Length && line[index] == '"')
            {
                var quotes = QuoteRun(line, index);
                if (quotes >= 3 && !verbatim)
                {
                    // A raw string literal opens here. Its content - braces included - is text,
                    // and the closing run may be on a later line.
                    state.RawQuotes = quotes;
                    index += quotes;
                    continue;
                }

                index = SkipString(line, index, verbatim);
                continue;
            }

            // Not a string after all: copy the character we started at and move on. Copying one
            // character at a time is what keeps the loop progressing on ordinary code.
            index = prefixStart;
            code.Append(line[index]);
            index++;
        }

        return code.ToString();
    }

    internal static string Code(string line)
    {
        var state = default(ScanState);
        return Code(line, ref state);
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

    private static int QuoteRun(string line, int index)
    {
        var cursor = index;
        while (cursor < line.Length && line[cursor] == '"')
        {
            cursor++;
        }

        return cursor - index;
    }

    /// <summary>
    /// Consumes raw-string content up to and including a closing run that is at least as long as
    /// the opener, and clears the state when it finds one.
    /// </summary>
    private static int SkipRawString(string line, int index, ref ScanState state)
    {
        while (index < line.Length)
        {
            if (line[index] != '"')
            {
                index++;
                continue;
            }

            var quotes = QuoteRun(line, index);
            index += quotes;
            if (quotes >= state.RawQuotes)
            {
                state.RawQuotes = 0;
                return index;
            }
        }

        return index;
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
