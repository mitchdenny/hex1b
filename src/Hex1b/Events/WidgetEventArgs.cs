using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Base class for all widget event arguments.
/// Provides access to the InputBindingActionContext for focus navigation, app control, and cancellation.
/// </summary>
public abstract class WidgetEventArgs
{
    /// <summary>
    /// The context providing access to focus navigation, RequestStop, and CancellationToken.
    /// </summary>
    public InputBindingActionContext Context { get; }

    /// <summary>
    /// Convenience accessor for the cancellation token from the application run loop.
    /// </summary>
    public CancellationToken CancellationToken => Context.CancellationToken;
    
    /// <summary>
    /// Convenience accessor for the popup stack of the nearest popup host.
    /// Use this to push popups, menus, and dialogs from event handlers.
    /// The root ZStack automatically provides a PopupStack, so this is never null within a Hex1bApp.
    /// </summary>
    public PopupStack Popups => Context.Popups;

    protected WidgetEventArgs(InputBindingActionContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }
}

/// <summary>
/// Strongly-typed base class for widget event arguments.
/// Provides typed access to both the widget configuration and the node instance.
/// </summary>
/// <typeparam name="TWidget">The widget type (e.g., ButtonWidget).</typeparam>
/// <typeparam name="TNode">The node type (e.g., ButtonNode).</typeparam>
public abstract class WidgetEventArgs<TWidget, TNode> : WidgetEventArgs
    where TWidget : Hex1bWidget
    where TNode : Hex1bNode
{
    /// <summary>
    /// The widget configuration that triggered this event.
    /// This is the immutable record describing the UI element.
    /// </summary>
    public TWidget Widget { get; }

    /// <summary>
    /// The node instance that raised this event.
    /// This is the mutable stateful object that manages the widget's lifecycle.
    /// </summary>
    public TNode Node { get; }

    protected WidgetEventArgs(TWidget widget, TNode node, InputBindingActionContext context)
        : base(context)
    {
        Widget = widget ?? throw new ArgumentNullException(nameof(widget));
        Node = node ?? throw new ArgumentNullException(nameof(node));
    }
    
    /// <summary>
    /// Pushes an anchored popup positioned relative to the node that triggered this event.
    /// This is a convenience method for the common pattern of anchoring a menu to the clicked button.
    /// </summary>
    /// <param name="position">Where to position the popup relative to this node.</param>
    /// <param name="contentBuilder">A function that builds the widget content for the popup.</param>
    /// <returns>The popup entry for optional fluent configuration (e.g., <c>.AsBarrier()</c>).</returns>
    /// <example>
    /// <code>
    /// menuBar.Button(" File ")
    ///     .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildFileMenu()));
    /// 
    /// // Or with barrier for modal-like behavior:
    /// menuBar.Button(" Dialog ")
    ///     .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildDialog()).AsBarrier());
    /// </code>
    /// </example>
    public PopupEntry PushAnchored(AnchorPosition position, Func<Hex1bWidget> contentBuilder)
    {
        return Popups.PushAnchored(Node, position, contentBuilder);
    }
    
    /// <summary>
    /// Pushes an anchored popup positioned relative to the node that triggered this event.
    /// This is a convenience method for the common pattern of anchoring a menu to the clicked button.
    /// </summary>
    /// <param name="position">Where to position the popup relative to this node.</param>
    /// <param name="content">The widget content for the popup.</param>
    /// <returns>The popup entry for optional fluent configuration (e.g., <c>.AsBarrier()</c>).</returns>
    public PopupEntry PushAnchored(AnchorPosition position, Hex1bWidget content)
    {
        return Popups.PushAnchored(Node, position, content);
    }
}
