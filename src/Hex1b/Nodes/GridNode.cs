using System.Buffers;
using System.Text;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for <see cref="GridWidget"/>. Arranges children in a two-dimensional grid,
/// distributing space according to column and row size hints with support for spanning.
/// </summary>
public sealed class GridNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// A child node with its grid placement metadata.
    /// </summary>
    internal sealed record CellEntry(Hex1bNode Node, int Row, int Column, int RowSpan, int ColumnSpan);

    /// <summary>
    /// The cells in this grid, each with placement information.
    /// </summary>
    internal List<CellEntry> CellEntries { get; set; } = new();

    /// <summary>
    /// The number of columns in the grid.
    /// </summary>
    internal int ColumnCount { get; set; }

    /// <summary>
    /// The number of rows in the grid.
    /// </summary>
    internal int RowCount { get; set; }

    /// <summary>
    /// The resolved size hints for each column (Fixed, Content, or Fill).
    /// </summary>
    internal SizeHint[] EffectiveColumnHints { get; set; } = [];

    /// <summary>
    /// The resolved size hints for each row (Fixed, Content, or Fill).
    /// </summary>
    internal SizeHint[] EffectiveRowHints { get; set; } = [];

    /// <summary>
    /// Gridline rendering mode.
    /// </summary>
    internal GridLinesMode GridLines { get; set; } = GridLinesMode.None;

    /// <inheritdoc />
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    // Cached column widths and row heights from arrange for rendering gridlines
    private int[]? _arrangedColWidths;
    private int[]? _arrangedRowHeights;

    #region ILayoutProvider Implementation

    /// <inheritdoc />
    public Rect ClipRect => Bounds;

    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    /// <inheritdoc />
    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    /// <inheritdoc />
    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);

    #endregion

    /// <summary>Whether GridLines == All (has outer border and inner dividers).</summary>
    private bool HasOuterBorder => GridLines == GridLinesMode.All;

    /// <summary>Whether any gridlines are rendered.</summary>
    private bool HasGridLines => GridLines != GridLinesMode.None;

    /// <summary>
    /// Extra width consumed by gridlines: outer border (2) + inner vertical dividers (cols-1).
    /// </summary>
    private int GridLineWidthOverhead =>
        HasGridLines ? (HasOuterBorder ? 2 : 0) + Math.Max(0, ColumnCount - 1) : 0;

    /// <summary>
    /// Extra height consumed by gridlines.
    /// For All: outer border (2) + inner horizontal dividers (rows-1).
    /// For HeaderSeparator: 1 divider below row 0 (only if rows > 1).
    /// </summary>
    private int GridLineHeightOverhead
    {
        get
        {
            if (GridLines == GridLinesMode.All)
                return 2 + Math.Max(0, RowCount - 1);
            if (GridLines == GridLinesMode.HeaderSeparator && RowCount > 1)
                return 1;
            return 0;
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren() => CellEntries.Select(e => e.Node);

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        foreach (var entry in CellEntries)
        {
            foreach (var focusable in entry.Node.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (ColumnCount == 0 || RowCount == 0)
            return constraints.Constrain(Size.Zero);

        // Subtract gridline overhead from available space for content measurement
        var contentMaxWidth = Math.Max(0, constraints.MaxWidth - GridLineWidthOverhead);

        var colWidths = ResolveAxis(
            ColumnCount, EffectiveColumnHints, contentMaxWidth,
            (entry, col) => entry.Column == col && entry.ColumnSpan == 1,
            (size) => size.Width,
            (entry, maxW) => entry.Node.Measure(new Constraints(0, maxW, 0, int.MaxValue)));

        var contentMaxHeight = Math.Max(0, constraints.MaxHeight - GridLineHeightOverhead);

        var rowHeights = ResolveAxis(
            RowCount, EffectiveRowHints, contentMaxHeight,
            (entry, row) => entry.Row == row && entry.RowSpan == 1,
            (size) => size.Height,
            (entry, maxH) => entry.Node.Measure(new Constraints(0, int.MaxValue, 0, maxH)));

        var totalWidth = Sum(colWidths, ColumnCount) + GridLineWidthOverhead;
        var totalHeight = Sum(rowHeights, RowCount) + GridLineHeightOverhead;

        return constraints.Constrain(new Size(totalWidth, totalHeight));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        if (ColumnCount == 0 || RowCount == 0 || CellEntries.Count == 0)
            return;

        var colWidths = ArrayPool<int>.Shared.Rent(ColumnCount);
        var rowHeights = ArrayPool<int>.Shared.Rent(RowCount);

        try
        {
            var contentWidth = Math.Max(0, bounds.Width - GridLineWidthOverhead);
            var contentHeight = Math.Max(0, bounds.Height - GridLineHeightOverhead);

            DistributeSpace(ColumnCount, EffectiveColumnHints, contentWidth, colWidths,
                (entry, col) => entry.Column == col && entry.ColumnSpan == 1,
                (size) => size.Width,
                (entry, maxW) => entry.Node.Measure(new Constraints(0, maxW, 0, int.MaxValue)));

            DistributeSpace(RowCount, EffectiveRowHints, contentHeight, rowHeights,
                (entry, idx) => entry.Row == idx && entry.RowSpan == 1,
                (size) => size.Height,
                (entry, maxH) => entry.Node.Measure(new Constraints(0, int.MaxValue, 0, maxH)));

            // Cache for rendering
            _arrangedColWidths = new int[ColumnCount];
            _arrangedRowHeights = new int[RowCount];
            Array.Copy(colWidths, _arrangedColWidths, ColumnCount);
            Array.Copy(rowHeights, _arrangedRowHeights, RowCount);

            // Build cumulative offsets accounting for gridlines
            var colOffsets = ArrayPool<int>.Shared.Rent(ColumnCount + 1);
            var rowOffsets = ArrayPool<int>.Shared.Rent(RowCount + 1);

            try
            {
                // Starting X: offset by 1 if outer border
                colOffsets[0] = bounds.X + (HasOuterBorder ? 1 : 0);
                for (int i = 0; i < ColumnCount; i++)
                {
                    colOffsets[i + 1] = colOffsets[i] + colWidths[i];
                    // Add 1 for inner vertical divider (except after last column)
                    if (HasGridLines && i < ColumnCount - 1)
                        colOffsets[i + 1] += 1;
                }

                // Starting Y: offset by 1 if outer border
                rowOffsets[0] = bounds.Y + (HasOuterBorder ? 1 : 0);
                for (int i = 0; i < RowCount; i++)
                {
                    rowOffsets[i + 1] = rowOffsets[i] + rowHeights[i];
                    // Add 1 for inner horizontal divider
                    if (GridLines == GridLinesMode.All && i < RowCount - 1)
                        rowOffsets[i + 1] += 1;
                    else if (GridLines == GridLinesMode.HeaderSeparator && i == 0 && RowCount > 1)
                        rowOffsets[i + 1] += 1;
                }

                // Arrange each cell
                foreach (var entry in CellEntries)
                {
                    var x = colOffsets[entry.Column];
                    var y = rowOffsets[entry.Row];
                    var endCol = Math.Min(entry.Column + entry.ColumnSpan, ColumnCount);
                    var endRow = Math.Min(entry.Row + entry.RowSpan, RowCount);

                    // For spanning cells, width spans across dividers too
                    var w = colOffsets[endCol] - x;
                    if (HasGridLines && entry.ColumnSpan > 1 && endCol < ColumnCount)
                        w -= 1; // Don't include the trailing divider
                    else if (HasOuterBorder && endCol == ColumnCount)
                        { } // Don't subtract — already excluded by offsets

                    var h = rowOffsets[endRow] - y;
                    if (GridLines == GridLinesMode.All && entry.RowSpan > 1 && endRow < RowCount)
                        h -= 1;

                    entry.Node.Arrange(new Rect(x, y, Math.Max(0, w), Math.Max(0, h)));
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(colOffsets);
                ArrayPool<int>.Shared.Return(rowOffsets);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(colWidths);
            ArrayPool<int>.Shared.Return(rowHeights);
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;

        // Render gridlines before cell content
        if (HasGridLines && _arrangedColWidths != null && _arrangedRowHeights != null)
        {
            RenderGridLines(context);
        }

        foreach (var entry in CellEntries)
        {
            context.RenderChild(entry.Node);
        }

        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    private void RenderGridLines(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var h = theme.Get(GridTheme.Horizontal);
        var v = theme.Get(GridTheme.Vertical);
        var tl = theme.Get(GridTheme.TopLeft);
        var tr = theme.Get(GridTheme.TopRight);
        var bl = theme.Get(GridTheme.BottomLeft);
        var br = theme.Get(GridTheme.BottomRight);
        var td = theme.Get(GridTheme.TeeDown);
        var tu = theme.Get(GridTheme.TeeUp);
        var tRight = theme.Get(GridTheme.TeeRight);
        var tLeft = theme.Get(GridTheme.TeeLeft);
        var cross = theme.Get(GridTheme.Cross);
        var borderColor = theme.Get(GridTheme.BorderColor);

        var colorPrefix = !borderColor.IsDefault ? borderColor.ToForegroundAnsi() : "";
        var colorSuffix = colorPrefix.Length > 0 ? "\x1b[0m" : "";

        if (GridLines == GridLinesMode.All)
        {
            RenderAllGridLines(context, h, v, tl, tr, bl, br, td, tu, tRight, tLeft, cross, colorPrefix, colorSuffix);
        }
        else if (GridLines == GridLinesMode.HeaderSeparator && RowCount > 1)
        {
            RenderHeaderSeparator(context, h, v, colorPrefix, colorSuffix);
        }
    }

    private void RenderAllGridLines(
        Hex1bRenderContext context,
        char h, char v, char tl, char tr, char bl, char br,
        char td, char tu, char tRight, char tLeft, char cross,
        string colorPrefix, string colorSuffix)
    {
        var colWidths = _arrangedColWidths!;
        var rowHeights = _arrangedRowHeights!;
        var y = Bounds.Y;

        // Top border: ┌───┬───┐
        var sb = new StringBuilder();
        sb.Append(colorPrefix);
        sb.Append(tl);
        for (int col = 0; col < ColumnCount; col++)
        {
            sb.Append(h, colWidths[col]);
            sb.Append(col < ColumnCount - 1 ? td : tr);
        }
        sb.Append(colorSuffix);
        context.WriteClipped(Bounds.X, y, sb.ToString());
        y++;

        // For each row: render vertical dividers on each row-line, then horizontal separator
        for (int row = 0; row < RowCount; row++)
        {
            // Vertical dividers for each line of this row
            for (int line = 0; line < rowHeights[row]; line++)
            {
                // Left border
                context.WriteClipped(Bounds.X, y + line, $"{colorPrefix}{v}{colorSuffix}");

                // Inner vertical dividers and right border
                var x = Bounds.X + 1;
                for (int col = 0; col < ColumnCount; col++)
                {
                    x += colWidths[col];
                    var divChar = col < ColumnCount - 1 ? v : v; // all are │
                    context.WriteClipped(x, y + line, $"{colorPrefix}{divChar}{colorSuffix}");
                    x++; // past the divider
                }
            }

            y += rowHeights[row];

            // Horizontal separator (or bottom border)
            if (row < RowCount - 1)
            {
                // Inner separator: ├───┼───┤
                sb.Clear();
                sb.Append(colorPrefix);
                sb.Append(tRight);
                for (int col = 0; col < ColumnCount; col++)
                {
                    sb.Append(h, colWidths[col]);
                    sb.Append(col < ColumnCount - 1 ? cross : tLeft);
                }
                sb.Append(colorSuffix);
                context.WriteClipped(Bounds.X, y, sb.ToString());
                y++;
            }
        }

        // Bottom border: └───┴───┘
        sb.Clear();
        sb.Append(colorPrefix);
        sb.Append(bl);
        for (int col = 0; col < ColumnCount; col++)
        {
            sb.Append(h, colWidths[col]);
            sb.Append(col < ColumnCount - 1 ? tu : br);
        }
        sb.Append(colorSuffix);
        context.WriteClipped(Bounds.X, y, sb.ToString());
    }

    private void RenderHeaderSeparator(
        Hex1bRenderContext context,
        char h, char v,
        string colorPrefix, string colorSuffix)
    {
        var colWidths = _arrangedColWidths!;
        var rowHeights = _arrangedRowHeights!;

        // The separator goes after row 0
        var y = Bounds.Y + rowHeights[0];

        var sb = new StringBuilder();
        sb.Append(colorPrefix);
        for (int col = 0; col < ColumnCount; col++)
        {
            sb.Append(h, colWidths[col]);
            if (col < ColumnCount - 1)
                sb.Append(v);
        }
        sb.Append(colorSuffix);
        context.WriteClipped(Bounds.X, y, sb.ToString());
    }

    /// <summary>
    /// Resolves axis sizes for measurement (returns array that caller must not pool-return).
    /// </summary>
    private int[] ResolveAxis(
        int count,
        SizeHint[] hints,
        int available,
        Func<CellEntry, int, bool> isNonSpanningInSlot,
        Func<Size, int> selectDimension,
        Func<CellEntry, int, Size> measure)
    {
        var sizes = new int[count];
        var totalFixed = 0;
        var totalWeight = 0;

        for (int i = 0; i < count; i++)
        {
            var hint = i < hints.Length ? hints[i] : SizeHint.Content;

            if (hint.IsFixed)
            {
                sizes[i] = hint.FixedValue;
                totalFixed += hint.FixedValue;
            }
            else if (hint.IsContent)
            {
                var maxContent = 0;
                foreach (var entry in CellEntries)
                {
                    if (isNonSpanningInSlot(entry, i))
                    {
                        var measured = measure(entry, int.MaxValue);
                        maxContent = Math.Max(maxContent, selectDimension(measured));
                    }
                }

                sizes[i] = maxContent;
                totalFixed += maxContent;
            }
            else if (hint.IsFill)
            {
                totalWeight += hint.FillWeight;
            }
        }

        // Distribute remaining to fill slots
        var remaining = Math.Max(0, available - totalFixed);
        if (totalWeight > 0)
        {
            for (int i = 0; i < count; i++)
            {
                var hint = i < hints.Length ? hints[i] : SizeHint.Content;
                if (hint.IsFill)
                {
                    sizes[i] = remaining * hint.FillWeight / totalWeight;
                }
            }
        }

        return sizes;
    }

    /// <summary>
    /// Distributes available space into a rented array of sizes.
    /// </summary>
    private void DistributeSpace(
        int count,
        SizeHint[] hints,
        int available,
        int[] sizes,
        Func<CellEntry, int, bool> isNonSpanningInSlot,
        Func<Size, int> selectDimension,
        Func<CellEntry, int, Size> measure)
    {
        var totalFixed = 0;
        var totalWeight = 0;

        for (int i = 0; i < count; i++)
        {
            sizes[i] = 0;
            var hint = i < hints.Length ? hints[i] : SizeHint.Content;

            if (hint.IsFixed)
            {
                sizes[i] = hint.FixedValue;
                totalFixed += hint.FixedValue;
            }
            else if (hint.IsContent)
            {
                var maxContent = 0;
                foreach (var entry in CellEntries)
                {
                    if (isNonSpanningInSlot(entry, i))
                    {
                        var measured = measure(entry, available);
                        maxContent = Math.Max(maxContent, selectDimension(measured));
                    }
                }

                sizes[i] = maxContent;
                totalFixed += maxContent;
            }
            else if (hint.IsFill)
            {
                totalWeight += hint.FillWeight;
            }
        }

        var remaining = Math.Max(0, available - totalFixed);
        if (totalWeight > 0)
        {
            for (int i = 0; i < count; i++)
            {
                var hint = i < hints.Length ? hints[i] : SizeHint.Content;
                if (hint.IsFill)
                {
                    sizes[i] = remaining * hint.FillWeight / totalWeight;
                }
            }
        }
    }

    private static int Sum(int[] values, int count)
    {
        var total = 0;
        for (int i = 0; i < count; i++)
            total += values[i];
        return total;
    }
}
