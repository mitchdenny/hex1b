using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that wraps a child tree and makes the entire area focusable, clickable, and hoverable.
/// The builder lambda is deferred to reconciliation time so it can read current focus/hover state
/// from the <see cref="InteractableContext"/>.
/// </summary>
/// <example>
/// <code>
/// ctx.Interactable(ic =>
///     ic.VStack(v => [
///         v.Text("Resource: API"),
///         v.Text("Status: Running"),
///     ])
/// )
/// .OnClick(args => NavigateToResource())
/// .OnFocusChanged(args => LogFocusChange(args.IsFocused))
/// </code>
/// </example>
public sealed record InteractableWidget(Func<InteractableContext, Hex1bWidget> Builder) : Hex1bWidget
{
    /// <summary>
    /// The async click handler. Called when activated via Enter, Space, or mouse click.
    /// </summary>
    internal Func<InteractableClickedEventArgs, Task>? ClickHandler { get; init; }

    /// <summary>
    /// Synchronous focus change handler.
    /// </summary>
    internal Action<InteractableFocusChangedEventArgs>? FocusChangedHandler { get; init; }

    /// <summary>
    /// Synchronous hover change handler.
    /// </summary>
    internal Action<InteractableHoverChangedEventArgs>? HoverChangedHandler { get; init; }

    /// <summary>
    /// Sets a synchronous click handler.
    /// </summary>
    public InteractableWidget OnClick(Action<InteractableClickedEventArgs> handler)
        => this with { ClickHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous click handler.
    /// </summary>
    public InteractableWidget OnClick(Func<InteractableClickedEventArgs, Task> handler)
        => this with { ClickHandler = handler };

    /// <summary>
    /// Sets a focus change handler. Called when the interactable gains or loses focus.
    /// </summary>
    public InteractableWidget OnFocusChanged(Action<InteractableFocusChangedEventArgs> handler)
        => this with { FocusChangedHandler = handler };

    /// <summary>
    /// Sets a hover change handler. Called when the mouse enters or leaves the interactable area.
    /// </summary>
    public InteractableWidget OnHoverChanged(Action<InteractableHoverChangedEventArgs> handler)
        => this with { HoverChangedHandler = handler };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as InteractableNode ?? new InteractableNode();
        node.SourceWidget = this;

        // Create context populated with current node state (defaults on first render)
        var ic = new InteractableContext(node);

        // Build child widget — builder can read ic.IsFocused, ic.IsHovered
        var childWidget = Builder(ic);

        // Reconcile child
        node.Child = await context.ReconcileChildAsync(node.Child, childWidget, node);

        // Wire up click handler
        if (ClickHandler != null)
        {
            node.ClickAction = async ctx =>
            {
                var args = new InteractableClickedEventArgs(this, node, ctx);
                await ClickHandler(args);
            };
        }
        else
        {
            node.ClickAction = null;
        }

        // Wire up focus/hover change handlers
        if (FocusChangedHandler != null)
        {
            node.FocusChangedAction = isFocused =>
                FocusChangedHandler(new InteractableFocusChangedEventArgs(isFocused));
        }
        else
        {
            node.FocusChangedAction = null;
        }

        if (HoverChangedHandler != null)
        {
            node.HoverChangedAction = isHovered =>
                HoverChangedHandler(new InteractableHoverChangedEventArgs(isHovered));
        }
        else
        {
            node.HoverChangedAction = null;
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(InteractableNode);
}
