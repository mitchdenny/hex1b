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

            // Fit to viewport by display width: truncate at boundary, pad remaining with spaces
            var displayText = FitToDisplayWidth(scrolledLine, viewportColumns);

            // Build per-column cell type map for this line
            var lineStartOffset = doc.PositionToOffset(new DocumentPosition(docLine, 1)).Value;
            var lineEndOffset = lineStartOffset + lineText.Length;
            var cellTypes = BuildCellTypes(displayText, docLine, lineStartOffset, lineEndOffset,
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

        // Convert display column (localX) to char position, accounting for wide characters
        var scrolledText = horizontalScrollOffset < lineText.Length
            ? lineText[horizontalScrollOffset..]
            : "";

        var charIndex = 0;
        var displayCol = 0;
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(scrolledText);
        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            var gw = DisplayWidth.GetGraphemeWidth(grapheme);

            if (displayCol + gw > localX)
                break;

            displayCol += gw;
            charIndex += grapheme.Length;
        }

        var column = Math.Min(charIndex + horizontalScrollOffset + 1, lineText.Length + 1);
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
            var lineWidth = DisplayWidth.GetStringWidth(document.GetLineText(line));
            if (lineWidth > maxWidth) maxWidth = lineWidth;
        }
        return maxWidth;
    }

    // ── Cell type logic (extracted from EditorNode) ──────────────

    /// <summary>
    /// Truncates text to fit within targetWidth display columns and pads remaining space.
    /// Wide characters that don't fully fit at the boundary are excluded.
    /// </summary>
    private static string FitToDisplayWidth(string text, int targetWidth)
    {
        if (targetWidth <= 0)
            return "";

        if (string.IsNullOrEmpty(text))
            return new string(' ', targetWidth);

        var (sliced, cols, _, _) = DisplayWidth.SliceByDisplayWidth(text, 0, targetWidth);
        var padding = targetWidth - cols;
        return padding > 0 ? sliced + new string(' ', padding) : sliced;
    }

    private enum CellType : byte
    {
        Normal = 0,
        Selected = 1,
        Cursor = 2
    }

    private static CellType[]? BuildCellTypes(
        string displayText,
        int docLine,
        int lineStartOffset,
        int lineEndOffset,
        HashSet<(int Line, int Column)> cursorPositions,
        List<(int Start, int End)> selectionRanges,
        int horizontalScrollOffset = 0)
    {
        var displayWidth = displayText.Length;
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
            // Selection should not highlight past the last printable character on the line
            var maxSelCol = lineTextLength - horizontalScrollOffset;

            foreach (var (start, end) in selectionRanges)
            {
                var selStartCol = Math.Max(0, start - lineStartOffset - horizontalScrollOffset);
                var selEndCol = Math.Min(Math.Min(displayWidth, maxSelCol), end - lineStartOffset - horizontalScrollOffset);

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

        // Extend cell types over surrogate pairs — both chars of a pair
        // must share the same type to avoid splitting them during rendering
        for (var i = 0; i < displayWidth - 1; i++)
        {
            if (char.IsHighSurrogate(displayText[i]) && char.IsLowSurrogate(displayText[i + 1]))
            {
                // Use the more significant type for the pair
                if (types[i] != CellType.Normal || types[i + 1] != CellType.Normal)
                {
                    var pairType = types[i] != CellType.Normal ? types[i] : types[i + 1];
                    if (types[i] == CellType.Cursor || types[i + 1] == CellType.Cursor)
                        pairType = CellType.Cursor;
                    types[i] = pairType;
                    types[i + 1] = pairType;
                }
                i++; // skip low surrogate
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

                // For surrogate pairs, use the cell type of the first char for both
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
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

                    // Write surrogate pair as a unit
                    sb.Append(text[i]);
                    sb.Append(text[i + 1]);
                    i++; // skip low surrogate
                    continue;
                }

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
