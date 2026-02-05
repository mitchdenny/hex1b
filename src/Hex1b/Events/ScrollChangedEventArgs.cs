using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for scroll position changes.
/// Provides information about the current scroll state when scrolling occurs.
/// </summary>
public sealed class ScrollChangedEventArgs : WidgetEventArgs<ScrollPanelWidget, ScrollPanelNode>
{
    /// <summary>
    /// The current scroll offset (in characters).
    /// For vertical scrolling, this is the row offset.
    /// For horizontal scrolling, this is the column offset.
    /// </summary>
    public int Offset { get; }
    
    /// <summary>
    /// The previous scroll offset before this change.
    /// </summary>
    public int PreviousOffset { get; }
    
    /// <summary>
    /// The size of the content being scrolled (in characters).
    /// For vertical scrolling, this is the total content height.
    /// For horizontal scrolling, this is the total content width.
    /// </summary>
    public int ContentSize { get; }
    
    /// <summary>
    /// The size of the visible viewport (in characters).
    /// For vertical scrolling, this is the viewport height.
    /// For horizontal scrolling, this is the viewport width.
    /// </summary>
    public int ViewportSize { get; }
    
    /// <summary>
    /// Whether the content is scrollable (content exceeds viewport).
    /// </summary>
    public bool IsScrollable => ContentSize > ViewportSize;
    
    /// <summary>
    /// The maximum scroll offset.
    /// </summary>
    public int MaxOffset => Math.Max(0, ContentSize - ViewportSize);
    
    /// <summary>
    /// The scroll progress as a value between 0 and 1.
    /// Returns 0 when at the start, 1 when at the end.
    /// </summary>
    public double Progress => MaxOffset > 0 ? (double)Offset / MaxOffset : 0;
    
    /// <summary>
    /// Whether the scroll position is at the start (offset is 0).
    /// </summary>
    public bool IsAtStart => Offset <= 0;
    
    /// <summary>
    /// Whether the scroll position is at the end (offset equals max offset).
    /// </summary>
    public bool IsAtEnd => Offset >= MaxOffset;

    /// <summary>
    /// Creates a new ScrollChangedEventArgs.
    /// </summary>
    public ScrollChangedEventArgs(
        ScrollPanelWidget widget,
        ScrollPanelNode node,
        InputBindingActionContext context,
        int offset,
        int previousOffset,
        int contentSize,
        int viewportSize)
        : base(widget, node, context)
    {
        Offset = offset;
        PreviousOffset = previousOffset;
        ContentSize = contentSize;
        ViewportSize = viewportSize;
    }
}
