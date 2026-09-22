using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

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
    /// When the popup is dismissed, focus will be restored to the node that was focused when the popup was opened.
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
        return Popups.PushAnchored(Node, position, contentBuilder, Context.FocusedNode);
    }
    
    /// <summary>
    /// Pushes an anchored popup positioned relative to the node that triggered this event.
    /// This is a convenience method for the common pattern of anchoring a menu to the clicked button.
    /// When the popup is dismissed, focus will be restored to the node that was focused when the popup was opened.
    /// </summary>
    /// <param name="position">Where to position the popup relative to this node.</param>
    /// <param name="content">The widget content for the popup.</param>
    /// <returns>The popup entry for optional fluent configuration (e.g., <c>.AsBarrier()</c>).</returns>
    public PopupEntry PushAnchored(AnchorPosition position, Hex1bWidget content)
    {
        return Popups.PushAnchored(Node, position, () => content, Context.FocusedNode);
    }
}
