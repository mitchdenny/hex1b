using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Renders document content as plain text lines. This is the default editor view.
/// </summary>
public sealed class TextEditorViewRenderer : IEditorViewRenderer
{
    /// <summary>
    /// Shared singleton instance (renderer is stateless).
    /// </summary>
    public static TextEditorViewRenderer Instance { get; } = new();

    /// <inheritdoc />
    public void Render(Hex1bRenderContext context, EditorState state, Rect viewport, int scrollOffset, int horizontalScrollOffset, bool isFocused, char? pendingNibble = null)
    {
        var theme = context.Theme;
        var fg = theme.Get(EditorTheme.ForegroundColor);
        var bg = theme.Get(EditorTheme.BackgroundColor);
        var cursorFg = theme.Get(EditorTheme.CursorForegroundColor);
        var cursorBg = theme.Get(EditorTheme.CursorBackgroundColor);
        var selFg = theme.Get(EditorTheme.SelectionForegroundColor);
        var selBg = theme.Get(EditorTheme.SelectionBackgroundColor);

        var doc = state.Document;
        var viewportLines = viewport.Height;
        var viewportColumns = viewport.Width;

        // Collect all cursor positions and selection ranges
        var cursorPositions = new HashSet<(int Line, int Column)>();
        var selectionRanges = new List<(int Start, int End)>();

        if (isFocused)
        {
            foreach (var cursor in state.Cursors)
            {
                var pos = doc.OffsetToPosition(cursor.Position);
                cursorPositions.Add((pos.Line, pos.Column));

                if (cursor.HasSelection)
                {
                    selectionRanges.Add((cursor.SelectionStart.Value, cursor.SelectionEnd.Value));
                }
            }
        }

        for (var viewLine = 0; viewLine < viewportLines; viewLine++)
        {
            var docLine = scrollOffset + viewLine;
            var screenY = viewport.Y + viewLine;
            var screenX = viewport.X;

            if (docLine > doc.LineCount)
            {
                var emptyLine = "~".PadRight(viewportColumns);
                RenderLine(context, screenX, screenY, emptyLine, fg, bg, cursorFg, cursorBg, selFg, selBg, null);
                continue;
            }

            var lineText = doc.GetLineText(docLine);

            // Apply horizontal scroll offset
            string scrolledLine;
            if (horizontalScrollOffset > 0)
            {
                scrolledLine = horizontalScrollOffset < lineText.Length
                    ? lineText[horizontalScrollOffset..]
                    : "";
            }
            else
            {
                scrolledLine = lineText;
            }

            string displayText;
            if (scrolledLine.Length >= viewportColumns)
            {
                displayText = scrolledLine[..viewportColumns];
            }
            else
            {
                displayText = scrolledLine.PadRight(viewportColumns);
            }

            // Build per-column cell type map for this line
            var lineStartOffset = doc.PositionToOffset(new DocumentPosition(docLine, 1)).Value;
            var lineEndOffset = lineStartOffset + lineText.Length;
            var cellTypes = BuildCellTypes(displayText.Length, docLine, lineStartOffset, lineEndOffset,
                cursorPositions, selectionRanges, horizontalScrollOffset);

            RenderLine(context, screenX, screenY, displayText, fg, bg, cursorFg, cursorBg, selFg, selBg, cellTypes);
        }
    }

    /// <inheritdoc />
    public DocumentOffset? HitTest(int localX, int localY, EditorState state, int viewportColumns, int viewportLines, int scrollOffset, int horizontalScrollOffset)
    {
        if (localX < 0 || localY < 0 || localX >= viewportColumns || localY >= viewportLines)
            return null;

        var docLine = scrollOffset + localY;
        var doc = state.Document;

        if (docLine > doc.LineCount)
        {
            // Clicked in the ~ area — clamp to end of document
            return new DocumentOffset(doc.Length);
        }

        var lineText = doc.GetLineText(docLine);
        var column = Math.Min(localX + horizontalScrollOffset + 1, lineText.Length + 1); // 1-based, clamp to line end + 1
        return doc.PositionToOffset(new DocumentPosition(docLine, column));
    }

    /// <inheritdoc />
    public int GetTotalLines(IHex1bDocument document, int viewportColumns) => document.LineCount;

