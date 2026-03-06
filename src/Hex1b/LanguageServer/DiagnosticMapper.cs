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

    /// <summary>
    /// Converts LSP diagnostics to public <see cref="DiagnosticInfo"/> records
    /// that can be displayed in a problems panel.
    /// </summary>
    public static IReadOnlyList<DiagnosticInfo> MapToDiagnosticInfo(LspDiagnostic[] diagnostics, string documentUri)
    {
        if (diagnostics.Length == 0) return [];

        var result = new List<DiagnosticInfo>(diagnostics.Length);

        foreach (var diag in diagnostics)
        {
            var start = new DocumentPosition(
                diag.Range.Start.Line + 1,
                diag.Range.Start.Character + 1);
            var end = new DocumentPosition(
                diag.Range.End.Line + 1,
                diag.Range.End.Character + 1);

            var severity = (DiagnosticSeverity)(diag.Severity ?? 1);
            var code = diag.Code?.ValueKind == System.Text.Json.JsonValueKind.String
                ? diag.Code.Value.GetString()
                : diag.Code?.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? diag.Code.Value.GetInt32().ToString()
                    : null;

            result.Add(new DiagnosticInfo(
                DocumentUri: documentUri,
                Message: diag.Message,
                Severity: severity,
                Start: start,
                End: end,
                Source: diag.Source,
                Code: code));
        }

        return result;
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
