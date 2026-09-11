using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Sixel;
using Hex1b.Surfaces;
using Hex1b.Tokens;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class TerminalWidgetSixelTests
{
    private static readonly TerminalCapabilities Capabilities = new()
    {
        SupportsSixel = true,
        SixelSupport = SixelPresentationSupport.Native,
        SupportsTrueColor = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
        SixelCellMetrics = new(8, 12, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative)
    };

    [TestMethod]
    [DataRow(SixelPresentationSupport.Native, true, SixelPresentationSupport.Headless)]
    [DataRow(SixelPresentationSupport.Headless, true, SixelPresentationSupport.Headless)]
    [DataRow(SixelPresentationSupport.Unknown, true, SixelPresentationSupport.Headless)]
    [DataRow(SixelPresentationSupport.None, true, SixelPresentationSupport.None)]
    [DataRow(SixelPresentationSupport.Unknown, false, SixelPresentationSupport.None)]
    public void Render_PropagatesSixelSupportAndProtocolMetrics(
        SixelPresentationSupport parent, bool legacyFlag, SixelPresentationSupport expected)
    {
        var handle = new TerminalWidgetHandle(8, 4);
        var node = CreateNode(handle);
        var context = CreateContext(Capabilities with { SixelSupport = parent, SupportsSixel = legacyFlag });
        node.Render(context);

        Assert.AreEqual(expected, handle.Capabilities.SixelSupport);
        Assert.AreEqual(expected == SixelPresentationSupport.Headless, handle.Capabilities.SupportsSixel);
        Assert.AreEqual(Capabilities.SixelCellMetrics, handle.Capabilities.SixelCellMetrics);
    }

    [TestMethod]
    public async Task Render_OverlappingSixels_PreservesPixelsPaletteAndText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(8, 4)
            .WithTerminalWidget(out var handle).Build();
        var node = CreateNode(handle);
        node.Render(CreateContext());
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            "text\x1b[2;2H\x1bP0;1;0q\"1;1;2;6#1;2;100;0;0~\x1b\\" +
            "\x1b[2;2H\x1bP0;1;0q\"1;1;2;6#1;2;0;100;0?~\x1b\\")));
        var context = CreateContext();
        node.Render(context);

        var data = context.Surface[3, 2].Sixel?.Data;
        Assert.IsNotNull(data);
        var pixels = data.GetPixels()!;
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), pixels[0, 0]);
        Assert.AreEqual(new Rgba32(0, 255, 0, 255), pixels[1, 0]);
        Assert.AreEqual("t", context.Surface[2, 1].Character);
    }

    [TestMethod]
    public async Task Render_SynchronizedUpdate_RetainsSixelUntilEndOnlyChunk()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(8, 4).WithTimeProvider(new FakeTimeProvider())
            .WithTerminalWidget(out var handle).Build();
        var node = CreateNode(handle);
        node.Render(CreateContext());
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            "\x1bP0;1;0q\"1;1;1;6#1;2;100;0;0~\x1b\\")));
        var first = CreateContext();
        node.Render(first);
        Assert.IsNotNull(first.Surface[2, 1].Sixel);
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            "\x1b[?2026h\x1b[2J")));
        var pending = CreateContext();
        node.Render(pending);
        Assert.IsNotNull(pending.Surface[2, 1].Sixel);
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(
            AnsiTokenizer.Tokenize("\x1b[?2026l")));
        var completed = CreateContext();
        node.Render(completed);
        Assert.IsFalse(completed.Surface.HasSixels);
    }

    private static TerminalNode CreateNode(TerminalWidgetHandle handle)
    {
        var node = new TerminalNode { Handle = handle };
        node.SetInvalidateCallback(() => { });
        node.Bind();
        node.Arrange(new Rect(2, 1, 8, 4));
        return node;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Render_ClippedAndTranslatedParent_PreservesNativePixelSize(bool cached)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(8, 4)
            .WithTerminalWidget(out var handle).Build();
        var node = CreateNode(handle);
        node.Render(CreateContext());
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            "\x1bP0;1;0q\"1;1;24;24#1;2;100;0;0!24~-!24~-!24~-!24~\x1b\\")));
        node.Arrange(new Rect(-1, -1, 8, 4));
        var context = new SurfaceRenderContext(new Surface(4, 2)) { CachingEnabled = cached };
        context.SetCapabilities(Capabilities);
        context.WriteClipped(3, 1, "X");
        context.RenderChild(node);
        var image = context.Surface[0, 0].Sixel?.Data;
        Assert.IsNotNull(image);
        Assert.AreEqual(16, image.GetPixels()!.Width);
        Assert.AreEqual(12, image.GetPixels()!.Height);
        Assert.AreEqual(2, image.WidthInCells);
        Assert.AreEqual(1, image.HeightInCells);
    }

    [TestMethod]
    public async Task Render_LargeSparseImage_DoesNotMaterializeTheSourceRaster()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(8, 4)
            .WithTerminalWidget(out var handle).Build();
        var node = CreateNode(handle);
        node.Render(CreateContext());
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            "\x1bP0;1;0q\"1;1;1000000;6#1;2;100;0;0~\x1b\\")));
        using var snapshot = terminal.CreateSnapshot();
        var source = TestSeq.Single(snapshot.SixelPlacements).Image;
        Assert.IsFalse(source.HasMaterializedPixels);
        var context = CreateContext();
        node.Render(context);
        Assert.IsNotNull(context.Surface[2, 1].Sixel);
        Assert.IsFalse(source.HasMaterializedPixels);
    }

    [TestMethod]
    public async Task Render_FractionalMetrics_PreservesDestructiveCellDamage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(8, 4)
            .WithTerminalWidget(out var handle).Build();
        var capabilities = Capabilities with
        {
            SixelCellMetrics = new(8.5, 12.5, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative)
        };
        var node = CreateNode(handle);
        node.Render(CreateContext(capabilities));
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            "\x1bP0;1;0q\"1;1;25;6#1;2;100;0;0!25~\x1b\\\x1b[1;1HX")));
        using var snapshot = terminal.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        Assert.AreEqual(0, placement.Row);
        Assert.AreEqual(0, placement.Column);
        Assert.IsFalse(placement.IsGeometryOnly);
        var context = CreateContext(capabilities);
        node.Render(context);
        var data = context.Surface[2, 1].Sixel?.Data;
        Assert.IsNotNull(data, $"Crop: {placement.PaintedLeft},{placement.PaintedTop} {placement.PaintedColumnCount}x{placement.PaintedRowCount}");
        var pixels = data.GetPixels()!;
        Assert.AreEqual(Rgba32.Transparent, pixels[8, 0]);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), pixels[9, 0]);
    }

    [TestMethod]
    public async Task Render_Scrollback_ProjectsRetainedHistoricalGraphics()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(8, 4).WithScrollback(20)
            .WithTerminalWidget(out var handle).Build();
        var node = CreateNode(handle);
        node.Render(CreateContext());
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            "\x1bP0;1;0q\"1;1;2;6#1;2;100;0;0~~\x1b\\\x1b[4;1H\n")));
        var live = CreateContext();
        node.Render(live);
        Assert.IsFalse(live.Surface.HasSixels);
        handle.EnterCopyMode();
        handle.CurrentScrollbackOffset = 1;
        var historical = CreateContext();
        node.Render(historical);
        Assert.IsNotNull(historical.Surface[2, 1].Sixel);
    }

    [TestMethod]
    public void ExactComposite_MoreThan256Colors_DoesNotQuantizeOrDropColors()
    {
        var pixels = new SixelPixelBuffer(300, 6);
        for (var x = 0; x < pixels.Width; x++)
            pixels[x, 0] = SixelColorConverter.FromRgbPercent(x % 101, x / 101, 0);
        var image = SixelData.FromExactPixels(pixels, 38, 1, Capabilities.SixelCellMetrics!.Value);
        var decoded = image.GetPixels()!;
        for (var x = 0; x < pixels.Width; x++)
            Assert.AreEqual(pixels[x, 0], decoded[x, 0], $"pixel {x}");

        var fragment = new SixelFragment(image, 0, 0, new PixelRect(10, 0, 280, 6));
        var cropped = new SixelData(fragment.GetPayload()!, 35, 1, [], 280, 6);
        var croppedPixels = cropped.GetPixels()!;
        for (var x = 0; x < croppedPixels.Width; x++)
            Assert.AreEqual(pixels[x + 10, 0], croppedPixels[x, 0], $"cropped pixel {x}");
    }

    [TestMethod]
    public async Task Render_NativeReplacementAndRemoval_DoesNotLeaveGraphicsOrDamageOutsideText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(8, 4)
            .WithTerminalWidget(out var handle).Build();
        using var outerWorkload = new Hex1bAppWorkloadAdapter();
        using var outer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(outerWorkload).WithHeadless(Capabilities).WithDimensions(12, 8).Build();
        var node = CreateNode(handle);
        node.Render(CreateContext());
        var previous = new Surface(12, 8);

        for (var frame = 0; frame < 3; frame++)
        {
            await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
                "\x1b[H\x1b[2J" + (frame < 2
                    ? $"\x1bP0;1;0q\"1;1;2;6#1;2;{100 - frame * 100};{frame * 100};0{(frame == 0 ? "~" : "?~")}\x1b\\"
                    : ""))));
            var current = CreateContext();
            current.WriteClipped(0, 0, "HOST");
            node.Render(current);
            var tokens = SurfaceComparer.ToTokens(
                SurfaceComparer.Compare(previous, current.Surface), current.Surface, previous);
            outer.ApplyTokens(AnsiTokenizer.Tokenize(AnsiTokenSerializer.Serialize(tokens)));
            using var snapshot = outer.CreateSnapshot();
            Assert.IsTrue(snapshot.ContainsText("HOST"));
            Assert.AreEqual(frame < 2 ? 1 : 0, snapshot.SixelPlacements.Count);
            previous = current.Surface;
        }
    }

    private static SurfaceRenderContext CreateContext(TerminalCapabilities? capabilities = null)
    {
        var context = new SurfaceRenderContext(new Surface(12, 8));
        context.SetCapabilities(capabilities ?? Capabilities);
        context.CachingEnabled = false;
        return context;
    }
}
