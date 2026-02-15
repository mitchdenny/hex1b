using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for displaying animated spinners. Created by reconciling a <see cref="SpinnerWidget"/>.
/// </summary>
/// <remarks>
/// <para>
/// SpinnerNode handles measuring and rendering spinner frames. The spinner style is resolved
/// from the widget's explicit style or the theme's default.
/// </para>
/// <para>
/// Animation is time-based: the node tracks when it started and calculates the current frame
/// based on elapsed time and the style's interval. This ensures consistent animation speed
/// regardless of how often the screen is redrawn.
/// </para>
/// <para>
/// This node is not focusable and does not handle input. It is a display-only widget.
/// </para>
/// </remarks>
/// <seealso cref="SpinnerWidget"/>
/// <seealso cref="SpinnerStyle"/>
public sealed class SpinnerNode : Hex1bNode
{
    /// <summary>
    /// Gets or sets the explicit spinner style, or null to use theme default.
    /// </summary>
    public SpinnerStyle? Style { get; set; }

    /// <summary>
    /// Gets or sets an explicit frame index for manual control.
    /// When null, the spinner uses time-based animation.
    /// </summary>
    public int? ExplicitFrameIndex { get; set; }

    // Time-based animation state
    private DateTime _animationStartTime = DateTime.UtcNow;
    private int _lastRenderedFrame = -1;

    // Cached resolved style for measuring without render context
    private SpinnerStyle _resolvedStyle = SpinnerStyle.Dots;

    /// <summary>
    /// Gets the current frame index based on elapsed time and style interval.
    /// </summary>
    private int GetCurrentFrameIndex()
    {
        if (ExplicitFrameIndex.HasValue)
        {
            return ExplicitFrameIndex.Value;
        }

        var elapsed = DateTime.UtcNow - _animationStartTime;
        var intervalMs = _resolvedStyle.Interval.TotalMilliseconds;
        if (intervalMs <= 0) intervalMs = 80; // Fallback

        return (int)(elapsed.TotalMilliseconds / intervalMs);
    }

    /// <summary>
    /// Gets the time until the next frame transition for scheduling redraws.
    /// </summary>
    public TimeSpan GetTimeUntilNextFrame()
    {
        if (ExplicitFrameIndex.HasValue)
        {
            // Manual mode - no automatic timing
            return TimeSpan.MaxValue;
        }

        var elapsed = DateTime.UtcNow - _animationStartTime;
        var intervalMs = _resolvedStyle.Interval.TotalMilliseconds;
        if (intervalMs <= 0) intervalMs = 80;

        var currentFrameStartMs = (long)(elapsed.TotalMilliseconds / intervalMs) * intervalMs;
        var nextFrameMs = currentFrameStartMs + intervalMs;
        var remainingMs = nextFrameMs - elapsed.TotalMilliseconds;

        return TimeSpan.FromMilliseconds(Math.Max(1, remainingMs));
    }

    /// <summary>
    /// Measures the size required for the spinner.
    /// </summary>
    /// <param name="constraints">The size constraints for layout.</param>
    /// <returns>The measured size based on the current frame's display width.</returns>
    protected override Size MeasureCore(Constraints constraints)
    {
        var frameIndex = GetCurrentFrameIndex();
        var frame = _resolvedStyle.GetFrame(frameIndex);
        var width = GetDisplayWidth(frame);
        return constraints.Constrain(new Size(width, 1));
    }

    /// <summary>
    /// Renders the spinner to the terminal.
    /// </summary>
    /// <param name="context">The render context providing terminal access and theming.</param>
    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;

        // Resolve style: explicit > theme default
        _resolvedStyle = Style ?? theme.Get(SpinnerTheme.Style);

        var frameIndex = GetCurrentFrameIndex();
        
        // Track if frame changed (for potential future optimizations)
        if (frameIndex != _lastRenderedFrame)
        {
            _lastRenderedFrame = frameIndex;
        }

        var frame = _resolvedStyle.GetFrame(frameIndex);
        var fg = theme.Get(SpinnerTheme.ForegroundColor);
        var bg = theme.Get(SpinnerTheme.BackgroundColor);
        var resetCodes = theme.GetResetToGlobalCodes();

        // Build output with colors
        var output = !fg.IsDefault || !bg.IsDefault
            ? $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{frame}{resetCodes}"
            : frame;

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }

    /// <summary>
    /// Gets the display width of a string, accounting for wide Unicode characters.
    /// </summary>
    private static int GetDisplayWidth(string text)
    {
        // Simple implementation - count characters
        // TODO: Use proper Unicode width calculation for emoji/CJK
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
