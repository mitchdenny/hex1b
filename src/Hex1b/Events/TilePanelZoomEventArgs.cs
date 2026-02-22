using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for TilePanel zoom events.
/// </summary>
public sealed class TilePanelZoomEventArgs
{
    /// <summary>
    /// The new zoom level after the zoom change.
    /// </summary>
    public int NewZoomLevel { get; }

    /// <summary>
    /// The zoom delta (+1 for zoom in, -1 for zoom out).
    /// </summary>
    public int Delta { get; }

    /// <summary>
    /// The widget that raised the event.
    /// </summary>
    public TilePanelWidget Widget { get; }

    /// <summary>
    /// The node that raised the event.
    /// </summary>
    public TilePanelNode Node { get; }

    /// <summary>
    /// The input binding context providing access to app services.
    /// </summary>
    public InputBindingActionContext Context { get; }

    internal TilePanelZoomEventArgs(
        int newZoomLevel,
        int delta,
        TilePanelWidget widget,
        TilePanelNode node,
        InputBindingActionContext context)
    {
        NewZoomLevel = newZoomLevel;
        Delta = delta;
        Widget = widget;
        Node = node;
        Context = context;
    }
}
