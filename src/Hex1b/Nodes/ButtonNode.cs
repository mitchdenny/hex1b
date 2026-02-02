using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class ButtonNode : Hex1bNode
{
    public string Label { get; set; } = "";
    
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// Used to create typed event args.
    /// </summary>
    public ButtonWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// The async action to execute when the button is activated.
    /// This is the wrapped handler that receives InputBindingActionContext.
    /// </summary>
    public Func<InputBindingActionContext, Task>? ClickAction { get; set; }
    
    private bool _isFocused;
    public override bool IsFocused 
    { 
        get => _isFocused; 
        set 
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    public override bool IsHovered 
    { 
        get => _isHovered; 
        set 
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Only register activation bindings if there's an action to perform
        if (ClickAction != null)
        {
            // Enter and Space trigger the button
            bindings.Key(Hex1bKey.Enter).Action(ClickAction, "Activate button");
            bindings.Key(Hex1bKey.Spacebar).Action(ClickAction, "Activate button");
            
            // Left click activates the button
            bindings.Mouse(MouseButton.Left).Action(ClickAction, "Click button");
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Button renders as "[ Label ]" - 4 chars for brackets/spaces + label length
        var width = Label.Length + 4;
        var height = 1;
        _measuredSize = constraints.Constrain(new Size(width, height));
        return _measuredSize;
    }
    
    private Size _measuredSize;
    
    /// <summary>
    /// Hit test bounds are limited to the actual button content, not the full arranged bounds.
    /// If the button hasn't been arranged (Bounds is zero), returns empty bounds to prevent
    /// accidental hits on nodes that are scrolled out of view or otherwise not displayed.
    /// </summary>
    public override Rect HitTestBounds
    {
        get
        {
            // If Bounds has zero dimensions, this node wasn't arranged (e.g., scrolled out of view)
            // Return zero-size rect to prevent hit testing
            if (Bounds.Width == 0 && Bounds.Height == 0)
            {
                return Rect.Zero;
            }
            // Use measured size if available, otherwise fall back to arranged bounds
            var width = _measuredSize.Width > 0 ? _measuredSize.Width : Bounds.Width;
            var height = _measuredSize.Height > 0 ? _measuredSize.Height : Bounds.Height;
            return new Rect(Bounds.X, Bounds.Y, width, height);
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var leftBracket = theme.Get(ButtonTheme.LeftBracket);
        var rightBracket = theme.Get(ButtonTheme.RightBracket);
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        string output;
        if (IsFocused)
        {
            var fg = theme.Get(ButtonTheme.FocusedForegroundColor);
            var bg = theme.Get(ButtonTheme.FocusedBackgroundColor);
            output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{leftBracket}{Label}{rightBracket}{resetToGlobal}";
        }
        else if (IsHovered)
        {
            var fg = theme.Get(ButtonTheme.HoveredForegroundColor);
            var bg = theme.Get(ButtonTheme.HoveredBackgroundColor);
            output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{leftBracket}{Label}{rightBracket}{resetToGlobal}";
        }
        else
        {
            var fg = theme.Get(ButtonTheme.ForegroundColor);
            var bg = theme.Get(ButtonTheme.BackgroundColor);
            // Use global colors if theme colors are default
            var fgCode = fg.IsDefault ? theme.GetGlobalForeground().ToForegroundAnsi() : fg.ToForegroundAnsi();
            var bgCode = bg.IsDefault ? theme.GetGlobalBackground().ToBackgroundAnsi() : bg.ToBackgroundAnsi();
            output = $"{fgCode}{bgCode}{leftBracket}{Label}{rightBracket}{resetToGlobal}";
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
