using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Tokens;

namespace Hex1b.Tests;

public class KgpOcclusionSolverTests
{
    private static KgpCellData MakeKgpData(uint imageId, int cellW, int cellH, int pixelW = 100, int pixelH = 100)
    {
        var hash = new byte[32];
        hash[0] = (byte)(imageId >> 24);
        hash[1] = (byte)(imageId >> 16);
        hash[2] = (byte)(imageId >> 8);
        hash[3] = (byte)(imageId);
        return new KgpCellData(
            $"\x1b_Ga=t,f=32,s={pixelW},v={pixelH},i={imageId},t=d,q=2;AAAA\x1b\\",
            imageId, cellW, cellH, (uint)pixelW, (uint)pixelH, hash);
    }

    [Fact]
    public void NoOccluders_ReturnsFullImage()
    {
        var registry = new KgpImageRegistry();
        var data = MakeKgpData(1, 20, 10);
        registry.RegisterImage(data, 5, 5);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        Assert.Single(fragments);
        var f = fragments[0];
        Assert.Equal(1u, f.ImageId);
        Assert.Equal(5, f.AbsoluteX);
        Assert.Equal(5, f.AbsoluteY);
        Assert.Equal(20, f.CellWidth);
        Assert.Equal(10, f.CellHeight);
        // No clip (original image had no clip)
        Assert.Equal(0, f.ClipX);
        Assert.Equal(0, f.ClipY);
        Assert.Equal(0, f.ClipW);
        Assert.Equal(0, f.ClipH);
    }

    [Fact]
    public void FullyOccluded_ReturnsEmpty()
    {
        var registry = new KgpImageRegistry();
        var data = MakeKgpData(1, 10, 10);
        registry.RegisterImage(data, 5, 5);

        // Window at layer 1 fully covers the image
        registry.PushLayer();
        registry.RegisterOccluder(0, 0, 30, 30);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        Assert.Empty(fragments);
    }

    [Fact]
    public void PartialOcclusion_RightSide_ReturnsLeftStrip()
    {
        var registry = new KgpImageRegistry();
        // Image at (0,0), 20x10 cells
        var data = MakeKgpData(1, 20, 10, 200, 100);
        registry.RegisterImage(data, 0, 0);

        // Window covers the right half: (10,0), 20x10
        registry.PushLayer();
        registry.RegisterOccluder(10, 0, 20, 10);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        Assert.Single(fragments);
        var f = fragments[0];
        Assert.Equal(0, f.AbsoluteX);
        Assert.Equal(0, f.AbsoluteY);
        Assert.Equal(10, f.CellWidth);
        Assert.Equal(10, f.CellHeight);
        // Clip: left half of source image
        Assert.Equal(0, f.ClipX);
        Assert.Equal(0, f.ClipY);
        Assert.Equal(100, f.ClipW); // 10/20 * 200 = 100
        Assert.Equal(100, f.ClipH);
    }

    [Fact]
    public void PartialOcclusion_Center_ReturnsFourStrips()
    {
        var registry = new KgpImageRegistry();
        // Image at (0,0), 20x20 cells, 200x200 pixels
        var data = MakeKgpData(1, 20, 20, 200, 200);
        registry.RegisterImage(data, 0, 0);

        // Window covers center: (5,5), 10x10
        registry.PushLayer();
        registry.RegisterOccluder(5, 5, 10, 10);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        Assert.Equal(4, fragments.Count);

        // Top strip: (0,0) 20x5
        var top = fragments.First(f => f.AbsoluteY == 0 && f.CellHeight == 5);
        Assert.Equal(0, top.AbsoluteX);
        Assert.Equal(20, top.CellWidth);

        // Bottom strip: (0,15) 20x5
        var bottom = fragments.First(f => f.AbsoluteY == 15);
        Assert.Equal(0, bottom.AbsoluteX);
        Assert.Equal(20, bottom.CellWidth);
        Assert.Equal(5, bottom.CellHeight);

        // Left strip: (0,5) 5x10
        var left = fragments.First(f => f.AbsoluteX == 0 && f.AbsoluteY == 5);
        Assert.Equal(5, left.CellWidth);
        Assert.Equal(10, left.CellHeight);

        // Right strip: (15,5) 5x10
        var right = fragments.First(f => f.AbsoluteX == 15 && f.AbsoluteY == 5);
        Assert.Equal(5, right.CellWidth);
        Assert.Equal(10, right.CellHeight);
    }

