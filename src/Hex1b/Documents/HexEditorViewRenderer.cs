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
    /// Minimum bytes per row (default 1). The layout never goes below this.
    /// </summary>
    public int MinBytesPerRow { get; init; } = 1;

    /// <summary>
    /// Maximum bytes per row (default 16). The layout never exceeds this.
    /// </summary>
    public int MaxBytesPerRow { get; init; } = 16;

    /// <summary>
    /// Optional snap points that control how many bytes per row are used at each
    /// width breakpoint. When set, the renderer rounds down to the largest snap
    /// point that fits. When <c>null</c> the renderer is fully fluid — any byte
    /// count between <see cref="MinBytesPerRow"/> and <see cref="MaxBytesPerRow"/>
    /// may be used.
    /// <para>Values must be in ascending order (e.g. <c>[1, 8, 16]</c>).</para>
    /// </summary>
    public int[]? SnapPoints { get; init; }

    /// <summary>
    /// Whether to show the ASCII representation column on the right.
    /// </summary>
    public bool ShowAscii { get; init; } = true;

    /// <summary>
    /// Shared singleton instance with default settings (fluid layout, 1–16 bytes/row, ASCII enabled).
    /// </summary>
    public static HexEditorViewRenderer Instance { get; } = new();

    /// <summary>
    /// Well-known snap points: power-of-two breakpoints (1, 2, 4, 8, 16).
    /// </summary>
    public static int[] PowerOfTwoSnaps { get; } = [1, 2, 4, 8, 16];

    /// <summary>
    /// Well-known snap points: standard hex-dump breakpoints (1, 8, 16).
    /// </summary>
    public static int[] StandardSnaps { get; } = [1, 8, 16];

    // ── Responsive layout ────────────────────────────────────────

    // Layout: "XXXXXXXX HH HH ... HH cccc"
    //  Address (8) + space (1) + hex (3*N - 1) + space (1) + ASCII (N)
    //  = 4N + 9  (no mid-group gap)
    //  = 4N + 10 (with 1-char mid-group gap when N >= 4)
    private const int AddressWidth = 8;

    /// <summary>
    /// Calculates the responsive layout for a given viewport width.
    /// Returns (bytesPerRow, hasMidGroup) where bytesPerRow respects
    /// <see cref="MinBytesPerRow"/>, <see cref="MaxBytesPerRow"/>,
    /// and <see cref="SnapPoints"/> constraints.
    /// </summary>
    internal (int bytesPerRow, bool hasMidGroup) CalculateLayout(int availableWidth)
    {
        // Without mid-group gap: width = 4N + 9  → N = (width - 9) / 4
        var maxFit = Math.Max(1, (availableWidth - 9) / 4);

        // Clamp to configured min/max
        var bytesPerRow = Math.Clamp(maxFit, MinBytesPerRow, MaxBytesPerRow);

        // Apply snap points: round down to the largest snap that doesn't exceed bytesPerRow
        if (SnapPoints is { Length: > 0 })
        {
            var snapped = SnapPoints[0];
            foreach (var sp in SnapPoints)
            {
                if (sp <= bytesPerRow)
                    snapped = sp;
                else
                    break;
            }
            bytesPerRow = Math.Max(snapped, MinBytesPerRow);
        }

        // Mid-group gap is shown when >= 4 bytes AND the wider row still fits
        var hasMidGroup = bytesPerRow >= 4
            && RowWidthForLayout(bytesPerRow, hasMidGroup: true) <= availableWidth;

        return (bytesPerRow, hasMidGroup);
    }

    private static int RowWidthForLayout(int bytesPerRow, bool hasMidGroup)
    {
        var hexWidth = bytesPerRow * 3 - 1 + (hasMidGroup ? 1 : 0);
        return AddressWidth + 1 + hexWidth + 1 + bytesPerRow;
    }

    /// <inheritdoc />
    public void Render(Hex1bRenderContext context, EditorState state, Rect viewport, int scrollOffset, int horizontalScrollOffset, bool isFocused)
    {
        var (bytesPerRow, hasMidGroup) = CalculateLayout(viewport.Width);
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

        // Collect selection ranges for highlight
        var selectionRanges = new List<(int Start, int End)>();
        int cursorByteOffset = -1;

        if (isFocused)
        {
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

        var half = bytesPerRow / 2;

        for (var viewLine = 0; viewLine < viewport.Height; viewLine++)
        {
            var row = (scrollOffset - 1) + viewLine;
            var screenY = viewport.Y + viewLine;
            var screenX = viewport.X;
            var rowByteStart = row * bytesPerRow;

            if (rowByteStart >= totalBytes)
            {
                var emptyLine = "~".PadRight(viewport.Width);
                RenderPlainLine(context, screenX, screenY, emptyLine, fg, bg);
                continue;
            }

            var rowByteEnd = Math.Min(rowByteStart + bytesPerRow, totalBytes);
            var rowByteCount = rowByteEnd - rowByteStart;

            var rowWidth = RowWidthForLayout(bytesPerRow, hasMidGroup);
            var sb = new System.Text.StringBuilder(rowWidth + 10);

            // Address
            sb.Append(rowByteStart.ToString("X8"));
            sb.Append(' ');

            // Hex bytes
            for (int i = 0; i < bytesPerRow; i++)
            {
                if (hasMidGroup && i == half) sb.Append(' ');

                sb.Append(i < rowByteCount
                    ? docBytes[rowByteStart + i].ToString("X2")
                    : "  ");

                if (i < bytesPerRow - 1 && !(hasMidGroup && i == half - 1))
                    sb.Append(' ');
            }

            sb.Append(' ');

            // ASCII
            for (int i = 0; i < bytesPerRow; i++)
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

            var line = sb.ToString();

            if (line.Length < viewport.Width)
                line = line.PadRight(viewport.Width);
            else if (line.Length > viewport.Width)
                line = line[..viewport.Width];

            // Build color map
            var cellColors = new CellColorType[line.Length];

            if (isFocused)
            {
                for (int i = 0; i < rowByteCount; i++)
                {
                    var byteIdx = rowByteStart + i;
                    var hexCol = GetHexColumnForByte(i, bytesPerRow, hasMidGroup);
                    var asciiCol = GetAsciiColumnForByte(i, bytesPerRow, hasMidGroup);

                    if (byteIdx == cursorByteOffset)
                    {
                        SetCellRange(cellColors, hexCol, 2, CellColorType.Cursor, line.Length);
                        if (asciiCol < line.Length)
                            cellColors[asciiCol] = CellColorType.Cursor;
                    }
                    else
                    {
                        foreach (var (selStart, selEnd) in selectionRanges)
                        {
                            if (byteIdx >= selStart && byteIdx < selEnd)
                            {
                                SetCellRange(cellColors, hexCol, 2, CellColorType.Selected, line.Length);
                                if (asciiCol < line.Length)
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
    public DocumentOffset? HitTest(int localX, int localY, EditorState state, int viewportColumns, int viewportLines, int scrollOffset, int horizontalScrollOffset)
    {
        if (localX < 0 || localY < 0 || localX >= viewportColumns || localY >= viewportLines)
            return null;

        var (bytesPerRow, hasMidGroup) = CalculateLayout(viewportColumns);
        var doc = state.Document;
        var docText = doc.GetText();
        var docBytes = System.Text.Encoding.UTF8.GetBytes(docText);
        var row = (scrollOffset - 1) + localY;
        var rowByteStart = row * bytesPerRow;

        if (rowByteStart >= docBytes.Length)
            return new DocumentOffset(doc.Length);

        var byteIndex = GetByteIndexFromColumn(localX, bytesPerRow, hasMidGroup);
        if (byteIndex < 0) byteIndex = 0;

        var targetByte = Math.Min(rowByteStart + byteIndex, docBytes.Length);

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
    public int GetTotalLines(IHex1bDocument document, int viewportColumns)
    {
        var (bytesPerRow, _) = CalculateLayout(viewportColumns);
        var docText = document.GetText();
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(docText);
        return Math.Max(1, (byteCount + bytesPerRow - 1) / bytesPerRow);
    }

    /// <inheritdoc />
    public int GetMaxLineWidth(IHex1bDocument document, int scrollOffset, int viewportLines, int viewportColumns)
    {
        // Responsive: row width always fits within viewport, so no horizontal scrollbar needed
        var (bytesPerRow, hasMidGroup) = CalculateLayout(viewportColumns);
        return RowWidthForLayout(bytesPerRow, hasMidGroup);
    }

    // ── Column mapping helpers ─────────────────────────────────

    private static int GetHexColumnForByte(int byteInRow, int bytesPerRow, bool hasMidGroup)
    {
        // After "XXXXXXXX " (9 chars), hex bytes start
        var hexStart = AddressWidth + 1;
        var half = bytesPerRow / 2;

        if (!hasMidGroup)
            return hexStart + byteInRow * 3;

        return byteInRow < half
            ? hexStart + byteInRow * 3
            : hexStart + half * 3 + 1 + (byteInRow - half) * 3;
    }

    private static int GetAsciiColumnForByte(int byteInRow, int bytesPerRow, bool hasMidGroup)
    {
        var hexWidth = bytesPerRow * 3 - 1 + (hasMidGroup ? 1 : 0);
        var asciiStart = AddressWidth + 1 + hexWidth + 1;
        return asciiStart + byteInRow;
    }

    private static int GetByteIndexFromColumn(int column, int bytesPerRow, bool hasMidGroup)
    {
        var hexStart = AddressWidth + 1;
        var half = bytesPerRow / 2;
        var hexWidth = bytesPerRow * 3 - 1 + (hasMidGroup ? 1 : 0);
        var asciiStart = hexStart + hexWidth + 1;

        // In ASCII region
        if (column >= asciiStart && column < asciiStart + bytesPerRow)
            return column - asciiStart;

        // In hex region
        if (column >= hexStart && column < hexStart + hexWidth)
        {
            if (!hasMidGroup)
                return Math.Min((column - hexStart) / 3, bytesPerRow - 1);

            var firstGroupEnd = hexStart + half * 3 - 1;
            if (column <= firstGroupEnd)
                return Math.Min((column - hexStart) / 3, half - 1);

            var secondGroupStart = hexStart + half * 3 + 1;
            if (column >= secondGroupStart)
                return Math.Min(half + (column - secondGroupStart) / 3, bytesPerRow - 1);

            // In the mid-group gap — snap to nearest
            return half;
        }

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
