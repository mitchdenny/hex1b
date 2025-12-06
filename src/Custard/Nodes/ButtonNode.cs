using Custard.Layout;
using Custard.Theming;

namespace Custard;

public sealed class ButtonNode : CustardNode
{
    public string Label { get; set; } = "";
    public Action? OnClick { get; set; }
    public bool IsFocused { get; set; } = false;

    public override bool IsFocusable => true;

    public override Size Measure(Constraints constraints)
    {
        // Button renders as "[ Label ]" - 4 chars for brackets/spaces + label length
        var width = Label.Length + 4;
        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Render(CustardRenderContext context)
    {
        var theme = context.Theme;
        var leftBracket = theme.Get(ButtonTheme.LeftBracket);
        var rightBracket = theme.Get(ButtonTheme.RightBracket);
        
        if (IsFocused)
        {
            var fg = theme.Get(ButtonTheme.FocusedForegroundColor);
            var bg = theme.Get(ButtonTheme.FocusedBackgroundColor);
            context.Write($"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{leftBracket}{Label}{rightBracket}\x1b[0m");
        }
        else
        {
            var fg = theme.Get(ButtonTheme.ForegroundColor);
            var bg = theme.Get(ButtonTheme.BackgroundColor);
            context.Write($"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{leftBracket}{Label}{rightBracket}\x1b[0m");
        }
    }

    public override bool HandleInput(CustardInputEvent evt)
    {
        if (!IsFocused) return false;

        if (evt is KeyInputEvent keyEvent)
        {
            // Enter or Space triggers the button
            if (keyEvent.Key == ConsoleKey.Enter || keyEvent.Key == ConsoleKey.Spacebar)
            {
                OnClick?.Invoke();
                return true;
            }
        }
        return false;
    }
}
