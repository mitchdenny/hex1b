using Hex1b.Layout;

namespace Hex1b.Nodes;

/// <summary>
/// Internal node that holds a cached <see cref="Hex1b.Widgets.EditorState"/> for a code block
/// and delegates rendering to its child <see cref="EditorNode"/>.
/// </summary>
internal sealed class MarkdownCodeBlockNode : Hex1bNode
{
    internal string? CachedContent { get; set; }
    internal Hex1b.Widgets.EditorState? CachedState { get; set; }
    internal int LineCount { get; set; }
    internal Hex1bNode? EditorChild { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        // Clamp height to actual line count so parent containers (VStack)
        // don't allocate int.MaxValue rows when passing unconstrained height.
        var clampedMaxHeight = Math.Min(constraints.MaxHeight, LineCount);
        var clamped = new Constraints(
            constraints.MinWidth, constraints.MaxWidth,
            Math.Min(constraints.MinHeight, clampedMaxHeight), clampedMaxHeight);
        return EditorChild?.Measure(clamped) ?? new Size(constraints.MaxWidth, LineCount);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        EditorChild?.Arrange(bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (EditorChild != null)
            context.RenderChild(EditorChild);
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (EditorChild != null)
            yield return EditorChild;
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (EditorChild != null)
        {
            foreach (var focusable in EditorChild.GetFocusableNodes())
                yield return focusable;
        }
    }
}