    [Fact]
    public void MultipleOccluders_SubtractsAll()
    {
        var registry = new KgpImageRegistry();
        // Image at (0,0), 30x10
        var data = MakeKgpData(1, 30, 10, 300, 100);
        registry.RegisterImage(data, 0, 0);

        // Two windows at layer 1 covering left and right thirds
        registry.PushLayer();
        registry.RegisterOccluder(0, 0, 10, 10); // left
        registry.RegisterOccluder(20, 0, 10, 10); // right

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        // Only the middle strip should remain: (10,0) 10x10
        Assert.Single(fragments);
        var f = fragments[0];
        Assert.Equal(10, f.AbsoluteX);
        Assert.Equal(0, f.AbsoluteY);
        Assert.Equal(10, f.CellWidth);
        Assert.Equal(10, f.CellHeight);
    }

    [Fact]
    public void SameLayerImages_NoOcclusion()
    {
        var registry = new KgpImageRegistry();
        var data1 = MakeKgpData(1, 10, 10);
        var data2 = MakeKgpData(2, 10, 10);
        // Both at layer 0, overlapping
        registry.RegisterImage(data1, 0, 0);
        registry.RegisterImage(data2, 5, 5);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        // Both images should have single fragment (no mutual occlusion)
        Assert.Equal(2, fragments.Count);
        Assert.Contains(fragments, f => f.ImageId == 1);
        Assert.Contains(fragments, f => f.ImageId == 2);
    }

    [Fact]
    public void ClipCoordinates_MapCorrectly()
    {
        var registry = new KgpImageRegistry();
        // Image at (0,0), 20x10 cells, 400x200 pixels
        var data = MakeKgpData(1, 20, 10, 400, 200);
        registry.RegisterImage(data, 0, 0);

        // Window covers top-left quadrant: (0,0), 10x5
        registry.PushLayer();
        registry.RegisterOccluder(0, 0, 10, 5);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        // Should have 3 fragments: bottom strip, right strip on top row, and left+right don't overlap
        // Actually: top strip (above occluder) is zero height, bottom strip, left strip, right strip

        // Bottom strip: (0,5) 20x5, clipY = 5/10*200 = 100, clipH = 5/10*200 = 100
        var bottom = fragments.First(f => f.AbsoluteY == 5 && f.CellWidth == 20);
        Assert.Equal(0, bottom.ClipX);
        Assert.Equal(100, bottom.ClipY);
        Assert.Equal(400, bottom.ClipW);
        Assert.Equal(100, bottom.ClipH);

        // Right strip (in middle band): (10,0) 10x5
        var right = fragments.First(f => f.AbsoluteX == 10 && f.AbsoluteY == 0);
        Assert.Equal(200, right.ClipX); // 10/20*400 = 200
        Assert.Equal(0, right.ClipY);
        Assert.Equal(200, right.ClipW); // 10/20*400 = 200
        Assert.Equal(100, right.ClipH); // 5/10*200 = 100
    }

    [Fact]
    public void AlreadyClippedImage_ComposesClip()
    {
        var registry = new KgpImageRegistry();
        // Image already has clip: showing pixels (50,50)-(150,150) of a 200x200 source
        var hash = new byte[32];
        hash[0] = 0; hash[1] = 0; hash[2] = 0; hash[3] = 1;
        var data = new KgpCellData(
            "\x1b_Ga=t,f=32,s=200,v=200,i=1,t=d,q=2;AAAA\x1b\\",
            1, 10, 10, 200, 200, hash,
            clipX: 50, clipY: 50, clipW: 100, clipH: 100);
        registry.RegisterImage(data, 0, 0);

        // Window covers right half: (5,0), 10x10
        registry.PushLayer();
        registry.RegisterOccluder(5, 0, 10, 10);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        Assert.Single(fragments);
        var f = fragments[0];
        Assert.Equal(0, f.AbsoluteX);
        Assert.Equal(0, f.AbsoluteY);
        Assert.Equal(5, f.CellWidth);
        Assert.Equal(10, f.CellHeight);
        // Composed clip: original (50,50,100,100) + left half (0-5 of 10 cells)
        // clipX = 50 + 0 * 100 / 10 = 50
        // clipW = 5 * 100 / 10 = 50
        Assert.Equal(50, f.ClipX);
        Assert.Equal(50, f.ClipY);
        Assert.Equal(50, f.ClipW);
        Assert.Equal(100, f.ClipH);
    }

