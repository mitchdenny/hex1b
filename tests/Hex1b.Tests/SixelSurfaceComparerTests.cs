#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Tokens;
using Hex1b.Layout;
using Hex1b.Sixel;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class SixelSurfaceComparerTests
{
    [TestMethod]
    public void ToTokens_ReplacedSixel_ClearsOldPixelsBeforeEmittingReplacement()
    {
        var previous = CreateSixelSurface(
            CreateSolidPixels(20, 20, Rgba32.FromRgb(255, 0, 0)),
            0,
            0,
            2,
            1);
        var current = CreateSixelSurface(
            CreateSolidPixels(19, 20, Rgba32.FromRgb(0, 0, 255)),
            0,
            0,
            2,
            1);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        var clearIndex = FindSpaceToken(tokens);
        var sixelIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.token is UnrecognizedSequenceToken sequence &&
                           sequence.Sequence.StartsWith("\x1bP", StringComparison.Ordinal))
            .index;

        Assert.IsTrue(clearIndex >= 0 && clearIndex < sixelIndex, Describe(tokens));
    }

    [TestMethod]
    public void ToTokens_RemovedSixel_ClearsEntirePreviousRegion()
    {
        var previous = CreateSixelSurface(Rgba32.FromRgb(255, 0, 0), 1, 1, 3, 2);
        var current = new Surface(8, 4, CellMetrics.Default);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        Assert.IsTrue(tokens.OfType<TextToken>().Sum(token => token.Text.Length) >= 6);
        Assert.IsFalse(tokens.OfType<UnrecognizedSequenceToken>().Any(
            token => token.Sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ToTokens_MovedSixel_ClearsOldRegionThenEmitsNewAnchor()
    {
        var previous = CreateSixelSurface(Rgba32.FromRgb(0, 200, 120), 0, 0, 2, 1);
        var current = CreateSixelSurface(Rgba32.FromRgb(0, 200, 120), 4, 2, 2, 1);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        var clearIndex = FindSpaceToken(tokens);
        var newPositionIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.token is CursorPositionToken { Row: 3, Column: 5 })
            .index;

        Assert.IsTrue(clearIndex >= 0 && clearIndex < newPositionIndex, Describe(tokens));
    }

    [TestMethod]
    public void ToTokens_TextOverAnchor_FragmentsImageAndRendersTextAfterward()
    {
        var previous = new Surface(6, 2, CellMetrics.Default);
        var current = new Surface(6, 2, CellMetrics.Default);
        var context = new SurfaceRenderContext(current);
        context.WriteSixel(CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40)), 2, 1);
        context.SetCursorPosition(0, 0);
        context.Write("X");

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);

        var sixelIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.token is UnrecognizedSequenceToken sequence &&
                           sequence.Sequence.StartsWith("\x1bP", StringComparison.Ordinal))
            .index;
        var textIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.token is TextToken { Text: "X" })
            .index;

        Assert.IsTrue(sixelIndex < textIndex);
    }

    [TestMethod]
    public void ToTokens_SixelWrittenWithActiveBackground_EmitsImage()
    {
        var previous = new Surface(6, 2, CellMetrics.Default);
        var current = new Surface(6, 2, CellMetrics.Default);
        var context = new SurfaceRenderContext(current);
        context.Write("\x1b[44m");
        context.WriteSixel(CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40)), 2, 1);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);

        Assert.IsTrue(tokens.OfType<UnrecognizedSequenceToken>().Any(
            token => token.Sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ToTokens_SixelCompositedOverBackground_EmitsImage()
    {
        var previous = new Surface(6, 2, CellMetrics.Default);
        var current = new Surface(6, 2, CellMetrics.Default);
        current.Clear(new SurfaceCell(" ", null, Hex1bColor.Blue));

        var child = CreateSixelSurface(
            CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40)),
            0,
            0,
            2,
            1);
        current.Composite(child, 0, 0);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);

        Assert.IsTrue(tokens.OfType<UnrecognizedSequenceToken>().Any(
            token => token.Sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ToTokens_NonAnchorOpaqueBackground_ClipsSixel()
    {
        var current = CreateLayeredSixelSurface(
            new Rect(1, 0, 1, 1),
            new SurfaceCell(" ", null, Hex1bColor.Blue));
        var previous = new Surface(current.Width, current.Height, current.CellMetrics);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        var sixelIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.token is UnrecognizedSequenceToken)
            .index;
        var backgroundIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.index > sixelIndex &&
                           item.token is TextToken { Text: " " })
            .index;
        var payload = ((UnrecognizedSequenceToken)tokens[sixelIndex]).Sequence;

        Assert.AreEqual(10, SixelParser.ParsePayload(payload).DeclaredExtent.Width);
        Assert.IsTrue(sixelIndex < backgroundIndex, Describe(tokens));
    }

    [TestMethod]
    public void ToTokens_AnchorOpaqueBackground_ClipsAndReanchorsSixel()
    {
        var current = CreateLayeredSixelSurface(
            new Rect(0, 0, 1, 1),
            new SurfaceCell(" ", null, Hex1bColor.Blue));
        var previous = new Surface(current.Width, current.Height, current.CellMetrics);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        var sixelIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.token is UnrecognizedSequenceToken)
            .index;
        var backgroundIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.index > sixelIndex &&
                           item.token is TextToken { Text: " " })
            .index;

        Assert.IsTrue(tokens.Take(sixelIndex).OfType<CursorPositionToken>().Any(
            token => token.Row == 1 && token.Column == 2));
        Assert.IsTrue(sixelIndex < backgroundIndex, Describe(tokens));
    }

    [TestMethod]
    public void ToTokens_FullyOpaqueBackground_SuppressesSixel()
    {
        var current = CreateLayeredSixelSurface(
            new Rect(0, 0, 2, 1),
            new SurfaceCell(" ", null, Hex1bColor.Blue));
        var previous = new Surface(current.Width, current.Height, current.CellMetrics);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);

        Assert.IsFalse(tokens.OfType<UnrecognizedSequenceToken>().Any(
            token => token.Sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
        Assert.IsTrue(tokens.OfType<TextToken>().Sum(token => token.Text.Length) >= 2);
    }

    [TestMethod]
    public void ToTokens_TransparentSpace_DoesNotOccludeSixel()
    {
        var current = CreateLayeredSixelSurface(
            new Rect(0, 0, 2, 1),
            new SurfaceCell(" ", null, null));
        var previous = new Surface(current.Width, current.Height, current.CellMetrics);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        var payload = TestSeq.Single(tokens.OfType<UnrecognizedSequenceToken>()).Sequence;

        Assert.AreEqual(20, SixelParser.ParsePayload(payload).DeclaredExtent.Width);
    }

    [TestMethod]
    public void ToTokens_OcclusionUsesCapturedSixelMetrics()
    {
        var textMetrics = new CellMetrics(8, 16);
        var previous = new Surface(4, 2, textMetrics);
        var current = new Surface(4, 2, textMetrics);
        var context = CreateSixelContext(current, 10, 20);
        context.WriteSixel(CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40)), 2, 1);
        context.SetCursorPosition(0, 0);
        context.Write("X");

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        var payload = TestSeq.Single(tokens.OfType<UnrecognizedSequenceToken>()).Sequence;

        Assert.AreEqual(10, SixelParser.ParsePayload(payload).DeclaredExtent.Width);
    }

    [TestMethod]
    public void Composite_LeftClipUsesCapturedSixelMetrics()
    {
        var textMetrics = new CellMetrics(8, 16);
        var source = new Surface(3, 2, textMetrics);
        var context = CreateSixelContext(source, 10, 20);
        context.WriteSixel(CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40)), 2, 1);
        var target = new Surface(3, 2, textMetrics);

        target.Composite(source, -1, 0);

        var clipped = target[0, 0].Sixel!.Data;
        Assert.AreEqual(10, clipped.PixelWidth);
        Assert.AreEqual(10, SixelParser.ParsePayload(clipped.Payload).DeclaredExtent.Width);
    }

    [TestMethod]
    public void ComputedCellPixelAccess_UsesCapturedSixelMetrics()
    {
        var textMetrics = new CellMetrics(8, 16);
        var source = new Surface(4, 2, textMetrics);
        var context = CreateSixelContext(source, 10, 20);
        var pixels = new SixelPixelBuffer(20, 20);
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                pixels[x, y] = x < 10
                    ? Rgba32.FromRgb(255, 0, 0)
                    : Rgba32.FromRgb(0, 0, 255);
            }
        }
        context.WriteSixel(pixels, 2, 1);

        var composite = new CompositeSurface(4, 2, textMetrics);
        composite.AddLayer(source, 0, 0);
        SixelPixelAccess access = default;
        composite.AddComputedLayer(4, 2, compute =>
        {
            if (compute.X == 1 && compute.Y == 0)
            {
                access = compute.GetSixelBelow();
            }
            return SurfaceCells.Empty;
        }, 0, 0);

        _ = composite.GetCell(1, 0);

        Assert.IsTrue(access.IsValid);
        Assert.AreEqual(10, access.PixelWidth);
        var pixel = access.GetPixel(0, 0);
        Assert.IsTrue(pixel.B > 200 && pixel.R < 10, $"Expected blue pixel, got {pixel}.");
    }

    [TestMethod]
    public void CompositeSurface_AnchorOpaqueBackground_ClipsAndReanchorsSixel()
    {
        var source = CreateSixelSurface(
            CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40)),
            0,
            0,
            2,
            1);
        var overlay = new Surface(source.Width, source.Height, source.CellMetrics);
        overlay.Fill(new Rect(0, 0, 1, 1), new SurfaceCell(" ", null, Hex1bColor.Blue));
        var composite = new CompositeSurface(source.Width, source.Height, source.CellMetrics);
        composite.AddLayer(source, 0, 0);
        composite.AddLayer(overlay, 0, 0);
        var current = composite.Flatten();
        var previous = new Surface(current.Width, current.Height, current.CellMetrics);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        var payload = TestSeq.Single(tokens.OfType<UnrecognizedSequenceToken>()).Sequence;

        Assert.AreEqual(10, SixelParser.ParsePayload(payload).DeclaredExtent.Width);
        Assert.IsTrue(tokens.OfType<CursorPositionToken>().Any(
            token => token.Row == 1 && token.Column == 2));
    }

    [TestMethod]
    public void CompositeSurface_ManualAnchorOpaqueBackground_PreservesSixel()
    {
        var metrics = new CellMetrics(10, 20);
        var store = new TrackedObjectStore();
        var pixels = CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40));
        var payload = SixelEncoder.Encode(pixels);
        var tracked = store.GetOrCreateSixel(
            payload,
            2,
            1,
            SixelParser.ParsePayload(payload),
            cellMetrics: new SixelCellMetrics(
                10,
                20,
                SixelCellMetricsSource.Direct,
                SixelCellMetricsReliability.Authoritative));
        var source = new Surface(4, 2, metrics);
        source[0, 0] = new SurfaceCell(" ", null, null, Sixel: tracked);
        var overlay = new Surface(4, 2, metrics);
        overlay[0, 0] = new SurfaceCell(" ", null, Hex1bColor.Blue);
        var composite = new CompositeSurface(4, 2, metrics);
        composite.AddLayer(source, 0, 0);
        composite.AddLayer(overlay, 0, 0);

        var current = composite.Flatten();
        var previous = new Surface(4, 2, metrics);
        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);

        Assert.IsTrue(tokens.OfType<UnrecognizedSequenceToken>().Any(
            token => token.Sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
        Assert.IsTrue(tokens.OfType<CursorPositionToken>().Any(
            token => token.Row == 1 && token.Column == 2));
    }

    [TestMethod]
    public void ToTokens_ManualAnchorBackground_DoesNotOccludeItsOwnSixel()
    {
        var metrics = new CellMetrics(10, 20);
        var store = new TrackedObjectStore();
        var pixels = CreateSolidPixels(10, 18, Rgba32.FromRgb(200, 80, 40));
        var payload = SixelEncoder.Encode(pixels);
        var tracked = store.GetOrCreateSixel(
            payload,
            1,
            1,
            SixelParser.ParsePayload(payload),
            cellMetrics: new SixelCellMetrics(
                10,
                20,
                SixelCellMetricsSource.Direct,
                SixelCellMetricsReliability.Authoritative));
        var current = new Surface(2, 1, metrics);
        current[0, 0] = new SurfaceCell(" ", null, Hex1bColor.Red, Sixel: tracked);
        var previous = new Surface(2, 1, metrics);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);

        Assert.IsTrue(tokens.OfType<UnrecognizedSequenceToken>().Any(
            token => token.Sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void FragmentPosition_UsesFractionalProtocolBoundaries()
    {
        var surface = new Surface(4, 4, new CellMetrics(8, 16));
        var context = CreateSixelContext(surface, 9.4, 19.4);
        context.WriteSixel(CreateSolidPixels(20, 40, Rgba32.FromRgb(200, 80, 40)), 2, 2);
        var tracked = surface[0, 0].Sixel!;
        var horizontal = new SixelVisibility(tracked, 0, 0, 0);
        horizontal.ApplyOcclusion(new Rect(0, 0, 1, 2));
        var horizontalFragment = TestSeq.Single(horizontal.GenerateFragments());

        var vertical = new SixelVisibility(tracked, 0, 0, 0);
        vertical.ApplyOcclusion(new Rect(0, 0, 2, 1));
        var verticalFragment = TestSeq.Single(vertical.GenerateFragments());

        Assert.AreEqual((1, 0), horizontalFragment.CellPosition);
        Assert.AreEqual((0, 1), verticalFragment.CellPosition);
        Assert.AreEqual(1, tracked.Data.CellMetrics.ColumnsFor(horizontalFragment.PixelRegion.Width));
        Assert.AreEqual(1, tracked.Data.CellMetrics.RowsFor(verticalFragment.PixelRegion.Height));
    }

    [TestMethod]
    public void TrackedStore_SamePayloadUnderDifferentMetrics_PreservesEachFragmentGeometry()
    {
        var store = new TrackedObjectStore();
        var pixels = CreateSolidPixels(18, 18, Rgba32.FromRgb(200, 80, 40));
        var payload = SixelEncoder.Encode(pixels);
        var parseResult = SixelParser.ParsePayload(payload);
        var integerMetrics = new SixelCellMetrics(
            10,
            20,
            SixelCellMetricsSource.Direct,
            SixelCellMetricsReliability.Authoritative);
        var fractionalMetrics = new SixelCellMetrics(
            9.4,
            19.4,
            SixelCellMetricsSource.Direct,
            SixelCellMetricsReliability.Authoritative);

        var integer = store.GetOrCreateSixel(payload, 2, 1, parseResult, cellMetrics: integerMetrics);
        var duplicate = store.GetOrCreateSixel(payload, 2, 1, parseResult, cellMetrics: integerMetrics);
        var fractional = store.GetOrCreateSixel(payload, 2, 1, parseResult, cellMetrics: fractionalMetrics);

        try
        {
            Assert.AreSame(integer, duplicate);
            Assert.AreNotSame(integer, fractional);
            Assert.AreEqual(2, store.SixelCount);
            Assert.IsFalse(SixelData.HashEquals(integer.Data.ContentHash, fractional.Data.ContentHash));

            var integerVisibility = new SixelVisibility(integer, 0, 0, 0);
            integerVisibility.ApplyOcclusion(new Rect(0, 0, 1, 1));
            var integerFragment = TestSeq.Single(integerVisibility.GenerateFragments());

            var fractionalVisibility = new SixelVisibility(fractional, 0, 0, 0);
            fractionalVisibility.ApplyOcclusion(new Rect(0, 0, 1, 1));
            var fractionalFragment = TestSeq.Single(fractionalVisibility.GenerateFragments());

            Assert.AreEqual((1, 0), integerFragment.CellPosition);
            Assert.AreEqual((1, 0), fractionalFragment.CellPosition);
            Assert.AreEqual(new PixelRect(10, 0, 8, 18), integerFragment.PixelRegion);
            Assert.AreEqual(new PixelRect(9, 0, 9, 18), fractionalFragment.PixelRegion);
        }
        finally
        {
            integer.Release();
            duplicate.Release();
            fractional.Release();
        }

        Assert.AreEqual(0, store.SixelCount);
    }

    [TestMethod]
    public void Compare_SamePayloadAndSpanWithDifferentMetrics_IsDifferent()
    {
        var store = new TrackedObjectStore();
        var pixels = CreateSolidPixels(18, 18, Rgba32.FromRgb(80, 160, 240));
        var payload = SixelEncoder.Encode(pixels);
        var parseResult = SixelParser.ParsePayload(payload);
        var previousSixel = store.GetOrCreateSixel(
            payload,
            2,
            1,
            parseResult,
            cellMetrics: new SixelCellMetrics(
                10,
                20,
                SixelCellMetricsSource.Direct,
                SixelCellMetricsReliability.Authoritative));
        var currentSixel = store.GetOrCreateSixel(
            payload,
            2,
            1,
            parseResult,
            cellMetrics: new SixelCellMetrics(
                9.4,
                19.4,
                SixelCellMetricsSource.Direct,
                SixelCellMetricsReliability.Authoritative));
        var previous = new Surface(4, 2, CellMetrics.Default);
        var current = new Surface(4, 2, CellMetrics.Default);
        previous[0, 0] = new SurfaceCell(" ", null, null, Sixel: previousSixel);
        current[0, 0] = new SurfaceCell(" ", null, null, Sixel: currentSixel);

        try
        {
            Assert.IsFalse(SurfaceComparer.Compare(previous, current).IsEmpty);
        }
        finally
        {
            previous.ClearAndReleaseTrackedObjects();
            current.ClearAndReleaseTrackedObjects();
        }
    }

    [TestMethod]
    public void SurfaceLayerContext_CreateSixel_CapturesProtocolMetrics()
    {
        var store = new TrackedObjectStore();
        var capabilities = new TerminalCapabilities
        {
            SixelSupport = SixelPresentationSupport.Headless,
            SixelCellMetrics = new SixelCellMetrics(
                10,
                20,
                SixelCellMetricsSource.Direct,
                SixelCellMetricsReliability.Authoritative)
        };
        var context = new SurfaceLayerContext(
            4,
            2,
            -1,
            -1,
            new Hex1bTheme("Test"),
            store,
            new CellMetrics(8, 16),
            capabilities);
        var pixels = CreateSolidPixels(20, 18, Rgba32.FromRgb(200, 80, 40));

        var structured = context.CreateSixel(pixels)!;
        var encoded = context.CreateSixel(SixelEncoder.Encode(pixels), 2, 1)!;

        Assert.AreEqual(2, structured.Data.WidthInCells);
        Assert.AreEqual(1, structured.Data.HeightInCells);
        Assert.AreEqual(capabilities.SixelCellMetrics, structured.Data.CellMetrics);
        Assert.AreEqual(capabilities.SixelCellMetrics, encoded.Data.CellMetrics);

        structured.Release();
        encoded.Release();
    }

    [TestMethod]
    public void Compare_SamePayloadWithDifferentCellSpan_IsDifferent()
    {
        var pixels = CreateSolidPixels(20, 20, Rgba32.FromRgb(80, 160, 240));
        var previous = CreateSixelSurface(pixels, 0, 0, 2, 1);
        var current = CreateSixelSurface(pixels, 0, 0, 3, 2);

        var diff = SurfaceComparer.Compare(previous, current);

        Assert.IsFalse(diff.IsEmpty);
    }

    [TestMethod]
    public void Compare_RepeatedIdenticalSixelRender_HasNoChanges()
    {
        var pixels = CreateSolidPixels(20, 20, Rgba32.FromRgb(80, 160, 240));
        var previous = CreateSixelSurface(pixels, 0, 0, 2, 1);
        var current = CreateSixelSurface(pixels, 0, 0, 2, 1);

        Assert.IsTrue(SurfaceComparer.Compare(previous, current).IsEmpty);
    }

    private static Surface CreateSixelSurface(
        Rgba32 color,
        int x,
        int y,
        int width,
        int height)
        => CreateSixelSurface(CreateSolidPixels(width * 10, height * 20, color), x, y, width, height);

    private static Surface CreateSixelSurface(
        SixelPixelBuffer pixels,
        int x,
        int y,
        int width,
        int height)
    {
        var surface = new Surface(8, 4, CellMetrics.Default);
        var context = new SurfaceRenderContext(surface);
        context.SetCursorPosition(x, y);
        context.WriteSixel(pixels, width, height);
        return surface;
    }

    private static Surface CreateLayeredSixelSurface(Rect overlayRect, SurfaceCell overlayCell)
    {
        var current = CreateSixelSurface(
            CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40)),
            0,
            0,
            2,
            1);
        var overlay = new Surface(current.Width, current.Height, current.CellMetrics);
        overlay.Fill(overlayRect, overlayCell);
        current.Composite(overlay, 0, 0);
        return current;
    }

    private static SurfaceRenderContext CreateSixelContext(
        Surface surface,
        double sixelCellWidth,
        double sixelCellHeight)
    {
        var context = new SurfaceRenderContext(surface);
        context.SetCapabilities(new TerminalCapabilities
        {
            SixelSupport = SixelPresentationSupport.Headless,
            SixelCellMetrics = new SixelCellMetrics(
                sixelCellWidth,
                sixelCellHeight,
                SixelCellMetricsSource.Direct,
                SixelCellMetricsReliability.Authoritative)
        });
        return context;
    }

    private static SixelPixelBuffer CreateSolidPixels(int width, int height, Rgba32 color)
    {
        var pixels = new SixelPixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[x, y] = color;
            }
        }

        return pixels;
    }

    private static int FindSpaceToken(IReadOnlyList<AnsiToken> tokens)
        => tokens
            .Select((token, index) => (token, index))
            .Where(item => item.token is TextToken text &&
                           text.Text.Length > 0 &&
                           text.Text.All(static character => character == ' '))
            .Select(static item => item.index)
            .DefaultIfEmpty(-1)
            .First();

    private static string Describe(IReadOnlyList<AnsiToken> tokens)
        => string.Join(Environment.NewLine, tokens.Select((token, index) => $"{index}: {token}"));
}
