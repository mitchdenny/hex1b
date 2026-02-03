using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for displaying icons with optional click handling and loading state.
/// </summary>
/// <remarks>
/// <para>
/// IconNode renders either an icon string or an animated spinner based on its loading state.
/// When a click handler is attached, the icon responds to mouse clicks.
/// </para>
/// </remarks>
/// <seealso cref="IconWidget"/>
public sealed class IconNode : Hex1bNode
{
    /// <summary>
    /// Gets or sets the icon to display.
    /// </summary>
    public string Icon { get; set; } = "";
    
    /// <summary>
    /// Gets or sets whether the icon is in loading state.
    /// </summary>
    public bool IsLoading { get; set; }
    
    /// <summary>
    /// Gets or sets the spinner style for loading state.
    /// </summary>
    public SpinnerStyle? LoadingStyle { get; set; }
    
    /// <summary>
    /// The source widget for typed event args.
    /// </summary>
    public IconWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// Callback for click events.
    /// </summary>
    public Func<InputBindingActionContext, Task>? ClickCallback { get; set; }
    
    // Time-based animation state for spinner
    private DateTime _loadingStartTime = DateTime.UtcNow;
    
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
    /// Measures the size required for the icon or spinner.
    /// </summary>
    public override Size Measure(Constraints constraints)
    {
        string displayText;
        
        if (IsLoading)
        {
            var style = LoadingStyle ?? SpinnerStyle.Dots;
            var frameIndex = GetCurrentSpinnerFrame(style);
            displayText = style.GetFrame(frameIndex);
        }
        else
        {
            displayText = Icon;
        }
        
        var width = GetDisplayWidth(displayText);
        return constraints.Constrain(new Size(width, 1));
    }

    /// <summary>
    /// Renders the icon or spinner to the terminal.
    /// </summary>
    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        string displayText;
        
        if (IsLoading)
        {
            var style = LoadingStyle ?? theme.Get(SpinnerTheme.Style);
            var frameIndex = GetCurrentSpinnerFrame(style);
            displayText = style.GetFrame(frameIndex);
        }
        else
        {
            displayText = Icon;
        }
        
        var fg = theme.Get(IconTheme.ForegroundColor);
        var bg = theme.Get(IconTheme.BackgroundColor);
        var resetCodes = theme.GetResetToGlobalCodes();

        // Build output with colors
        var output = !fg.IsDefault || !bg.IsDefault
            ? $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{displayText}{resetCodes}"
            : displayText;

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }
    
    private int GetCurrentSpinnerFrame(SpinnerStyle style)
    {
        var elapsed = DateTime.UtcNow - _loadingStartTime;
        var intervalMs = style.Interval.TotalMilliseconds;
        if (intervalMs <= 0) intervalMs = 80;
        return (int)(elapsed.TotalMilliseconds / intervalMs);
    }
    
    /// <summary>
    /// Resets the loading animation start time. Call this when starting a new load operation.
    /// </summary>
    public void ResetLoadingAnimation()
    {
        _loadingStartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the display width of a string, accounting for wide Unicode characters.
    /// </summary>
    private static int GetDisplayWidth(string text)
    {
        var width = 0;
        foreach (var c in text)
        {
            // Basic wide character detection
            if (c >= 0x1100 && (
                (c <= 0x115F) || // Hangul Jamo
                (c >= 0x2E80 && c <= 0x9FFF) || // CJK
                (c >= 0xAC00 && c <= 0xD7A3) || // Hangul Syllables
                (c >= 0xF900 && c <= 0xFAFF) || // CJK Compatibility
                (c >= 0xFE10 && c <= 0xFE1F) || // Vertical forms
                (c >= 0xFE30 && c <= 0xFE6F) || // CJK Compatibility Forms
                (c >= 0xFF00 && c <= 0xFF60) || // Fullwidth forms
                (c >= 0xFFE0 && c <= 0xFFE6))) // Fullwidth signs
            {
                width += 2;
            }
            else
            {
                width += 1;
            }
        }
        return width;
    }
}
