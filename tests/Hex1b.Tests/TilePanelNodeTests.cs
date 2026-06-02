using Hex1b;
using Hex1b.Data;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
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

[TestClass]
public class TilePanelNodeTests
{
    [TestMethod]
    public void EffectiveTileWidth_ZoomLevel0_ReturnsBaseSize()
    {
        var ds = new TestTileDataSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 0,
        };
        node.SetDataSource(ds);

        Assert.AreEqual(3, node.EffectiveTileWidth);
        Assert.AreEqual(1, node.EffectiveTileHeight);
    }

    [TestMethod]
    public void EffectiveTileWidth_ZoomLevel1_DoubleSize()
    {
        var ds = new TestTileDataSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 1,
        };
        node.SetDataSource(ds);

        Assert.AreEqual(6, node.EffectiveTileWidth);
        Assert.AreEqual(2, node.EffectiveTileHeight);
    }

    [TestMethod]
    public void EffectiveTileWidth_ZoomLevel2_QuadrupleSize()
    {
        var ds = new TestTileDataSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 2,
        };
        node.SetDataSource(ds);

        Assert.AreEqual(12, node.EffectiveTileWidth);
        Assert.AreEqual(4, node.EffectiveTileHeight);
    }

    [TestMethod]
    public void GetVisibleTileRange_CenteredAtOrigin_ReturnsCorrectRange()
    {
        var ds = new TestTileDataSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 0,
            CameraX = 0,
            CameraY = 0,
        };
        node.SetDataSource(ds);
        // Set bounds to simulate a 30x10 viewport
        node.Arrange(new Rect(0, 0, 30, 10));

        var (tileX, tileY, tilesWide, tilesTall) = node.GetVisibleTileRange();

        // With 30 chars wide, 3 chars/tile = 10 tiles + 2 = 12
        Assert.AreEqual(12, tilesWide);
        // With 10 rows, 1 char/tile = 10 tiles + 2 = 12
        Assert.AreEqual(12, tilesTall);
        // Camera at origin, viewport is 30 wide, tile is 3 wide
        // startTileX = floor(0 - 30/(2*3)) = floor(-5) = -5
        Assert.AreEqual(-5, tileX);
        Assert.AreEqual(-5, tileY);
    }

    [TestMethod]
    public void TileToScreen_AtCameraCenter_ReturnsViewportCenter()
    {
        var ds = new TestTileDataSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 0,
            CameraX = 0,
            CameraY = 0,
        };
        node.SetDataSource(ds);
        node.Arrange(new Rect(0, 0, 80, 24));

        var (screenX, screenY) = node.TileToScreen(0, 0);

        // Camera at origin, tile at origin → center of viewport
        Assert.AreEqual(40, screenX);
        Assert.AreEqual(12, screenY);
    }

    [TestMethod]
    public void TileToScreen_OffsetFromCamera_ReturnsCorrectScreenPosition()
    {
        var ds = new TestTileDataSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 0,
            CameraX = 5,
            CameraY = 3,
        };
        node.SetDataSource(ds);
        node.Arrange(new Rect(0, 0, 80, 24));

        // Tile at (5, 3) should be at center since camera is at (5, 3)
        var (screenX, screenY) = node.TileToScreen(5, 3);
        Assert.AreEqual(40, screenX);
        Assert.AreEqual(12, screenY);

        // Tile at (6, 3) should be 3 chars to the right (tileWidth = 3)
        var (screenX2, _) = node.TileToScreen(6, 3);
        Assert.AreEqual(43, screenX2);
    }

    [TestMethod]
    public void Reconcile_PreservesNode()
    {
        var ds = new TestTileDataSource();
        var widget1 = new TilePanelWidget { DataSource = ds, CameraX = 0, CameraY = 0, ZoomLevel = 0 };
        var widget2 = new TilePanelWidget { DataSource = ds, CameraX = 5, CameraY = 3, ZoomLevel = 1 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node1 = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult();
        var node2 = widget2.ReconcileAsync(node1, context).GetAwaiter().GetResult();

        Assert.AreSame(node1, node2);
        var tileNode = (TilePanelNode)node2;
        Assert.AreEqual(5, tileNode.CameraX);
        Assert.AreEqual(3, tileNode.CameraY);
        Assert.AreEqual(1, tileNode.ZoomLevel);
    }

    [TestMethod]
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

        Assert.IsTrue(tileNode.IsDirty);
    }

    [TestMethod]
    public void BuildContent_WithNoPois_ReturnsInteractable()
    {
        var ds = new TestTileDataSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 0,
            CameraX = 0,
            CameraY = 0,
            PointsOfInterest = [],
        };
        node.SetDataSource(ds);
        node.Arrange(new Rect(0, 0, 80, 24));

        var content = node.BuildContent();

        // With no POIs, content is just the Interactable wrapping SurfaceWidget
        TestSeq.IsType<InteractableWidget>(content);
    }

    [TestMethod]
    public void BuildContent_WithPois_ReturnsZStack()
    {
        var ds = new TestTileDataSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 0,
            CameraX = 0,
            CameraY = 0,
            PointsOfInterest = [new TilePointOfInterest(0, 0, "📍", "Test")],
        };
        node.SetDataSource(ds);
        node.Arrange(new Rect(0, 0, 80, 24));

        var content = node.BuildContent();

        TestSeq.IsType<ZStackWidget>(content);
        var zstack = (ZStackWidget)content;
        // ZStack with tile layer + float POI children
        Assert.AreEqual(2, zstack.Children.Count);
        TestSeq.IsType<InteractableWidget>(zstack.Children[0]);
        TestSeq.IsType<FloatWidget>(zstack.Children[1]);
    }

    [TestMethod]
    public void BuildContent_PoiOutsideViewport_IsExcluded()
    {
        var ds = new TestTileDataSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 0,
            CameraX = 0,
            CameraY = 0,
            PointsOfInterest = [new TilePointOfInterest(1000, 1000, "📍")],
        };
        node.SetDataSource(ds);
        node.Arrange(new Rect(0, 0, 80, 24));

        var content = node.BuildContent();

        // Far-away POI should be excluded, returning just the interactable
        TestSeq.IsType<InteractableWidget>(content);
    }

    [TestMethod]
    public void TilePanelNode_IsNotDirectlyFocusable()
    {
        // TilePanelNode delegates focus to the InteractableNode it wraps the surface in
        var node = new TilePanelNode();
        Assert.IsFalse(node.IsFocusable);
    }
}

