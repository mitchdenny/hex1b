using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="Hex1b.Widgets.BackgroundPanelWidget"/>.
/// Fills its bounds with a background color, then renders the child on top.
/// Everything else (measure, arrange, focus, input) is delegated to the child.
/// </summary>
public sealed class BackgroundPanelNode : Hex1bNode
{
    /// <summary>
    /// The background color to fill.
    /// </summary>
    public Hex1bColor Color { get; set; } = Hex1bColor.Default;

    /// <summary>
    /// The child node.
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

    public override Size Measure(Constraints constraints)
        => Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);

    public override void Arrange(Rect rect)
    {
        base.Arrange(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Fill bounds with background color
        if (!Color.IsDefault && Bounds.Width > 0 && Bounds.Height > 0)
        {
            var bgCode = Color.ToBackgroundAnsi();
            var resetCode = context.Theme.GetResetToGlobalCodes();
            var spaces = new string(' ', Bounds.Width);

            for (int y = Bounds.Y; y < Bounds.Y + Bounds.Height; y++)
            {
                context.SetCursorPosition(Bounds.X, y);
                context.Write($"{bgCode}{spaces}{resetCode}");
            }
        }

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
