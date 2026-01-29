using Hex1b.Layout;
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

    public override Size Measure(Constraints constraints)
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

    public override void Arrange(Rect rect)
    {
        base.Arrange(rect);

        if (Children.Count == 0) return;

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
        // Apply highlight (reverse video) if this row is focused
        if (IsHighlighted)
        {
            context.Write("\x1b[7m"); // Reverse video on
        }

        // Render all children
        foreach (var child in Children)
        {
            context.RenderChild(child);
        }

        if (IsHighlighted)
        {
            context.Write("\x1b[27m"); // Reverse video off
        }
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
