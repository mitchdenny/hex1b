using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A layout widget that arranges children in a two-dimensional grid with support for
/// row and column spanning, explicit column/row sizing, and automatic grid dimension inference.
/// </summary>
/// <param name="Cells">The cell descriptors defining the grid content and placement.</param>
/// <param name="ColumnDefinitions">Explicit column definitions. Auto-created columns default to Content sizing.</param>
/// <param name="RowDefinitions">Explicit row definitions. Auto-created rows default to Content sizing.</param>
public sealed record GridWidget(
    IReadOnlyList<GridCellWidget> Cells,
    IReadOnlyList<GridColumnDefinition> ColumnDefinitions,
    IReadOnlyList<GridRowDefinition> RowDefinitions) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as GridNode ?? new GridNode();

        // Compute grid dimensions
        var colCount = ColumnDefinitions.Count;
        var rowCount = RowDefinitions.Count;

        foreach (var cell in Cells)
        {
            colCount = Math.Max(colCount, cell.ColumnIndex + cell.ColumnSpanCount);
            rowCount = Math.Max(rowCount, cell.RowIndex + cell.RowSpanCount);
        }

        // Build effective column definitions (explicit + auto-created, with cell overrides)
        var effectiveColumns = new SizeHint[colCount];
        for (int i = 0; i < colCount; i++)
        {
            effectiveColumns[i] = i < ColumnDefinitions.Count
                ? ColumnDefinitions[i].Width
                : SizeHint.Content;
        }

        var effectiveRows = new SizeHint[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            effectiveRows[i] = i < RowDefinitions.Count
                ? RowDefinitions[i].Height
                : SizeHint.Content;
        }

        // Apply cell-level hints (cell overrides column/row definition)
        foreach (var cell in Cells)
        {
            if (cell.CellWidthHint.HasValue && cell.ColumnSpanCount == 1)
            {
                effectiveColumns[cell.ColumnIndex] = cell.CellWidthHint.Value;
            }

            if (cell.CellHeightHint.HasValue && cell.RowSpanCount == 1)
            {
                effectiveRows[cell.RowIndex] = cell.CellHeightHint.Value;
            }
        }

        // Track removed cells for orphaned bounds
        for (int i = Cells.Count; i < node.CellEntries.Count; i++)
        {
            var removed = node.CellEntries[i];
            if (removed.Node.Bounds.Width > 0 && removed.Node.Bounds.Height > 0)
            {
                node.AddOrphanedChildBounds(removed.Node.Bounds);
            }
        }

        // Reconcile each cell's child widget
        var newEntries = new List<GridNode.CellEntry>();
        for (int i = 0; i < Cells.Count; i++)
        {
            var cell = Cells[i];
            var existingChild = i < node.CellEntries.Count ? node.CellEntries[i].Node : null;
            var reconciledChild = await context.ReconcileChildAsync(existingChild, cell.Child, node);
            if (reconciledChild != null)
            {
                newEntries.Add(new GridNode.CellEntry(
                    reconciledChild,
                    cell.RowIndex,
                    cell.ColumnIndex,
                    cell.RowSpanCount,
                    cell.ColumnSpanCount));
            }
        }

        node.CellEntries = newEntries;
        node.ColumnCount = colCount;
        node.RowCount = rowCount;
        node.EffectiveColumnHints = effectiveColumns;
        node.EffectiveRowHints = effectiveRows;

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(GridNode);
}
