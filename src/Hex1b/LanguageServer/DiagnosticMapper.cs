using Hex1b.Documents;
using Hex1b.LanguageServer.Protocol;
using Hex1b.Theming;

namespace Hex1b.LanguageServer;

/// <summary>
/// Maps LSP <see cref="LspDiagnostic"/> objects to <see cref="TextDecorationSpan"/>
/// with underline styles based on severity.
/// </summary>
internal static class DiagnosticMapper
{
    /// <summary>
    /// Converts LSP diagnostics to text decoration spans with underlines.
    /// </summary>
    public static IReadOnlyList<TextDecorationSpan> MapDiagnostics(LspDiagnostic[] diagnostics)
    {
        if (diagnostics.Length == 0) return [];

        var spans = new List<TextDecorationSpan>(diagnostics.Length);

        foreach (var diag in diagnostics)
        {
            var (style, colorElement) = GetStyleForSeverity(diag.Severity);

            // LSP positions are 0-based; DocumentPosition is 1-based
            var start = new DocumentPosition(
                diag.Range.Start.Line + 1,
                diag.Range.Start.Character + 1);
            var end = new DocumentPosition(
                diag.Range.End.Line + 1,
                diag.Range.End.Character + 1);

            spans.Add(new TextDecorationSpan(
                start, end,
                new TextDecoration
                {
                    UnderlineStyle = style,
                    UnderlineColorThemeElement = colorElement,
                },
                Priority: 10)); // Diagnostics override syntax highlighting
        }

        return spans;
    }

    private static (UnderlineStyle, Hex1bThemeElement<Hex1bColor>) GetStyleForSeverity(int? severity) => severity switch
    {
        1 => (UnderlineStyle.Curly, DiagnosticTheme.ErrorUnderlineColor),   // Error
        2 => (UnderlineStyle.Curly, DiagnosticTheme.WarningUnderlineColor), // Warning
        3 => (UnderlineStyle.Dotted, DiagnosticTheme.InfoUnderlineColor),   // Information
        4 => (UnderlineStyle.Dashed, DiagnosticTheme.HintUnderlineColor),   // Hint
        _ => (UnderlineStyle.Curly, DiagnosticTheme.ErrorUnderlineColor),   // Default to error
    };
}
