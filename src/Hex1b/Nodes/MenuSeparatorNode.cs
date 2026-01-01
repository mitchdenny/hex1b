using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for MenuSeparatorWidget.
/// Non-focusable visual divider.
/// </summary>
public sealed class MenuSeparatorNode : Hex1bNode
{
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public MenuSeparatorWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// The width to render (set by parent MenuNode during layout).
    /// </summary>
    public int RenderWidth { get; set; }

    public override bool IsFocusable => false;

    public override Size Measure(Constraints constraints)
    {
        // Separator is 1 row high, uses parent's width
        var width = RenderWidth > 0 ? RenderWidth : constraints.MaxWidth;
        return constraints.Constrain(new Size(width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var separatorChar = theme.Get(MenuSeparatorTheme.Character);
        var color = theme.Get(MenuSeparatorTheme.Color);
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        var width = RenderWidth > 0 ? RenderWidth : Bounds.Width;
        var line = new string(separatorChar, width);
        var output = $"{color.ToForegroundAnsi()}{line}{resetToGlobal}";
        
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write(output);
        }
    }
}
