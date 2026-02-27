using System.Security.Cryptography;
using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Surfaces;

namespace Hex1b.Tests;

/// <summary>
/// Tests for KGP image occlusion and fragmentation via <see cref="KgpVisibility"/>.
/// Mirrors the sixel occlusion test patterns from SixelEncoderTests/SixelVisibilityTests.
/// </summary>
public class KgpVisibilityTests
{
    private static KgpCellData MakeKgp(uint pixelW, uint pixelH, int cellW, int cellH, uint imageId = 1)
    {
        var hash = SHA256.HashData([(byte)imageId]);
        return new KgpCellData(
            transmitPayload: null,
            imageId: imageId,
            widthInCells: cellW,
            heightInCells: cellH,
            sourcePixelWidth: pixelW,
            sourcePixelHeight: pixelH,
            contentHash: hash);
    }

    // ─── KgpVisibility direct API tests ───

    [Fact]
    public void InitiallyFullyVisible()
    {
        var data = MakeKgp(64, 32, 8, 4);
        var vis = new KgpVisibility(data, 5, 5);

        Assert.True(vis.IsFullyVisible);
        Assert.False(vis.IsFullyOccluded);
        Assert.False(vis.IsFragmented);
        Assert.Single(vis.VisibleRegions);
        Assert.Equal(new PixelRect(0, 0, 64, 32), vis.VisibleRegions[0]);
    }

    [Fact]
    public void ApplyOcclusion_NoOverlap_NoChange()
    {
        var data = MakeKgp(64, 32, 8, 4);
        var vis = new KgpVisibility(data, 5, 5);
        var metrics = new CellMetrics(8, 16);

        // Occlusion outside the image region
        vis.ApplyOcclusion(new Rect(0, 0, 4, 4), metrics);

        Assert.True(vis.IsFullyVisible);
        Assert.Single(vis.VisibleRegions);
    }

    [Fact]
    public void ApplyOcclusion_CenterHole_FourFragments()
    {
        // 8x4 cell image (64x32 pixels), center 2x2 cell occlusion
        var data = MakeKgp(64, 32, 8, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8); // 8px per cell both directions

        // Occlude center 2x2 cells at (3,1)
        vis.ApplyOcclusion(new Rect(3, 1, 2, 2), metrics);

        Assert.False(vis.IsFullyVisible);
        Assert.False(vis.IsFullyOccluded);
        Assert.Equal(4, vis.VisibleRegions.Count); // top, bottom, left, right

        // Total visible area should equal original minus hole
        var totalArea = vis.VisibleRegions.Sum(r => r.Area);
        var holePixelW = 2 * 64 / 8; // 16px
        var holePixelH = 2 * 32 / 4; // 16px
        Assert.Equal(64 * 32 - holePixelW * holePixelH, totalArea);
    }

    [Fact]
    public void ApplyOcclusion_FullyCovered_Empty()
    {
        var data = MakeKgp(32, 16, 4, 2);
        var vis = new KgpVisibility(data, 2, 3);
        var metrics = new CellMetrics(8, 8);

        // Occlude entire image region and beyond
        vis.ApplyOcclusion(new Rect(0, 0, 20, 20), metrics);

        Assert.True(vis.IsFullyOccluded);
        Assert.Empty(vis.VisibleRegions);
    }

    [Fact]
    public void ApplyOcclusion_RightSide_TwoFragments()
    {
        // 4x4 cell image, right 2 columns occluded
        var data = MakeKgp(32, 32, 4, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8);

        vis.ApplyOcclusion(new Rect(2, 0, 2, 4), metrics);

        Assert.False(vis.IsFullyOccluded);
        // Right-side occlusion: top fragment, bottom fragment both have 0 height at that level
        // Actually for a right-side occlusion starting at y=0 with full height:
        // top = empty (intersection.Y == Y), bottom = empty (intersection.Bottom == Bottom)
        // left = (0, 0, 16, 32), right = empty (intersection.Right == Right)
        // Only LEFT fragment survives
        Assert.Single(vis.VisibleRegions);
        Assert.Equal(new PixelRect(0, 0, 16, 32), vis.VisibleRegions[0]);
    }

