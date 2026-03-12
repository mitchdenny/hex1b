using Hex1b.Layout;
using Hex1b.Markdown;

namespace Hex1b.Nodes;

/// <summary>
/// Internal node that caches loaded image data and delegates rendering
/// to a child <see cref="KgpImageNode"/> or text fallback node.
/// </summary>
internal sealed class MarkdownImageNode : Hex1bNode
{
    internal string? CachedUrl { get; set; }
    internal MarkdownImageData? CachedImageData { get; set; }
    internal Hex1bNode? ContentChild { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        return ContentChild?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        ContentChild?.Arrange(bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (ContentChild != null)
            context.RenderChild(ContentChild);
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (ContentChild != null)
            yield return ContentChild;
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (ContentChild != null)
        {
            foreach (var focusable in ContentChild.GetFocusableNodes())
                yield return focusable;
        }
    }
}
