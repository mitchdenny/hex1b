using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Internal passthrough node that owns the <see cref="KgpImageCache"/> and sets it
/// on the render context before rendering children. This ensures all descendant
/// <see cref="KittyGraphicsNode"/>s share the same image cache and avoid
/// redundant transmissions.
/// </summary>
internal sealed class KittyGraphicsHostNode : Hex1bNode
{
    private readonly KgpImageCache _cache = new();

    /// <summary>
    /// The child node (the user's widget tree).
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// Gets the image cache owned by this host node.
    /// </summary>
    internal KgpImageCache Cache => _cache;

    protected override Size MeasureCore(Constraints constraints)
    {
        return Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        Child?.Arrange(bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Set the cache on the context so descendant KittyGraphicsNodes can use it
        context.KgpCache = _cache;

        if (Child != null)
        {
            context.RenderChild(Child);
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
                yield return focusable;
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }
}
