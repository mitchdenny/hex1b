using Hex1b.Data;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for TilePanel point-of-interest click events.
/// </summary>
public sealed class TilePanelPoiClickedEventArgs : WidgetEventArgs<TilePanelWidget, TilePanelNode>
{
    /// <summary>
    /// The point of interest that was clicked.
    /// </summary>
    public TilePointOfInterest PointOfInterest { get; }

    internal TilePanelPoiClickedEventArgs(
        TilePointOfInterest pointOfInterest,
        TilePanelWidget widget,
        TilePanelNode node,
        InputBindingActionContext context)
        : base(widget, node, context)
    {
        PointOfInterest = pointOfInterest;
    }
}
