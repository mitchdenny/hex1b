using Hex1b.Data;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="TilePanelWidget"/> instances.
/// </summary>
public static class TilePanelExtensions
{
    /// <summary>
    /// Creates a TilePanel with the specified data source at the default camera position.
    /// </summary>
    public static TilePanelWidget TilePanel<TParent>(
        this WidgetContext<TParent> ctx,
        ITileDataSource dataSource)
        where TParent : Hex1bWidget
        => new() { DataSource = dataSource, HeightHint = SizeHint.Fill };

    /// <summary>
    /// Creates a TilePanel with the specified data source and camera position.
    /// </summary>
    public static TilePanelWidget TilePanel<TParent>(
        this WidgetContext<TParent> ctx,
        ITileDataSource dataSource,
        double cameraX,
        double cameraY,
        int zoomLevel = 0)
        where TParent : Hex1bWidget
        => new()
        {
            DataSource = dataSource,
            CameraX = cameraX,
            CameraY = cameraY,
            ZoomLevel = zoomLevel,
            HeightHint = SizeHint.Fill,
        };
}
