#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using System.Security.Cryptography;
using System.Text;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for KGP compositing across surfaces and layers,
/// including clipping, z-ordering, and visibility tracking.
/// </summary>
[TestClass]
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

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Composite_NativeSprite_ClipsInPixelSpace(bool clipOrigin)
    {
        var parent = new Surface(1, 1, DefaultMetrics);
        var child = new Surface(2, 2, DefaultMetrics);
        var data = new KgpCellData(null, 1, 2, 2, 3, 3, new byte[32],
            clipW: 3, clipH: 3, cellOffsetX: 8, cellOffsetY: 18)
        {
            UsesNativeSize = true,
            NativeCellMetrics = DefaultMetrics
        };
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: Track(data));

        parent.Composite(child, offsetX: clipOrigin ? -1 : 0, offsetY: clipOrigin ? -1 : 0);

        var clipped = parent[0, 0].Kgp!.Data;
        Assert.IsTrue(clipped.UsesNativeSize);
        Assert.AreEqual(1, clipped.WidthInCells);
        Assert.AreEqual(1, clipped.HeightInCells);
        Assert.AreEqual(clipOrigin ? 2 : 0, clipped.ClipX);
        Assert.AreEqual(clipOrigin ? 2 : 0, clipped.ClipY);
        Assert.AreEqual(clipOrigin ? 1 : 2, clipped.ClipW);
        Assert.AreEqual(clipOrigin ? 1 : 2, clipped.ClipH);
        Assert.AreEqual(clipOrigin ? 0u : 8u, clipped.CellOffsetX);
        Assert.AreEqual(clipOrigin ? 0u : 18u, clipped.CellOffsetY);
        Assert.DoesNotContain(",c=", clipped.BuildPlacementPayload());
        Assert.DoesNotContain(",r=", clipped.BuildPlacementPayload());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Composite_MixedGraphics_ClipsBothProtocols(bool clipOrigin)
    {
        var parent = new Surface(1, 1, DefaultMetrics);
        parent[0, 0] = new SurfaceCell(" ", null, Hex1bColor.Blue);
        var child = new Surface(2, 2, DefaultMetrics);
        var pixels = new SixelPixelBuffer(1, 1);
        pixels[0, 0] = Rgba32.FromRgb(200, 80, 40);
        new SurfaceRenderContext(child).WriteSixel(pixels, 2, 2);
        var originalSixel = child[0, 0].Sixel!;
        var originalKgp = Track(new KgpCellData(null, 1, 2, 2, 3, 3, new byte[32],
            clipW: 3, clipH: 3, cellOffsetX: 8, cellOffsetY: 18)
        {
            UsesNativeSize = true,
            NativeCellMetrics = DefaultMetrics
        });
        child[0, 0] = child[0, 0] with { Kgp = originalKgp };

        parent.Composite(child, offsetX: clipOrigin ? -1 : 0, offsetY: clipOrigin ? -1 : 0);

        var cell = parent[0, 0];
        Assert.IsTrue(parent.HasSixels);
        Assert.IsTrue(parent.HasKgp);
        Assert.IsTrue(cell.HasSixel);
        Assert.IsTrue(cell.HasKgp);
        Assert.IsTrue(cell.IsSixelUnderlay);
        Assert.IsFalse(cell.OccludesSixel);
        Assert.AreEqual(Hex1bColor.Blue, cell.Background);
        var clippedSixel = cell.Sixel!;
        Assert.AreNotSame(originalSixel, clippedSixel);
        Assert.AreEqual(1, clippedSixel.Data.WidthInCells);
        Assert.AreEqual(1, clippedSixel.Data.HeightInCells);
        Assert.AreEqual(10, clippedSixel.Data.PixelWidth);
        Assert.AreEqual(20, clippedSixel.Data.PixelHeight);
        var clippedKgp = cell.Kgp!;
        Assert.AreNotSame(originalKgp, clippedKgp);
        var clipped = clippedKgp.Data;
        Assert.IsTrue(clipped.UsesNativeSize);
        Assert.AreEqual(DefaultMetrics, clipped.NativeCellMetrics);
        Assert.AreEqual(3u, clipped.SourcePixelWidth);
        Assert.AreEqual(3u, clipped.SourcePixelHeight);
        Assert.AreEqual(1, clipped.WidthInCells);
        Assert.AreEqual(1, clipped.HeightInCells);
        Assert.AreEqual(clipOrigin ? 2 : 0, clipped.ClipX);
        Assert.AreEqual(clipOrigin ? 2 : 0, clipped.ClipY);
        Assert.AreEqual(clipOrigin ? 1 : 2, clipped.ClipW);
        Assert.AreEqual(clipOrigin ? 1 : 2, clipped.ClipH);
        Assert.AreEqual(clipOrigin ? 0u : 8u, clipped.CellOffsetX);
        Assert.AreEqual(clipOrigin ? 0u : 18u, clipped.CellOffsetY);

        var tokens = SurfaceComparer.ToTokens(SurfaceComparer.CompareToEmpty(parent), parent);
        var sequences = tokens.OfType<UnrecognizedSequenceToken>().Select(token => token.Sequence).ToList();
        Assert.IsTrue(sequences.Any(sequence => sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
        var placement = TestSeq.Single(sequences.Where(
            sequence => sequence.StartsWith("\x1b_Ga=p,", StringComparison.Ordinal)));
        Assert.AreEqual(clipped.BuildPlacementPayload(), placement);
        Assert.DoesNotContain(",c=", placement);
        Assert.DoesNotContain(",r=", placement);

        Assert.AreEqual(1, clippedSixel.RefCount);
        Assert.AreEqual(1, clippedKgp.RefCount);
        parent.ClearAndReleaseTrackedObjects();
        Assert.AreEqual(0, clippedSixel.RefCount);
        Assert.AreEqual(0, clippedKgp.RefCount);
        Assert.AreEqual(1, originalSixel.RefCount);
        Assert.AreEqual(1, originalKgp.RefCount);
        child.ClearAndReleaseTrackedObjects();
        Assert.AreEqual(0, originalSixel.RefCount);
        Assert.AreEqual(0, originalKgp.RefCount);
    }

    [TestMethod]
    public void Composite_MixedGraphics_ClippedAwayNativeSpriteDoesNotReappear()
    {
        var parent = new Surface(1, 1, DefaultMetrics);
        var child = new Surface(2, 2, DefaultMetrics);
        var pixels = new SixelPixelBuffer(1, 1);
        pixels[0, 0] = Rgba32.FromRgb(200, 80, 40);
        new SurfaceRenderContext(child).WriteSixel(pixels, 2, 2);
        var originalSixel = child[0, 0].Sixel!;
        var originalKgp = Track(new KgpCellData(null, 1, 2, 2, 3, 3, new byte[32],
            clipW: 3, clipH: 3, cellOffsetX: 10, cellOffsetY: 20)
        {
            UsesNativeSize = true,
            NativeCellMetrics = DefaultMetrics
        });
        child[0, 0] = child[0, 0] with { Kgp = originalKgp };

        parent.Composite(child, 0, 0);

        Assert.IsTrue(parent.HasSixels);
        Assert.IsTrue(parent[0, 0].IsSixelUnderlay);
        Assert.IsFalse(parent[0, 0].OccludesSixel);
        Assert.IsFalse(parent.HasKgp);
        Assert.IsNull(parent[0, 0].Kgp);
        var tokens = SurfaceComparer.ToTokens(SurfaceComparer.CompareToEmpty(parent), parent);
        var sequences = tokens.OfType<UnrecognizedSequenceToken>().Select(token => token.Sequence).ToList();
        Assert.IsTrue(sequences.Any(sequence => sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
        Assert.IsFalse(sequences.Any(sequence => sequence.StartsWith("\x1b_G", StringComparison.Ordinal)));

        var clippedSixel = parent[0, 0].Sixel!;
        Assert.AreEqual(1, clippedSixel.RefCount);
        parent.ClearAndReleaseTrackedObjects();
        Assert.AreEqual(0, clippedSixel.RefCount);
        Assert.AreEqual(1, originalSixel.RefCount);
        Assert.AreEqual(1, originalKgp.RefCount);
        child.ClearAndReleaseTrackedObjects();
        Assert.AreEqual(0, originalSixel.RefCount);
        Assert.AreEqual(0, originalKgp.RefCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Composite_MixedGraphics_ReanchoredNativeSpritePreservesUnclippedSixel(bool occludesSixel)
    {
        var parent = new Surface(1, 1, DefaultMetrics);
        var child = new Surface(2, 2, DefaultMetrics);
        var pixels = new SixelPixelBuffer(1, 1);
        pixels[0, 0] = Rgba32.FromRgb(200, 80, 40);
        var context = new SurfaceRenderContext(child);
        context.SetCursorPosition(1, 1);
        context.WriteSixel(pixels, 1, 1);
        child[1, 1] = child[1, 1] with
        {
            Background = Hex1bColor.Blue,
            OccludesSixel = occludesSixel
        };
        var originalSixel = child[1, 1].Sixel!;
        var originalKgp = Track(new KgpCellData(null, 1, 2, 2, 3, 3, new byte[32],
            clipW: 3, clipH: 3, cellOffsetX: 8, cellOffsetY: 18)
        {
            UsesNativeSize = true,
            NativeCellMetrics = DefaultMetrics
        });
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: originalKgp);

        parent.Composite(child, -1, -1);

        Assert.IsTrue(parent.HasSixels);
        Assert.IsTrue(parent.HasKgp);
        Assert.AreSame(originalSixel, parent[0, 0].Sixel);
        Assert.IsTrue(parent[0, 0].IsSixelUnderlay);
        Assert.AreEqual(occludesSixel, parent[0, 0].OccludesSixel);
        Assert.AreEqual(Hex1bColor.Blue, parent[0, 0].Background);
        var tokens = SurfaceComparer.ToTokens(SurfaceComparer.CompareToEmpty(parent), parent);
        var sequences = tokens.OfType<UnrecognizedSequenceToken>().Select(token => token.Sequence).ToList();
        Assert.AreEqual(!occludesSixel,
            sequences.Any(sequence => sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
        Assert.IsTrue(sequences.Any(sequence => sequence.StartsWith("\x1b_Ga=p,", StringComparison.Ordinal)));

        var clippedKgp = parent[0, 0].Kgp!;
        Assert.AreEqual(2, originalSixel.RefCount);
        Assert.AreEqual(1, clippedKgp.RefCount);
        parent.ClearAndReleaseTrackedObjects();
        Assert.AreEqual(0, clippedKgp.RefCount);
        Assert.AreEqual(1, originalSixel.RefCount);
        Assert.AreEqual(1, originalKgp.RefCount);
        child.ClearAndReleaseTrackedObjects();
        Assert.AreEqual(0, originalSixel.RefCount);
        Assert.AreEqual(0, originalKgp.RefCount);
    }

    [TestMethod]
    public void Composite_KgpImage_FitsInBounds_NoClipping()
    {
        var parent = new Surface(10, 5, DefaultMetrics);
        var child = new Surface(4, 2, DefaultMetrics);
        
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 0, offsetY: 0);

        Assert.IsTrue(parent[0, 0].HasKgp);
        Assert.AreEqual(4, parent[0, 0].Kgp!.Data.WidthInCells);
        Assert.AreEqual(2, parent[0, 0].Kgp!.Data.HeightInCells);
    }

    [TestMethod]
    public void Composite_KgpImage_ExtendsRight_ClipsWidth()
    {
        var parent = new Surface(6, 5, DefaultMetrics);
        var child = new Surface(8, 2, DefaultMetrics);
        
        // 8-cell wide image placed at dest x=3 in a 6-wide parent → clips to 3 cells
        var kgpData = CreateKgpData(widthInCells: 8, heightInCells: 2, sourcePixelWidth: 80, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 3, offsetY: 0);

        Assert.IsTrue(parent[3, 0].HasKgp);
        Assert.AreEqual(3, parent[3, 0].Kgp!.Data.WidthInCells);
    }

    [TestMethod]
    public void Composite_KgpImage_ExtendsDown_ClipsHeight()
    {
        var parent = new Surface(10, 3, DefaultMetrics);
        var child = new Surface(4, 4, DefaultMetrics);
        
        // 4-cell tall image placed at dest y=2 in a 3-tall parent → clips to 1 cell
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 4, sourcePixelWidth: 40, sourcePixelHeight: 80);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 0, offsetY: 2);

        Assert.IsTrue(parent[0, 2].HasKgp);
        Assert.AreEqual(1, parent[0, 2].Kgp!.Data.HeightInCells);
    }

    [TestMethod]
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
        Assert.IsFalse(parent[0, 0].HasKgp);
    }

    [TestMethod]
    public void Composite_KgpImage_PreservesZIndex()
    {
        var parent = new Surface(10, 5, DefaultMetrics);
        var child = new Surface(4, 2, DefaultMetrics);
        
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, zIndex: 5);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 0, offsetY: 0);

        Assert.AreEqual(5, parent[0, 0].Kgp!.Data.ZIndex);
    }

    [TestMethod]
    public void Composite_KgpImage_ClippedPreservesZIndex()
    {
        var parent = new Surface(3, 3, DefaultMetrics);
        var child = new Surface(6, 2, DefaultMetrics);
        
        var kgpData = CreateKgpData(widthInCells: 6, heightInCells: 2, zIndex: 3, sourcePixelWidth: 60, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 1, offsetY: 0);

        Assert.IsTrue(parent[1, 0].HasKgp);
        Assert.AreEqual(3, parent[1, 0].Kgp!.Data.ZIndex);
    }

    [TestMethod]
    public void Composite_KgpImage_ClippedByClipRect_ReanchorsVisibleRegion()
    {
        var parent = new Surface(10, 5, DefaultMetrics);
        var child = new Surface(6, 4, DefaultMetrics);

        var kgpData = CreateKgpData(widthInCells: 6, heightInCells: 4, sourcePixelWidth: 60, sourcePixelHeight: 80);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));

        parent.Composite(child, offsetX: 0, offsetY: 0, clip: new Rect(2, 1, 8, 4));

        Assert.IsFalse(parent[0, 0].HasKgp);
        Assert.IsTrue(parent[2, 1].HasKgp);
        Assert.AreEqual(4, parent[2, 1].Kgp!.Data.WidthInCells);
        Assert.AreEqual(3, parent[2, 1].Kgp!.Data.HeightInCells);
        Assert.AreEqual(20, parent[2, 1].Kgp!.Data.ClipX);
        Assert.AreEqual(20, parent[2, 1].Kgp!.Data.ClipY);
        Assert.AreEqual(40, parent[2, 1].Kgp!.Data.ClipW);
        Assert.AreEqual(60, parent[2, 1].Kgp!.Data.ClipH);
    }

    [TestMethod]
    public void Composite_KgpImage_WithNegativeOffset_ReanchorsVisibleRegion()
    {
        var parent = new Surface(10, 5, DefaultMetrics);
        var child = new Surface(6, 4, DefaultMetrics);

        var kgpData = CreateKgpData(widthInCells: 6, heightInCells: 4, sourcePixelWidth: 60, sourcePixelHeight: 80);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));

        parent.Composite(child, offsetX: -2, offsetY: -1);

        Assert.IsTrue(parent[0, 0].HasKgp);
        Assert.AreEqual(4, parent[0, 0].Kgp!.Data.WidthInCells);
        Assert.AreEqual(3, parent[0, 0].Kgp!.Data.HeightInCells);
        Assert.AreEqual(20, parent[0, 0].Kgp!.Data.ClipX);
        Assert.AreEqual(20, parent[0, 0].Kgp!.Data.ClipY);
        Assert.AreEqual(40, parent[0, 0].Kgp!.Data.ClipW);
        Assert.AreEqual(60, parent[0, 0].Kgp!.Data.ClipH);
    }

    [TestMethod]
    public void RenderChild_OccluderContent_IsClippedToVisibleCompositeRegion()
    {
        var parent = new Surface(12, 12, DefaultMetrics);
        var registry = new Hex1b.Kgp.KgpImageRegistry();
        var context = new SurfaceRenderContext(parent, Hex1bThemes.Default)
        {
            CachingEnabled = false,
            KgpRegistry = registry,
            CurrentLayoutProvider = new RectLayoutProvider(new Rect(0, 0, 6, 5))
        };

        var child = new TestWritingNode((0, 1, "VISIBLE"), (1, 7, "BTN"));
        child.Arrange(new Rect(0, 0, 10, 10));

        context.RenderChild(child);

        var occluder = TestSeq.Single(registry.Occluders);
        Assert.AreEqual(new Rect(0, 1, 6, 4), occluder.Bounds);
    }

    [TestMethod]
    public void RenderChild_HiddenNestedButtonRow_DoesNotRegisterOffscreenOccluder()
    {
        var parent = new Surface(20, 12, DefaultMetrics);
        var registry = new Hex1b.Kgp.KgpImageRegistry();
        var context = new SurfaceRenderContext(parent, Hex1bThemes.Default)
        {
            CachingEnabled = false,
            KgpRegistry = registry,
            CurrentLayoutProvider = new RectLayoutProvider(new Rect(0, 0, 12, 5))
        };

        var child = new VStackNode
        {
            Children =
            [
                new TextBlockNode { Text = "Line 1" },
                new TextBlockNode { Text = "Line 2" },
                new TextBlockNode { Text = "Line 3" },
                new TextBlockNode { Text = "Line 4" },
                new TextBlockNode { Text = "Line 5" },
                new TextBlockNode { Text = "Line 6" },
                new HStackNode
                {
                    Children =
                    [
                        new TextBlockNode { Text = " " },
                        new ButtonNode { Label = "Close" }
                    ]
                }
            ]
        };

        child.Measure(new Constraints(0, 12, 0, 10));
        child.Arrange(new Rect(0, 0, 12, 10));

        context.RenderChild(child);

        Assert.IsFalse(registry.Occluders.Any(o => o.Bounds.Y >= 5));
    }

    #endregion

    #region CompositeSurface KGP Layer Resolution

    [TestMethod]
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
        Assert.IsTrue(resolved.HasKgp);
    }

    [TestMethod]
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
        Assert.AreEqual("X", resolved.Character);
        Assert.IsFalse(resolved.HasKgp);
    }

    [TestMethod]
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
        Assert.IsTrue(resolved.HasKgp);
    }

    [TestMethod]
    public void CompositeSurface_KgpOnlyLayer_Preserved()
    {
        var layer = new Surface(10, 5, DefaultMetrics);
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        layer[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        var composite = new CompositeSurface(10, 5);
        composite.AddLayer(layer);

        var resolved = composite.GetCell(0, 0);
        Assert.IsTrue(resolved.HasKgp);
        Assert.AreEqual(1u, resolved.Kgp!.Data.ImageId);
    }

    private sealed class TestWritingNode(params (int X, int Y, string Text)[] writes) : Hex1bNode
    {
        protected override Size MeasureCore(Constraints constraints) => new(Bounds.Width, Bounds.Height);

        public override void Render(Hex1bRenderContext context)
        {
            foreach (var (x, y, text) in writes)
            {
                context.SetCursorPosition(Bounds.X + x, Bounds.Y + y);
                context.Write(text);
            }
        }
    }

    #endregion

    #region KgpVisibility

    [TestMethod]
    public void KgpVisibility_FullyVisible_SinglePlacement()
    {
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, sourcePixelWidth: 40, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        Assert.IsTrue(vis.IsFullyVisible);
        Assert.IsFalse(vis.IsFullyOccluded);

        var placements = vis.GeneratePlacements(DefaultMetrics);
        TestSeq.Single(placements);
        Assert.AreEqual(0, placements[0].CellX);
        Assert.AreEqual(0, placements[0].CellY);
    }

    [TestMethod]
    public void KgpVisibility_FullyOccluded_NoPlacements()
    {
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, sourcePixelWidth: 40, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        // Occlude the entire image
        vis.ApplyOcclusion(new Rect(0, 0, 4, 2), DefaultMetrics);

        Assert.IsTrue(vis.IsFullyOccluded);
        Assert.IsFalse(vis.IsFullyVisible);

        var placements = vis.GeneratePlacements(DefaultMetrics);
        Assert.IsEmpty(placements);
    }

    [TestMethod]
    public void KgpVisibility_PartialOcclusion_ReducesVisibleRegions()
    {
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, sourcePixelWidth: 40, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        // Occlude the right half of the image
        vis.ApplyOcclusion(new Rect(2, 0, 2, 2), DefaultMetrics);

        Assert.IsFalse(vis.IsFullyVisible);
        Assert.IsFalse(vis.IsFullyOccluded);
        Assert.IsTrue(vis.VisibleRegions.Count >= 1);
    }

    [TestMethod]
    public void KgpVisibility_OcclusionOutsideImage_NoEffect()
    {
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, sourcePixelWidth: 40, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        // Occlude outside the image bounds
        vis.ApplyOcclusion(new Rect(10, 10, 5, 5), DefaultMetrics);

        Assert.IsFalse(vis.IsFullyVisible); // ApplyOcclusion sets this to false
        Assert.IsFalse(vis.IsFullyOccluded);
        
        var placements = vis.GeneratePlacements(DefaultMetrics);
        // Should still generate a placement for the visible region
        Assert.IsTrue(placements.Count >= 1);
    }

    [TestMethod]
    public void KgpVisibility_CenterOcclusion_GeneratesMultipleRegions()
    {
        var kgpData = CreateKgpData(widthInCells: 10, heightInCells: 10, sourcePixelWidth: 100, sourcePixelHeight: 200);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        // Occlude a 2x2 block in the center of the 10x10 image
        vis.ApplyOcclusion(new Rect(4, 4, 2, 2), DefaultMetrics);

        Assert.IsFalse(vis.IsFullyOccluded);
        // Punching a hole in the center should create 4 regions (top, bottom, left, right)
        Assert.IsTrue(vis.VisibleRegions.Count == 4);
    }

    [TestMethod]
    public void KgpVisibility_PreservesZIndex()
    {
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2, zIndex: 5, sourcePixelWidth: 40, sourcePixelHeight: 40);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        var placements = vis.GeneratePlacements(DefaultMetrics);
        TestSeq.Single(placements);
        Assert.AreEqual(5, placements[0].Data.ZIndex);
    }

    [TestMethod]
    public void KgpVisibility_PartialOcclusion_PlacementsPreserveZIndex()
    {
        var kgpData = CreateKgpData(widthInCells: 10, heightInCells: 10, zIndex: 3, sourcePixelWidth: 100, sourcePixelHeight: 200);
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 0, anchorY: 0, layerIndex: 0);

        vis.ApplyOcclusion(new Rect(4, 4, 2, 2), DefaultMetrics);

        var placements = vis.GeneratePlacements(DefaultMetrics);
        foreach (var (data, _, _) in placements)
        {
            Assert.AreEqual(3, data.ZIndex);
        }
    }

    [TestMethod]
    public void KgpVisibility_LayerIndex_Stored()
    {
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        var vis = new KgpVisibility(tracked, anchorX: 5, anchorY: 3, layerIndex: 7);

        Assert.AreEqual(7, vis.LayerIndex);
        Assert.AreEqual((5, 3), vis.AnchorPosition);
    }

    #endregion

    #region SurfaceLayerContext.CreateKgp

    [TestMethod]
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

        Assert.IsNotNull(tracked);
        Assert.AreEqual(kgpData.ImageId, tracked!.Data.ImageId);
    }

    [TestMethod]
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

        Assert.IsNull(tracked);
    }

    [TestMethod]
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

        Assert.IsNotNull(tracked);
    }

    [TestMethod]
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

        Assert.IsNotNull(tracked);
        Assert.IsTrue(tracked!.Data.ZIndex < 0);
    }

    [TestMethod]
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

        Assert.IsNotNull(tracked);
        Assert.IsTrue(tracked!.Data.ZIndex > 0);
    }

    [TestMethod]
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

        Assert.IsNotNull(tracked1);
        Assert.IsNotNull(tracked2);
        Assert.AreSame(tracked1, tracked2);
    }

    #endregion

    #region Surface.Composite preserves KGP through cell operations

    [TestMethod]
    public void Surface_HasKgp_TracksThroughComposite()
    {
        var parent = new Surface(10, 5, DefaultMetrics);
        Assert.IsFalse(parent.HasKgp);

        var child = new Surface(4, 2, DefaultMetrics);
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: tracked);

        parent.Composite(child, offsetX: 0, offsetY: 0);
        Assert.IsTrue(parent.HasKgp);
    }

    [TestMethod]
    public void Surface_Clone_PreservesKgp()
    {
        var surface = new Surface(10, 5, DefaultMetrics);
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);
        surface[2, 1] = new SurfaceCell(" ", null, null, Kgp: tracked);

        var clone = surface.Clone();
        Assert.IsTrue(clone.HasKgp);
        Assert.IsTrue(clone[2, 1].HasKgp);
    }

    #endregion
}
