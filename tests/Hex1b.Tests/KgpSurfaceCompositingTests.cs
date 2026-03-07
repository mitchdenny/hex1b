using System.Security.Cryptography;
using System.Text;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Tests for KGP compositing across surfaces and layers,
/// including clipping, z-ordering, and visibility tracking.
/// </summary>
public class KgpSurfaceCompositingTests
{
    private static readonly CellMetrics DefaultMetrics = new(10, 20); // 10px wide, 20px tall

    private static KgpCellData CreateKgpData(
        string imageContent = "test-image",
        int widthInCells = 4,
        int heightInCells = 2,
        int zIndex = -1,
        uint sourcePixelWidth = 0,
        uint sourcePixelHeight = 0)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(imageContent));
        var pw = sourcePixelWidth > 0 ? sourcePixelWidth : (uint)(widthInCells * 10);
        var ph = sourcePixelHeight > 0 ? sourcePixelHeight : (uint)(heightInCells * 20);
        return new KgpCellData(
            transmitPayload: $"\x1b_Ga=t,f=32,s={pw},v={ph},i=1;{Convert.ToBase64String(Encoding.UTF8.GetBytes(imageContent))}\x1b\\",
            imageId: 1,
            widthInCells: widthInCells,
            heightInCells: heightInCells,
            sourcePixelWidth: pw,
            sourcePixelHeight: ph,
            contentHash: hash,
            zIndex: zIndex);
    }

    private static TrackedObject<KgpCellData> Track(KgpCellData data)
        => new(data, _ => { });

    #region Surface.Composite KGP Clipping

    [Fact]
    public void Composite_KgpImage_FitsInBounds_NoClipping()
    {
        var parent = new Surface(10, 5, DefaultMetrics);
        var child = new Surface(4, 2, DefaultMetrics);
        
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 0, offsetY: 0);

        Assert.True(parent[0, 0].HasKgp);
        Assert.Equal(4, parent[0, 0].Kgp!.Data.WidthInCells);
        Assert.Equal(2, parent[0, 0].Kgp!.Data.HeightInCells);
    }

    [Fact]
    public void Composite_KgpImage_ExtendsRight_ClipsWidth()
    {
        var parent = new Surface(6, 5, DefaultMetrics);
        var child = new Surface(8, 2, DefaultMetrics);
        
        // 8-cell wide image placed at dest x=3 in a 6-wide parent → clips to 3 cells
        var kgpData = CreateKgpData(widthInCells: 8, heightInCells: 2, sourcePixelWidth: 80, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 3, offsetY: 0);

        Assert.True(parent[3, 0].HasKgp);
        Assert.Equal(3, parent[3, 0].Kgp!.Data.WidthInCells);
    }

    [Fact]
    public void Composite_KgpImage_ExtendsDown_ClipsHeight()
    {
        var parent = new Surface(10, 3, DefaultMetrics);
        var child = new Surface(4, 4, DefaultMetrics);
        
        // 4-cell tall image placed at dest y=2 in a 3-tall parent → clips to 1 cell
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 4, sourcePixelWidth: 40, sourcePixelHeight: 80);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 0, offsetY: 2);

        Assert.True(parent[0, 2].HasKgp);
        Assert.Equal(1, parent[0, 2].Kgp!.Data.HeightInCells);
    }

    [Fact]
    public void Composite_KgpImage_CompletelyOutOfBounds_RemovesKgp()
    {
        var parent = new Surface(3, 3, DefaultMetrics);
        var child = new Surface(4, 2, DefaultMetrics);
        
        // 4-wide image at dest x=3 in 3-wide parent → 0 visible cells
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 3, offsetY: 0);

        // Cell at x=3 is out of bounds, so nothing should be written
        // The composite should handle this gracefully
        Assert.False(parent[0, 0].HasKgp);
    }

    [Fact]
    public void Composite_KgpImage_PreservesZIndex()
    {
        var parent = new Surface(10, 5, DefaultMetrics);
        var child = new Surface(4, 2, DefaultMetrics);
        
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, zIndex: 5);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 0, offsetY: 0);

        Assert.Equal(5, parent[0, 0].Kgp!.Data.ZIndex);
    }

    [Fact]
    public void Composite_KgpImage_ClippedPreservesZIndex()
    {
        var parent = new Surface(3, 3, DefaultMetrics);
        var child = new Surface(6, 2, DefaultMetrics);
        
        var kgpData = CreateKgpData(widthInCells: 6, heightInCells: 2, zIndex: 3, sourcePixelWidth: 60, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 1, offsetY: 0);

        Assert.True(parent[1, 0].HasKgp);
        Assert.Equal(3, parent[1, 0].Kgp!.Data.ZIndex);
    }

    #endregion

    #region CompositeSurface KGP Layer Resolution

    [Fact]
    public void CompositeSurface_KgpOnUpperLayer_OccludesLower()
    {
        var layer1 = new Surface(10, 5, DefaultMetrics);
        layer1[2, 1] = new SurfaceCell("A", Hex1bColor.White, Hex1bColor.Black);

        var layer2 = new Surface(10, 5, DefaultMetrics);
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        layer2[2, 1] = new SurfaceCell(" ", null, null, Kgp: tracked);

        var composite = new CompositeSurface(10, 5);
        composite.AddLayer(layer1);
        composite.AddLayer(layer2);

        var resolved = composite.GetCell(2, 1);
        Assert.True(resolved.HasKgp);
    }

    [Fact]
    public void CompositeSurface_TextOverKgp_TextWins()
    {
        var layer1 = new Surface(10, 5, DefaultMetrics);
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        layer1[2, 1] = new SurfaceCell(" ", null, null, Kgp: tracked);

        var layer2 = new Surface(10, 5, DefaultMetrics);
        layer2[2, 1] = new SurfaceCell("X", Hex1bColor.White, Hex1bColor.Black);

        var composite = new CompositeSurface(10, 5);
        composite.AddLayer(layer1);
        composite.AddLayer(layer2);

        var resolved = composite.GetCell(2, 1);
        Assert.Equal("X", resolved.Character);
        Assert.False(resolved.HasKgp);
    }

    [Fact]
    public void CompositeSurface_TransparentAboveKgp_KgpShowsThrough()
    {
        var layer1 = new Surface(10, 5, DefaultMetrics);
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        layer1[2, 1] = new SurfaceCell(" ", null, null, Kgp: tracked);

        var layer2 = new Surface(10, 5, DefaultMetrics);
        // Leave layer2[2,1] as unwritten/transparent

        var composite = new CompositeSurface(10, 5);
        composite.AddLayer(layer1);
        composite.AddLayer(layer2);

        var resolved = composite.GetCell(2, 1);
        Assert.True(resolved.HasKgp);
    }

    [Fact]
    public void CompositeSurface_KgpOnlyLayer_Preserved()
    {
        var layer = new Surface(10, 5, DefaultMetrics);
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        layer[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        var composite = new CompositeSurface(10, 5);
        composite.AddLayer(layer);

        var resolved = composite.GetCell(0, 0);
        Assert.True(resolved.HasKgp);
        Assert.Equal(1u, resolved.Kgp!.Data.ImageId);
    }

    #endregion

    #region KgpVisibility

    [Fact]
    public void KgpVisibility_FullyVisible_SinglePlacement()
    {
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, sourcePixelWidth: 40, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        Assert.True(vis.IsFullyVisible);
        Assert.False(vis.IsFullyOccluded);

        var placements = vis.GeneratePlacements(DefaultMetrics);
        Assert.Single(placements);
        Assert.Equal(0, placements[0].CellX);
        Assert.Equal(0, placements[0].CellY);
    }

    [Fact]
    public void KgpVisibility_FullyOccluded_NoPlacements()
    {
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, sourcePixelWidth: 40, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        // Occlude the entire image
        vis.ApplyOcclusion(new Rect(0, 0, 4, 2), DefaultMetrics);

        Assert.True(vis.IsFullyOccluded);
        Assert.False(vis.IsFullyVisible);

        var placements = vis.GeneratePlacements(DefaultMetrics);
        Assert.Empty(placements);
    }

    [Fact]
    public void KgpVisibility_PartialOcclusion_ReducesVisibleRegions()
    {
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, sourcePixelWidth: 40, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        // Occlude the right half of the image
        vis.ApplyOcclusion(new Rect(2, 0, 2, 2), DefaultMetrics);

        Assert.False(vis.IsFullyVisible);
        Assert.False(vis.IsFullyOccluded);
        Assert.True(vis.VisibleRegions.Count >= 1);
    }

    [Fact]
    public void KgpVisibility_OcclusionOutsideImage_NoEffect()
    {
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, sourcePixelWidth: 40, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        // Occlude outside the image bounds
        vis.ApplyOcclusion(new Rect(10, 10, 5, 5), DefaultMetrics);

        Assert.False(vis.IsFullyVisible); // ApplyOcclusion sets this to false
        Assert.False(vis.IsFullyOccluded);
        
        var placements = vis.GeneratePlacements(DefaultMetrics);
        // Should still generate a placement for the visible region
        Assert.True(placements.Count >= 1);
    }

    [Fact]
    public void KgpVisibility_CenterOcclusion_GeneratesMultipleRegions()
    {
        var kgpData = CreateKgpData(widthInCells: 10, heightInCells: 10, sourcePixelWidth: 100, sourcePixelHeight: 200);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        // Occlude a 2x2 block in the center of the 10x10 image
        vis.ApplyOcclusion(new Rect(4, 4, 2, 2), DefaultMetrics);

        Assert.False(vis.IsFullyOccluded);
        // Punching a hole in the center should create 4 regions (top, bottom, left, right)
        Assert.True(vis.VisibleRegions.Count == 4);
    }

    [Fact]
    public void KgpVisibility_PreservesZIndex()
    {
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, zIndex: 5, sourcePixelWidth: 40, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        var placements = vis.GeneratePlacements(DefaultMetrics);
        Assert.Single(placements);
        Assert.Equal(5, placements[0].Data.ZIndex);
    }

    [Fact]
    public void KgpVisibility_PartialOcclusion_PlacementsPreserveZIndex()
    {
        var kgpData = CreateKgpData(widthInCells: 10, heightInCells: 10, zIndex: 3, sourcePixelWidth: 100, sourcePixelHeight: 200);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        vis.ApplyOcclusion(new Rect(4, 4, 2, 2), DefaultMetrics);

        var placements = vis.GeneratePlacements(DefaultMetrics);
        foreach (var (data, _, _) in placements)
        {
            Assert.Equal(3, data.ZIndex);
        }
    }

    [Fact]
    public void KgpVisibility_LayerIndex_Stored()
    {
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 5, anchorY: 3, layerIndex: 7);

        Assert.Equal(7, vis.LayerIndex);
        Assert.Equal((5, 3), vis.AnchorPosition);
    }

    #endregion

    #region SurfaceLayerContext.CreateKgp

    [Fact]
    public void SurfaceLayerContext_CreateKgp_FromCellData_ReturnsTrackedObject()
    {
        var store = new TrackedObjectStore();
        var ctx = new Hex1b.Widgets.SurfaceLayerContext(
            10, 5, 0, 0,
            new Hex1bTheme("Test"),
            store,
            DefaultMetrics,
            TerminalCapabilities.Modern);
        
        var kgpData = CreateKgpData();
        var tracked = ctx.CreateKgp(kgpData);

        Assert.NotNull(tracked);
        Assert.Equal(kgpData.ImageId, tracked!.Data.ImageId);
    }

    [Fact]
    public void SurfaceLayerContext_CreateKgp_NullStore_ReturnsNull()
    {
        var ctx = new Hex1b.Widgets.SurfaceLayerContext(
            10, 5, 0, 0,
            new Hex1bTheme("Test"),
            null,
            DefaultMetrics,
            TerminalCapabilities.Modern);
        
        var kgpData = CreateKgpData();
        var tracked = ctx.CreateKgp(kgpData);

        Assert.Null(tracked);
    }

    [Fact]
    public void SurfaceLayerContext_CreateKgp_FromPixelData_ReturnsTrackedObject()
    {
        var store = new TrackedObjectStore();
        var ctx = new Hex1b.Widgets.SurfaceLayerContext(
            10, 5, 0, 0,
            new Hex1bTheme("Test"),
            store,
            DefaultMetrics,
            TerminalCapabilities.Modern);
        
        // 2x2 RGBA32 image (16 bytes)
        var pixels = new byte[2 * 2 * 4];
        var tracked = ctx.CreateKgp(pixels, 2, 2);

        Assert.NotNull(tracked);
    }

    [Fact]
    public void SurfaceLayerContext_CreateKgp_FromPixelData_BelowText_NegativeZIndex()
    {
        var store = new TrackedObjectStore();
        var ctx = new Hex1b.Widgets.SurfaceLayerContext(
            10, 5, 0, 0,
            new Hex1bTheme("Test"),
            store,
            DefaultMetrics,
            TerminalCapabilities.Modern);
        
        var pixels = new byte[2 * 2 * 4];
        var tracked = ctx.CreateKgp(pixels, 2, 2, KgpZOrder.BelowText);

        Assert.NotNull(tracked);
        Assert.True(tracked!.Data.ZIndex < 0);
    }

    [Fact]
    public void SurfaceLayerContext_CreateKgp_FromPixelData_AboveText_PositiveZIndex()
    {
        var store = new TrackedObjectStore();
        var ctx = new Hex1b.Widgets.SurfaceLayerContext(
            10, 5, 0, 0,
            new Hex1bTheme("Test"),
            store,
            DefaultMetrics,
            TerminalCapabilities.Modern);
        
        var pixels = new byte[2 * 2 * 4];
        var tracked = ctx.CreateKgp(pixels, 2, 2, KgpZOrder.AboveText);

        Assert.NotNull(tracked);
        Assert.True(tracked!.Data.ZIndex > 0);
    }

    [Fact]
    public void SurfaceLayerContext_CreateKgp_Deduplicates()
    {
        var store = new TrackedObjectStore();
        var ctx = new Hex1b.Widgets.SurfaceLayerContext(
            10, 5, 0, 0,
            new Hex1bTheme("Test"),
            store,
            DefaultMetrics,
            TerminalCapabilities.Modern);
        
        var kgpData = CreateKgpData();
        var tracked1 = ctx.CreateKgp(kgpData);
        var tracked2 = ctx.CreateKgp(kgpData);

        Assert.NotNull(tracked1);
        Assert.NotNull(tracked2);
        Assert.Same(tracked1, tracked2);
    }

    #endregion

    #region Surface.Composite preserves KGP through cell operations

    [Fact]
    public void Surface_HasKgp_TracksThroughComposite()
    {
        var parent = new Surface(10, 5, DefaultMetrics);
        Assert.False(parent.HasKgp);

        var child = new Surface(4, 2, DefaultMetrics);
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 0, offsetY: 0);
        Assert.True(parent.HasKgp);
    }

    [Fact]
    public void Surface_Clone_PreservesKgp()
    {
        var surface = new Surface(10, 5, DefaultMetrics);
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        surface[2, 1] = new SurfaceCell(" ", null, null, Kgp: tracked);

        var clone = surface.Clone();
        Assert.True(clone.HasKgp);
        Assert.True(clone[2, 1].HasKgp);
    }

    #endregion
}
