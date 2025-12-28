using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b;

/// <summary>
/// Render node for displaying progress bars. Created by reconciling a <see cref="Widgets.ProgressWidget"/>.
/// </summary>
/// <remarks>
/// <para>
/// ProgressNode handles measuring and rendering progress bars in both determinate and indeterminate modes.
/// </para>
/// <para>
/// This node is not focusable and does not handle input. It is a display-only widget.
/// </para>
/// </remarks>
/// <seealso cref="Widgets.ProgressWidget"/>
public sealed class ProgressNode : Hex1bNode
{
    /// <summary>
    /// Gets or sets the current progress value.
    /// </summary>
    public double Value { get; set; }
    
    /// <summary>
    /// Gets or sets the minimum value of the progress range.
    /// </summary>
    public double Minimum { get; set; }
    
    /// <summary>
    /// Gets or sets the maximum value of the progress range.
    /// </summary>
    public double Maximum { get; set; } = 100.0;
    
    /// <summary>
    /// Gets or sets whether the progress bar is in indeterminate mode.
    /// </summary>
    public bool IsIndeterminate { get; set; }
    
    /// <summary>
    /// Gets or sets the animation position for indeterminate mode (0.0 to 1.0).
    /// </summary>
    public double AnimationPosition { get; set; }

    /// <summary>
    /// Measures the size required for the progress bar.
    /// </summary>
    /// <param name="constraints">The size constraints for layout.</param>
    /// <returns>
    /// The measured size. The progress bar fills available width and has a height of 1.
    /// </returns>
    /// <remarks>
    /// The progress bar is designed to fill horizontal space by default. Use layout
    /// extensions like <c>FixedWidth()</c> to constrain its width.
    /// </remarks>
    public override Size Measure(Constraints constraints)
    {
        // Fill available width, height is always 1
        var width = constraints.MaxWidth == int.MaxValue ? 20 : constraints.MaxWidth;
        return constraints.Constrain(new Size(width, 1));
    }

    /// <summary>
    /// Renders the progress bar to the terminal.
    /// </summary>
    /// <param name="context">The render context providing terminal access and theming.</param>
    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        // Get theme elements
        var filledChar = theme.Get(ProgressTheme.FilledCharacter);
        var emptyChar = theme.Get(ProgressTheme.EmptyCharacter);
        var indeterminateChar = theme.Get(ProgressTheme.IndeterminateCharacter);
        var filledFg = theme.Get(ProgressTheme.FilledForegroundColor);
        var filledBg = theme.Get(ProgressTheme.FilledBackgroundColor);
        var emptyFg = theme.Get(ProgressTheme.EmptyForegroundColor);
        var emptyBg = theme.Get(ProgressTheme.EmptyBackgroundColor);
        var indeterminateFg = theme.Get(ProgressTheme.IndeterminateForegroundColor);
        var indeterminateBg = theme.Get(ProgressTheme.IndeterminateBackgroundColor);
        
        var resetCodes = theme.GetResetToGlobalCodes();
        
        string output;
        
        if (IsIndeterminate)
        {
            output = RenderIndeterminate(
                Bounds.Width, 
                indeterminateChar, 
                emptyChar,
                indeterminateFg, 
                indeterminateBg,
                emptyFg,
                emptyBg,
                resetCodes);
        }
        else
        {
            output = RenderDeterminate(
                Bounds.Width,
                filledChar,
                emptyChar,
                filledFg,
                filledBg,
                emptyFg,
                emptyBg,
                resetCodes);
        }
        
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }
    
    private string RenderDeterminate(
        int width,
        char filledChar,
        char emptyChar,
        Hex1bColor filledFg,
        Hex1bColor filledBg,
        Hex1bColor emptyFg,
        Hex1bColor emptyBg,
        string resetCodes)
    {
        if (width <= 0) return "";
        
        // Calculate fill percentage
        var range = Maximum - Minimum;
        var percentage = range > 0 ? Math.Clamp((Value - Minimum) / range, 0.0, 1.0) : 0.0;
        var filledWidth = (int)Math.Round(percentage * width);
        var emptyWidth = width - filledWidth;
        
        // Build the progress bar string
        var filledPart = filledWidth > 0
            ? $"{filledFg.ToForegroundAnsi()}{filledBg.ToBackgroundAnsi()}{new string(filledChar, filledWidth)}"
            : "";
            
        var emptyPart = emptyWidth > 0
            ? $"{emptyFg.ToForegroundAnsi()}{emptyBg.ToBackgroundAnsi()}{new string(emptyChar, emptyWidth)}"
            : "";
        
        return $"{filledPart}{emptyPart}{resetCodes}";
    }
    
    private string RenderIndeterminate(
        int width,
        char indeterminateChar,
        char emptyChar,
        Hex1bColor indeterminateFg,
        Hex1bColor indeterminateBg,
        Hex1bColor emptyFg,
        Hex1bColor emptyBg,
        string resetCodes)
    {
        if (width <= 0) return "";
        
        // Calculate the position of the animated segment
        // The segment is 3 characters wide and bounces back and forth
        var segmentWidth = Math.Min(3, width);
        var travelDistance = width - segmentWidth;
        
        int segmentStart;
        if (travelDistance <= 0)
        {
            segmentStart = 0;
        }
        else
        {
            // Use a ping-pong animation (0->1->0)
            var normalizedPos = AnimationPosition * 2;
            if (normalizedPos > 1) normalizedPos = 2 - normalizedPos;
            segmentStart = (int)Math.Round(normalizedPos * travelDistance);
        }
        
        // Build the output
        var chars = new char[width];
        for (int i = 0; i < width; i++)
        {
            chars[i] = (i >= segmentStart && i < segmentStart + segmentWidth) 
                ? indeterminateChar 
                : emptyChar;
        }
        
        // Build with colors - segment colored differently
        var result = new System.Text.StringBuilder();
        var inSegment = false;
        
        for (int i = 0; i < width; i++)
        {
            var isInSegment = i >= segmentStart && i < segmentStart + segmentWidth;
            
            if (isInSegment && !inSegment)
            {
                // Entering segment
                result.Append(indeterminateFg.ToForegroundAnsi());
                result.Append(indeterminateBg.ToBackgroundAnsi());
                inSegment = true;
            }
            else if (!isInSegment && inSegment)
            {
                // Exiting segment
                result.Append(emptyFg.ToForegroundAnsi());
                result.Append(emptyBg.ToBackgroundAnsi());
                inSegment = false;
            }
            else if (i == 0 && !isInSegment)
            {
                // Starting with empty
                result.Append(emptyFg.ToForegroundAnsi());
                result.Append(emptyBg.ToBackgroundAnsi());
            }
            
            result.Append(chars[i]);
        }
        
        result.Append(resetCodes);
        return result.ToString();
    }
}
