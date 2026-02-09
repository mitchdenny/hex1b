using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Renders document content as a hex dump with offset, hex bytes, and ASCII columns.
/// Format per row: "XXXXXXXX  XX XX XX XX XX XX XX XX  XX XX XX XX XX XX XX XX  |................|"
/// </summary>
public sealed class HexEditorViewRenderer : IEditorViewRenderer
{
    /// <summary>
    /// Number of bytes displayed per row.
    /// </summary>
    public int BytesPerRow { get; init; } = 16;

    /// <summary>
    /// Whether to show the ASCII representation column on the right.
    /// </summary>
    public bool ShowAscii { get; init; } = true;

    /// <summary>
    /// Shared singleton instance with default settings (16 bytes/row, ASCII enabled).
    /// </summary>
    public static HexEditorViewRenderer Instance { get; } = new();

    // Column layout for 16 bytes/row:
    // "00000000  XX XX XX XX XX XX XX XX  XX XX XX XX XX XX XX XX  |................|"
    //  [8 addr] [2 gap] [23 hex-left] [2 gap] [23 hex-right] [2 gap] [1+16+1 ascii]
    // Total: 8 + 2 + 23 + 2 + 23 + 2 + 18 = 78
    private int AddressWidth => 8;
    private int GapWidth => 2;
    private int HexGroupWidth => BytesPerRow / 2 * 3 - 1; // "XX XX ... XX" for half the bytes
    private int AsciiWidth => ShowAscii ? 1 + BytesPerRow + 1 : 0; // "|...|"

    private int RowWidth =>
        AddressWidth + GapWidth + HexGroupWidth + GapWidth + HexGroupWidth + GapWidth + AsciiWidth;

    /// <inheritdoc />
    public void Render(Hex1bRenderContext context, EditorState state, Rect viewport, int scrollOffset, bool isFocused)
    {
        var theme = context.Theme;
        var fg = theme.Get(EditorTheme.ForegroundColor);
        var bg = theme.Get(EditorTheme.BackgroundColor);
        var cursorFg = theme.Get(EditorTheme.CursorForegroundColor);
        var cursorBg = theme.Get(EditorTheme.CursorBackgroundColor);
        var selFg = theme.Get(EditorTheme.SelectionForegroundColor);
        var selBg = theme.Get(EditorTheme.SelectionBackgroundColor);

        var doc = state.Document;
        var docText = doc.GetText();
        var docBytes = System.Text.Encoding.UTF8.GetBytes(docText);
        var totalBytes = docBytes.Length;
        var totalRows = GetTotalLines(doc);

        // Collect selection ranges for highlight
        var selectionRanges = new List<(int Start, int End)>();
        int cursorByteOffset = -1;

        if (isFocused)
        {
            // Map cursor document offset to byte offset (approximate — UTF-8 aware)
            var cursorDocOffset = state.Cursor.Position.Value;
            cursorByteOffset = System.Text.Encoding.UTF8.GetByteCount(docText.AsSpan(0, Math.Min(cursorDocOffset, docText.Length)));

            foreach (var cursor in state.Cursors)
            {
                if (cursor.HasSelection)
                {
                    var startByte = System.Text.Encoding.UTF8.GetByteCount(
                        docText.AsSpan(0, Math.Min(cursor.SelectionStart.Value, docText.Length)));
                    var endByte = System.Text.Encoding.UTF8.GetByteCount(
                        docText.AsSpan(0, Math.Min(cursor.SelectionEnd.Value, docText.Length)));
                    selectionRanges.Add((startByte, endByte));
                }
            }
        }

        for (var viewLine = 0; viewLine < viewport.Height; viewLine++)
        {
            var row = (scrollOffset - 1) + viewLine; // 0-based row index
            var screenY = viewport.Y + viewLine;
            var screenX = viewport.X;
            var rowByteStart = row * BytesPerRow;

            if (rowByteStart >= totalBytes)
            {
                // Past end of data — render empty
                var emptyLine = "~".PadRight(viewport.Width);
                RenderPlainLine(context, screenX, screenY, emptyLine, fg, bg);
                continue;
            }

            var rowByteEnd = Math.Min(rowByteStart + BytesPerRow, totalBytes);
            var rowByteCount = rowByteEnd - rowByteStart;

            var sb = new System.Text.StringBuilder(RowWidth + 10);
            var colorSb = new System.Text.StringBuilder(RowWidth * 4);

            // Address column
            sb.Append(rowByteStart.ToString("X8"));
            sb.Append("  ");

            // Hex bytes — two groups of BytesPerRow/2
            var half = BytesPerRow / 2;
            for (int i = 0; i < BytesPerRow; i++)
            {
                if (i == half) sb.Append(' ');

                if (i < rowByteCount)
                {
                    sb.Append(docBytes[rowByteStart + i].ToString("X2"));
                }
                else
                {
                    sb.Append("  ");
                }

                if (i < BytesPerRow - 1 && i != half - 1)
                    sb.Append(' ');
            }

            sb.Append("  ");

            // ASCII column
            if (ShowAscii)
            {
                sb.Append('|');
                for (int i = 0; i < BytesPerRow; i++)
                {
                    if (i < rowByteCount)
                    {
                        var b = docBytes[rowByteStart + i];
                        sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
                    }
                    else
                    {
                        sb.Append(' ');
                    }
                }
                sb.Append('|');
            }

            var line = sb.ToString();

            // Pad or truncate to viewport width
            if (line.Length < viewport.Width)
                line = line.PadRight(viewport.Width);
            else if (line.Length > viewport.Width)
                line = line[..viewport.Width];

            // Build color map — highlight hex bytes that are under cursor or selection
            var cellColors = new CellColorType[line.Length];

            if (isFocused)
            {
                for (int i = 0; i < rowByteCount; i++)
                {
                    var byteIdx = rowByteStart + i;
                    var hexCol = GetHexColumnForByte(i);
                    var asciiCol = GetAsciiColumnForByte(i);

                    // Check cursor
                    if (byteIdx == cursorByteOffset)
                    {
                        SetCellRange(cellColors, hexCol, 2, CellColorType.Cursor, line.Length);
                        if (ShowAscii && asciiCol < line.Length)
                            cellColors[asciiCol] = CellColorType.Cursor;
                    }
                    else
                    {
                        // Check selection
                        foreach (var (selStart, selEnd) in selectionRanges)
                        {
                            if (byteIdx >= selStart && byteIdx < selEnd)
                            {
                                SetCellRange(cellColors, hexCol, 2, CellColorType.Selected, line.Length);
                                if (ShowAscii && asciiCol < line.Length)
                                    cellColors[asciiCol] = CellColorType.Selected;
                                break;
                            }
                        }
                    }
                }
            }

            RenderColoredLine(context, screenX, screenY, line, fg, bg, cursorFg, cursorBg, selFg, selBg, cellColors);
        }
    }

