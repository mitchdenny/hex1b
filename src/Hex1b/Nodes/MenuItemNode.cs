using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for MenuItemWidget.
/// Focusable item that triggers an action when activated.
/// </summary>
public sealed class MenuItemNode : Hex1bNode
{
    /// <summary>
    /// The display label for the item.
    /// </summary>
    public string Label { get; set; } = "";
    
    /// <summary>
    /// Whether the item is disabled (grayed out and non-interactive).
    /// </summary>
    public bool IsDisabled { get; set; }
    
    /// <summary>
    /// The accelerator character for this item (uppercase).
    /// </summary>
    public char? Accelerator { get; set; }
    
    /// <summary>
    /// The index of the accelerator character in the label.
    /// </summary>
    public int AcceleratorIndex { get; set; } = -1;
    
    /// <summary>
    /// The width to render (set by parent MenuNode during layout).
    /// </summary>
    public int RenderWidth { get; set; }
    
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public MenuItemWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// The action to execute when the item is selected.
    /// </summary>
    public Func<InputBindingActionContext, Task>? SelectAction { get; set; }

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

    public override bool IsFocusable => !IsDisabled;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        if (SelectAction != null && !IsDisabled)
        {
            bindings.Key(Hex1bKey.Enter).Action(SelectAction, "Select item");
            bindings.Key(Hex1bKey.Spacebar).Action(SelectAction, "Select item");
            bindings.Mouse(MouseButton.Left).Action(SelectAction, "Click item");
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Item uses the render width set by parent, or label length + padding
        var width = RenderWidth > 0 ? RenderWidth : Label.Length + 2;
        return constraints.Constrain(new Size(width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        var width = RenderWidth > 0 ? RenderWidth : Bounds.Width;
        
        // Pad the label to fill the width
        var paddedLabel = Label.PadRight(width);
        
        if (IsDisabled)
        {
            // Disabled: gray out
            var fg = theme.Get(MenuItemTheme.DisabledForegroundColor);
            var bg = theme.Get(MenuItemTheme.BackgroundColor);
            var output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{paddedLabel}{resetToGlobal}";
            WriteOutput(context, output);
        }
        else if (IsFocused)
        {
            // Focused: highlight
            var fg = theme.Get(MenuItemTheme.FocusedForegroundColor);
            var bg = theme.Get(MenuItemTheme.FocusedBackgroundColor);
            var output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{paddedLabel}{resetToGlobal}";
            WriteOutput(context, output);
        }
        else if (IsHovered)
        {
            // Hovered: subtle highlight
            var fg = theme.Get(MenuItemTheme.FocusedForegroundColor);
            var bg = theme.Get(MenuItemTheme.FocusedBackgroundColor);
            var output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{paddedLabel}{resetToGlobal}";
            WriteOutput(context, output);
        }
        else
        {
            // Normal: render with accelerator highlighting
            var fg = theme.Get(MenuItemTheme.ForegroundColor);
            var bg = theme.Get(MenuItemTheme.BackgroundColor);
            var accelFg = theme.Get(MenuItemTheme.AcceleratorForegroundColor);
            var accelBg = theme.Get(MenuItemTheme.AcceleratorBackgroundColor);
            var accelUnderline = theme.Get(MenuItemTheme.AcceleratorUnderline);
            
            var output = RenderWithAccelerator(paddedLabel, AcceleratorIndex, fg, bg, accelFg, accelBg, accelUnderline, resetToGlobal);
            WriteOutput(context, output);
        }
    }
    
    private void WriteOutput(Hex1bRenderContext context, string output)
    {
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
    
    private static string RenderWithAccelerator(
        string text,
        int accelIndex,
        Hex1bColor fg,
        Hex1bColor bg,
        Hex1bColor accelFg,
        Hex1bColor accelBg,
        bool accelUnderline,
        string resetToGlobal)
    {
        if (accelIndex < 0 || accelIndex >= text.Length)
        {
            // No accelerator, just render plain
            return $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{text}{resetToGlobal}";
        }
        
        var before = text[..accelIndex];
        var accelChar = text[accelIndex];
        var after = text[(accelIndex + 1)..];
        
        var normalCodes = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}";
        var accelCodes = $"{accelFg.ToForegroundAnsi()}{accelBg.ToBackgroundAnsi()}";
        if (accelUnderline)
        {
            accelCodes += "\x1b[4m"; // Underline on
        }
        var accelReset = accelUnderline ? "\x1b[24m" : ""; // Underline off
        
        return $"{normalCodes}{before}{accelCodes}{accelChar}{accelReset}{normalCodes}{after}{resetToGlobal}";
    }
}
