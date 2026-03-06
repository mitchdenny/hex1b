using Hex1b;
using Hex1b.Documents;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace LanguageServerDemo;

/// <summary>
/// A simple decoration provider that applies keyword-style syntax highlighting
/// for a subset of C# keywords and constructs. This is a static/hardcoded
/// highlighter for demo purposes — a real implementation would use a parser or LSP.
/// </summary>
internal sealed class CSharpSyntaxHighlighter : ITextDecorationProvider
{
    private static readonly HashSet<string> Keywords =
    [
        "using", "namespace", "class", "public", "private", "protected", "internal",
        "static", "void", "int", "string", "bool", "var", "new", "return", "if",
        "else", "for", "foreach", "while", "in", "true", "false", "null", "async",
        "await", "Task", "readonly", "sealed", "record", "interface", "abstract",
        "override", "virtual", "this", "base", "try", "catch", "finally", "throw"
    ];

    private static readonly HashSet<string> TypeNames =
    [
        "Console", "String", "Int32", "Boolean", "Task", "List", "Dictionary",
        "IEnumerable", "IReadOnlyList", "Action", "Func", "Exception",
        "ArgumentException", "InvalidOperationException"
    ];

    public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
    {
        var spans = new List<TextDecorationSpan>();

        for (var line = startLine; line <= Math.Min(endLine, document.LineCount); line++)
        {
            var text = document.GetLineText(line);
            HighlightLine(text, line, spans);
        }

        return spans;
    }

    private static void HighlightLine(string text, int line, List<TextDecorationSpan> spans)
    {
        var i = 0;

        while (i < text.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
                continue;
            }

            // Line comments
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/')
            {
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, i + 1),
                    new DocumentPosition(line, text.Length + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.CommentColor }));
                break;
            }

            // String literals
            if (text[i] == '"')
            {
                var start = i;
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length) i++; // skip escape
                    i++;
                }
                if (i < text.Length) i++; // skip closing quote
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, start + 1),
                    new DocumentPosition(line, i + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.StringColor }));
                continue;
            }

            // Numbers
            if (char.IsDigit(text[i]))
            {
                var start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                    i++;
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, start + 1),
                    new DocumentPosition(line, i + 1),
                    new TextDecoration { ForegroundThemeElement = SyntaxTheme.NumberColor }));
                continue;
            }

            // Identifiers / keywords
            if (char.IsLetter(text[i]) || text[i] == '_')
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                    i++;
                var word = text[start..i];

                if (Keywords.Contains(word))
                {
                    spans.Add(new TextDecorationSpan(
                        new DocumentPosition(line, start + 1),
                        new DocumentPosition(line, i + 1),
                        new TextDecoration { ForegroundThemeElement = SyntaxTheme.KeywordColor }));
                }
                else if (TypeNames.Contains(word) || (word.Length > 1 && char.IsUpper(word[0]) && word.Any(char.IsLower)))
                {
                    // Heuristic: PascalCase identifiers are likely type names
                    spans.Add(new TextDecorationSpan(
                        new DocumentPosition(line, start + 1),
                        new DocumentPosition(line, i + 1),
                        new TextDecoration { ForegroundThemeElement = SyntaxTheme.TypeColor }));
                }
                continue;
            }

            i++;
        }
    }
}
