using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Renders document content as a hex dump with offset, hex bytes, and ASCII columns.
/// Format per row: "XXXXXXXX  XX XX XX XX XX XX XX XX XX XX XX XX XX XX XX XX  ................"
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
    /// When enabled, bytes that belong to multi-byte UTF-8 characters are rendered
    /// with a distinct background color to visually indicate character boundaries.
    /// The focused byte within a multi-byte group uses the cursor color; the other
    /// bytes in the same character use the multi-byte highlight color.
    /// </summary>
    public bool HighlightMultiByteChars { get; init; } = false;

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

    // Layout: "XXXXXXXX  HH HH ... HH  cccc"
    //  Address (8) + gap (2) + hex (3*N - 1) + gap (2) + ASCII (N)
    //  = 4N + 11
    private const int AddressWidth = 8;
    private const int SeparatorWidth = 2;

    /// <summary>
    /// Calculates the responsive layout for a given viewport width.
    /// Returns the number of bytes per row, respecting
    /// <see cref="MinBytesPerRow"/>, <see cref="MaxBytesPerRow"/>,
    /// and <see cref="SnapPoints"/> constraints.
    /// </summary>
    internal int CalculateLayout(int availableWidth)
    {
        // width = 4N + 11  → N = (width - 11) / 4
        var maxFit = Math.Max(1, (availableWidth - 11) / 4);

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

        return bytesPerRow;
    }

    private static int RowWidthForLayout(int bytesPerRow)
    {
        var hexWidth = bytesPerRow * 3 - 1;
        return AddressWidth + SeparatorWidth + hexWidth + SeparatorWidth + bytesPerRow;
    }

    // ── Hex input handling ───────────────────────────────────────

    /// <inheritdoc />
    public bool HandlesCharInput => true;

    /// <inheritdoc />
    public bool HandleCharInput(char c, EditorState state, ref char? pendingNibble, int viewportColumns)
    {
        if (state.IsReadOnly) return false;

        var nibbleValue = HexCharToNibble(c);
        if (nibbleValue < 0)
        {
            // Not a hex character — cancel any pending nibble and don't consume
            pendingNibble = null;
            return false;
        }

        if (pendingNibble is null)
        {
            // First nibble — store it, don't edit the document yet
            pendingNibble = c;
            return true;
        }

        // Second nibble — combine with first and commit the byte
        var highNibble = HexCharToNibble(pendingNibble.Value);
        var byteValue = (byte)((highNibble << 4) | nibbleValue);
        pendingNibble = null;

        CommitByte(state, byteValue, viewportColumns);
        return true;
    }

    private void CommitByte(EditorState state, byte byteValue, int viewportColumns)
    {
        var doc = state.Document;
        var totalBytes = doc.ByteCount;

        // Map cursor's character position to byte offset using actual document bytes
        var cursorCharPos = state.Cursor.Position.Value;
        var map = new Utf8ByteMap(doc.GetBytes().Span);
        var cursorByteOffset = cursorCharPos < map.CharCount
            ? map.CharToByteStart(cursorCharPos)
            : totalBytes;

        if (cursorByteOffset >= totalBytes)
        {
            // Append at end — use byte API to insert the raw byte
            doc.ApplyBytes(new ByteInsertOperation(totalBytes, [byteValue]));
            var newText = doc.GetText();
            state.Cursor.Position = new DocumentOffset(newText.Length);
            state.Cursor.ClearSelection();
            return;
        }

        // Replace the single byte directly — no UTF-8 round-trip corruption
        doc.ApplyBytes(new ByteReplaceOperation(cursorByteOffset, 1, [byteValue]));

        // Position cursor after the replaced byte
        var targetByteAfterEdit = cursorByteOffset + 1;
        if (targetByteAfterEdit < doc.ByteCount)
        {
            var newMap = new Utf8ByteMap(doc.GetBytes().Span);
            var (nextCharIdx, _) = newMap.ByteToChar(targetByteAfterEdit);
            state.Cursor.Position = new DocumentOffset(nextCharIdx);
        }
        else
        {
            state.Cursor.Position = new DocumentOffset(doc.GetText().Length);
        }
        state.Cursor.ClearSelection();
    }

    private static int HexCharToNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };

    /// <inheritdoc />
    public void Render(Hex1bRenderContext context, EditorState state, Rect viewport, int scrollOffset, int horizontalScrollOffset, bool isFocused, char? pendingNibble = null)
    {
        var bytesPerRow = CalculateLayout(viewport.Width);
        var theme = context.Theme;
        var fg = theme.Get(EditorTheme.ForegroundColor);
        var bg = theme.Get(EditorTheme.BackgroundColor);
        var cursorFg = theme.Get(EditorTheme.CursorForegroundColor);
        var cursorBg = theme.Get(EditorTheme.CursorBackgroundColor);
        var selFg = theme.Get(EditorTheme.SelectionForegroundColor);
        var selBg = theme.Get(EditorTheme.SelectionBackgroundColor);

        var doc = state.Document;
        var docBytesMemory = doc.GetBytes();
        var docBytes = docBytesMemory.Span;
        var totalBytes = docBytes.Length;

        // Build byte↔char map from actual document bytes
        var byteMap = new Utf8ByteMap(docBytes);

        // Collect selection ranges for highlight (in byte offsets)
        var selectionRanges = new List<(int Start, int End)>();
        int cursorByteOffset = -1;

        if (isFocused)
        {
            var cursorDocOffset = Math.Min(state.Cursor.Position.Value, byteMap.CharCount);
            cursorByteOffset = cursorDocOffset < byteMap.CharCount
                ? byteMap.CharToByteStart(cursorDocOffset)
                : totalBytes;

            foreach (var cursor in state.Cursors)
            {
                if (cursor.HasSelection)
                {
                    var selStart = Math.Min(cursor.SelectionStart.Value, byteMap.CharCount);
                    var selEnd = Math.Min(cursor.SelectionEnd.Value, byteMap.CharCount);
                    var startByte = selStart < byteMap.CharCount
                        ? byteMap.CharToByteStart(selStart) : totalBytes;
                    var endByte = selEnd < byteMap.CharCount
                        ? byteMap.CharToByteStart(selEnd) : totalBytes;
                    selectionRanges.Add((startByte, endByte));
                }
            }
        }

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

            var rowWidth = RowWidthForLayout(bytesPerRow);
            var sb = new System.Text.StringBuilder(rowWidth + 10);

            // Address
            sb.Append(rowByteStart.ToString("X8"));
            sb.Append("  ");

            // Hex bytes
            for (int i = 0; i < bytesPerRow; i++)
            {

                var byteIdx = rowByteStart + i;
                if (i < rowByteCount && pendingNibble.HasValue && byteIdx == cursorByteOffset)
                {
                    // Show pending nibble: e.g. "A_" instead of the current byte
                    sb.Append(char.ToUpper(pendingNibble.Value));
                    sb.Append('_');
                }
                else
                {
                    sb.Append(i < rowByteCount
                        ? docBytes[rowByteStart + i].ToString("X2")
                        : "  ");
                }

                if (i < bytesPerRow - 1)
                    sb.Append(' ');
            }

            sb.Append("  ");

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
                // Apply multi-byte highlights first (lowest priority)
                if (HighlightMultiByteChars)
                {
                    for (int i = 0; i < rowByteCount; i++)
                    {
                        var byteIdx = rowByteStart + i;
                        if (byteIdx < byteMap.TotalBytes)
                        {
                            var (charIdx, _) = byteMap.ByteToChar(byteIdx);
                            if (byteMap.CharByteLength(charIdx) > 1)
                            {
                                var hexCol = GetHexColumnForByte(i, bytesPerRow);
                                var asciiCol = GetAsciiColumnForByte(i, bytesPerRow);
                                SetCellRange(cellColors, hexCol, 2, CellColorType.MultiByte, line.Length);
                                if (asciiCol < line.Length)
                                    cellColors[asciiCol] = CellColorType.MultiByte;
                            }
                        }
                    }
                }

                // Apply selection and cursor (higher priority, overwrites multi-byte)
                for (int i = 0; i < rowByteCount; i++)
                {
                    var byteIdx = rowByteStart + i;
                    var hexCol = GetHexColumnForByte(i, bytesPerRow);
                    var asciiCol = GetAsciiColumnForByte(i, bytesPerRow);

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

            var multiByteBg = HighlightMultiByteChars
                ? theme.Get(EditorTheme.MultiByteBackgroundColor)
                : bg;

            RenderColoredLine(context, screenX, screenY, line, fg, bg, cursorFg, cursorBg, selFg, selBg, multiByteBg, cellColors);
        }
    }

    /// <inheritdoc />
    public DocumentOffset? HitTest(int localX, int localY, EditorState state, int viewportColumns, int viewportLines, int scrollOffset, int horizontalScrollOffset)
    {
        if (localX < 0 || localY < 0 || localX >= viewportColumns || localY >= viewportLines)
            return null;

        var bytesPerRow = CalculateLayout(viewportColumns);
        var doc = state.Document;
        var totalBytes = doc.ByteCount;
        var row = (scrollOffset - 1) + localY;
        var rowByteStart = row * bytesPerRow;

        if (rowByteStart >= totalBytes)
            return new DocumentOffset(doc.Length);

        var byteIndex = GetByteIndexFromColumn(localX, bytesPerRow);
        if (byteIndex < 0) byteIndex = 0;

        var targetByte = Math.Min(rowByteStart + byteIndex, totalBytes);

        if (targetByte >= totalBytes)
            return new DocumentOffset(doc.Length);

        // Use Utf8ByteMap built from actual bytes for correct byte→char mapping
        var map = new Utf8ByteMap(doc.GetBytes().Span);
        var (charIndex, _) = map.ByteToChar(targetByte);

        return new DocumentOffset(Math.Min(charIndex, doc.Length));
    }

    /// <inheritdoc />
    public int GetTotalLines(IHex1bDocument document, int viewportColumns)
    {
        var bytesPerRow = CalculateLayout(viewportColumns);
        var byteCount = document.ByteCount;
        return Math.Max(1, (byteCount + bytesPerRow - 1) / bytesPerRow);
    }

    /// <inheritdoc />
    public int GetMaxLineWidth(IHex1bDocument document, int scrollOffset, int viewportLines, int viewportColumns)
    {
        // Responsive: row width always fits within viewport, so no horizontal scrollbar needed
        var bytesPerRow = CalculateLayout(viewportColumns);
        return RowWidthForLayout(bytesPerRow);
    }

    // ── Column mapping helpers ─────────────────────────────────

    private static int GetHexColumnForByte(int byteInRow, int bytesPerRow)
    {
        // After "XXXXXXXX  " (10 chars), hex bytes start
        var hexStart = AddressWidth + SeparatorWidth;
        return hexStart + byteInRow * 3;
    }

    private static int GetAsciiColumnForByte(int byteInRow, int bytesPerRow)
    {
        var hexWidth = bytesPerRow * 3 - 1;
        var asciiStart = AddressWidth + SeparatorWidth + hexWidth + SeparatorWidth;
        return asciiStart + byteInRow;
    }

    private static int GetByteIndexFromColumn(int column, int bytesPerRow)
    {
        var hexStart = AddressWidth + SeparatorWidth;
        var hexWidth = bytesPerRow * 3 - 1;
        var asciiStart = hexStart + hexWidth + SeparatorWidth;

        // In ASCII region
        if (column >= asciiStart && column < asciiStart + bytesPerRow)
            return column - asciiStart;

        // In hex region
        if (column >= hexStart && column < hexStart + hexWidth)
            return Math.Min((column - hexStart) / 3, bytesPerRow - 1);

        return 0;
    }

    // ── Rendering helpers ──────────────────────────────────────

    private enum CellColorType : byte
    {
        Normal = 0,
        Selected = 1,
        Cursor = 2,
        MultiByte = 3
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
        Hex1bColor multiByteBg,
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
                    case CellColorType.MultiByte:
                        sb.Append(fg.ToForegroundAnsi());
                        sb.Append(multiByteBg.ToBackgroundAnsi());
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
