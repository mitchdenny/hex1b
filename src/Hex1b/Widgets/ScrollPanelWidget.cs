using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A scroll panel that provides scrolling capability for content that exceeds the available space.
/// Only supports one direction at a time (vertical or horizontal).
/// </summary>
public sealed record ScrollPanelWidget : Hex1bWidget
{
    public static readonly ActionId ScrollUpAction = new($"{nameof(ScrollPanelWidget)}.{nameof(ScrollUpAction)}");
    public static readonly ActionId ScrollDownAction = new($"{nameof(ScrollPanelWidget)}.{nameof(ScrollDownAction)}");
    public static readonly ActionId ScrollLeftAction = new($"{nameof(ScrollPanelWidget)}.{nameof(ScrollLeftAction)}");
    public static readonly ActionId ScrollRightAction = new($"{nameof(ScrollPanelWidget)}.{nameof(ScrollRightAction)}");
    public static readonly ActionId PageUpAction = new($"{nameof(ScrollPanelWidget)}.{nameof(PageUpAction)}");
    public static readonly ActionId PageDownAction = new($"{nameof(ScrollPanelWidget)}.{nameof(PageDownAction)}");
    public static readonly ActionId ScrollToStartAction = new($"{nameof(ScrollPanelWidget)}.{nameof(ScrollToStartAction)}");
    public static readonly ActionId ScrollToEndAction = new($"{nameof(ScrollPanelWidget)}.{nameof(ScrollToEndAction)}");
    public static readonly ActionId FocusNextAction = new($"{nameof(ScrollPanelWidget)}.{nameof(FocusNextAction)}");
    public static readonly ActionId FocusPreviousAction = new($"{nameof(ScrollPanelWidget)}.{nameof(FocusPreviousAction)}");
    public static readonly ActionId FocusFirstAction = new($"{nameof(ScrollPanelWidget)}.{nameof(FocusFirstAction)}");
    public static readonly ActionId MouseScrollUpAction = new($"{nameof(ScrollPanelWidget)}.{nameof(MouseScrollUpAction)}");
    public static readonly ActionId MouseScrollDownAction = new($"{nameof(ScrollPanelWidget)}.{nameof(MouseScrollDownAction)}");

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
    /// When true, the scroll panel automatically follows the end of content.
    /// The panel scrolls to the bottom when content grows, and disengages
    /// when the user scrolls away. Re-engages when the user scrolls back to the end.
    /// </summary>
    public bool IsFollowing { get; init; }
    
    /// <summary>
    /// The async scroll handler. Called when the scroll position changes.
    /// </summary>
    internal Func<ScrollChangedEventArgs, Task>? ScrollHandler { get; init; }

    /// <summary>
    /// Creates a new ScrollPanelWidget.
    /// </summary>
    /// <param name="child">The child widget to scroll.</param>
    /// <param name="orientation">The scroll orientation. Defaults to Vertical.</param>
    /// <param name="showScrollbar">Whether to show the scrollbar. Defaults to true.</param>
    public ScrollPanelWidget(
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
    /// <returns>A new ScrollPanelWidget with the handler set.</returns>
    public ScrollPanelWidget OnScroll(Action<ScrollChangedEventArgs> handler)
        => this with { ScrollHandler = args => { handler(args); return Task.CompletedTask; } };
    
    /// <summary>
    /// Sets an asynchronous scroll handler. Called when the scroll position changes.
    /// </summary>
    /// <param name="handler">The handler to call when scrolling occurs.</param>
    /// <returns>A new ScrollPanelWidget with the handler set.</returns>
    public ScrollPanelWidget OnScroll(Func<ScrollChangedEventArgs, Task> handler)
        => this with { ScrollHandler = handler };

    /// <summary>
    /// Enables follow mode: the scroll panel automatically scrolls to the end
    /// when content grows. Disengages when the user scrolls away, and re-engages
    /// when the user scrolls back to the end.
    /// </summary>
    /// <returns>A new ScrollPanelWidget with follow mode enabled.</returns>
    public ScrollPanelWidget Follow()
        => this with { IsFollowing = true };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ScrollPanelNode ?? new ScrollPanelNode();
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        node.SourceWidget = this;
        node.Orientation = Orientation;
        node.ShowScrollbar = ShowScrollbar;
        
        // Enable follow mode — on first reconciliation (new node), initialize IsFollowing.
        // On subsequent reconciliations, preserve the node's runtime IsFollowing state.
        if (IsFollowing)
        {
            node.FollowEnabled = true;
            if (context.IsNew)
                node.IsFollowing = true;
        }
        else
        {
            node.FollowEnabled = false;
            node.IsFollowing = false;
        }
        
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

    internal override Type GetExpectedNodeType() => typeof(ScrollPanelNode);
}
