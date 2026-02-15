using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for a single table row. Handles horizontal layout of cells with optional highlighting.
/// </summary>
internal sealed class TableRowNode : Hex1bNode
{
    /// <summary>
    /// Child nodes (border chars, selection column, cells).
    /// </summary>
    public List<Hex1bNode> Children { get; set; } = [];
    
    /// <summary>
    /// Whether this row is highlighted (focused).
    /// </summary>
    public bool IsHighlighted { get; set; }
    
    /// <summary>
    /// Whether this row is selected. Null for non-data rows (header/footer).
    /// </summary>
    public bool? IsSelected { get; set; }
    
    /// <summary>
    /// Pre-calculated column widths from parent TableNode.
    /// When set, these are used instead of calculating from child hints.
    /// </summary>
    public int[]? ColumnWidths { get; set; }
    
    /// <summary>
    /// Whether this row has a selection column (first cell after left border).
    /// </summary>
    public bool HasSelectionColumn { get; set; }
    
    /// <summary>
    /// Width of the selection column if present.
    /// </summary>
    public int SelectionColumnWidth { get; set; }
    
    /// <summary>
    /// Whether this row uses Full render mode (has padding around cells).
    /// </summary>
    public bool HasCellPadding { get; set; }
    
    /// <summary>
    /// Whether the parent table is focused (for outer border styling).
    /// </summary>
    public bool TableIsFocused { get; set; }
    
    /// <summary>
    /// Whether this is a header or footer row (all vertical bars use outer border color).
    /// </summary>
    public bool IsOuterRow { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Children.Count == 0)
        {
            return constraints.Constrain(Size.Zero);
        }

        // Measure all children to get their natural sizes
        int totalFixedWidth = 0;
        int fillWeightTotal = 0;
        var childSizes = new Size[Children.Count];
        var childHints = new SizeHint?[Children.Count];

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var hint = child.WidthHint;
            childHints[i] = hint;

            // Measure child with loose constraints
            var childSize = child.Measure(Constraints.Loose(constraints.MaxWidth, constraints.MaxHeight));
            childSizes[i] = childSize;