    /// <inheritdoc />
    public DocumentOffset? HitTest(int localX, int localY, EditorState state, int viewportColumns, int viewportLines, int scrollOffset)
    {
        if (localX < 0 || localY < 0 || localX >= viewportColumns || localY >= viewportLines)
            return null;

        var doc = state.Document;
        var docText = doc.GetText();
        var docBytes = System.Text.Encoding.UTF8.GetBytes(docText);
        var row = (scrollOffset - 1) + localY;
        var rowByteStart = row * BytesPerRow;

        if (rowByteStart >= docBytes.Length)
            return new DocumentOffset(doc.Length);

        // Determine which byte was clicked based on X position
        var byteIndex = GetByteIndexFromColumn(localX);
        if (byteIndex < 0) byteIndex = 0;

        var targetByte = Math.Min(rowByteStart + byteIndex, docBytes.Length);

        // Convert byte offset back to character offset
        var charOffset = 0;
        var byteCount = 0;
        while (charOffset < docText.Length && byteCount < targetByte)
        {
            byteCount += System.Text.Encoding.UTF8.GetByteCount(docText.AsSpan(charOffset, 1));
            charOffset++;
        }

        return new DocumentOffset(Math.Min(charOffset, doc.Length));
    }

    /// <inheritdoc />
    public int GetTotalLines(IHex1bDocument document)
    {
        var docText = document.GetText();
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(docText);
        return Math.Max(1, (byteCount + BytesPerRow - 1) / BytesPerRow);
    }

    /// <inheritdoc />
    public int GetMaxLineWidth(IHex1bDocument document, int scrollOffset, int viewportLines) => RowWidth;

    // ── Column mapping helpers ─────────────────────────────────

