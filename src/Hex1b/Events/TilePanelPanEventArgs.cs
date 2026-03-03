using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for TilePanel pan events.
/// </summary>
public sealed class TilePanelPanEventArgs : WidgetEventArgs<TilePanelWidget, TilePanelNode>
{
    /// <summary>
    /// The horizontal pan delta in tile units.
    /// </summary>
    public double DeltaX { get; }

    /// <summary>
    /// The vertical pan delta in tile units.
    /// </summary>
    public double DeltaY { get; }

    internal TilePanelPanEventArgs(
        double deltaX,
        double deltaY,
        TilePanelWidget widget,
        TilePanelNode node,
        InputBindingActionContext context)
        : base(widget, node, context)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }
}
