using Hex1b;
using Hex1b.Documents;
using Hex1b.Theming;

namespace LanguageServerDemo;

/// <summary>
/// A decoration provider that simulates diagnostic underlines (errors, warnings, info, hints).
/// Scans for patterns in code and applies styled underlines with severity-colored underline colors.
/// </summary>
internal sealed class DiagnosticHighlighter : ITextDecorationProvider
{
    public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
    {
        var spans = new List<TextDecorationSpan>();

        for (var line = startLine; line <= Math.Min(endLine, document.LineCount); line++)
        {
            var text = document.GetLineText(line);
            ScanLine(text, line, spans);
        }

        return spans;
    }

    private static void ScanLine(string text, int line, List<TextDecorationSpan> spans)
    {
        // Error: simulate undefined identifier
        FindAndMark(text, line, spans, "undefinedVar",
            UnderlineStyle.Curly, DiagnosticTheme.ErrorUnderlineColor);

        // Warning: simulate unused variable
        FindAndMark(text, line, spans, "unusedResult",
            UnderlineStyle.Curly, DiagnosticTheme.WarningUnderlineColor);

        // Info: TODO comments get info-level dotted underline
        var todoIdx = text.IndexOf("TODO", StringComparison.Ordinal);
        while (todoIdx >= 0)
        {
            // Underline the entire comment from TODO onward
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, todoIdx + 1),
                new DocumentPosition(line, text.Length + 1),
                new TextDecoration
                {
                    UnderlineStyle = UnderlineStyle.Dotted,
                    UnderlineColorThemeElement = DiagnosticTheme.InfoUnderlineColor,
                    ForegroundThemeElement = SyntaxTheme.CommentColor,
                },
                Priority: 5));

            todoIdx = text.IndexOf("TODO", todoIdx + 4, StringComparison.Ordinal);
        }

        // Hint: deprecated API
        FindAndMark(text, line, spans, "DeprecatedMethod",
            UnderlineStyle.Dashed, DiagnosticTheme.HintUnderlineColor);
    }

    private static void FindAndMark(string text, int line, List<TextDecorationSpan> spans,
        string pattern, UnderlineStyle style, Hex1bThemeElement<Hex1bColor> colorElement)
    {
        var idx = text.IndexOf(pattern, StringComparison.Ordinal);
        while (idx >= 0)
        {
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, idx + 1),
                new DocumentPosition(line, idx + pattern.Length + 1),
                new TextDecoration
                {
                    UnderlineStyle = style,
                    UnderlineColorThemeElement = colorElement,
                },
                Priority: 10));

            idx = text.IndexOf(pattern, idx + pattern.Length, StringComparison.Ordinal);
        }
    }
}
