using Hex1b.Events;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Specifies how the backdrop is visually rendered.
/// </summary>
public enum BackdropStyle
{
    /// <summary>
    /// Transparent backdrop - base layer content shows through unchanged.
    /// Clicks are still captured.
    /// </summary>
    Transparent,
    
    /// <summary>
    /// Solid color backdrop - completely covers the base layer.
    /// </summary>
    Opaque
}

/// <summary>
/// A widget that fills its available space and intercepts all input.
/// Used as a modal backdrop to prevent interaction with layers below.
/// Can optionally display a background color (for dimming effects) and
/// trigger a callback when clicked (for "click-away to dismiss" behavior).
/// </summary>
/// <param name="Child">Optional child widget to display on top of the backdrop.</param>
public sealed record BackdropWidget(Hex1bWidget? Child = null) : Hex1bWidget
{
    /// <summary>
    /// The visual style for the backdrop.
    /// </summary>
    internal BackdropStyle Style { get; init; } = BackdropStyle.Transparent;
    
    /// <summary>
    /// The background color for the backdrop (used when Style is Opaque).
    /// </summary>
    internal Hex1bColor? BackgroundColor { get; init; }
    
    /// <summary>
    /// Optional layer identifier for popup stack management.
    /// </summary>
    internal string? LayerId { get; init; }

    /// <summary>
    /// Simple callback invoked when the backdrop itself is clicked (not the child content).
    /// Use this for simple "click away to dismiss" behavior.
    /// </summary>
    internal Func<Task>? ClickAwayHandler { get; init; }
    
    /// <summary>
    /// Rich callback with event args invoked when the backdrop is clicked.
    /// Use this when you need click coordinates or popup stack integration.
    /// </summary>
    internal Func<BackdropClickedEventArgs, Task>? ClickAwayEventHandler { get; init; }

    /// <summary>
    /// Sets the backdrop to transparent mode - base layer shows through unchanged.
    /// </summary>
    /// <returns>A new BackdropWidget with transparent style.</returns>
    public BackdropWidget Transparent()
        => this with { Style = BackdropStyle.Transparent, BackgroundColor = null };

    /// <summary>
    /// Sets the backdrop to opaque mode with the specified background color.
    /// </summary>
    /// <param name="color">The background color.</param>
    /// <returns>A new BackdropWidget with opaque style.</returns>
    public BackdropWidget Opaque(Hex1bColor color)
        => this with { Style = BackdropStyle.Opaque, BackgroundColor = color };

    /// <summary>
    /// Sets the background color for the backdrop (implies opaque mode).
    /// </summary>
    /// <param name="color">The background color.</param>
    /// <returns>A new BackdropWidget with the background color set.</returns>
    public BackdropWidget WithBackground(Hex1bColor color)
        => this with { Style = BackdropStyle.Opaque, BackgroundColor = color };
    
    /// <summary>
    /// Sets a layer identifier for this backdrop.
    /// Used by PopupStack to identify which layer was clicked.
    /// </summary>
    /// <param name="id">The layer identifier.</param>
    /// <returns>A new BackdropWidget with the layer ID set.</returns>
    public BackdropWidget WithLayerId(string id)
        => this with { LayerId = id };

    /// <summary>
    /// Sets a simple callback to be invoked when clicking on the backdrop (outside child content).
    /// </summary>
    /// <param name="handler">The click-away handler.</param>
    /// <returns>A new BackdropWidget with the handler set.</returns>
    public BackdropWidget OnClickAway(Action handler)
        => this with { ClickAwayHandler = () => { handler(); return Task.CompletedTask; }, ClickAwayEventHandler = null };

    /// <summary>
    /// Sets a simple async callback to be invoked when clicking on the backdrop (outside child content).
    /// </summary>
    /// <param name="handler">The async click-away handler.</param>
    /// <returns>A new BackdropWidget with the handler set.</returns>
    public BackdropWidget OnClickAway(Func<Task> handler)
        => this with { ClickAwayHandler = handler, ClickAwayEventHandler = null };
    
    /// <summary>
    /// Sets a rich callback with event args to be invoked when clicking on the backdrop.
    /// The event args include click coordinates and the layer ID for popup stack integration.
    /// </summary>
    /// <param name="handler">The click-away event handler.</param>
    /// <returns>A new BackdropWidget with the handler set.</returns>
    public BackdropWidget OnClickAway(Action<BackdropClickedEventArgs> handler)
        => this with { ClickAwayEventHandler = args => { handler(args); return Task.CompletedTask; }, ClickAwayHandler = null };
    
    /// <summary>
    /// Sets a rich async callback with event args to be invoked when clicking on the backdrop.
    /// The event args include click coordinates and the layer ID for popup stack integration.
    /// </summary>
    /// <param name="handler">The async click-away event handler.</param>
    /// <returns>A new BackdropWidget with the handler set.</returns>
    public BackdropWidget OnClickAway(Func<BackdropClickedEventArgs, Task> handler)
        => this with { ClickAwayEventHandler = handler, ClickAwayHandler = null };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as BackdropNode ?? new BackdropNode();

        // Reconcile properties
        if (node.Style != Style || !Nullable.Equals(node.BackgroundColor, BackgroundColor))
        {
            node.MarkDirty();
        }
        node.Style = Style;
        node.BackgroundColor = BackgroundColor;
        node.LayerId = LayerId;
        node.SourceWidget = this;
        node.ClickAwayHandler = ClickAwayHandler;
        node.ClickAwayEventHandler = ClickAwayEventHandler;

        // Reconcile child if present
        if (Child != null)
        {
            var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
            node.Child = await childContext.ReconcileChildAsync(node.Child, Child, node);
        }
        else
        {
            if (node.Child != null)
            {
                node.AddOrphanedChildBounds(node.Child.Bounds);
                node.Child = null;
            }
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(BackdropNode);
}
