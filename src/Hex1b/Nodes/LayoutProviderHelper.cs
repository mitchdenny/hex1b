using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Provides common clipping logic for layout providers.
/// Handles intersection with parent layout providers automatically.
/// </summary>
public static class LayoutProviderHelper
{
    /// <summary>
    /// Computes the effective clip rect by intersecting this provider's clip rect
    /// with its parent's clip rect (if any).
    /// </summary>
    public static Rect GetEffectiveClipRect(ILayoutProvider provider)
    {
        var clipRect = provider.ClipRect;
        
        if (provider.ParentLayoutProvider != null)
        {
            var parentRect = GetEffectiveClipRect(provider.ParentLayoutProvider);
            clipRect = IntersectRects(clipRect, parentRect);
        }
        
        return clipRect;
    }
    
    /// <summary>
    /// Determines if a character at the given absolute position should be rendered,
    /// considering both this provider's clip rect and any parent's.
    /// </summary>
    public static bool ShouldRenderAt(ILayoutProvider provider, int x, int y)
    {
        if (provider.ClipMode == ClipMode.Overflow && 
            (provider.ParentLayoutProvider == null || provider.ParentLayoutProvider.ClipMode == ClipMode.Overflow))
            return true;
        
        var effectiveRect = GetEffectiveClipRect(provider);
        
        return x >= effectiveRect.X && 
               x < effectiveRect.X + effectiveRect.Width &&
               y >= effectiveRect.Y && 
               y < effectiveRect.Y + effectiveRect.Height;
    }
    
    /// <summary>
    /// Clips a string to the effective clip rect (intersection of this provider and parent).
    /// </summary>
    public static (int adjustedX, string clippedText) ClipString(ILayoutProvider provider, int x, int y, string text)
    {
        if (provider.ClipMode == ClipMode.Overflow && 
            (provider.ParentLayoutProvider == null || provider.ParentLayoutProvider.ClipMode == ClipMode.Overflow))
            return (x, text);
        
        var effectiveRect = GetEffectiveClipRect(provider);
        
        // If entire line is outside vertical bounds, return empty
        if (y < effectiveRect.Y || y >= effectiveRect.Y + effectiveRect.Height)
            return (x, "");
            
        var clipLeft = effectiveRect.X;
        var clipRight = effectiveRect.X + effectiveRect.Width;

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
    
    /// <summary>
    /// Computes the intersection of two rectangles.
    /// Returns a zero-sized rect if they don't overlap.
    /// </summary>
    private static Rect IntersectRects(Rect a, Rect b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        
        var width = Math.Max(0, right - left);
        var height = Math.Max(0, bottom - top);
        
        return new Rect(left, top, width, height);
    }
}
