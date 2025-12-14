using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b;

public sealed class ButtonNode : Hex1bNode
{
    public string Label { get; set; } = "";
    public Action? OnClick { get; set; }
    
    private bool _isFocused;
    public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

    private bool _isHovered;
    public override bool IsHovered { get => _isHovered; set => _isHovered = value; }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Enter and Space trigger the button
        bindings.Key(Hex1bKey.Enter).Action(Click, "Activate button");
        bindings.Key(Hex1bKey.Spacebar).Action(Click, "Activate button");
        
        // Left click activates the button
        bindings.Mouse(MouseButton.Left).Action(Click, "Click button");
    }

    private void Click()
    {
        OnClick?.Invoke();
    }

    public override Size Measure(Constraints constraints)
    {
        // Button renders as "[ Label ]" - 4 chars for brackets/spaces + label length
        var width = Label.Length + 4;
        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var leftBracket = theme.Get(ButtonTheme.LeftBracket);
        var rightBracket = theme.Get(ButtonTheme.RightBracket);
        var resetToInherited = context.GetResetToInheritedCodes();
        
        string output;
        if (IsFocused)
        {
            var fg = theme.Get(ButtonTheme.FocusedForegroundColor);
            var bg = theme.Get(ButtonTheme.FocusedBackgroundColor);
            output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{leftBracket}{Label}{rightBracket}{resetToInherited}";
        }
        else
        {
            var fg = theme.Get(ButtonTheme.ForegroundColor);
            var bg = theme.Get(ButtonTheme.BackgroundColor);
            // Use inherited colors if theme colors are default
            var fgCode = fg.IsDefault ? context.InheritedForeground.ToForegroundAnsi() : fg.ToForegroundAnsi();
            var bgCode = bg.IsDefault ? context.InheritedBackground.ToBackgroundAnsi() : bg.ToBackgroundAnsi();
            output = $"{fgCode}{bgCode}{leftBracket}{Label}{rightBracket}{resetToInherited}";
        }
        
        // Use clipped rendering when a layout provider is active
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
