using Hex1b.Data;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for TilePanel point-of-interest click events.
/// </summary>
public sealed class TilePanelPoiClickedEventArgs
{
    /// <summary>
    /// The point of interest that was clicked.
    /// </summary>
    public TilePointOfInterest PointOfInterest { get; }

    /// <summary>
    /// The widget that raised the event.
    /// </summary>
    public TilePanelWidget Widget { get; }

    /// <summary>
    /// The node that raised the event.
    /// </summary>
    public TilePanelNode Node { get; }

    /// <summary>
    /// The input binding context providing access to app services (e.g., pushing popups).
    /// </summary>
    public InputBindingActionContext Context { get; }

    internal TilePanelPoiClickedEventArgs(
        TilePointOfInterest pointOfInterest,
        TilePanelWidget widget,
        TilePanelNode node,
        InputBindingActionContext context)
    {
        PointOfInterest = pointOfInterest;
        Widget = widget;
        Node = node;
        Context = context;
    }
}
