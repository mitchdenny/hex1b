using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for TilePanel zoom events.
/// </summary>
public sealed class TilePanelZoomEventArgs : WidgetEventArgs<TilePanelWidget, TilePanelNode>
{
    /// <summary>
    /// The new zoom level after the zoom change.
    /// </summary>
    public int NewZoomLevel { get; }

    /// <summary>
    /// The zoom delta (+1 for zoom in, -1 for zoom out).
    /// </summary>
    public int Delta { get; }

    internal TilePanelZoomEventArgs(
        int newZoomLevel,
        int delta,
        TilePanelWidget widget,
        TilePanelNode node,
        InputBindingActionContext context)
        : base(widget, node, context)
    {
        NewZoomLevel = newZoomLevel;
        Delta = delta;
    }
}
