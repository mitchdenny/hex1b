using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A container node for InfoBar that holds the composed widget tree.
/// InfoBar builds a ThemePanel(HStack(...)) tree internally; this node
/// wraps that tree for consistent reconciliation.
/// </summary>
public sealed class InfoBarNode : Hex1bNode
{
    /// <summary>
    /// The child node containing the composed widget tree (ThemePanel or HStack).
    /// </summary>
    public Hex1bNode? Child { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Child == null)
            return new Size(0, 1);

        return Child.Measure(constraints);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.Arrange(bounds);
        Child?.Arrange(bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        Child?.Render(context);
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

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null)
            yield return Child;
    }
}
