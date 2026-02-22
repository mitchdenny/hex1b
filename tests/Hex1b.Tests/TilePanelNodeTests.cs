using Hex1b;
using Hex1b.Data;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
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
    public void BuildContent_WithNoPois_ReturnsInteractable()
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

        // With no POIs, content is just the Interactable wrapping SurfaceWidget
        Assert.IsType<InteractableWidget>(content);
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
        Assert.IsType<InteractableWidget>(zstack.Children[0]);
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

        // Far-away POI should be excluded, returning just the interactable
        Assert.IsType<InteractableWidget>(content);
    }

    [Fact]
    public void TilePanelNode_IsNotDirectlyFocusable()
    {
        // TilePanelNode delegates focus to the InteractableNode it wraps the surface in
        var node = new TilePanelNode();
        Assert.False(node.IsFocusable);
    }
}

public class TilePanelFocusTests
{
    [Fact]
    public void TilePanelNode_InVStack_InteractableReceivesFocus()
    {
        var ds = new TestTileDataSource();
        var tilePanelWidget = new TilePanelWidget { DataSource = ds, CameraX = 0, CameraY = 0, ZoomLevel = 0 }
            .OnPan(e => { });

        var vstack = new VStackWidget([new TextBlockWidget("header"), tilePanelWidget]);
        var context = ReconcileContext.CreateRoot(new FocusRing());

        // Use ReconcileChildAsync like the real app does — this sets IsNew = true
        var dummyParent = new TextBlockNode();
        var rootNode = (VStackNode)context.ReconcileChildAsync(null, vstack, dummyParent).GetAwaiter().GetResult()!;

        // Find the TilePanelNode
        TilePanelNode? tilePanelNode = null;
        foreach (var child in rootNode.GetChildren())
        {
            if (child is TilePanelNode tpn)
            {
                tilePanelNode = tpn;
                break;
            }
        }

        Assert.NotNull(tilePanelNode);

        // The InteractableNode inside TilePanelNode should be focusable and receive focus
        var focusables = tilePanelNode.GetFocusableNodes().ToList();
        Assert.True(focusables.Count > 0, "TilePanelNode should have focusable descendants");
        Assert.IsType<InteractableNode>(focusables[0]);
        Assert.True(focusables[0].IsFocused, "InteractableNode should receive focus");

        // Verify PanCallback is wired
        Assert.NotNull(tilePanelNode.PanCallback);
    }
}

public class TilePanelIntegrationTests
{
    [Fact]
    public async Task TilePanel_ArrowKey_PansCamera()
    {
        var cameraX = 0.0;
        var cameraY = 0.0;
        var zoomLevel = 0;
        var panCount = 0;
        var ds = new TestTileDataSource();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => ctx.VStack(v =>
            [
                v.Text($"Camera: ({cameraX:F1}, {cameraY:F1})"),
                v.TilePanel(ds, cameraX, cameraY, zoomLevel)
                    .OnPan(e =>
                    {
                        cameraX += e.DeltaX;
                        cameraY += e.DeltaY;
                        panCount++;
                    })
                    .OnZoom(e => zoomLevel = e.NewZoomLevel),
            ]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        // Wait for initial render, then press right arrow
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Camera:"), TimeSpan.FromSeconds(5), "initial render")
            .Key(Hex1bKey.RightArrow)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("1.0"), TimeSpan.FromSeconds(2), "camera to move right")
            .Capture("after-pan")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(panCount > 0, "Pan handler should have been called");
        Assert.Equal(1.0, cameraX);
        Assert.Equal(0.0, cameraY);
    }

    [Fact]
    public async Task TilePanel_PlusKey_Zooms()
    {
        var zoomLevel = 0;
        var zoomCount = 0;
        var ds = new TestTileDataSource();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => ctx => ctx.VStack(v =>
            [
                v.Text($"Zoom: {zoomLevel}"),
                v.TilePanel(ds, 0, 0, zoomLevel)
                    .OnPan(e => { })
                    .OnZoom(e =>
                    {
                        zoomLevel = e.NewZoomLevel;
                        zoomCount++;
                    }),
            ]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Zoom: 0"), TimeSpan.FromSeconds(5), "initial render")
            .Key(Hex1bKey.OemPlus)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Zoom: 1"), TimeSpan.FromSeconds(2), "zoom to change")
            .Capture("after-zoom-key")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(zoomCount > 0, "Zoom handler should have been called");
        Assert.Equal(1, zoomLevel);
    }

    [Fact]
    public async Task TilePanel_MouseScroll_Zooms()
    {
        var cameraX = 0.0;
        var cameraY = 0.0;
        var zoomLevel = 0;
        var zoomCount = 0;
        var ds = new TestTileDataSource();

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v =>
            [
                v.Text($"Zoom: {zoomLevel}"),
                v.TilePanel(ds, cameraX, cameraY, zoomLevel)
                    .OnPan(e =>
                    {
                        cameraX += e.DeltaX;
                        cameraY += e.DeltaY;
                    })
                    .OnZoom(e =>
                    {
                        zoomLevel = e.NewZoomLevel;
                        zoomCount++;
                    }),
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for initial render, send scroll event, verify zoom changed
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Zoom:"), TimeSpan.FromSeconds(5), "initial render")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await terminal.SendEventAsync(new Hex1bMouseEvent(MouseButton.ScrollUp, MouseAction.Down, 20, 5, Hex1bModifiers.None));
        await Task.Delay(300);

        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(zoomCount > 0, "Zoom handler should have been called");
        Assert.Equal(1, zoomLevel);
    }
}
