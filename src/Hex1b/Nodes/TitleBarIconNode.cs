using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A specialized icon node for title bar actions with spacing.
/// Renders as: space + icon (for proper visual separation).
/// </summary>
internal sealed class TitleBarIconNode : Hex1bNode
{
    /// <summary>
    /// The window action this node represents.
    /// </summary>
    public WindowAction? Action { get; set; }
    
    /// <summary>
    /// The window entry for invoking the action.
    /// </summary>
    public WindowEntry? Entry { get; set; }

    /// <summary>
    /// Measures the size: 1 (space) + icon display width.
    /// </summary>
    protected override Size MeasureCore(Constraints constraints)
    {
        var iconWidth = Action != null ? DisplayWidth.GetStringWidth(Action.Icon) : 0;
        // space + icon
        return constraints.Constrain(new Size(1 + iconWidth, 1));
    }

    /// <summary>
    /// Renders the icon with leading space.
    /// </summary>
    public override void Render(Hex1bRenderContext context)
    {
        if (Action == null) return;
        
        var theme = context.Theme;
        var actionFg = theme.Get(WindowTheme.CloseButtonForeground);
        var resetCodes = theme.GetResetToGlobalCodes();
        
        // Include ambient background so icon has the title bar background
        var bgCode = "";
        if (!context.AmbientBackground.IsDefault)
        {
            bgCode = context.AmbientBackground.ToBackgroundAnsi();
        }
        
        var output = $"{bgCode} {actionFg.ToForegroundAnsi()}{Action.Icon}{resetCodes}";
        context.WriteClipped(Bounds.X, Bounds.Y, output);
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        bindings.Mouse(Input.MouseButton.Left).Triggers(WindowNode.TitleBarIconClickAction, ctx =>
        {
            if (Action != null && Entry != null)
            {
                var actionContext = new WindowActionContext(Entry, ctx);
                Action.Handler(actionContext);
            }
            return Task.CompletedTask;
        }, "Click action");
    }

    public override bool IsFocusable => true;
}