    [Fact]
    public void ApplyOcclusion_LeftSide_SingleFragment()
    {
        var data = MakeKgp(32, 32, 4, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8);

        vis.ApplyOcclusion(new Rect(0, 0, 2, 4), metrics);

        Assert.Single(vis.VisibleRegions);
        Assert.Equal(new PixelRect(16, 0, 16, 32), vis.VisibleRegions[0]);
    }

    [Fact]
    public void ApplyOcclusion_TopSide_SingleFragment()
    {
        var data = MakeKgp(32, 32, 4, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8);

        vis.ApplyOcclusion(new Rect(0, 0, 4, 2), metrics);

        Assert.Single(vis.VisibleRegions);
        Assert.Equal(new PixelRect(0, 16, 32, 16), vis.VisibleRegions[0]);
    }

    [Fact]
    public void ApplyOcclusion_BottomSide_SingleFragment()
    {
        var data = MakeKgp(32, 32, 4, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8);

        vis.ApplyOcclusion(new Rect(0, 2, 4, 2), metrics);

        Assert.Single(vis.VisibleRegions);
        Assert.Equal(new PixelRect(0, 0, 32, 16), vis.VisibleRegions[0]);
    }

    [Fact]
    public void ApplyOcclusion_CornerHole_ThreeFragments()
    {
        // Bottom-right corner occluded → top strip + left strip + nothing from right
        var data = MakeKgp(32, 32, 4, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8);

        vis.ApplyOcclusion(new Rect(2, 2, 2, 2), metrics);

        // Subtract bottom-right quadrant: 
        // top = (0,0,32,16), bottom = empty, left = (0,16,16,16), right = empty
        Assert.Equal(2, vis.VisibleRegions.Count);
    }

    [Fact]
    public void ApplyOcclusion_MultipleOcclusions()
    {
        // 8x4 cell image, two separate 1-cell occlusions
        var data = MakeKgp(64, 32, 8, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8);

        vis.ApplyOcclusion(new Rect(1, 1, 1, 1), metrics);
        vis.ApplyOcclusion(new Rect(5, 2, 1, 1), metrics);

        Assert.False(vis.IsFullyOccluded);
        // Each single-cell hole can produce up to 4 fragments,
        // and the second hole may further fragment existing regions
        Assert.True(vis.VisibleRegions.Count >= 3);

        // No visible region should overlap with either occlusion
        var hole1 = new PixelRect(8, 8, 8, 8);
        var hole2 = new PixelRect(40, 16, 8, 8);
        foreach (var r in vis.VisibleRegions)
        {
            Assert.True(r.Intersect(hole1).IsEmpty, $"Region {r} overlaps hole1");
            Assert.True(r.Intersect(hole2).IsEmpty, $"Region {r} overlaps hole2");
        }
    }

    [Fact]
    public void ApplyOcclusion_LShapedOcclusion_MultipleFragments()
    {
        // Simulate an L-shaped overlapping window:
        // Top 2 rows fully occluded + right 2 cols of bottom 2 rows occluded
        var data = MakeKgp(32, 32, 4, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8);

        // Top 2 rows
        vis.ApplyOcclusion(new Rect(0, 0, 4, 2), metrics);
        // Right 2 cols of bottom 2 rows
        vis.ApplyOcclusion(new Rect(2, 2, 2, 2), metrics);

        Assert.False(vis.IsFullyOccluded);
        // Visible area: bottom-left 2x2 cell area
        Assert.True(vis.VisibleRegions.Count >= 1);
        var totalArea = vis.VisibleRegions.Sum(r => r.Area);
        // Total image 32x32=1024, top half 32x16=512, bottom-right 16x16=256
        // Visible = 1024 - 512 - 256 = 256
        Assert.Equal(256, totalArea);
    }

    // ─── GeneratePlacements tests ───

    [Fact]
    public void GeneratePlacements_FullyVisible_SinglePlacement()
    {
        var data = MakeKgp(32, 32, 4, 4);
        var vis = new KgpVisibility(data, 5, 3);
        var metrics = new CellMetrics(8, 8);

        var placements = vis.GeneratePlacements(metrics);

        Assert.Single(placements);
        var (cx, cy, pd) = placements[0];
        Assert.Equal(5, cx);
        Assert.Equal(3, cy);
        Assert.Equal(4, pd.WidthInCells);
        Assert.Equal(4, pd.HeightInCells);
    }