[TestClass]
public class TilePanelFocusTests
{
    [TestMethod]
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

        Assert.IsNotNull(tilePanelNode);

        // The InteractableNode inside TilePanelNode should be focusable and receive focus
        var focusables = tilePanelNode.GetFocusableNodes().ToList();
        Assert.IsTrue(focusables.Count > 0, "TilePanelNode should have focusable descendants");
        TestSeq.IsType<InteractableNode>(focusables[0]);
        Assert.IsTrue(focusables[0].IsFocused, "InteractableNode should receive focus");

        // Verify PanCallback is wired
        Assert.IsNotNull(tilePanelNode.PanCallback);
    }
}

[TestClass]
public class TilePanelIntegrationTests
{
    [TestMethod]
    public async Task TilePanel_ArrowKey_PansCamera()
    {
        var cameraX = 0.0;
        var cameraY = 0.0;
        var zoomLevel = 0;
        var panCount = 0;
        var ds = new TestTileDataSource();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => ctx.VStack(v =>
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

        Assert.IsTrue(panCount > 0, "Pan handler should have been called");
        Assert.AreEqual(1.0, cameraX);
        Assert.AreEqual(0.0, cameraY);
    }

    [TestMethod]
    public async Task TilePanel_PlusKey_Zooms()
    {
        var zoomLevel = 0;
        var zoomCount = 0;
        var ds = new TestTileDataSource();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => ctx.VStack(v =>
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

        Assert.IsTrue(zoomCount > 0, "Zoom handler should have been called");
        Assert.AreEqual(1, zoomLevel);
    }

    [TestMethod]
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

        Assert.IsTrue(zoomCount > 0, "Zoom handler should have been called");
        Assert.AreEqual(1, zoomLevel);
    }

    /// <summary>
    /// Invokes the private DrawTiles method via the DrawSurfaceLayer action.
    /// </summary>
    private static void DrawTilesViaReflection(TilePanelNode node, Surface surface)
    {
        // Use the internal method to invoke DrawTiles
        var method = typeof(TilePanelNode).GetMethod("DrawTiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(node, [surface]);
    }

    /// <summary>
    /// Verifies that large viewports (which exceed the default MaxCachedTiles=10000)
    /// don't show persistent placeholder dots due to cache eviction thrashing.
    /// The TileCache must auto-expand when the viewport requires more tiles than the max.
    /// </summary>
    [TestMethod]
    [DataRow(200, 55)]  // 202 * 57 = 11,514 tiles — exceeds 10,000
    [DataRow(250, 60)]  // 252 * 62 = 15,624 tiles — well over limit
    public async Task DrawTiles_LargeViewport_NoEvictionThrashing(int width, int height)
    {
        var ds = new SingleCellTileSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 0,
            CameraX = 0.0,
            CameraY = 0.0,
        };
        node.SetDataSource(ds);
        node.Arrange(new Rect(0, 0, width, height));

        // First DrawTiles: cache is empty, shows placeholders, starts background fetch
        var surface1 = new Surface(width, height, new CellMetrics(8, 16));
        DrawTilesViaReflection(node, surface1);

        await Task.Delay(200);

        // Second DrawTiles: all tiles should be cached (no eviction thrashing)
        var surface2 = new Surface(width, height, new CellMetrics(8, 16));
        DrawTilesViaReflection(node, surface2);

        var placeholderCount = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (surface2[x, y].Character == "·")
                    placeholderCount++;

        Assert.IsTrue(placeholderCount == 0, $"Viewport {width}x{height}: Found {placeholderCount} placeholder '·' cells — " +
            $"cache eviction is thrashing (viewport needs {(width + 2) * (height + 2)} tiles, " +
            $"which exceeds default MaxCachedTiles)");
    }

    /// <summary>
    /// Verifies that after tiles are cached, DrawTiles fills ALL surface cells
    /// with tile content and leaves no unwritten or placeholder cells.
    /// Tests multiple viewport widths to catch rounding/alignment issues.
    /// </summary>
    [TestMethod]
    [DataRow(39)]
    [DataRow(40)]
    [DataRow(41)]
    [DataRow(79)]
    [DataRow(80)]
    [DataRow(81)]
    [DataRow(100)]
    [DataRow(120)]
    public async Task DrawTiles_AllWidths_NoPlaceholderCells(int width)
    {
        // Use a 1x1 tile source that always returns content synchronously
        var ds = new SingleCellTileSource();
        var node = new TilePanelNode
        {
            ZoomLevel = 0,
            CameraX = 100.0,
            CameraY = 100.0,
        };
        node.SetDataSource(ds);
        node.Arrange(new Rect(0, 0, width, 10));

        // First DrawTiles populates the cache via background fetch
        var surface1 = new Surface(width, 10, new CellMetrics(8, 16));
        DrawTilesViaReflection(node, surface1);

        // Wait for background fetch to complete
        await Task.Delay(200);

        // Second DrawTiles should use cached tiles — no placeholders
        var surface2 = new Surface(width, 10, new CellMetrics(8, 16));
        DrawTilesViaReflection(node, surface2);

        var unwrittenCount = 0;
        var placeholderCount = 0;
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cell = surface2[x, y];
                if (cell.Character == SurfaceCells.UnwrittenMarker)
                    unwrittenCount++;
                if (cell.Character == "·")
                    placeholderCount++;
            }
        }

        Assert.IsTrue(unwrittenCount == 0, $"Width={width}: Found {unwrittenCount} unwritten cells (should be 0 after cache is populated)");
        Assert.IsTrue(placeholderCount == 0, $"Width={width}: Found {placeholderCount} placeholder '·' cells (should be 0 after cache is populated)");
    }
}

/// <summary>
/// 1x1 tile data source where every tile has content. Returns synchronously.
/// </summary>
internal class SingleCellTileSource : ITileDataSource
{
    public Size TileSize => new(1, 1);

    public ValueTask<TileData[,]> GetTilesAsync(
        int tileX, int tileY, int tilesWide, int tilesTall,
        CancellationToken cancellationToken = default)
    {
        var tiles = new TileData[tilesWide, tilesTall];
        for (int y = 0; y < tilesTall; y++)
            for (int x = 0; x < tilesWide; x++)
                tiles[x, y] = new TileData("X", Hex1bColor.White, Hex1bColor.Black);
        return ValueTask.FromResult(tiles);
    }
}
