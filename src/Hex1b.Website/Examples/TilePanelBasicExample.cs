using Hex1b;
using Hex1b.Data;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// TilePanel Widget Documentation: Basic Usage
/// Demonstrates a simple pannable and zoomable tile map.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/tilepanel.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TilePanelBasicExample(ILogger<TilePanelBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<TilePanelBasicExample> _logger = logger;

    public override string Id => "tilepanel-basic";
    public override string Title => "TilePanel Widget - Basic Usage";
    public override string Description => "Demonstrates a pannable and zoomable tile map with keyboard and mouse controls";
    public override bool EnableMouse => true;

    private class MapState
    {
        public double CameraX { get; set; }
        public double CameraY { get; set; }
        public int ZoomLevel { get; set; }
    }

    /// <summary>
    /// Simple tile data source that renders a colored coordinate grid.
    /// </summary>
    private class GridTileDataSource : ITileDataSource
    {
        public Size TileSize => new(3, 1);

        public ValueTask<TileData[,]> GetTilesAsync(
            int tileX, int tileY, int tilesWide, int tilesTall,
            CancellationToken cancellationToken = default)
        {
            var tiles = new TileData[tilesWide, tilesTall];
            for (int y = 0; y < tilesTall; y++)
            {
                for (int x = 0; x < tilesWide; x++)
                {
                    var tx = tileX + x;
                    var ty = tileY + y;
                    var isEven = (tx + ty) % 2 == 0;

                    tiles[x, y] = new TileData(
                        FormatCoord(tx, ty),
                        isEven ? Hex1bColor.FromRgb(100, 180, 255) : Hex1bColor.FromRgb(180, 180, 180),
                        isEven ? Hex1bColor.FromRgb(20, 40, 80) : Hex1bColor.FromRgb(30, 50, 30));
                }
            }
            return ValueTask.FromResult(tiles);
        }

        private static string FormatCoord(int x, int y)
        {
            // Format as 3-char string for tile size
            var s = $"{x},{y}";
            return s.Length <= 3 ? s.PadRight(3) : s[..3];
        }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating TilePanel basic example widget builder");

        var state = new MapState();
        var dataSource = new GridTileDataSource();
        var pois = new List<TilePointOfInterest>
        {
            new(0, 0, "📍", "Origin"),
            new(5, 3, "🏠", "Home"),
            new(-3, -2, "⭐", "Star"),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text($"Camera: ({state.CameraX:F1}, {state.CameraY:F1})  Zoom: {state.ZoomLevel}  [Arrows=Pan, +/-=Zoom, Home=Reset]"),
                v.TilePanel(dataSource, state.CameraX, state.CameraY, state.ZoomLevel)
                    .WithPointsOfInterest(pois)
                    .OnPan(e =>
                    {
                        state.CameraX += e.DeltaX;
                        state.CameraY += e.DeltaY;
                    })
                    .OnZoom(e => state.ZoomLevel = e.NewZoomLevel)
                    .OnPoiClicked(e =>
                    {
                        // Center camera on clicked POI
                        state.CameraX = e.PointOfInterest.X;
                        state.CameraY = e.PointOfInterest.Y;
                    })
            ]);
        };
    }
}