            if (hint?.IsFixed == true)
            {
                totalFixedWidth += hint.Value.FixedValue;
            }
            else if (hint?.IsFill == true)
            {
                fillWeightTotal += hint.Value.FillWeight;
            }
            else
            {
                // Content-sized or no hint - use measured width
                totalFixedWidth += childSize.Width;
            }
        }

        // Calculate width
        int width = Math.Min(constraints.MaxWidth, Math.Max(totalFixedWidth, constraints.MinWidth));
        
        // Height is always 1 for table rows
        return constraints.Constrain(new Size(width, 1));
    }

    protected override void ArrangeCore(Rect rect)
    {
        base.Arrange(rect);

        if (Children.Count == 0) return;

        // If we have pre-calculated column widths from the parent table, use those
        if (ColumnWidths != null && ColumnWidths.Length > 0)
        {
            ArrangeWithColumnWidths(rect);
            return;
        }

        // Otherwise calculate widths from child hints (fallback for standalone usage)
        ArrangeWithChildHints(rect);
    }
    
    /// <summary>
    /// Arranges children using pre-calculated column widths from parent TableNode.
    /// </summary>
    private void ArrangeWithColumnWidths(Rect rect)
    {
        int x = rect.X;
        int colIndex = 0;
        
        // In Full mode with padding, the structure for each cell is:
        // [border] [padding] [content] [padding] [border] ...
        // We need to track where we are in this pattern
        
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            int width;
            
            // Check if this is a border character
            bool isBorder = child is TextBlockNode textNode && textNode.Text == "│";
            
            // Check if this is a padding space (single space in Full mode)
            bool isPadding = HasCellPadding && child is TextBlockNode padNode && padNode.Text == " ";
            
            if (isBorder)
            {
                width = 1;
            }
            else if (isPadding)
            {
                width = 1; // Padding is always 1 character
            }
            else if (HasSelectionColumn && colIndex == 0)
            {
                // First non-border, non-padding child is selection column
                width = SelectionColumnWidth;
                colIndex++; // Don't count this as a data column
            }
            else
            {
                // Data cell - use column width
                int dataColIndex = HasSelectionColumn ? colIndex - 1 : colIndex;
                if (dataColIndex >= 0 && dataColIndex < ColumnWidths!.Length)
                {
                    width = ColumnWidths[dataColIndex];
                }
                else
                {
                    width = 1; // Fallback
                }
                colIndex++;
            }
            
            var childRect = new Rect(x, rect.Y, width, 1);
            child.Arrange(childRect);
            x += width;
        }
    }
    
    /// <summary>
    /// Arranges children by calculating widths from their hints (fallback for standalone usage).
    /// </summary>
    private void ArrangeWithChildHints(Rect rect)
    {
        // Calculate widths for each child
        var childWidths = new int[Children.Count];
        int totalFixedWidth = 0;
        int fillWeightTotal = 0;
        var fillChildren = new List<int>();

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var hint = child.WidthHint;

            if (hint?.IsFixed == true)
            {
                childWidths[i] = hint.Value.FixedValue;
                totalFixedWidth += childWidths[i];
            }
            else if (hint?.IsFill == true)
            {
                fillChildren.Add(i);
                fillWeightTotal += hint.Value.FillWeight;
            }
            else
            {
                // Content-sized - use measured width
                var childSize = child.Measure(Constraints.Loose(rect.Width, rect.Height));
                childWidths[i] = childSize.Width;
                totalFixedWidth += childWidths[i];
            }
        }

        // Distribute remaining width to fill children
        int remainingWidth = rect.Width - totalFixedWidth;
        if (fillChildren.Count > 0 && remainingWidth > 0)
        {
            int distributed = 0;
            for (int j = 0; j < fillChildren.Count; j++)
            {
                int i = fillChildren[j];
                var hint = Children[i].WidthHint;
                int weight = hint?.FillWeight ?? 1;

                if (j == fillChildren.Count - 1)
                {
                    // Last fill child gets remainder
                    childWidths[i] = remainingWidth - distributed;
                }
                else
                {
                    int share = (int)Math.Floor((double)remainingWidth * weight / fillWeightTotal);
                    childWidths[i] = share;
                    distributed += share;
                }
            }
        }

        // Arrange children horizontally
        int x = rect.X;
        for (int i = 0; i < Children.Count; i++)
        {
            var childRect = new Rect(x, rect.Y, childWidths[i], 1);
            Children[i].Arrange(childRect);
            x += childWidths[i];
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var borderColor = theme.Get(TableTheme.BorderColor);
        var focusedBorderColor = theme.Get(TableTheme.FocusedBorderColor);
        var tableFocusedBorderColor = theme.Get(TableTheme.TableFocusedBorderColor);
        
        // Effective border color: mid grey when table focused, dark grey when not
        var effectiveBorderColor = TableIsFocused && theme.Get(TableTheme.ShowFocusIndicator) 
            ? tableFocusedBorderColor 
            : borderColor;
        
        // Determine row background color for focused row only
        string rowBgAnsi = "";
        if (IsHighlighted)
        {
            var bg = theme.Get(TableTheme.FocusedRowBackground);
            if (!bg.IsDefault)
            {
                rowBgAnsi = bg.ToBackgroundAnsi();
                // Fill entire row with background color first
                var spaces = new string(' ', Bounds.Width);
                context.WriteClipped(Bounds.X, Bounds.Y, $"{rowBgAnsi}{spaces}\x1b[0m");
            }
        }
        
        // Find the indices of vertical bar children to identify first/last (outer edges)
        var verticalBarIndices = new List<int>();
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is TextBlockNode textNode && textNode.Text == "│")
            {
                verticalBarIndices.Add(i);
            }
        }
        
        // Set ambient background so child cells inherit the row background
        var previousAmbient = context.AmbientBackground;
        if (!string.IsNullOrEmpty(rowBgAnsi))
        {
            var effectiveBg = theme.Get(TableTheme.FocusedRowBackground);
            if (!effectiveBg.IsDefault)
            {
                context.AmbientBackground = effectiveBg;
            }
        }
        
        // Render children, applying border colors and replacing vertical bars with thicker ones if highlighted
        var selectionColumnVertical = theme.Get(TableTheme.SelectionColumnVertical);
        
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            
            // Check if this is a vertical border
            if (child is TextBlockNode textNode && textNode.Text == "│")
            {
                string borderChar;
                Hex1bColor cellBorderColor;
                
                // Check if this is the selection column separator (second vertical bar when HasSelectionColumn)
                bool isSelectionColumnSeparator = HasSelectionColumn && verticalBarIndices.Count >= 2 && 
                    i == verticalBarIndices[1];
                
                if (IsHighlighted)
                {
                    // Render a thicker border with focused color
                    cellBorderColor = focusedBorderColor;
                    borderChar = "┃"; // Heavy vertical line as focus indicator
                }
                else
                {
                    // All borders (outer, inner, selection separator) use effective color
                    cellBorderColor = effectiveBorderColor;
                    borderChar = isSelectionColumnSeparator ? selectionColumnVertical.ToString() : "│";
                }
                
                // Include row background so border chars also show the highlight
                context.WriteClipped(child.Bounds.X, child.Bounds.Y, $"{rowBgAnsi}{cellBorderColor.ToForegroundAnsi()}{borderChar}\x1b[0m");
            }
            else
            {
                context.RenderChild(child);
            }
        }
        
        // Restore previous ambient background
        context.AmbientBackground = previousAmbient;
    }

    public override IEnumerable<Hex1bNode> GetChildren() => Children;

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        foreach (var child in Children)
        {
            foreach (var focusable in child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }
}