    private int GetHexColumnForByte(int byteInRow)
    {
        // After "XXXXXXXX  " (10 chars), hex bytes start
        // First half: bytes 0-7, each "XX " (3 chars) except last "XX" (2 chars)
        // Then 1 space gap, then second half
        var half = BytesPerRow / 2;
        var hexStart = AddressWidth + GapWidth;

        if (byteInRow < half)
        {
            return hexStart + byteInRow * 3;
        }
        else
        {
            return hexStart + half * 3 + (byteInRow - half) * 3;
        }
    }

    private int GetAsciiColumnForByte(int byteInRow)
    {
        // ASCII starts after hex section + gap + "|"
        var asciiStart = AddressWidth + GapWidth + HexGroupWidth + GapWidth + HexGroupWidth + GapWidth + 1;
        return asciiStart + byteInRow;
    }

    private int GetByteIndexFromColumn(int column)
    {
        var hexStart = AddressWidth + GapWidth;
        var half = BytesPerRow / 2;
        var secondHexStart = hexStart + half * 3;
        var asciiStart = AddressWidth + GapWidth + HexGroupWidth + GapWidth + HexGroupWidth + GapWidth + 1;

        // Check if in first hex group
        if (column >= hexStart && column < hexStart + half * 3)
        {
            return (column - hexStart) / 3;
        }
        // Check if in second hex group
        if (column >= secondHexStart && column < secondHexStart + half * 3)
        {
            return half + (column - secondHexStart) / 3;
        }
        // Check if in ASCII column
        if (ShowAscii && column >= asciiStart && column < asciiStart + BytesPerRow)
        {
            return column - asciiStart;
        }
        // In address or gap area
        return 0;
    }

    // ── Rendering helpers ──────────────────────────────────────

    private enum CellColorType : byte
    {
        Normal = 0,
        Selected = 1,
        Cursor = 2
    }

    private static void SetCellRange(CellColorType[] cells, int start, int length, CellColorType type, int maxLen)
    {
        for (int i = start; i < start + length && i < maxLen; i++)
            cells[i] = type;
    }

    private static void RenderPlainLine(
        Hex1bRenderContext context, int x, int y, string text,
        Hex1bColor fg, Hex1bColor bg)
    {
        var output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{text}";
        if (context.CurrentLayoutProvider != null)
            context.WriteClipped(x, y, output);
        else
            context.Write(output);
    }

    private static void RenderColoredLine(
        Hex1bRenderContext context, int x, int y, string text,
        Hex1bColor fg, Hex1bColor bg,
        Hex1bColor cursorFg, Hex1bColor cursorBg,
        Hex1bColor selFg, Hex1bColor selBg,
        CellColorType[] cellColors)
    {
        var hasDecorations = false;
        foreach (var c in cellColors)
        {
            if (c != CellColorType.Normal) { hasDecorations = true; break; }
        }

        if (!hasDecorations)
        {
            RenderPlainLine(context, x, y, text, fg, bg);
            return;
        }

        var globalColors = context.Theme.GetGlobalColorCodes();
        var resetToGlobal = context.Theme.GetResetToGlobalCodes();
        var sb = new System.Text.StringBuilder(text.Length * 2);
        sb.Append(globalColors);

        var prevType = CellColorType.Normal;
        sb.Append(fg.ToForegroundAnsi());
        sb.Append(bg.ToBackgroundAnsi());

        for (var i = 0; i < text.Length; i++)
        {
            var cellType = i < cellColors.Length ? cellColors[i] : CellColorType.Normal;

            if (cellType != prevType)
            {
                switch (cellType)
                {
                    case CellColorType.Cursor:
                        sb.Append(cursorFg.ToForegroundAnsi());
                        sb.Append(cursorBg.ToBackgroundAnsi());
                        break;
                    case CellColorType.Selected:
                        sb.Append(selFg.ToForegroundAnsi());
                        sb.Append(selBg.ToBackgroundAnsi());
                        break;
                    case CellColorType.Normal:
                        sb.Append(resetToGlobal);
                        sb.Append(fg.ToForegroundAnsi());
                        sb.Append(bg.ToBackgroundAnsi());
                        break;
                }
                prevType = cellType;
            }

            sb.Append(text[i]);
        }

        if (prevType != CellColorType.Normal)
        {
            sb.Append(resetToGlobal);
            sb.Append(fg.ToForegroundAnsi());
            sb.Append(bg.ToBackgroundAnsi());
        }

        var output = sb.ToString();
        if (context.CurrentLayoutProvider != null)
            context.WriteClipped(x, y, output);
        else
            context.Write(output);
    }
}
