using System.Text;

namespace Parser.Models;

/// <summary>
/// Lexical helpers over raw VHDL text — the single implementation of "where
/// does the <c>--</c> line comment start" and "what is inside a string
/// literal" for every component that scans VHDL source as text (tokenizer,
/// lint, elaborator, library scanners, block builder, replay).
/// Strings never span lines in VHDL, so string state resets at each newline.
/// </summary>
public static class VhdlText
{
    /// <summary>
    /// Index of the <c>--</c> that starts a line comment in <paramref name="line"/>
    /// (a single line), ignoring <c>--</c> inside a string literal; -1 when none.
    /// </summary>
    public static int LineCommentStart(ReadOnlySpan<char> line)
    {
        bool insideString = false;
        for (int i = 0; i + 1 < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') insideString = !insideString;
            else if (!insideString && c == '-' && line[i + 1] == '-') return i;
        }
        return -1;
    }

    /// <summary><paramref name="line"/> without its trailing line comment.</summary>
    public static string StripLineComment(string line)
    {
        int cut = LineCommentStart(line);
        return cut < 0 ? line : line[..cut];
    }

    /// <summary>
    /// <paramref name="source"/> with every line comment removed and every
    /// newline kept, so line numbers are preserved.
    /// </summary>
    public static string StripLineComments(string source) => RewriteComments(source, blank: false);

    /// <summary>
    /// <paramref name="source"/> with every line comment replaced by spaces,
    /// so offsets and columns are preserved.
    /// </summary>
    public static string BlankLineComments(string source) => RewriteComments(source, blank: true);

    /// <summary>
    /// <paramref name="source"/> with the contents of every string literal
    /// replaced by <c>.</c> (quotes kept, length preserved), so patterns that
    /// scan code never match text inside a message.
    /// </summary>
    public static string MaskStringLiterals(string source)
    {
        var chars = source.ToCharArray();
        bool insideString = false;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c == '\n') insideString = false;
            else if (c == '"') insideString = !insideString;
            else if (insideString) chars[i] = '.';
        }
        return new string(chars);
    }

    private static string RewriteComments(string source, bool blank)
    {
        var sb = new StringBuilder(source.Length);
        var rest = source.AsSpan();
        while (true)
        {
            int nl = rest.IndexOf('\n');
            var line = nl < 0 ? rest : rest[..nl];
            int cut = LineCommentStart(line);
            if (cut < 0) sb.Append(line);
            else
            {
                sb.Append(line[..cut]);
                if (blank) sb.Append(' ', line.Length - cut);
            }
            if (nl < 0) return sb.ToString();
            sb.Append('\n');
            rest = rest[(nl + 1)..];
        }
    }

    /// <summary>Splits <paramref name="text"/> at <paramref name="separator"/> outside parentheses and quotes.</summary>
    public static List<string> TopLevelSplit(string text, string separator)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                int close = text.IndexOf('"', i + 1);
                if (close > 0) i = close;
                continue;
            }
            if (c == '\'' && i + 2 < text.Length && text[i + 2] == '\'') { i += 2; continue; }
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0 && string.CompareOrdinal(text, i, separator, 0, separator.Length) == 0)
            {
                parts.Add(text[start..i]);
                start = i + separator.Length;
                i = start - 1;
            }
        }
        parts.Add(text[start..]);
        return parts;
    }

    /// <summary>Index of the <c>)</c> closing the <c>(</c> at <paramref name="open"/>, or -1.</summary>
    public static int MatchingParen(string text, int open)
    {
        int depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')' && --depth == 0) return i;
        }
        return -1;
    }
}
