namespace Hex1b.Widgets;

/// <summary>
/// How text should handle horizontal overflow.
/// </summary>
public enum TextOverflow
{
    /// <summary>
    /// Text extends beyond bounds (default, for backward compatibility).
    /// Clipping is handled by parent LayoutNode if present.
    /// </summary>
    Overflow,
    
    /// <summary>
    /// Text wraps to next line when it exceeds available width.
    /// This affects the measured height of the node.
    /// </summary>
    Wrap,
    
    /// <summary>
    /// Text is truncated with ellipsis when it exceeds available width.
    /// </summary>
    Ellipsis,
}