    [Fact]
    public void OccluderFullyOutside_NoChange()
    {
        var registry = new KgpImageRegistry();
        var data = MakeKgpData(1, 10, 10);
        registry.RegisterImage(data, 0, 0);

        // Window that doesn't overlap the image
        registry.PushLayer();
        registry.RegisterOccluder(20, 20, 10, 10);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        Assert.Single(fragments);
        Assert.Equal(10, fragments[0].CellWidth);
        Assert.Equal(10, fragments[0].CellHeight);
    }

    [Fact]
    public void MultipleImages_IndependentFragments()
    {
        var registry = new KgpImageRegistry();
        // Image 1 at layer 0
        var data1 = MakeKgpData(1, 20, 10);
        registry.RegisterImage(data1, 0, 0);

        // Image 2 at layer 1 (inside window 1)
        registry.PushLayer();
        registry.RegisterOccluder(10, 0, 15, 10);
        var data2 = MakeKgpData(2, 10, 5);
        registry.RegisterImage(data2, 12, 2);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        // Image 1 should be shredded (window 1 occluder at layer 1 > image layer 0)
        var img1Fragments = fragments.Where(f => f.ImageId == 1).ToList();
        Assert.True(img1Fragments.Count >= 1); // At least the left strip

        // Image 2 should be full (no occluder above layer 1)
        var img2Fragments = fragments.Where(f => f.ImageId == 2).ToList();
        Assert.Single(img2Fragments);
        Assert.Equal(10, img2Fragments[0].CellWidth);
        Assert.Equal(5, img2Fragments[0].CellHeight);
    }

    [Fact]
    public void SubtractRect_NoOverlap_ReturnsOriginal()
    {
        var rects = new List<Rect> { new(0, 0, 10, 10) };
        var result = KgpOcclusionSolver.SubtractFromAll(rects, new Rect(20, 20, 5, 5));

        Assert.Single(result);
        Assert.Equal(new Rect(0, 0, 10, 10), result[0]);
    }

    [Fact]
    public void SubtractRect_FullOverlap_ReturnsEmpty()
    {
        var rects = new List<Rect> { new(5, 5, 10, 10) };
        var result = KgpOcclusionSolver.SubtractFromAll(rects, new Rect(0, 0, 20, 20));

        Assert.Empty(result);
    }

    [Fact]
    public void SubtractRect_PartialOverlap_ReturnsStrips()
    {
        // 10x10 rect, subtract center 4x4
        var rects = new List<Rect> { new(0, 0, 10, 10) };
        var result = KgpOcclusionSolver.SubtractFromAll(rects, new Rect(3, 3, 4, 4));

        // Should produce 4 strips
        Assert.Equal(4, result.Count);

        // Top: (0,0) 10x3
        Assert.Contains(result, r => r.X == 0 && r.Y == 0 && r.Width == 10 && r.Height == 3);
        // Bottom: (0,7) 10x3
        Assert.Contains(result, r => r.X == 0 && r.Y == 7 && r.Width == 10 && r.Height == 3);
        // Left: (0,3) 3x4
        Assert.Contains(result, r => r.X == 0 && r.Y == 3 && r.Width == 3 && r.Height == 4);
        // Right: (7,3) 3x4
        Assert.Contains(result, r => r.X == 7 && r.Y == 3 && r.Width == 3 && r.Height == 4);
    }

    [Fact]
    public void TrackerIntegration_FragmentsChangeOnDrag()
    {
        var tracker = new KgpPlacementTracker();

        // Frame 1: Image at (0,0) 20x10, no occlusion
        var data = MakeKgpData(1, 20, 10, 200, 100);
        var fragments1 = new List<KgpFragment>
        {
            new(1, 0, 0, 20, 10, 0, 0, 0, 0, data)
        };
        var (before1, _) = tracker.GenerateCommands(fragments1);
        // Should transmit + place
        Assert.True(before1.Count > 0);

        // Frame 2: Same image, now occluded by window → 2 fragments
        var fragments2 = new List<KgpFragment>
        {
            new(1, 0, 0, 10, 10, 0, 0, 100, 100, data),
            new(1, 0, 10, 20, 5, 0, 100, 200, 50, data) // bottom strip doesn't exist, but for test
        };
        var (before2, _) = tracker.GenerateCommands(fragments2);
        // Should delete old placement + emit 2 new placements
        Assert.Contains(before2, t => t is UnrecognizedSequenceToken u && u.Sequence.Contains("a=d"));
    }
}
