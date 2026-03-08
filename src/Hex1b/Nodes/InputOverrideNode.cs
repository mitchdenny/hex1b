using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A pass-through container node for <see cref="InputOverrideWidget"/>.
/// Delegates all measure, arrange, render, and focus operations to its child.
/// The actual binding override logic happens during reconciliation via
/// <see cref="ReconcileContext"/>, not at render time.
/// </summary>
internal sealed class InputOverrideNode : Hex1bNode
{
    /// <summary>
    /// The single child node that this override wraps.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    protected override Size MeasureCore(Constraints constraints)
        => Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);

    protected override void ArrangeCore(Rect rect)
        => Child?.Arrange(rect);

    public override void Render(Hex1bRenderContext context)
    {
        if (Child is not null)
        {
            context.RenderChild(Child);
        }
    }

    public override bool IsFocusable => false;

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
}