    /// <inheritdoc />
    public int GetMaxLineWidth(IHex1bDocument document, int scrollOffset, int viewportLines, int viewportColumns)
    {
        var maxWidth = 0;
        for (var line = scrollOffset; line <= Math.Min(scrollOffset + viewportLines - 1, document.LineCount); line++)
        {
            var lineLen = document.GetLineText(line).Length;
            if (lineLen > maxWidth) maxWidth = lineLen;
        }
        return maxWidth;
    }

    // ── Cell type logic (extracted from EditorNode) ──────────────

    private enum CellType : byte
    {
        Normal = 0,
        Selected = 1,
        Cursor = 2
    }

    private static CellType[]? BuildCellTypes(
        int displayWidth,
        int docLine,
        int lineStartOffset,
        int lineEndOffset,
        HashSet<(int Line, int Column)> cursorPositions,
        List<(int Start, int End)> selectionRanges,
        int horizontalScrollOffset = 0)
    {
        var hasCursor = false;
        foreach (var (line, _) in cursorPositions)
        {
            if (line == docLine) { hasCursor = true; break; }
        }

        var hasSelection = false;
        foreach (var (start, end) in selectionRanges)
        {
            if (start < lineEndOffset + 1 && end > lineStartOffset)
            {
                hasSelection = true;
                break;
            }
        }

        if (!hasCursor && !hasSelection) return null;

        var types = new CellType[displayWidth];

        if (hasSelection)
        {
            var lineTextLength = lineEndOffset - lineStartOffset;

            foreach (var (start, end) in selectionRanges)
            {
                var selStartCol = Math.Max(0, start - lineStartOffset - horizontalScrollOffset);
                var selEndCol = Math.Min(displayWidth, end - lineStartOffset - horizontalScrollOffset);

                for (var col = selStartCol; col < selEndCol; col++)
                {
                    if (col >= 0)
                        types[col] = CellType.Selected;
                }
            }
        }

        if (hasCursor)
        {
            foreach (var (line, column) in cursorPositions)
            {
                if (line == docLine)
                {
                    var col = column - 1 - horizontalScrollOffset; // 0-based, adjusted for scroll
                    if (col >= 0 && col < displayWidth)
                    {
                        types[col] = CellType.Cursor;
                    }
                }
            }
        }

        return types;
    }

    private static void RenderLine(
        Hex1bRenderContext context,
        int x, int y,
        string text,
        Hex1bColor fg, Hex1bColor bg,
        Hex1bColor cursorFg, Hex1bColor cursorBg,
        Hex1bColor selFg, Hex1bColor selBg,
        CellType[]? cellTypes)
    {
        string output;

        if (cellTypes != null)
        {
            var globalColors = context.Theme.GetGlobalColorCodes();
            var resetToGlobal = context.Theme.GetResetToGlobalCodes();
            var sb = new System.Text.StringBuilder(text.Length * 2);
            sb.Append(globalColors);

            var prevType = CellType.Normal;
            sb.Append(fg.ToForegroundAnsi());
            sb.Append(bg.ToBackgroundAnsi());

            for (var i = 0; i < text.Length; i++)
            {
                var cellType = i < cellTypes.Length ? cellTypes[i] : CellType.Normal;

                if (cellType != prevType)
                {
                    switch (cellType)
                    {
                        case CellType.Cursor:
                            sb.Append(cursorFg.ToForegroundAnsi());
                            sb.Append(cursorBg.ToBackgroundAnsi());
                            break;
                        case CellType.Selected:
                            sb.Append(selFg.ToForegroundAnsi());
                            sb.Append(selBg.ToBackgroundAnsi());
                            break;
                        case CellType.Normal:
                            sb.Append(resetToGlobal);
                            sb.Append(fg.ToForegroundAnsi());
                            sb.Append(bg.ToBackgroundAnsi());
                            break;
                    }
                    prevType = cellType;
                }

                sb.Append(text[i]);
            }

            if (prevType != CellType.Normal)
            {
                sb.Append(resetToGlobal);
                sb.Append(fg.ToForegroundAnsi());
                sb.Append(bg.ToBackgroundAnsi());
            }

            output = sb.ToString();
        }
        else
        {
            output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{text}";
        }

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(x, y, output);
        }
        else
        {
            context.Write(output);
        }
    }
}
