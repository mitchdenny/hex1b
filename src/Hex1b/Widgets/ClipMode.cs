namespace Hex1b.Widgets;

/// <summary>
/// Determines how content that exceeds bounds is handled.
/// </summary>
public enum ClipMode
{
    /// <summary>
    /// Content that exceeds bounds is not rendered.
    /// </summary>
    Clip,
    
    /// <summary>
    /// Content is allowed to overflow (no clipping).
    /// </summary>
    Overflow,
    
    // Future: Wrap, Scroll, Ellipsis, etc.
}
