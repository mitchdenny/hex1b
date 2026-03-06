using Hex1b;
using Hex1b.Documents;
using Hex1b.Theming;

namespace LanguageServerDemo;

/// <summary>
/// A decoration provider that shows hover information overlays when the cursor
/// is on a diagnostic span. Demonstrates the overlay push/dismiss mechanism.
/// </summary>
internal sealed class HoverInfoProvider : ITextDecorationProvider
{
    private IEditorSession? _session;
    private DocumentPosition _lastCursorPosition;

    private static readonly Dictionary<string, DiagnosticInfo> s_diagnostics = new()
    {
        ["undefinedVar"] = new("error CS0103", "The name 'undefinedVar' does not exist in the current context.",
            Hex1bColor.FromRgb(255, 80, 80)),
        ["unusedResult"] = new("warning CS0219", "The variable 'unusedResult' is assigned but its value is never used.",
            Hex1bColor.FromRgb(255, 200, 60)),
        ["DeprecatedMethod"] = new("hint", "DeprecatedMethod is obsolete. Use 'NewMethod()' instead.",
            Hex1bColor.FromRgb(150, 150, 150)),
    };

    public void Activate(IEditorSession session) => _session = session;
    public void Deactivate() => _session = null;

    public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
    {
        if (_session is null) return [];

        var cursorOffset = _session.State.Cursor.Position;
        var cursorPos = document.OffsetToPosition(cursorOffset);
        if (cursorPos != _lastCursorPosition)
        {
            _lastCursorPosition = cursorPos;
            UpdateHoverOverlay(cursorPos, document);
        }

        // This provider doesn't add decorations itself — DiagnosticHighlighter handles that.
        return [];
    }

    private void UpdateHoverOverlay(DocumentPosition cursor, IHex1bDocument document)
    {
        if (_session is null) return;

        var line = cursor.Line;
        if (line < 1 || line > document.LineCount)
        {
            _session.DismissOverlay("hover-info");
            return;
        }

        var text = document.GetLineText(line);

        // Check if cursor is on any known diagnostic pattern
        foreach (var (pattern, info) in s_diagnostics)
        {
            var idx = text.IndexOf(pattern, StringComparison.Ordinal);
            while (idx >= 0)
            {
                var startCol = idx + 1; // 1-based
                var endCol = idx + pattern.Length + 1;

                if (cursor.Column >= startCol && cursor.Column < endCol)
                {
                    // Cursor is on this diagnostic — show hover
                    _session.PushOverlay(new EditorOverlay(
                        Id: "hover-info",
                        AnchorPosition: new DocumentPosition(line, startCol),
                        Placement: OverlayPlacement.Above,
                        Content:
                        [
                            new OverlayLine($" {info.Code} ", info.SeverityColor),
                            new OverlayLine($" {info.Message} "),
                        ]));
                    return;
                }

                idx = text.IndexOf(pattern, idx + pattern.Length, StringComparison.Ordinal);
            }
        }

        // Cursor not on a diagnostic — dismiss hover
        _session.DismissOverlay("hover-info");
    }

    private record DiagnosticInfo(string Code, string Message, Hex1bColor SeverityColor);
}