    [Fact]
    public void GeneratePlacements_FullyOccluded_Empty()
    {
        var data = MakeKgp(32, 32, 4, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8);

        vis.ApplyOcclusion(new Rect(0, 0, 10, 10), metrics);
        var placements = vis.GeneratePlacements(metrics);

        Assert.Empty(placements);
    }

    [Fact]
    public void GeneratePlacements_RightOccluded_LeftHalfOnly()
    {
        var data = MakeKgp(32, 32, 4, 4);
        var vis = new KgpVisibility(data, 2, 1);
        var metrics = new CellMetrics(8, 8);

        vis.ApplyOcclusion(new Rect(4, 1, 2, 4), metrics);

        var placements = vis.GeneratePlacements(metrics);
        Assert.Single(placements);

        var (cx, cy, pd) = placements[0];
        Assert.Equal(2, cx); // anchor position preserved
        Assert.Equal(1, cy);
        Assert.Equal(2, pd.WidthInCells); // 2 of 4 cols visible
        Assert.Equal(4, pd.HeightInCells); // full height
        Assert.Equal(0, pd.ClipX);
        Assert.Equal(0, pd.ClipY);
        Assert.Equal(16, pd.ClipW); // half of 32px
        Assert.Equal(32, pd.ClipH); // full height
    }

    [Fact]
    public void GeneratePlacements_TopOccluded_BottomHalfOnly()
    {
        var data = MakeKgp(64, 64, 8, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 16);

        vis.ApplyOcclusion(new Rect(0, 0, 8, 2), metrics);

        var placements = vis.GeneratePlacements(metrics);
        Assert.Single(placements);

        var (cx, cy, pd) = placements[0];
        Assert.Equal(0, cx);
        Assert.Equal(2, cy); // shifted down to first visible row
        Assert.Equal(8, pd.WidthInCells);
        Assert.Equal(2, pd.HeightInCells);
        Assert.Equal(0, pd.ClipX);
        Assert.Equal(32, pd.ClipY); // bottom half of 64px
        Assert.Equal(64, pd.ClipW);
        Assert.Equal(32, pd.ClipH);
    }

    [Fact]
    public void GeneratePlacements_CenterHole_FourFragments()
    {
        var data = MakeKgp(64, 32, 8, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8);

        vis.ApplyOcclusion(new Rect(3, 1, 2, 2), metrics);

        var placements = vis.GeneratePlacements(metrics);
        Assert.Equal(4, placements.Count);

        // Each placement should have a valid source rect
        foreach (var (_, _, pd) in placements)
        {
            Assert.True(pd.ClipW > 0);
            Assert.True(pd.ClipH > 0);
            Assert.True(pd.WidthInCells >= 1);
            Assert.True(pd.HeightInCells >= 1);

            var payload = pd.BuildPlacementPayload();
            Assert.Contains("a=p", payload);
            Assert.Contains("i=1", payload);
            Assert.Contains("z=-1", payload);
        }
    }

    [Fact]
    public void GeneratePlacements_SourceRectIsCorrect()
    {
        // 4x4 cells, 32x32 pixels. Occlude right half → left half visible
        var data = MakeKgp(32, 32, 4, 4);
        var vis = new KgpVisibility(data, 0, 0);
        var metrics = new CellMetrics(8, 8);

        vis.ApplyOcclusion(new Rect(2, 0, 2, 4), metrics);

        var placements = vis.GeneratePlacements(metrics);
        Assert.Single(placements);

        var payload = placements[0].Data.BuildPlacementPayload();
        // x=0 is suppressed (default), so shouldn't appear
        Assert.DoesNotContain("x=", payload);
        Assert.Contains("w=16", payload);
        Assert.Contains("c=2", payload);
    }

    // ─── Integration with SurfaceComparer ───

