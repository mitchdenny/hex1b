using System.Security.Cryptography;
using System.Text;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for KGP support within the surface layer system:
/// draw layers, computed layers, widget layers, and cross-layer queries.
/// </summary>
[TestClass]
public class KgpSurfaceLayerTests
{
    private static readonly CellMetrics DefaultMetrics = new(10, 20);

    private static KgpCellData CreateKgpData(
        string imageContent = "test-image",
        int widthInCells = 4,
        int heightInCells = 2,
        int zIndex = -1,
        uint imageId = 1)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(imageContent));
        var pw = (uint)(widthInCells * 10);
        var ph = (uint)(heightInCells * 20);
        return new KgpCellData(
            transmitPayload: $"\x1b_Ga=t,f=32,s={pw},v={ph},i={imageId};{Convert.ToBase64String(hash)}\x1b\\",
            imageId: imageId,
            widthInCells: widthInCells,
            heightInCells: heightInCells,
            sourcePixelWidth: pw,
            sourcePixelHeight: ph,
            contentHash: hash,
            zIndex: zIndex);
    }

    private static TrackedObject<KgpCellData> CreateTrackedKgp(
        string imageContent = "test-image",
        int widthInCells = 4,
        int heightInCells = 2,
        int zIndex = -1,
        uint imageId = 1)
    {
        var store = new TrackedObjectStore();
        return store.GetOrCreateKgp(CreateKgpData(imageContent, widthInCells, heightInCells, zIndex, imageId));
    }

    // --- ISurfaceSource.HasKgp ---

    [TestMethod]
    public void ISurfaceSource_Surface_HasKgp_ReturnsTrueWhenKgpPresent()
    {
        var surface = new Surface(10, 5, DefaultMetrics);
        ISurfaceSource source = surface;
        Assert.IsFalse(source.HasKgp);

        surface[0, 0] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp() };
        Assert.IsTrue(source.HasKgp);
    }

    [TestMethod]
    public void ISurfaceSource_CompositeSurface_HasKgp_ReturnsTrueWhenLayerHasKgp()
    {
        var layer = new Surface(10, 5, DefaultMetrics);
        layer[0, 0] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp() };

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(layer);
        Assert.IsTrue(composite.HasKgp);
    }

    [TestMethod]
    public void ISurfaceSource_CompositeSurface_HasKgp_FalseWhenNoKgp()
    {
        var layer = new Surface(10, 5, DefaultMetrics);
        layer[0, 0] = new SurfaceCell("A", null, null);

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(layer);
        Assert.IsFalse(composite.HasKgp);
    }

    // --- DrawSurfaceLayer with KGP ---

    [TestMethod]
    public void DrawLayer_WritesKgpCell_AppearsInSurface()
    {
        var trackedKgp = CreateTrackedKgp();

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        var drawSurface = new Surface(10, 5, DefaultMetrics);
        drawSurface[1, 1] = new SurfaceCell { Character = " ", Kgp = trackedKgp };
        composite.AddLayer(drawSurface);

        var flattened = composite.Flatten();
        Assert.IsTrue(flattened[1, 1].HasKgp);
        Assert.AreEqual(trackedKgp.Data.ImageId, flattened[1, 1].Kgp!.Data.ImageId);
    }

    [TestMethod]
    public void DrawLayer_KgpBelowTextLayer_TextOverlaysKgp()
    {
        // Layer 0: KGP image (below text)
        var kgpSurface = new Surface(10, 5, DefaultMetrics);
        var trackedKgp = CreateTrackedKgp(zIndex: -1);
        kgpSurface[0, 0] = new SurfaceCell { Character = " ", Kgp = trackedKgp };

        // Layer 1: Text on top
        var textSurface = new Surface(10, 5, DefaultMetrics);
        textSurface[0, 0] = new SurfaceCell("X", null, null);

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(kgpSurface);
        composite.AddLayer(textSurface);

        var flattened = composite.Flatten();
        // Text is opaque — it replaces the KGP cell
        Assert.AreEqual("X", flattened[0, 0].Character);
    }

    [TestMethod]
    public void DrawLayer_KgpAboveTextLayer_KgpOverlaysText()
    {
        // Layer 0: Text
        var textSurface = new Surface(10, 5, DefaultMetrics);
        textSurface[0, 0] = new SurfaceCell("X", null, null);

        // Layer 1: KGP on top (above text)
        var kgpSurface = new Surface(10, 5, DefaultMetrics);
        var trackedKgp = CreateTrackedKgp(zIndex: 1);
        kgpSurface[0, 0] = new SurfaceCell { Character = " ", Kgp = trackedKgp };

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(textSurface);
        composite.AddLayer(kgpSurface);

        var flattened = composite.Flatten();
        Assert.IsTrue(flattened[0, 0].HasKgp);
    }

    [TestMethod]
    public void DrawLayer_KgpPreservedThroughTransparentLayer()
    {
        // Layer 0: KGP image
        var kgpSurface = new Surface(10, 5, DefaultMetrics);
        kgpSurface[0, 0] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp() };

        // Layer 1: empty (transparent / unwritten) cells
        var emptySurface = new Surface(10, 5, DefaultMetrics);

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(kgpSurface);
        composite.AddLayer(emptySurface);

        var flattened = composite.Flatten();
        Assert.IsTrue(flattened[0, 0].HasKgp);
    }

    // --- ComputeContext.HasKgpBelow ---

    [TestMethod]
    public void ComputedLayer_HasKgpBelow_ReturnsTrueWhenKgpInLowerLayer()
    {
        var kgpSurface = new Surface(10, 5, DefaultMetrics);
        kgpSurface[2, 1] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp() };

        bool hasKgpAtAnchor = false;
        bool hasKgpAtEmpty = false;

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(kgpSurface);
        composite.AddComputedLayer(10, 5, ctx =>
        {
            if (ctx.X == 2 && ctx.Y == 1)
                hasKgpAtAnchor = ctx.HasKgpBelow();
            if (ctx.X == 8 && ctx.Y == 4)
                hasKgpAtEmpty = ctx.HasKgpBelow();
            return SurfaceCells.Empty;
        });

        composite.Flatten();

        Assert.IsTrue(hasKgpAtAnchor);
        Assert.IsFalse(hasKgpAtEmpty);
    }

    [TestMethod]
    public void ComputedLayer_HasKgpBelow_ReturnsTrueForSpannedCells()
    {
        // KGP image is 4 cells wide, 2 cells tall, anchored at (0,0)
        var kgpSurface = new Surface(10, 5, DefaultMetrics);
        kgpSurface[0, 0] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp(widthInCells: 4, heightInCells: 2) };

        var kgpPositions = new List<(int x, int y)>();

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(kgpSurface);
        composite.AddComputedLayer(10, 5, ctx =>
        {
            if (ctx.HasKgpBelow())
                kgpPositions.Add((ctx.X, ctx.Y));
            return SurfaceCells.Empty;
        });

        composite.Flatten();

        // Should detect KGP at positions (0,0) through (3,1) = 4x2 = 8 cells
        Assert.AreEqual(8, kgpPositions.Count);
        Assert.Contains((0, 0), kgpPositions);
        Assert.Contains((3, 0), kgpPositions);
        Assert.Contains((0, 1), kgpPositions);
        Assert.Contains((3, 1), kgpPositions);
    }

    // --- ComputeContext.GetKgpBelow ---

    [TestMethod]
    public void ComputedLayer_GetKgpBelow_ReturnsValidAccessor()
    {
        var kgpSurface = new Surface(10, 5, DefaultMetrics);
        var trackedKgp = CreateTrackedKgp(widthInCells: 4, heightInCells: 2, imageId: 42);
        kgpSurface[1, 1] = new SurfaceCell { Character = " ", Kgp = trackedKgp };

        KgpCellAccess accessAtAnchor = default;
        KgpCellAccess accessAtOffset = default;
        KgpCellAccess accessAtEmpty = default;

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(kgpSurface);
        composite.AddComputedLayer(10, 5, ctx =>
        {
            if (ctx.X == 1 && ctx.Y == 1) // anchor
                accessAtAnchor = ctx.GetKgpBelow();
            if (ctx.X == 3 && ctx.Y == 2) // within image, offset (2,1)
                accessAtOffset = ctx.GetKgpBelow();
            if (ctx.X == 8 && ctx.Y == 4) // outside image
                accessAtEmpty = ctx.GetKgpBelow();
            return SurfaceCells.Empty;
        });

        composite.Flatten();

        Assert.IsTrue(accessAtAnchor.IsValid);
        Assert.AreEqual(42u, accessAtAnchor.ImageId);
        Assert.AreEqual(0, accessAtAnchor.CellOffsetX);
        Assert.AreEqual(0, accessAtAnchor.CellOffsetY);
        Assert.IsTrue(accessAtAnchor.IsAnchor);

        Assert.IsTrue(accessAtOffset.IsValid);
        Assert.AreEqual(42u, accessAtOffset.ImageId);
        Assert.AreEqual(2, accessAtOffset.CellOffsetX);
        Assert.AreEqual(1, accessAtOffset.CellOffsetY);
        Assert.IsFalse(accessAtOffset.IsAnchor);

        Assert.IsFalse(accessAtEmpty.IsValid);
    }

    [TestMethod]
    public void ComputedLayer_GetKgpBelow_ReturnsImageMetadata()
    {
        var kgpSurface = new Surface(10, 5, DefaultMetrics);
        var trackedKgp = CreateTrackedKgp(widthInCells: 6, heightInCells: 3, zIndex: 1, imageId: 99);
        kgpSurface[0, 0] = new SurfaceCell { Character = " ", Kgp = trackedKgp };

        KgpCellAccess access = default;

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(kgpSurface);
        composite.AddComputedLayer(10, 5, ctx =>
        {
            if (ctx.X == 0 && ctx.Y == 0)
                access = ctx.GetKgpBelow();
            return SurfaceCells.Empty;
        });

        composite.Flatten();

        Assert.IsTrue(access.IsValid);
        Assert.AreEqual(99u, access.ImageId);
        Assert.AreEqual(6, access.WidthInCells);
        Assert.AreEqual(3, access.HeightInCells);
        Assert.AreEqual(60u, access.SourcePixelWidth);  // 6 * 10
        Assert.AreEqual(60u, access.SourcePixelHeight); // 3 * 20
        Assert.AreEqual(1, access.ZIndex);
        Assert.IsNotNull(access.Data);
    }

    // --- ComputeContext.GetKgpBelowAt ---

    [TestMethod]
    public void ComputedLayer_GetKgpBelowAt_ReturnsAccessorAtPosition()
    {
        var kgpSurface = new Surface(10, 5, DefaultMetrics);
        var trackedKgp = CreateTrackedKgp(widthInCells: 3, heightInCells: 2, imageId: 55);
        kgpSurface[2, 1] = new SurfaceCell { Character = " ", Kgp = trackedKgp };

        KgpCellAccess accessAt = default;
        KgpCellAccess accessOutside = default;

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(kgpSurface);
        composite.AddComputedLayer(10, 5, ctx =>
        {
            if (ctx.X == 0 && ctx.Y == 0)
            {
                accessAt = ctx.GetKgpBelowAt(3, 2); // inside image at offset (1,1)
                accessOutside = ctx.GetKgpBelowAt(9, 4); // outside image
            }
            return SurfaceCells.Empty;
        });

        composite.Flatten();

        Assert.IsTrue(accessAt.IsValid);
        Assert.AreEqual(55u, accessAt.ImageId);
        Assert.AreEqual(1, accessAt.CellOffsetX);
        Assert.AreEqual(1, accessAt.CellOffsetY);

        Assert.IsFalse(accessOutside.IsValid);
    }

    [TestMethod]
    public void ComputedLayer_GetKgpBelowAt_OutOfBounds_ReturnsDefault()
    {
        var kgpSurface = new Surface(10, 5, DefaultMetrics);
        kgpSurface[0, 0] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp() };

        KgpCellAccess access = default;

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(kgpSurface);
        composite.AddComputedLayer(10, 5, ctx =>
        {
            if (ctx.X == 0 && ctx.Y == 0)
                access = ctx.GetKgpBelowAt(-1, -1);
            return SurfaceCells.Empty;
        });

        composite.Flatten();

        Assert.IsFalse(access.IsValid);
    }

    // --- Multi-layer KGP with computed layer effects ---

    [TestMethod]
    public void ComputedLayer_KgpPresence_CanDriveTextStyling()
    {
        // Layer 0: KGP background image at (0,0) size 4x2
        var kgpSurface = new Surface(10, 5, DefaultMetrics);
        kgpSurface[0, 0] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp(widthInCells: 4, heightInCells: 2) };

        // Layer 1: Computed layer that adds a background color where KGP exists
        // (simulating a dim overlay effect)
        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(kgpSurface);
        composite.AddComputedLayer(10, 5, ctx =>
        {
            if (ctx.HasKgpBelow())
            {
                // Add semi-transparent overlay styling
                return new SurfaceCell(" ", null, Hex1bColor.Black); // Black overlay
            }
            return SurfaceCells.Empty;
        });

        var flattened = composite.Flatten();

        // Cells within KGP region should have the computed overlay
        Assert.IsNotNull(flattened[0, 0].Background);
        Assert.IsNotNull(flattened[3, 1].Background);

        // Cells outside KGP region should be empty
        Assert.IsNull(flattened[5, 3].Background);
    }

    // --- Multi-layer z-index allocation ---

    [TestMethod]
    public void MultiLayer_KgpFromDifferentLayers_PreservesDistinctZIndexes()
    {
        // Layer 0: below-text KGP (z=-1)
        var belowSurface = new Surface(10, 5, DefaultMetrics);
        belowSurface[0, 0] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp(imageContent: "bg", zIndex: -1, imageId: 10) };

        // Layer 1: above-text KGP (z=1)
        var aboveSurface = new Surface(10, 5, DefaultMetrics);
        aboveSurface[5, 0] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp(imageContent: "fg", zIndex: 1, imageId: 20) };

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(belowSurface);
        composite.AddLayer(aboveSurface);

        var flattened = composite.Flatten();

        var belowKgp = flattened[0, 0].Kgp!.Data;
        var aboveKgp = flattened[5, 0].Kgp!.Data;

        Assert.AreEqual(-1, belowKgp.ZIndex);
        Assert.AreEqual(1, aboveKgp.ZIndex);
        Assert.AreNotEqual(belowKgp.ImageId, aboveKgp.ImageId);
    }

    // --- SurfaceLayerContext.CreateKgp in layer context ---

    [TestMethod]
    public void SurfaceLayerContext_CreateKgp_FromCellData_ReturnsTrackedObject()
    {
        var store = new TrackedObjectStore();
        var ctx = new SurfaceLayerContext(10, 5, -1, -1, new Hex1bTheme("Test"), store, DefaultMetrics);

        var kgpData = CreateKgpData();
        var tracked = ctx.CreateKgp(kgpData);

        Assert.IsNotNull(tracked);
        Assert.AreEqual(kgpData.ImageId, tracked!.Data.ImageId);
    }

    [TestMethod]
    public void SurfaceLayerContext_CreateKgp_FromPixels_ReturnsTrackedObject()
    {
        var store = new TrackedObjectStore();
        var ctx = new SurfaceLayerContext(10, 5, -1, -1, new Hex1bTheme("Test"), store, DefaultMetrics);

        var pixels = new byte[10 * 20 * 4]; // 10x20 RGBA
        var tracked = ctx.CreateKgp(pixels, 10, 20, KgpZOrder.BelowText);

        Assert.IsNotNull(tracked);
        Assert.AreEqual(1, tracked!.Data.WidthInCells);
        Assert.AreEqual(1, tracked.Data.HeightInCells);
    }

    [TestMethod]
    public void SurfaceLayerContext_CreateKgp_NoStore_ReturnsNull()
    {
        var ctx = new SurfaceLayerContext(10, 5, -1, -1, new Hex1bTheme("Test"), store: null, DefaultMetrics);

        var tracked = ctx.CreateKgp(CreateKgpData());
        Assert.IsNull(tracked);
    }

    // --- FindKgpAtPosition across layers ---

    [TestMethod]
    public void FindKgpAtPosition_TopLayerKgp_FoundFirst()
    {
        // Layer 0: KGP at (0,0) image ID 10
        var layer0 = new Surface(10, 5, DefaultMetrics);
        layer0[0, 0] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp(imageContent: "bg", imageId: 10) };

        // Layer 1: Different KGP at (0,0) image ID 20
        var layer1 = new Surface(10, 5, DefaultMetrics);
        layer1[0, 0] = new SurfaceCell { Character = " ", Kgp = CreateTrackedKgp(imageContent: "fg", imageId: 20) };

        // Computed layer (layer 2) should find layer 1's KGP (top-most below)
        uint foundImageId = 0;

        var composite = new CompositeSurface(10, 5, DefaultMetrics);
        composite.AddLayer(layer0);
        composite.AddLayer(layer1);
        composite.AddComputedLayer(10, 5, ctx =>
        {
            if (ctx.X == 0 && ctx.Y == 0)
            {
                var kgp = ctx.GetKgpBelow();
                if (kgp.IsValid)
                    foundImageId = kgp.ImageId;
            }
            return SurfaceCells.Empty;
        });

        composite.Flatten();

        Assert.AreEqual(20u, foundImageId);
    }

    // --- SVG round-trip ---

    [TestMethod]
    public void MultiLayer_KgpAndText_SvgRoundTrip()
    {
        // Build a composite with KGP background + text overlay
        var kgpSurface = new Surface(10, 3, DefaultMetrics);
        var trackedKgp = CreateTrackedKgp(widthInCells: 4, heightInCells: 2, zIndex: -1, imageId: 77);
        kgpSurface[0, 0] = new SurfaceCell { Character = " ", Kgp = trackedKgp };

        var textSurface = new Surface(10, 3, DefaultMetrics);
        textSurface[5, 0] = new SurfaceCell("H", null, null);
        textSurface[6, 0] = new SurfaceCell("i", null, null);

        var composite = new CompositeSurface(10, 3, DefaultMetrics);
        composite.AddLayer(kgpSurface);
        composite.AddLayer(textSurface);

        var flattened = composite.Flatten();

        // Feed through SurfaceComparer to generate tokens
        var prev = new Surface(10, 3, DefaultMetrics);
        var diff = SurfaceComparer.Compare(prev, flattened);
        var tokens = SurfaceComparer.ToTokens(diff, flattened);

        // Serialize tokens to escape sequences and feed to terminal
        var serialized = AnsiTokenSerializer.Serialize(tokens);

        using var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
            .WithDimensions(10, 3)
            .Build();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(serialized));

        // Verify SVG output contains text
        var svg = terminal.CreateSnapshot().ToSvg();
        Assert.Contains(">H<", svg);
        Assert.Contains(">i<", svg);
    }
}
