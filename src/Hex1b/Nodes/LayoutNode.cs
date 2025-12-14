using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Interface for nodes that provide layout/clipping services to descendants.
/// Child nodes can query their ancestor layout node to determine rendering behavior.
/// </summary>
public interface ILayoutProvider
{
    /// <summary>
    /// The effective clipping rectangle for this layout region.
    /// </summary>
    Rect ClipRect { get; }
    
    /// <summary>
    /// The clip mode for this layout region.
    /// </summary>
    ClipMode ClipMode { get; }
    
    /// <summary>
    /// Determines if a character at the given absolute position should be rendered.
    /// </summary>
    /// <param name="x">Absolute X position in terminal coordinates.</param>
    /// <param name="y">Absolute Y position in terminal coordinates.</param>
    /// <returns>True if the character should be rendered, false if it should be clipped.</returns>
    bool ShouldRenderAt(int x, int y);
    
    /// <summary>
    /// Clips a string that starts at the given position, returning only the visible portion.
    /// </summary>
    /// <param name="x">Starting absolute X position.</param>
    /// <param name="y">Absolute Y position.</param>
    /// <param name="text">The text to potentially clip.</param>
    /// <returns>
    /// A tuple containing:
    /// - adjustedX: The X position to start rendering (may be > x if left-clipped)
    /// - clippedText: The portion of text that should be rendered (may be empty)
    /// </returns>
    (int adjustedX, string clippedText) ClipString(int x, int y, string text);
}

/// <summary>
/// A node that provides clipping and rendering assistance to its children.
/// </summary>
public sealed class LayoutNode : Hex1bNode, ILayoutProvider
{
    public Hex1bNode? Child { get; set; }
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;
    
    /// <summary>
    /// The clip rectangle, defaults to Bounds but could be overridden for scrolling.
    /// </summary>
    public Rect ClipRect => Bounds;

    public bool ShouldRenderAt(int x, int y)
    {
        if (ClipMode == ClipMode.Overflow)
            return true;
            
        return x >= ClipRect.X && 
               x < ClipRect.X + ClipRect.Width &&
               y >= ClipRect.Y && 
               y < ClipRect.Y + ClipRect.Height;
    }

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
    {
        if (ClipMode == ClipMode.Overflow)
            return (x, text);
            
        // If entire line is outside vertical bounds, return empty
        if (y < ClipRect.Y || y >= ClipRect.Y + ClipRect.Height)
            return (x, "");
            
        var clipLeft = ClipRect.X;
        var clipRight = ClipRect.X + ClipRect.Width;

        // Entirely outside horizontal bounds (based on starting X)
        if (x >= clipRight)
            return (x, "");

        // Clip by visible columns (ANSI-aware) so we never cut escape sequences.
        var visibleLength = AnsiString.VisibleLength(text);
        if (visibleLength <= 0)
            return (x, "");

        var startColumn = Math.Max(0, clipLeft - x);
        var endColumnExclusive = Math.Min(visibleLength, clipRight - x);

        // Entirely left of the clip region.
        if (endColumnExclusive <= 0)
            return (x, "");

        if (endColumnExclusive <= startColumn)
            return (x, "");

        var sliceLength = endColumnExclusive - startColumn;
        
        // Use SliceByDisplayWidth to properly handle wide characters and get padding info
        var (slicedText, _, paddingBefore, paddingAfter) = 
            DisplayWidth.SliceByDisplayWidthWithAnsi(text, startColumn, sliceLength);
        
        if (slicedText.Length == 0 && paddingBefore == 0)
            return (x, "");

        // Build the result with padding if needed
        var clippedText = new string(' ', paddingBefore) + slicedText + new string(' ', paddingAfter);

        // If we clipped away printable characters on the right, preserve any trailing
        // escape suffix (typically a reset-to-inherited) to avoid style leaking.
        if (endColumnExclusive < visibleLength)
        {
            var suffix = AnsiString.TrailingEscapeSuffix(text);
            if (!string.IsNullOrEmpty(suffix) && !clippedText.EndsWith(suffix, StringComparison.Ordinal))
                clippedText += suffix;
        }

        var adjustedX = x + startColumn;
        return (adjustedX, clippedText);
    }

    public override Size Measure(Constraints constraints)
    {
        return Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        Child?.Arrange(bounds);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Store ourselves as the current layout provider in the context
        var previousLayout = context.CurrentLayoutProvider;
        context.CurrentLayoutProvider = this;
        
        Child?.Render(context);
        
        context.CurrentLayoutProvider = previousLayout;
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }
}
