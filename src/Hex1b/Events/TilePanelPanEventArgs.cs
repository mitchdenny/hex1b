using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for TilePanel pan events.
/// </summary>
public sealed class TilePanelPanEventArgs
{
    /// <summary>
    /// The horizontal pan delta in tile units.
    /// </summary>
    public double DeltaX { get; }

    /// <summary>
    /// The vertical pan delta in tile units.
    /// </summary>
    public double DeltaY { get; }

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

    internal TilePanelPanEventArgs(
        double deltaX,
        double deltaY,
        TilePanelWidget widget,
        TilePanelNode node,
        InputBindingActionContext context)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
        Widget = widget;
        Node = node;
        Context = context;
    }
}
