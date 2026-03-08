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

    /// <summary>
    /// The X coordinate in tile-space that the zoom should pivot around.
    /// For mouse-initiated zooms, this is the tile-space position under the cursor.
    /// For keyboard zooms, this equals <see cref="TilePanelNode.CameraX"/> (viewport center).
    /// </summary>
    public double PivotX { get; }

    /// <summary>
    /// The Y coordinate in tile-space that the zoom should pivot around.
    /// For mouse-initiated zooms, this is the tile-space position under the cursor.
    /// For keyboard zooms, this equals <see cref="TilePanelNode.CameraY"/> (viewport center).
    /// </summary>
    public double PivotY { get; }

    internal TilePanelZoomEventArgs(
        int newZoomLevel,
        int delta,
        double pivotX,
        double pivotY,
        TilePanelWidget widget,
        TilePanelNode node,
        InputBindingActionContext context)
        : base(widget, node, context)
    {
        NewZoomLevel = newZoomLevel;
        Delta = delta;
        PivotX = pivotX;
        PivotY = pivotY;
    }
}