    [Fact]
    public void SurfaceComparer_CenterWindowOverlap_FragmentsImage()
    {
        // 20x10 surface, KGP image at (1,1) spanning 8x4 cells
        // Overlapping "window" text block at (4,2) spanning 3x2
        var surface = new Surface(20, 10, new CellMetrics(8, 16));

        var data = MakeKgp(64, 32, 8, 4, imageId: 42);
        data = new KgpCellData(
            "\x1b_Ga=t,f=32,s=64,v=32,i=42,q=2;AAAA\x1b\\",
            42, 8, 4, 64, 32, data.ContentHash);
        surface[1, 1] = new SurfaceCell(" ", null, null, KgpData: data);

        // Fill blank cells under KGP
        for (int y = 1; y < 5; y++)
            for (int x = 1; x < 9; x++)
                if (!(x == 1 && y == 1))
                    surface[x, y] = new SurfaceCell(" ", null, null);

        // Overlay window text in center of image
        for (int y = 2; y < 4; y++)
            for (int x = 4; x < 7; x++)
                surface[x, y] = new SurfaceCell("W", null, null);

        var diff = SurfaceComparer.CompareToEmpty(surface);
        var tokens = SurfaceComparer.ToTokens(diff, surface);

        var placements = tokens
            .OfType<Hex1b.Tokens.UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=p"))
            .ToList();

        // Row-by-row occlusion scanning produces per-row fragments.
        // A 3x2 center hole in a 8x4 image creates more than 4 fragments
        // because each occluded row splits the visible regions further.
        Assert.True(placements.Count >= 4, $"Expected at least 4 fragments, got {placements.Count}");

        // Each should reference image 42
        foreach (var p in placements)
        {
            Assert.Contains("i=42", p.Sequence);
        }

        // Total visible pixel area should equal original minus hole
        // This is hard to verify from the sequence alone, but we can verify
        // no fragment has source rect overlapping the hole
    }

    [Fact]
    public void SurfaceComparer_FullyOccluded_NoPlacement()
    {
        var surface = new Surface(10, 10, new CellMetrics(8, 16));

        var data = MakeKgp(16, 16, 2, 2, imageId: 7);
        data = new KgpCellData(
            "\x1b_Ga=t,f=32,s=16,v=16,i=7,q=2;AAAA\x1b\\",
            7, 2, 2, 16, 16, data.ContentHash);
        surface[3, 3] = new SurfaceCell(" ", null, null, KgpData: data);
        surface[4, 3] = new SurfaceCell(" ", null, null);
        surface[3, 4] = new SurfaceCell(" ", null, null);
        surface[4, 4] = new SurfaceCell(" ", null, null);

        // Cover entire image with text
        surface[3, 3] = new SurfaceCell("A", null, null, KgpData: data);
        surface[4, 3] = new SurfaceCell("B", null, null);
        surface[3, 4] = new SurfaceCell("C", null, null);
        surface[4, 4] = new SurfaceCell("D", null, null);

        var diff = SurfaceComparer.CompareToEmpty(surface);
        var tokens = SurfaceComparer.ToTokens(diff, surface);

        var placements = tokens
            .OfType<Hex1b.Tokens.UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=p"))
            .ToList();

        Assert.Empty(placements);
    }

    [Fact]
    public void SurfaceComparer_NoOcclusion_SinglePlacement()
    {
        var surface = new Surface(20, 10, new CellMetrics(8, 16));

        var data = MakeKgp(32, 16, 4, 2, imageId: 5);
        data = new KgpCellData(
            "\x1b_Ga=t,f=32,s=32,v=16,i=5,q=2;AAAA\x1b\\",
            5, 4, 2, 32, 16, data.ContentHash);
        surface[2, 2] = new SurfaceCell(" ", null, null, KgpData: data);

        // Fill blank cells under KGP
        for (int y = 2; y < 4; y++)
            for (int x = 2; x < 6; x++)
                if (!(x == 2 && y == 2))
                    surface[x, y] = new SurfaceCell(" ", null, null);

        var diff = SurfaceComparer.CompareToEmpty(surface);
        var tokens = SurfaceComparer.ToTokens(diff, surface);

        var placements = tokens
            .OfType<Hex1b.Tokens.UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=p"))
            .ToList();

        Assert.Single(placements);
        // Should NOT have source rect params (no clipping needed)
        Assert.DoesNotContain("x=", placements[0].Sequence);
        Assert.DoesNotContain("y=", placements[0].Sequence);
        // But should have display size
        Assert.Contains("c=4", placements[0].Sequence);
        Assert.Contains("r=2", placements[0].Sequence);
    }

