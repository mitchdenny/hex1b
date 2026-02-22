using Hex1b;
using Hex1b.Data;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Simple tile data source for testing — renders coordinates as tile content.
/// </summary>
internal class TestTileDataSource : ITileDataSource
{
    public Size TileSize { get; } = new(3, 1);

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
                // Checkerboard pattern
                var isEven = (tx + ty) % 2 == 0;
                tiles[x, y] = new TileData(
                    $"{tx},{ty}"[..Math.Min(3, $"{tx},{ty}".Length)],
                    isEven ? Hex1bColor.Blue : Hex1bColor.Gray,
                    isEven ? Hex1bColor.Blue : Hex1bColor.Green);
            }
        }
        return ValueTask.FromResult(tiles);
    }
}

public class TilePanelNodeTests
{
    [Fact]
    public void EffectiveTileWidth_ZoomLevel0_ReturnsBaseSize()
    {
        var node = new TilePanelNode
        {
            DataSource = new TestTileDataSource(),
            ZoomLevel = 0,
        };

        Assert.Equal(3, node.EffectiveTileWidth);
        Assert.Equal(1, node.EffectiveTileHeight);
    }

    [Fact]
    public void EffectiveTileWidth_ZoomLevel1_DoubleSize()
    {
        var node = new TilePanelNode
        {
            DataSource = new TestTileDataSource(),
            ZoomLevel = 1,
        };

        Assert.Equal(6, node.EffectiveTileWidth);
        Assert.Equal(2, node.EffectiveTileHeight);
    }

    [Fact]
    public void EffectiveTileWidth_ZoomLevel2_QuadrupleSize()
    {
        var node = new TilePanelNode
        {
            DataSource = new TestTileDataSource(),
            ZoomLevel = 2,
        };

        Assert.Equal(12, node.EffectiveTileWidth);
        Assert.Equal(4, node.EffectiveTileHeight);
    }

    [Fact]
    public void GetVisibleTileRange_CenteredAtOrigin_ReturnsCorrectRange()
    {
        var node = new TilePanelNode
        {
            DataSource = new TestTileDataSource(),
            ZoomLevel = 0,
            CameraX = 0,
            CameraY = 0,
        };
        // Set bounds to simulate a 30x10 viewport
        node.Arrange(new Rect(0, 0, 30, 10));

        var (tileX, tileY, tilesWide, tilesTall) = node.GetVisibleTileRange();

        // With 30 chars wide, 3 chars/tile = 10 tiles + 2 = 12
        Assert.Equal(12, tilesWide);
        // With 10 rows, 1 char/tile = 10 tiles + 2 = 12
        Assert.Equal(12, tilesTall);
        // Camera at origin, viewport is 30 wide, tile is 3 wide
        // startTileX = floor(0 - 30/(2*3)) = floor(-5) = -5
        Assert.Equal(-5, tileX);
        Assert.Equal(-5, tileY);
    }

    [Fact]
    public void TileToScreen_AtCameraCenter_ReturnsViewportCenter()
    {
        var node = new TilePanelNode
        {
            DataSource = new TestTileDataSource(),
            ZoomLevel = 0,
            CameraX = 0,
            CameraY = 0,
        };
        node.Arrange(new Rect(0, 0, 80, 24));

        var (screenX, screenY) = node.TileToScreen(0, 0);

        // Camera at origin, tile at origin → center of viewport
        Assert.Equal(40, screenX);
        Assert.Equal(12, screenY);
    }

    [Fact]
    public void TileToScreen_OffsetFromCamera_ReturnsCorrectScreenPosition()
    {
        var node = new TilePanelNode
        {
            DataSource = new TestTileDataSource(),
            ZoomLevel = 0,
            CameraX = 5,
            CameraY = 3,
        };
        node.Arrange(new Rect(0, 0, 80, 24));

        // Tile at (5, 3) should be at center since camera is at (5, 3)
        var (screenX, screenY) = node.TileToScreen(5, 3);
        Assert.Equal(40, screenX);
        Assert.Equal(12, screenY);

        // Tile at (6, 3) should be 3 chars to the right (tileWidth = 3)
        var (screenX2, _) = node.TileToScreen(6, 3);
        Assert.Equal(43, screenX2);
    }

    [Fact]
    public void Reconcile_PreservesNode()
    {
        var ds = new TestTileDataSource();
        var widget1 = new TilePanelWidget { DataSource = ds, CameraX = 0, CameraY = 0, ZoomLevel = 0 };
        var widget2 = new TilePanelWidget { DataSource = ds, CameraX = 5, CameraY = 3, ZoomLevel = 1 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node1 = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult();
        var node2 = widget2.ReconcileAsync(node1, context).GetAwaiter().GetResult();

        Assert.Same(node1, node2);
        var tileNode = (TilePanelNode)node2;
        Assert.Equal(5, tileNode.CameraX);
        Assert.Equal(3, tileNode.CameraY);
        Assert.Equal(1, tileNode.ZoomLevel);
    }

    [Fact]
    public void Reconcile_CameraChange_MarksDirty()
    {
        var ds = new TestTileDataSource();
        var widget1 = new TilePanelWidget { DataSource = ds, CameraX = 0, CameraY = 0, ZoomLevel = 0 };
        var widget2 = new TilePanelWidget { DataSource = ds, CameraX = 5, CameraY = 0, ZoomLevel = 0 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node1 = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult();
        var tileNode = (TilePanelNode)node1;
        tileNode.ClearDirty();

        widget2.ReconcileAsync(node1, context).GetAwaiter().GetResult();

        Assert.True(tileNode.IsDirty);
    }

    [Fact]
    public void BuildContent_WithNoPois_ReturnsSurfaceOnly()
    {
        var node = new TilePanelNode
        {
            DataSource = new TestTileDataSource(),
            ZoomLevel = 0,
            CameraX = 0,
            CameraY = 0,
            PointsOfInterest = [],
        };
        node.Arrange(new Rect(0, 0, 80, 24));

        var content = node.BuildContent();

        Assert.IsType<SurfaceWidget>(content);
    }

    [Fact]
    public void BuildContent_WithPois_ReturnsZStack()
    {
        var node = new TilePanelNode
        {
            DataSource = new TestTileDataSource(),
            ZoomLevel = 0,
            CameraX = 0,
            CameraY = 0,
            PointsOfInterest = [new TilePointOfInterest(0, 0, "📍", "Test")],
        };
        node.Arrange(new Rect(0, 0, 80, 24));

        var content = node.BuildContent();

        Assert.IsType<ZStackWidget>(content);
        var zstack = (ZStackWidget)content;
        Assert.Equal(2, zstack.Children.Count);
        Assert.IsType<SurfaceWidget>(zstack.Children[0]);
        Assert.IsType<FloatPanelWidget>(zstack.Children[1]);
    }

    [Fact]
    public void BuildContent_PoiOutsideViewport_IsExcluded()
    {
        var node = new TilePanelNode
        {
            DataSource = new TestTileDataSource(),
            ZoomLevel = 0,
            CameraX = 0,
            CameraY = 0,
            PointsOfInterest = [new TilePointOfInterest(1000, 1000, "📍")],
        };
        node.Arrange(new Rect(0, 0, 80, 24));

        var content = node.BuildContent();

        // Far-away POI should be excluded, returning just the surface
        Assert.IsType<SurfaceWidget>(content);
    }

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new TilePanelNode();
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void IsFocused_WhenChanged_MarksDirty()
    {
        var node = new TilePanelNode();
        node.ClearDirty();

        node.IsFocused = true;

        Assert.True(node.IsDirty);
    }
}
