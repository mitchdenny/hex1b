using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Nodes;

/// <summary>
/// A node that provides a styled background for its child content.
/// Panel is not focusable - focus passes through to the child.
/// </summary>
public sealed class PanelNode : Hex1bNode
{
    public Hex1bNode? Child { get; set; }

    public override Size Measure(Constraints constraints)
    {
        // Panel doesn't add any size - child takes all available space
        return Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        // Child gets the full bounds
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
        var theme = context.Theme;
        var backgroundColor = theme.Get(PanelTheme.BackgroundColor);
        var foregroundColor = theme.Get(PanelTheme.ForegroundColor);

        // Fill background for the panel area
        if (!backgroundColor.IsDefault)
        {
            var bgCode = backgroundColor.ToBackgroundAnsi();
            var resetCode = "\x1b[0m";

            for (int row = 0; row < Bounds.Height; row++)
            {
                context.SetCursorPosition(Bounds.X, Bounds.Y + row);
                context.Write($"{bgCode}{new string(' ', Bounds.Width)}{resetCode}");
            }
        }

        // Apply foreground color for child content
        var fgCode = foregroundColor.IsDefault ? "" : foregroundColor.ToForegroundAnsi();
        var fgReset = foregroundColor.IsDefault ? "" : "\x1b[39m";

        if (!string.IsNullOrEmpty(fgCode))
        {
            context.Write(fgCode);
        }

        // Render child content (on top of background)
        Child?.Render(context);

        if (!string.IsNullOrEmpty(fgReset))
        {
            context.Write(fgReset);
        }
    }

    public override bool HandleInput(Hex1bInputEvent evt)
    {
        // Pass input to child
        return Child?.HandleInput(evt) ?? false;
    }
}