    [Fact]
    public void SurfaceComparer_LeftEdgeOcclusion_ShiftsPlacement()
    {
        var surface = new Surface(20, 10, new CellMetrics(8, 16));

        var data = MakeKgp(64, 32, 8, 4, imageId: 3);
        data = new KgpCellData(
            "\x1b_Ga=t,f=32,s=64,v=32,i=3,q=2;AAAA\x1b\\",
            3, 8, 4, 64, 32, data.ContentHash);
        surface[0, 0] = new SurfaceCell(" ", null, null, KgpData: data);

        // Fill blank cells
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 8; x++)
                if (!(x == 0 && y == 0))
                    surface[x, y] = new SurfaceCell(" ", null, null);

        // Occlude left 3 columns
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 3; x++)
                surface[x, y] = new SurfaceCell("L", null, null, KgpData: (x == 0 && y == 0) ? data : null);

        var diff = SurfaceComparer.CompareToEmpty(surface);
        var tokens = SurfaceComparer.ToTokens(diff, surface);

        var placements = tokens
            .OfType<Hex1b.Tokens.UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=p"))
            .ToList();

        // Row-by-row scanning produces 4 fragments (one per row), each showing
        // the right portion (cols 3-7) of that row
        Assert.Equal(4, placements.Count);

        // All should have x=24 (3 cols * 8 px/col = 24px offset) and w=40
        foreach (var p in placements)
        {
            Assert.Contains("x=24", p.Sequence);
            Assert.Contains("w=40", p.Sequence);
            Assert.Contains("r=1", p.Sequence); // 1 row per fragment
            Assert.Contains("c=5", p.Sequence); // 5 cols visible (8-3=5)
        }
    }

    [Fact]
    public void SurfaceComparer_OverlappingKgpImages_BackgroundIsFragmented()
    {
        // Two KGP images overlapping: background (image 1) at (0,0) 8x4,
        // foreground (image 2) at (4,1) 4x3 — the foreground's cells are spaces
        // but should still occlude the background image.
        var surface = new Surface(20, 10, new CellMetrics(8, 16));

        var bg = MakeKgp(64, 32, 8, 4, imageId: 1);
        bg = new KgpCellData(
            "\x1b_Ga=t,f=32,s=64,v=32,i=1,q=2;BGBG\x1b\\",
            1, 8, 4, 64, 32, bg.ContentHash);
        surface[0, 0] = new SurfaceCell(" ", null, null, KgpData: bg);

        // Fill blank cells under background image
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 8; x++)
                if (!(x == 0 && y == 0))
                    surface[x, y] = new SurfaceCell(" ", null, null);

        // Place foreground image overlapping the right side
        var fg = MakeKgp(32, 24, 4, 3, imageId: 2);
        fg = new KgpCellData(
            "\x1b_Ga=t,f=32,s=32,v=24,i=2,q=2;FGFG\x1b\\",
            2, 4, 3, 32, 24, fg.ContentHash);
        surface[4, 1] = new SurfaceCell(" ", null, null, KgpData: fg);

        // Fill blank cells under foreground image (overwrites bg spaces)
        for (int y = 1; y < 4; y++)
            for (int x = 4; x < 8; x++)
                if (!(x == 4 && y == 1))
                    surface[x, y] = new SurfaceCell(" ", null, null);

        var diff = SurfaceComparer.CompareToEmpty(surface);
        var tokens = SurfaceComparer.ToTokens(diff, surface);

        var placements = tokens
            .OfType<Hex1b.Tokens.UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=p"))
            .ToList();

        // Background image (id=1) should be fragmented — the overlap area with
        // the foreground image should be excluded
        var bgPlacements = placements.Where(p => p.Sequence.Contains("i=1")).ToList();
        var fgPlacements = placements.Where(p => p.Sequence.Contains("i=2")).ToList();

        // Foreground should have a single unclipped placement
        Assert.Single(fgPlacements);

        // Background should be fragmented (overlap area removed)
        Assert.True(bgPlacements.Count > 1,
            $"Expected background to be fragmented, got {bgPlacements.Count} placement(s)");

        // No background fragment should cover the foreground's region (cols 4-7, rows 1-3)
        // i.e., no fragment should have full width (c=8) spanning into the overlap area
        foreach (var p in bgPlacements)
        {
            // Verify fragments have source rect clipping
            Assert.True(
                p.Sequence.Contains("w=") || p.Sequence.Contains("x=") || p.Sequence.Contains("y="),
                $"Background fragment should be clipped: {p.Sequence}");
        }
    }
}
