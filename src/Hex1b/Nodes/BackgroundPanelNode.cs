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
    /// The explicit background color to fill. Takes precedence over <see cref="ThemeElement"/>.
    /// </summary>
    public Hex1bColor Color { get; set; } = Hex1bColor.Default;

    /// <summary>
    /// Optional theme element to read background color from at render time.
    /// Used when the color should come from the theme rather than being hardcoded.
    /// </summary>
    public Hex1bThemeElement<Hex1bColor>? ThemeElement { get; set; }

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

    protected override Size MeasureCore(Constraints constraints)
        => Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);

    protected override void ArrangeCore(Rect rect)
    {
        base.Arrange(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Resolve color: explicit Color takes precedence, then ThemeElement
        var bgColor = !Color.IsDefault ? Color
            : ThemeElement != null ? context.Theme.Get(ThemeElement)
            : Hex1bColor.Default;

        // Request post-render background fill. After this node's surface is rendered,
        // RenderChild will fill all remaining transparent/empty cells with this color.
        // This is a belt-and-suspenders complement to the pre-fill below.
        if (!bgColor.IsDefault)
        {
            FillBackground = bgColor;
        }

        if (!bgColor.IsDefault && Bounds.Width > 0 && Bounds.Height > 0)
        {
            var bgCode = bgColor.ToBackgroundAnsi();
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
