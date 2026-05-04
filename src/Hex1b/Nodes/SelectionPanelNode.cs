using Hex1b.Layout;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="Hex1b.Widgets.SelectionPanelWidget"/>.
/// </summary>
/// <remarks>
/// At this stage the panel is purely a pass-through wrapper. Layout, focus,
/// and input are all delegated to the child. The class exists so that future
/// copy-mode behaviour (selection overlay, snapshot, mouse drag) can be added
/// without changing the widget API surface or consumer code.
/// </remarks>
public sealed class SelectionPanelNode : Hex1bNode
{
    /// <summary>
    /// The child node wrapped by this panel.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    public override bool IsFocusable => false;

    public override bool IsFocused
    {
        get => false;
        set
        {
            if (Child != null)
                Child.IsFocused = value;
        }
    }

    protected override Size MeasureCore(Constraints constraints)
        => Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
        {
            context.RenderChild(Child);
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
                yield return focusable;
        }
    }
}
