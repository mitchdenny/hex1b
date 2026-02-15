using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that provides clipping and rendering assistance to its children.
/// </summary>
public sealed class LayoutNode : Hex1bNode, ILayoutProvider
{
    public Hex1bNode? Child { get; set; }
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;
    
    /// <summary>
    /// The clip rectangle, defaults to Bounds but could be overridden for scrolling.
    /// </summary>
    public Rect ClipRect => Bounds;
    
    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);

    protected override Size MeasureCore(Constraints constraints)
    {
        return Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.Arrange(bounds);
        Child?.Arrange(bounds);
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
        // Store ourselves as the current layout provider in the context
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;
        
        // Use RenderChild for automatic caching support
        if (Child != null)
        {
            context.RenderChild(Child);
        }
        
        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }
}
