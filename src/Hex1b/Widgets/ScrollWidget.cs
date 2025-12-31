using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A scroll widget that provides scrolling capability for content that exceeds the available space.
/// Only supports one direction at a time (vertical or horizontal).
/// </summary>
public sealed record ScrollWidget : Hex1bWidget
{
    /// <summary>
    /// The child widget to scroll.
    /// </summary>
    public Hex1bWidget Child { get; }
    
    /// <summary>
    /// The scroll orientation (vertical or horizontal).
    /// </summary>
    public ScrollOrientation Orientation { get; init; }
    
    /// <summary>
    /// Whether to show the scrollbar when content is scrollable.
    /// </summary>
    public bool ShowScrollbar { get; init; }
    
    /// <summary>
    /// The async scroll handler. Called when the scroll position changes.
    /// </summary>
    internal Func<ScrollChangedEventArgs, Task>? ScrollHandler { get; init; }

    /// <summary>
    /// Creates a new ScrollWidget.
    /// </summary>
    /// <param name="child">The child widget to scroll.</param>
    /// <param name="orientation">The scroll orientation. Defaults to Vertical.</param>
    /// <param name="showScrollbar">Whether to show the scrollbar. Defaults to true.</param>
    public ScrollWidget(
        Hex1bWidget child,
        ScrollOrientation orientation = ScrollOrientation.Vertical,
        bool showScrollbar = true)
    {
        Child = child;
        Orientation = orientation;
        ShowScrollbar = showScrollbar;
    }
    
    /// <summary>
    /// Sets a synchronous scroll handler. Called when the scroll position changes.
    /// </summary>
    /// <param name="handler">The handler to call when scrolling occurs.</param>
    /// <returns>A new ScrollWidget with the handler set.</returns>
    public ScrollWidget OnScroll(Action<ScrollChangedEventArgs> handler)
        => this with { ScrollHandler = args => { handler(args); return Task.CompletedTask; } };
    
    /// <summary>
    /// Sets an asynchronous scroll handler. Called when the scroll position changes.
    /// </summary>
    /// <param name="handler">The handler to call when scrolling occurs.</param>
    /// <returns>A new ScrollWidget with the handler set.</returns>
    public ScrollWidget OnScroll(Func<ScrollChangedEventArgs, Task> handler)
        => this with { ScrollHandler = handler };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ScrollNode ?? new ScrollNode();
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        node.SourceWidget = this;
        node.Orientation = Orientation;
        node.ShowScrollbar = ShowScrollbar;
        
        // Convert the typed event handler to the internal InputBindingActionContext handler
        if (ScrollHandler != null)
        {
            node.ScrollAction = async (ctx, offset, previousOffset, contentSize, viewportSize) =>
            {
                var args = new ScrollChangedEventArgs(this, node, ctx, offset, previousOffset, contentSize, viewportSize);
                await ScrollHandler(args);
            };
        }
        else
        {
            node.ScrollAction = null;
        }
        
        // Invalidate focus cache since children may have changed
        node.InvalidateFocusCache();
        
        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            node.SetInitialFocus();
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ScrollNode);
}
