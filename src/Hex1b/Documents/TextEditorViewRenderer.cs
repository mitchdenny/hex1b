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
    public void Render(Hex1bRenderContext context, EditorState state, Rect viewport, int scrollOffset, int horizontalScrollOffset, bool isFocused, char? pendingNibble = null, IReadOnlyList<ITextDecorationProvider>? decorationProviders = null, IReadOnlyList<InlineHint>? inlineHints = null, bool wordWrap = false)
    {
        if (wordWrap)
        {
            RenderWrapped(context, state, viewport, scrollOffset, isFocused, decorationProviders, inlineHints);
            return;
        }

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
                try
                {
                    var pos = doc.OffsetToPosition(cursor.Position);
                    cursorPositions.Add((pos.Line, pos.Column));
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Cursor offset stale due to concurrent mutation — skip
                }

                if (cursor.HasSelection)
                {
                    selectionRanges.Add((cursor.SelectionStart.Value, cursor.SelectionEnd.Value));
                }
            }
        }

        // Collect decorations from all providers for the visible range
        List<TextDecorationSpan>? allDecorations = null;
        if (decorationProviders is { Count: > 0 })
        {
            var startLine = scrollOffset;
            var endLine = Math.Min(scrollOffset + viewportLines - 1, doc.LineCount);
            allDecorations = [];
            foreach (var provider in decorationProviders)
            {
                var spans = provider.GetDecorations(startLine, endLine, doc);
                if (spans.Count > 0)
                    allDecorations.AddRange(spans);
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
                RenderLine(context, screenX, screenY, emptyLine, fg, bg, cursorFg, cursorBg, selFg, selBg, null, null);
                continue;
            }

            // Snapshot line count — document may be mutated concurrently
            var lineCount = doc.LineCount;
            if (docLine > lineCount)
            {
                var emptyLine = "~".PadRight(viewportColumns);
                RenderLine(context, screenX, screenY, emptyLine, fg, bg, cursorFg, cursorBg, selFg, selBg, null, null);
                continue;
            }

            string lineText;
            try
            {
                lineText = doc.GetLineText(docLine);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Document was mutated concurrently — treat as empty line
                var emptyLine = "~".PadRight(viewportColumns);
                RenderLine(context, screenX, screenY, emptyLine, fg, bg, cursorFg, cursorBg, selFg, selBg, null, null);
                continue;
            }

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
            int lineStartOffset;
            try
            {
                lineStartOffset = doc.PositionToOffset(new DocumentPosition(docLine, 1)).Value;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Document mutated concurrently — render what we have without cursor/selection highlighting
                RenderLine(context, screenX, screenY, displayText, fg, bg, cursorFg, cursorBg, selFg, selBg, null, null);
                continue;
            }
            var lineEndOffset = lineStartOffset + lineText.Length;
            var cellTypes = BuildCellTypes(displayText, docLine, lineStartOffset, lineEndOffset,
                cursorPositions, selectionRanges, horizontalScrollOffset);

            // Build per-column decoration map for this line
            var lineDecorations = BuildLineDecorations(allDecorations, docLine, horizontalScrollOffset, displayText.Length, theme);

            // Expand line with inline hints if present
            if (inlineHints is { Count: > 0 })
            {
                var lineHints = CollectLineHints(inlineHints, docLine, horizontalScrollOffset, displayText.Length);
                if (lineHints is { Count: > 0 })
                {
                    (displayText, cellTypes, lineDecorations) = ExpandLineWithInlineHints(
                        displayText, cellTypes, lineDecorations, lineHints, viewportColumns, theme);
                }
            }

            RenderLine(context, screenX, screenY, displayText, fg, bg, cursorFg, cursorBg, selFg, selBg, cellTypes, lineDecorations);
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

    // ── Soft line wrapping ────────────────────────────────────

    /// <summary>
    /// Represents a single display row produced by wrapping a document line.
    /// </summary>
    internal readonly record struct WrappedLine(int DocLine, string Text, int StartColumn, bool IsContinuation);

    /// <summary>
    /// Wraps a single line of text into multiple display lines based on viewport width.
    /// Returns the wrapped segments.
    /// </summary>
    internal static List<string> WrapLine(string line, int viewportWidth)
    {
        if (viewportWidth <= 0 || line.Length <= viewportWidth)
            return [line];

        var segments = new List<string>();
        var remaining = line;
        while (remaining.Length > viewportWidth)
        {
            // Try to break at a word boundary
            var breakPoint = remaining.LastIndexOf(' ', viewportWidth - 1);
            if (breakPoint <= 0)
                breakPoint = viewportWidth; // Hard break if no word boundary

            segments.Add(remaining[..breakPoint]);
            remaining = remaining[breakPoint..].TrimStart();
        }
        if (remaining.Length > 0)
            segments.Add(remaining);

        return segments;
    }

    /// <summary>
    /// Builds a flat list of wrapped display lines for all document lines starting
    /// from <paramref name="firstDocLine"/> until enough display rows are produced
    /// to fill the viewport (or the document is exhausted).
    /// </summary>
    private static List<WrappedLine> BuildWrappedLines(IHex1bDocument doc, int firstDocLine, int viewportWidth, int viewportLines)
    {
        var result = new List<WrappedLine>();
        var docLine = firstDocLine;
        while (result.Count < viewportLines && docLine <= doc.LineCount)
        {
            string lineText;
            try
            {
                lineText = doc.GetLineText(docLine);
            }
            catch (ArgumentOutOfRangeException)
            {
                break;
            }

            var segments = WrapLine(lineText, viewportWidth);
            var startCol = 0;
            for (var i = 0; i < segments.Count && result.Count < viewportLines; i++)
            {
                result.Add(new WrappedLine(docLine, segments[i], startCol, i > 0));
                startCol += segments[i].Length;
                // Account for trimmed leading spaces on continuation segments
                if (i > 0)
                {
                    var originalOffset = lineText.IndexOf(segments[i], startCol - segments[i].Length, StringComparison.Ordinal);
                    if (originalOffset >= 0)
                        startCol = originalOffset + segments[i].Length;
                }
            }
            docLine++;
        }
        return result;
    }

    /// <summary>
    /// Renders document content with soft line wrapping enabled.
    /// Horizontal scrolling is disabled; long lines wrap at viewport width.
    /// </summary>
    private void RenderWrapped(Hex1bRenderContext context, EditorState state, Rect viewport, int scrollOffset, bool isFocused, IReadOnlyList<ITextDecorationProvider>? decorationProviders, IReadOnlyList<InlineHint>? inlineHints)
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

        // Collect cursor positions and selection ranges
        var cursorPositions = new HashSet<(int Line, int Column)>();
        var selectionRanges = new List<(int Start, int End)>();

        if (isFocused)
        {
            foreach (var cursor in state.Cursors)
            {
                try
                {
                    var pos = doc.OffsetToPosition(cursor.Position);
                    cursorPositions.Add((pos.Line, pos.Column));
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Cursor offset stale — skip
                }

                if (cursor.HasSelection)
                    selectionRanges.Add((cursor.SelectionStart.Value, cursor.SelectionEnd.Value));
            }
        }

        // Build wrapped display lines
        var wrappedLines = BuildWrappedLines(doc, scrollOffset, viewportColumns, viewportLines);

        // Collect decorations for the visible document line range
        List<TextDecorationSpan>? allDecorations = null;
        if (decorationProviders is { Count: > 0 } && wrappedLines.Count > 0)
        {
            var startLine = wrappedLines[0].DocLine;
            var endLine = wrappedLines[^1].DocLine;
            allDecorations = [];
            foreach (var provider in decorationProviders)
            {
                var spans = provider.GetDecorations(startLine, endLine, doc);
                if (spans.Count > 0)
                    allDecorations.AddRange(spans);
            }
        }

        for (var viewLine = 0; viewLine < viewportLines; viewLine++)
        {
            var screenY = viewport.Y + viewLine;
            var screenX = viewport.X;

            if (viewLine >= wrappedLines.Count)
            {
                // Past end of document — render tilde placeholder
                var emptyLine = "~".PadRight(viewportColumns);
                RenderLine(context, screenX, screenY, emptyLine, fg, bg, cursorFg, cursorBg, selFg, selBg, null, null);
                continue;
            }

            var wrapped = wrappedLines[viewLine];
            var docLine = wrapped.DocLine;
            var startColumn = wrapped.StartColumn;

            // Fit display text to viewport width
            var displayText = FitToDisplayWidth(wrapped.Text, viewportColumns);

            // Build cell types for cursor/selection highlighting
            int lineStartOffset;
            try
            {
                lineStartOffset = doc.PositionToOffset(new DocumentPosition(docLine, 1)).Value;
            }
            catch (ArgumentOutOfRangeException)
            {
                RenderLine(context, screenX, screenY, displayText, fg, bg, cursorFg, cursorBg, selFg, selBg, null, null);
                continue;
            }

            string fullLineText;
            try
            {
                fullLineText = doc.GetLineText(docLine);
            }
            catch (ArgumentOutOfRangeException)
            {
                RenderLine(context, screenX, screenY, displayText, fg, bg, cursorFg, cursorBg, selFg, selBg, null, null);
                continue;
            }

            var lineEndOffset = lineStartOffset + fullLineText.Length;
            // For wrapped segments, offset the horizontal scroll to the segment's start column
            var cellTypes = BuildCellTypes(displayText, docLine, lineStartOffset, lineEndOffset,
                cursorPositions, selectionRanges, startColumn);

            // Build decorations for this display line
            var lineDecorations = BuildLineDecorations(allDecorations, docLine, startColumn, displayText.Length, theme);

            RenderLine(context, screenX, screenY, displayText, fg, bg, cursorFg, cursorBg, selFg, selBg, cellTypes, lineDecorations);
        }
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

    /// <summary>
    /// Resolved per-character decoration for a single cell.
    /// </summary>
    private readonly record struct ResolvedDecoration(
        Hex1bColor Foreground,
        Hex1bColor Background,
        bool Bold,
        bool Italic,
        UnderlineStyle UnderlineStyle,
        Hex1bColor UnderlineColor)
    {
        public bool HasForeground => !Foreground.IsDefault;
        public bool HasBackground => !Background.IsDefault;
        public bool HasUnderline => UnderlineStyle != UnderlineStyle.None;
        public bool HasUnderlineColor => !UnderlineColor.IsDefault;
        public bool HasAnyDecoration => HasForeground || HasBackground || Bold || Italic || HasUnderline;
    }

    /// <summary>
    /// Builds a per-column resolved decoration array for a single line by merging
    /// all applicable decoration spans. Higher-priority decorations win per attribute.
    /// </summary>
    private static ResolvedDecoration[]? BuildLineDecorations(
        List<TextDecorationSpan>? allDecorations,
        int docLine,
        int horizontalScrollOffset,
        int displayWidth,
        Hex1bTheme theme)
    {
        if (allDecorations is null or { Count: 0 })
            return null;

        // Collect spans that touch this line
        List<TextDecorationSpan>? lineSpans = null;
        foreach (var span in allDecorations)
        {
            if (span.Start.Line <= docLine && span.End.Line >= docLine)
            {
                lineSpans ??= [];
                lineSpans.Add(span);
            }
        }

        if (lineSpans is null)
            return null;

        var result = new ResolvedDecoration[displayWidth];
        // Initialize with Default colors so HasForeground/HasBackground return false
        // for unset cells (default(Hex1bColor) is RGB(0,0,0) with IsDefault=false,
        // which would incorrectly appear as "has black foreground/background").
        var empty = new ResolvedDecoration(Hex1bColor.Default, Hex1bColor.Default, false, false, UnderlineStyle.None, Hex1bColor.Default);
        Array.Fill(result, empty);
        // Track priority per attribute per column
        var fgPriority = new int[displayWidth];
        var bgPriority = new int[displayWidth];
        var boldPriority = new int[displayWidth];
        var italicPriority = new int[displayWidth];
        var underlinePriority = new int[displayWidth];
        // Initialize priorities to int.MinValue so any decoration wins
        Array.Fill(fgPriority, int.MinValue);
        Array.Fill(bgPriority, int.MinValue);
        Array.Fill(boldPriority, int.MinValue);
        Array.Fill(italicPriority, int.MinValue);
        Array.Fill(underlinePriority, int.MinValue);

        foreach (var span in lineSpans)
        {
            // Determine the column range this span covers on this line
            var startCol = span.Start.Line == docLine
                ? span.Start.Column - 1 - horizontalScrollOffset
                : 0;
            var endCol = span.End.Line == docLine
                ? span.End.Column - 1 - horizontalScrollOffset
                : displayWidth;

            startCol = Math.Max(0, startCol);
            endCol = Math.Min(displayWidth, endCol);

            if (startCol >= endCol)
                continue;

            var dec = span.Decoration;
            var resolvedFg = dec.ResolveForeground(theme) ?? Hex1bColor.Default;
            var resolvedBg = dec.ResolveBackground(theme) ?? Hex1bColor.Default;

            for (var col = startCol; col < endCol; col++)
            {
                if (!resolvedFg.IsDefault && span.Priority >= fgPriority[col])
                {
                    result[col] = result[col] with { Foreground = resolvedFg };
                    fgPriority[col] = span.Priority;
                }

                if (!resolvedBg.IsDefault && span.Priority >= bgPriority[col])
                {
                    result[col] = result[col] with { Background = resolvedBg };
                    bgPriority[col] = span.Priority;
                }

                if (dec.Bold is true && span.Priority >= boldPriority[col])
                {
                    result[col] = result[col] with { Bold = true };
                    boldPriority[col] = span.Priority;
                }

                if (dec.Italic is true && span.Priority >= italicPriority[col])
                {
                    result[col] = result[col] with { Italic = true };
                    italicPriority[col] = span.Priority;
                }

                if (dec.UnderlineStyle is not null and not UnderlineStyle.None && span.Priority >= underlinePriority[col])
                {
                    var ulColor = dec.ResolveUnderlineColor(theme) ?? Hex1bColor.Default;
                    result[col] = result[col] with
                    {
                        UnderlineStyle = dec.UnderlineStyle.Value,
                        UnderlineColor = ulColor
                    };
                    underlinePriority[col] = span.Priority;
                }
            }
        }

        return result;
    }

    // ── Inline hint expansion ──────────────────────────────────

    /// <summary>
    /// Collects inline hints that are visible on a given document line,
    /// returning their display column positions (0-based, scroll-adjusted).
    /// </summary>
    private static List<(int DisplayCol, InlineHint Hint)>? CollectLineHints(
        IReadOnlyList<InlineHint> inlineHints, int docLine, int horizontalScrollOffset, int displayWidth)
    {
        List<(int DisplayCol, InlineHint Hint)>? result = null;
        foreach (var hint in inlineHints)
        {
            if (hint.Position.Line != docLine)
                continue;

            var displayCol = hint.Position.Column - 1 - horizontalScrollOffset;
            if (displayCol < 0)
                continue;

            result ??= [];
            result.Add((displayCol, hint));
        }

        result?.Sort((a, b) => a.DisplayCol.CompareTo(b.DisplayCol));
        return result;
    }

    /// <summary>
    /// Expands displayText, cellTypes, and lineDecorations to include inline hint text.
    /// Hints are inserted before the character at their display column position.
    /// The result is truncated/padded back to viewportColumns display width.
    /// </summary>
    private static (string DisplayText, CellType[]? CellTypes, ResolvedDecoration[]? Decorations) ExpandLineWithInlineHints(
        string displayText,
        CellType[]? cellTypes,
        ResolvedDecoration[]? lineDecorations,
        List<(int DisplayCol, InlineHint Hint)> hints,
        int viewportColumns,
        Hex1bTheme theme)
    {
        var totalHintChars = 0;
        foreach (var (_, hint) in hints)
            totalHintChars += hint.Text.Length;

        var capacity = displayText.Length + totalHintChars;
        var textBuilder = new System.Text.StringBuilder(capacity);
        var typesList = new List<CellType>(capacity);
        var decsList = new List<ResolvedDecoration>(capacity);

        var emptyDec = new ResolvedDecoration(
            Hex1bColor.Default, Hex1bColor.Default, false, false, UnderlineStyle.None, Hex1bColor.Default);

        int prevPos = 0;
        foreach (var (displayCol, hint) in hints)
        {
            var insertPos = Math.Max(0, Math.Min(displayCol, displayText.Length));

            // Append original characters from prevPos to insertPos
            for (var i = prevPos; i < insertPos; i++)
            {
                textBuilder.Append(displayText[i]);
                typesList.Add(cellTypes != null && i < cellTypes.Length ? cellTypes[i] : CellType.Normal);
                decsList.Add(lineDecorations != null && i < lineDecorations.Length ? lineDecorations[i] : emptyDec);
            }

            // Resolve hint styling: decoration overrides > theme defaults
            var hintFg = Hex1bColor.Default;
            var hintBg = Hex1bColor.Default;
            var hintBold = false;
            var hintItalic = false;

            if (hint.Decoration != null)
            {
                hintFg = hint.Decoration.ResolveForeground(theme) ?? Hex1bColor.Default;
                hintBg = hint.Decoration.ResolveBackground(theme) ?? Hex1bColor.Default;
                hintBold = hint.Decoration.Bold ?? false;
                hintItalic = hint.Decoration.Italic ?? false;
            }

            if (hintFg.IsDefault) hintFg = theme.Get(InlineHintTheme.ForegroundColor);
            if (hintBg.IsDefault) hintBg = theme.Get(InlineHintTheme.BackgroundColor);
            if (hint.Decoration?.Bold == null) hintBold = theme.Get(InlineHintTheme.IsBold);
            if (hint.Decoration?.Italic == null) hintItalic = theme.Get(InlineHintTheme.IsItalic);

            var hintDec = new ResolvedDecoration(hintFg, hintBg, hintBold, hintItalic, UnderlineStyle.None, Hex1bColor.Default);

            // Append hint characters
            foreach (var ch in hint.Text)
            {
                textBuilder.Append(ch);
                typesList.Add(CellType.Normal);
                decsList.Add(hintDec);
            }

            prevPos = insertPos;
        }

        // Append remaining original characters
        for (var i = prevPos; i < displayText.Length; i++)
        {
            textBuilder.Append(displayText[i]);
            typesList.Add(cellTypes != null && i < cellTypes.Length ? cellTypes[i] : CellType.Normal);
            decsList.Add(lineDecorations != null && i < lineDecorations.Length ? lineDecorations[i] : emptyDec);
        }

        // Re-fit to viewport width (hint insertion may have pushed content past viewport)
        var expandedText = FitToDisplayWidth(textBuilder.ToString(), viewportColumns);

        var finalTypes = typesList.ToArray();
        var finalDecs = decsList.ToArray();

        // Resize arrays to match final text length
        if (finalTypes.Length != expandedText.Length)
            Array.Resize(ref finalTypes, expandedText.Length);
        if (finalDecs.Length != expandedText.Length)
            Array.Resize(ref finalDecs, expandedText.Length);

        return (expandedText, finalTypes, finalDecs);
    }

    private static void RenderLine(
        Hex1bRenderContext context,
        int x, int y,
        string text,
        Hex1bColor fg, Hex1bColor bg,
        Hex1bColor cursorFg, Hex1bColor cursorBg,
        Hex1bColor selFg, Hex1bColor selBg,
        CellType[]? cellTypes,
        ResolvedDecoration[]? lineDecorations)
    {
        // When decorations are present without cellTypes, create a dummy cellTypes array
        if (cellTypes == null && lineDecorations != null)
        {
            cellTypes = new CellType[text.Length]; // All Normal
        }

        string output;

        if (cellTypes != null)
        {
            var globalColors = context.Theme.GetGlobalColorCodes();
            var resetToGlobal = context.Theme.GetResetToGlobalCodes();
            var sb = new System.Text.StringBuilder(text.Length * 2);
            sb.Append(globalColors);

            var prevType = CellType.Normal;
            ResolvedDecoration prevDec = default;
            var hasActiveDec = false;
            sb.Append(fg.ToForegroundAnsi());
            sb.Append(bg.ToBackgroundAnsi());

            for (var i = 0; i < text.Length; i++)
            {
                var cellType = i < cellTypes.Length ? cellTypes[i] : CellType.Normal;

                // For surrogate pairs, use the cell type of the first char for both
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    // Reset decorations before type transition on surrogate pairs
                    if (cellType != prevType && hasActiveDec)
                    {
                        ResetDecoration(sb, prevDec, context.Capabilities);
                        hasActiveDec = false;
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

                    // Write surrogate pair as a unit
                    sb.Append(text[i]);
                    sb.Append(text[i + 1]);
                    i++; // skip low surrogate
                    continue;
                }

                // Reset decorations before type transition
                if (cellType != prevType && hasActiveDec)
                {
                    ResetDecoration(sb, prevDec, context.Capabilities);
                    hasActiveDec = false;
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

                // Apply decoration for Normal cells
                if (lineDecorations != null && cellType == CellType.Normal && i < lineDecorations.Length)
                {
                    var dec = lineDecorations[i];

                    // Only re-emit ANSI if decoration changed from previous
                    if (!hasActiveDec || !dec.Equals(prevDec))
                    {
                        // Reset previous decoration if active
                        if (hasActiveDec)
                        {
                            ResetDecoration(sb, prevDec, context.Capabilities);
                            // Re-apply base colors after reset
                            sb.Append(fg.ToForegroundAnsi());
                            sb.Append(bg.ToBackgroundAnsi());
                        }

                        if (dec.HasAnyDecoration)
                        {
                            if (dec.HasForeground) sb.Append(dec.Foreground.ToForegroundAnsi());
                            if (dec.HasBackground) sb.Append(dec.Background.ToBackgroundAnsi());
                            if (dec.Bold) sb.Append("\x1b[1m");
                            if (dec.Italic) sb.Append("\x1b[3m");
                            if (dec.HasUnderline)
                            {
                                var caps = context.Capabilities;
                                var style = dec.UnderlineStyle;
                                if (!caps.SupportsStyledUnderlines && style is not UnderlineStyle.Single and not UnderlineStyle.None)
                                    style = UnderlineStyle.Single;

                                sb.Append(style switch
                                {
                                    UnderlineStyle.Single => "\x1b[4m",
                                    UnderlineStyle.Double => "\x1b[21m",
                                    UnderlineStyle.Curly => "\x1b[4:3m",
                                    UnderlineStyle.Dotted => "\x1b[4:4m",
                                    UnderlineStyle.Dashed => "\x1b[4:5m",
                                    _ => ""
                                });

                                if (dec.HasUnderlineColor && caps.SupportsUnderlineColor)
                                    sb.Append(dec.UnderlineColor.ToUnderlineColorAnsi());
                            }
                            hasActiveDec = true;
                            prevDec = dec;
                        }
                        else
                        {
                            hasActiveDec = false;
                        }
                    }
                }
                else if (hasActiveDec)
                {
                    // Moved out of Normal cell range while decoration was active
                    ResetDecoration(sb, prevDec, context.Capabilities);
                    sb.Append(fg.ToForegroundAnsi());
                    sb.Append(bg.ToBackgroundAnsi());
                    hasActiveDec = false;
                }

                sb.Append(text[i]);
            }

            // Clean up any trailing decoration state
            if (hasActiveDec)
            {
                ResetDecoration(sb, prevDec, context.Capabilities);
                sb.Append(fg.ToForegroundAnsi());
                sb.Append(bg.ToBackgroundAnsi());
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

        context.WriteClipped(x, y, output);
    }

    private static void EmitDecorationStart(System.Text.StringBuilder sb, ResolvedDecoration dec, Hex1bColor defaultFg, Hex1bColor defaultBg)
    {
        sb.Append(dec.HasForeground ? dec.Foreground.ToForegroundAnsi() : defaultFg.ToForegroundAnsi());
        sb.Append(dec.HasBackground ? dec.Background.ToBackgroundAnsi() : defaultBg.ToBackgroundAnsi());
        if (dec.Bold) sb.Append("\x1b[1m");
        if (dec.Italic) sb.Append("\x1b[3m");
    }

    /// <summary>
    /// Resets individual SGR attributes that were set by a decoration,
    /// without using a full SGR reset that would clobber the terminal's global state.
    /// </summary>
    private static void ResetDecoration(System.Text.StringBuilder sb, ResolvedDecoration dec, TerminalCapabilities caps)
    {
        if (dec.Bold) sb.Append("\x1b[22m");
        if (dec.Italic) sb.Append("\x1b[23m");
        if (dec.HasUnderline) sb.Append("\x1b[24m");
        if (dec.HasUnderlineColor && caps.SupportsUnderlineColor)
            sb.Append("\x1b[59m");
    }
}
