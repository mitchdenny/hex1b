using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A widget that positions its child at absolute screen coordinates.
/// Used internally for drag ghost overlays that follow the mouse cursor.
/// </summary>
internal sealed record DragOverlayWidget(
    Hex1bWidget Child,
    int CursorX,
    int CursorY) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DragOverlayNode ?? new DragOverlayNode();

        node.CursorX = CursorX;
        node.CursorY = CursorY;

        var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
        node.Child = await childContext.ReconcileChildAsync(node.Child, Child, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(DragOverlayNode);
}

/// <summary>
/// Render node for <see cref="DragOverlayWidget"/>.
/// Positions its child at the cursor coordinates, clamped to screen bounds.
/// Does not capture focus or participate in hit testing.
/// </summary>
internal sealed class DragOverlayNode : Hex1bNode
{
    public Hex1bNode? Child { get; set; }
    public int CursorX { get; set; }
    public int CursorY { get; set; }

    private Size _childSize;

    public override bool IsFocusable => false;

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Child == null) return Size.Zero;

        // Measure child with loose constraints
        var childConstraints = new Constraints(0, constraints.MaxWidth, 0, constraints.MaxHeight);
        _childSize = Child.Measure(childConstraints);

        // Return full available size so we get full screen bounds for positioning
        return new Size(constraints.MaxWidth, constraints.MaxHeight);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        if (Child == null) return;

        // Position child at cursor, offset slightly so it doesn't obscure the cursor
        var x = CursorX + 1;
        var y = CursorY;

        // Clamp to screen bounds
        x = Math.Max(0, Math.Min(x, bounds.Width - _childSize.Width));
        y = Math.Max(0, Math.Min(y, bounds.Height - _childSize.Height));

        Child.Arrange(new Rect(x, y, _childSize.Width, _childSize.Height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
            context.RenderChild(Child);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        yield break; // Drag overlay never captures focus
    }

    public override IReadOnlyList<Hex1bNode> GetChildren()
        => Child != null ? [Child] : [];
}
