namespace Hex1b.Widgets;

/// <summary>
/// State for tracking scroll position.
/// </summary>
public class ScrollState
{
    /// <summary>
    /// The current scroll offset (in characters).
    /// For vertical scrolling, this is the row offset.
    /// For horizontal scrolling, this is the column offset.
    /// </summary>
    public int Offset { get; set; }
    
    /// <summary>
    /// The size of the content being scrolled (in characters).
    /// This is set by the ScrollNode during layout.
    /// </summary>
    public int ContentSize { get; internal set; }
    
    /// <summary>
    /// The size of the visible viewport (in characters).
    /// This is set by the ScrollNode during layout.
    /// </summary>
    public int ViewportSize { get; internal set; }
    
    /// <summary>
    /// Whether the scrollbar is currently needed (content exceeds viewport).
    /// </summary>
    public bool IsScrollable => ContentSize > ViewportSize;
    
    /// <summary>
    /// The maximum scroll offset.
    /// </summary>
    public int MaxOffset => Math.Max(0, ContentSize - ViewportSize);
    
    /// <summary>
    /// Scroll up (or left) by the specified amount.
    /// </summary>
    public void ScrollUp(int amount = 1)
    {
        Offset = Math.Max(0, Offset - amount);
    }
    
    /// <summary>
    /// Scroll down (or right) by the specified amount.
    /// </summary>
    public void ScrollDown(int amount = 1)
    {
        Offset = Math.Min(MaxOffset, Offset + amount);
    }
    
    /// <summary>
    /// Scroll to the beginning.
    /// </summary>
    public void ScrollToStart()
    {
        Offset = 0;
    }
    
    /// <summary>
    /// Scroll to the end.
    /// </summary>
    public void ScrollToEnd()
    {
        Offset = MaxOffset;
    }
    
    /// <summary>
    /// Scroll up by a full page (viewport size).
    /// </summary>
    public void PageUp()
    {
        ScrollUp(Math.Max(1, ViewportSize - 1));
    }
    
    /// <summary>
    /// Scroll down by a full page (viewport size).
    /// </summary>
    public void PageDown()
    {
        ScrollDown(Math.Max(1, ViewportSize - 1));
    }
}
