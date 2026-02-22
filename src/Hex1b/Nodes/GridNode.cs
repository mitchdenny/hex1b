using System.Buffers;
using Hex1b.Layout;
using Hex1b.Nodes;
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

    /// <inheritdoc />
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

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

        var colWidths = ResolveAxis(
            ColumnCount, EffectiveColumnHints, constraints.MaxWidth,
            (entry, col) => entry.Column == col && entry.ColumnSpan == 1,
            (size) => size.Width,
            (entry, maxW) => entry.Node.Measure(new Constraints(0, maxW, 0, int.MaxValue)));

        var rowHeights = ResolveAxis(
            RowCount, EffectiveRowHints, constraints.MaxHeight,
            (entry, row) => entry.Row == row && entry.RowSpan == 1,
            (size) => size.Height,
            (entry, maxH) => entry.Node.Measure(new Constraints(0, int.MaxValue, 0, maxH)));

        var totalWidth = Sum(colWidths, ColumnCount);
        var totalHeight = Sum(rowHeights, RowCount);

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
            DistributeSpace(ColumnCount, EffectiveColumnHints, bounds.Width, colWidths,
                (entry, col) => entry.Column == col && entry.ColumnSpan == 1,
                (size) => size.Width,
                (entry, maxW) => entry.Node.Measure(new Constraints(0, maxW, 0, int.MaxValue)));

            DistributeSpace(RowCount, EffectiveRowHints, bounds.Height, rowHeights,
                (entry, idx) => entry.Row == idx && entry.RowSpan == 1,
                (size) => size.Height,
                (entry, maxH) => entry.Node.Measure(new Constraints(0, int.MaxValue, 0, maxH)));

            // Build cumulative offsets
            var colOffsets = ArrayPool<int>.Shared.Rent(ColumnCount + 1);
            var rowOffsets = ArrayPool<int>.Shared.Rent(RowCount + 1);

            try
            {
                colOffsets[0] = bounds.X;
                for (int i = 0; i < ColumnCount; i++)
                    colOffsets[i + 1] = colOffsets[i] + colWidths[i];

                rowOffsets[0] = bounds.Y;
                for (int i = 0; i < RowCount; i++)
                    rowOffsets[i + 1] = rowOffsets[i] + rowHeights[i];

                // Arrange each cell
                foreach (var entry in CellEntries)
                {
                    var x = colOffsets[entry.Column];
                    var y = rowOffsets[entry.Row];
                    var endCol = Math.Min(entry.Column + entry.ColumnSpan, ColumnCount);
                    var endRow = Math.Min(entry.Row + entry.RowSpan, RowCount);
                    var w = colOffsets[endCol] - x;
                    var h = rowOffsets[endRow] - y;

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

        foreach (var entry in CellEntries)
        {
            context.RenderChild(entry.Node);
        }

        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
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
