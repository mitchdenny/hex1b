using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Column definition for table layout, containing width hint and alignment.
/// </summary>
/// <param name="Width">The width hint for this column.</param>
/// <param name="Alignment">The horizontal alignment for cell content.</param>
internal record TableColumnDef(SizeHint Width, Alignment Alignment);

/// <summary>
/// Internal widget representing a single table row with borders and cells.
/// </summary>
internal sealed record TableRowWidget(
    IReadOnlyList<TableCell> Cells,
    IReadOnlyList<TableColumnDef> Columns,
    bool IsHighlighted = false,
    bool? IsSelected = null,
    bool ShowSelectionColumn = false,
    bool IsHeader = false,
    bool IsFooter = false,
    TableRenderMode RenderMode = TableRenderMode.Compact
) : Hex1bWidget
{
    // Border characters
    private const char Vertical = '│';

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TableRowNode ?? new TableRowNode();
        
        // Build the child widgets for this row
        var children = BuildRowChildren(context);
        
        // Reconcile children
        var childContext = context.WithLayoutAxis(LayoutAxis.Horizontal);
        var newChildren = new List<Hex1bNode>();
        
        for (int i = 0; i < children.Count; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var positionedContext = childContext.WithChildPosition(i, children.Count);
            var reconciledChild = await positionedContext.ReconcileChildAsync(existingChild, children[i], node);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
            }
        }
        
        node.Children = newChildren;
        node.IsHighlighted = IsHighlighted;
        node.IsSelected = IsSelected;
        node.HasCellPadding = RenderMode == TableRenderMode.Full;
        
        return node;
    }

    private List<Hex1bWidget> BuildRowChildren(ReconcileContext context)
    {
        var widgets = new List<Hex1bWidget>();
        
        // Left border
        widgets.Add(new TextBlockWidget(Vertical.ToString()));
        
        // Selection column (if enabled)
        if (ShowSelectionColumn)
        {
            // Footer rows get an empty cell, others get a checkbox
            string checkText;
            if (IsFooter)
            {
                checkText = "   "; // Empty space for footer
            }
            else
            {
                checkText = IsSelected == true ? "[x]" : "[ ]";
            }
            widgets.Add(new TextBlockWidget(checkText).FixedWidth(3));
            widgets.Add(new TextBlockWidget(Vertical.ToString()));
        }
        
        // Cell widgets with borders between them
        for (int i = 0; i < Cells.Count && i < Columns.Count; i++)
        {
            var cell = Cells[i];
            var column = Columns[i];
            
            // In Full mode, add left padding
            if (RenderMode == TableRenderMode.Full)
            {
                widgets.Add(new TextBlockWidget(" "));
            }
            
            // Build cell content widget
            Hex1bWidget cellWidget;
            if (cell.WidgetBuilder != null)
            {
                cellWidget = cell.WidgetBuilder(new TableCellContext());
            }
            else
            {
                cellWidget = new TextBlockWidget(cell.Text ?? "") { Overflow = TextOverflow.Ellipsis };
            }
            
            // Wrap in AlignWidget if not left-aligned
            if (column.Alignment != Alignment.Left && column.Alignment != Alignment.None)
            {
                cellWidget = new AlignWidget(cellWidget, column.Alignment);
            }
            
            // Apply width hint from column definition
            cellWidget = cellWidget.Width(column.Width);
            
            widgets.Add(cellWidget);
            
            // In Full mode, add right padding
            if (RenderMode == TableRenderMode.Full)
            {
                widgets.Add(new TextBlockWidget(" "));
            }
            
            // Separator between cells (not after last cell)
            if (i < Cells.Count - 1)
            {
                widgets.Add(new TextBlockWidget(Vertical.ToString()));
            }
        }
        
        // Right border
        widgets.Add(new TextBlockWidget(Vertical.ToString()));
        
        return widgets;
    }

    internal override Type GetExpectedNodeType() => typeof(TableRowNode);
}
