using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A simple spacer node for title bar padding.
/// </summary>
internal sealed class TitleBarSpacerNode : Hex1bNode
{
    private readonly int _width;

    public TitleBarSpacerNode(int width)
    {
        _width = width;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        return constraints.Constrain(new Size(_width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Render as spaces with the ambient background
        var bgCode = "";
        if (!context.AmbientBackground.IsDefault)
        {
            bgCode = context.AmbientBackground.ToBackgroundAnsi();
        }
        var resetCodes = context.Theme.GetResetToGlobalCodes();
        context.WriteClipped(Bounds.X, Bounds.Y, $"{bgCode}{new string(' ', _width)}{resetCodes}");
    }
}
