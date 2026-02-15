using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for displaying icons with optional click handling.
/// </summary>
/// <seealso cref="IconWidget"/>
public sealed class IconNode : Hex1bNode
{
    /// <summary>
    /// Gets or sets the icon to display.
    /// </summary>
    public string Icon { get; set; } = "";
    
    /// <summary>
    /// The source widget for typed event args.
    /// </summary>
    public IconWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// Callback for click events.
    /// </summary>
    public Func<InputBindingActionContext, Task>? ClickCallback { get; set; }
    
    /// <summary>
    /// Gets whether this icon is clickable.
    /// </summary>
    public bool IsClickable => ClickCallback != null;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        if (ClickCallback != null)
        {
            bindings.Mouse(MouseButton.Left).Action(ClickCallback, "Click icon");
        }
    }

    /// <summary>
    /// Measures the size required for the icon.
    /// </summary>
    protected override Size MeasureCore(Constraints constraints)
    {
        var width = DisplayWidth.GetStringWidth(Icon);
        return constraints.Constrain(new Size(width, 1));
    }

    /// <summary>
    /// Renders the icon to the terminal.
    /// </summary>
    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        var fg = theme.Get(IconTheme.ForegroundColor);
        var bg = theme.Get(IconTheme.BackgroundColor);
        var resetCodes = theme.GetResetToGlobalCodes();

        // Build output with colors
        var output = !fg.IsDefault || !bg.IsDefault
            ? $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{Icon}{resetCodes}"
            : Icon;

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }
}
