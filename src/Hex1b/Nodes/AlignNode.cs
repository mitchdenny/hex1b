using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="AlignWidget"/>. Positions its child based on alignment flags.
/// </summary>
/// <remarks>
/// AlignNode expands to fill available space and positions its child within that space.
/// The alignment is determined by checking the <see cref="Alignment"/> flags for horizontal
/// (Left, HCenter, Right) and vertical (Top, VCenter, Bottom) positioning.
/// </remarks>
/// <seealso cref="AlignWidget"/>
/// <seealso cref="Alignment"/>
public sealed class AlignNode : Hex1bNode
{
    /// <summary>
    /// The child node to align.
    /// </summary>
    public Hex1bNode? Child { get; set; }
    
    /// <summary>
    /// The alignment flags for positioning the child.
    /// </summary>
    public Alignment Alignment { get; set; }

    public override Size Measure(Constraints constraints)
    {
        if (Child == null)
        {
            return constraints.Constrain(Size.Zero);
        }

        // Build child constraints respecting Fixed hints
        var childMaxWidth = constraints.MaxWidth;
        var childMaxHeight = constraints.MaxHeight;
        
        // If child has a fixed width hint, use that as a tight constraint
        if (Child.WidthHint is { IsFixed: true } widthHint)
        {
            childMaxWidth = Math.Min(childMaxWidth, widthHint.FixedValue);
        }
        
        // If child has a fixed height hint, use that as a tight constraint
        if (Child.HeightHint is { IsFixed: true } heightHint)
        {
            childMaxHeight = Math.Min(childMaxHeight, heightHint.FixedValue);
        }
        
        // Measure the child with potentially constrained size
        var childConstraints = Constraints.Loose(childMaxWidth, childMaxHeight);
        var childSize = Child.Measure(childConstraints);
        
        // Return the child's natural size. When Fill() is applied, the parent container
        // will give us more space during Arrange, and alignment happens then.
        return constraints.Constrain(childSize);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        if (Child == null) return;

        // Build child constraints respecting Fixed hints
        var childMaxWidth = bounds.Width;
        var childMaxHeight = bounds.Height;
        
        // If child has a fixed width hint, use that as a tight constraint
        if (Child.WidthHint is { IsFixed: true } widthHint)
        {
            childMaxWidth = Math.Min(childMaxWidth, widthHint.FixedValue);
        }
        
        // If child has a fixed height hint, use that as a tight constraint
        if (Child.HeightHint is { IsFixed: true } heightHint)
        {
            childMaxHeight = Math.Min(childMaxHeight, heightHint.FixedValue);
        }

        // Measure child again with constrained size
        var childSize = Child.Measure(Constraints.Loose(childMaxWidth, childMaxHeight));
        
        // Calculate horizontal position
        int x = bounds.X;
        if (Alignment.HasFlag(Alignment.HCenter))
        {
            x = bounds.X + Math.Max(0, (bounds.Width - childSize.Width) / 2);
        }
        else if (Alignment.HasFlag(Alignment.Right))
        {
            x = bounds.X + Math.Max(0, bounds.Width - childSize.Width);
        }
        // Left is default (x = bounds.X)
        
        // Calculate vertical position
        int y = bounds.Y;
        if (Alignment.HasFlag(Alignment.VCenter))
        {
            y = bounds.Y + Math.Max(0, (bounds.Height - childSize.Height) / 2);
        }
        else if (Alignment.HasFlag(Alignment.Bottom))
        {
            y = bounds.Y + Math.Max(0, bounds.Height - childSize.Height);
        }
        // Top is default (y = bounds.Y)
        
        Child.Arrange(new Rect(x, y, childSize.Width, childSize.Height));
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        Child?.Render(context);
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }
}
