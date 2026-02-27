using System.Text;
using Hex1b.Input;
using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class KgpWindowRenderingTests
{
    [Fact]
    public void KgpNode_InChildSurface_SurvivesComposite()
    {
        var pixelData = MakePixelData();
        var parentSurface = new Surface(40, 20, new CellMetrics(8, 16));
        var childSurface = new Surface(10, 5, new CellMetrics(8, 16));
        var childCtx = new SurfaceRenderContext(childSurface, 5, 3, null);
        childCtx.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });
        childCtx.KgpCache = new KgpImageCache();

        var node = new KittyGraphicsNode
        {
            PixelData = pixelData, PixelWidth = 4, PixelHeight = 4,
            DisplayColumns = 4, DisplayRows = 2
        };
        node.Measure(new Constraints(0, 10, 0, 5));
        node.Arrange(new Rect(6, 4, 4, 2));
        node.Render(childCtx);

        Assert.True(childSurface.HasKgp, "Child surface should have KGP");
        parentSurface.Composite(childSurface, 5, 3);
        Assert.True(parentSurface.HasKgp, "Parent surface should have KGP after composite");
    }

    [Fact]
    public void KgpNode_InComposite_AppearsInDiffTokens()
    {
        var pixelData = MakePixelData();
        var surface = new Surface(40, 20, new CellMetrics(8, 16));
        var childSurface = new Surface(10, 5, new CellMetrics(8, 16));
        var childCtx = new SurfaceRenderContext(childSurface, 5, 3, null);
        childCtx.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });
        childCtx.KgpCache = new KgpImageCache();

        var node = new KittyGraphicsNode
        {
            PixelData = pixelData, PixelWidth = 4, PixelHeight = 4,
            DisplayColumns = 4, DisplayRows = 2
        };
        node.Measure(new Constraints(0, 10, 0, 5));
        node.Arrange(new Rect(6, 4, 4, 2));
        node.Render(childCtx);
        surface.Composite(childSurface, 5, 3);

        var diff = SurfaceComparer.CompareToEmpty(surface);
        var tokens = SurfaceComparer.ToTokens(diff, surface);
        var hasKgp = tokens.Any(t => t is Hex1b.Tokens.UnrecognizedSequenceToken ust && ust.Sequence.Contains("_G"));
        Assert.True(hasKgp, $"Expected KGP token. Tokens: {string.Join(", ", tokens.Select(t => t.GetType().Name))}");
    }

    [Fact]
    public async Task FullApp_KgpInWindow_ProducesValidKgpOutput()
    {
        var pixelData = MakePixelData();
        var capabilities = new TerminalCapabilities
        {
            SupportsKgp = true, SupportsTrueColor = true, Supports256Colors = true
        };

        var workload = new Hex1bAppWorkloadAdapter(capabilities);
        await workload.ResizeAsync(80, 24);

        var allBytes = new List<byte>();
        using var readCts = new CancellationTokenSource();
        var readTask = Task.Run(async () =>
        {
            try
            {
                while (!readCts.Token.IsCancellationRequested)
                {
                    var item = await workload.ReadOutputItemAsync(readCts.Token);
                    if (!item.Bytes.IsEmpty) allBytes.AddRange(item.Bytes.ToArray());
                }
            }
            catch (OperationCanceledException) { }
        });

        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.Button("Open").OnClick(e =>
                    {
                        var handle = e.Windows.Window(w => w.VStack(v => [
                            v.Text("Image:"),
                            v.KittyGraphics(pixelData, 4, 4).WithDisplaySize(4, 2)
                        ]))
                        .Title("KGP Test")
                        .Size(12, 8)
                        .Position(new WindowPositionSpec(WindowPosition.Center));
                        e.Windows.Open(handle);
                    }),
                    outer.WindowPanel().Fill()
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = false }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await Task.Delay(300);
        workload.SendKey(Hex1bKey.Enter);
        await Task.Delay(500);

        readCts.Cancel();
        try { await readTask; } catch { }
        await app.DisposeAsync();
        try { await runTask; } catch { }

        var text = Encoding.UTF8.GetString(allBytes.ToArray());

        // Extract all KGP sequences
        var kgpSequences = new List<string>();
        for (int i = 0; i < text.Length - 2; i++)
        {
            if (text[i] == '\x1b' && text[i + 1] == '_' && text[i + 2] == 'G')
            {
                var endIdx = text.IndexOf("\x1b\\", i + 3, StringComparison.Ordinal);
                if (endIdx > 0)
                {
                    kgpSequences.Add(text.Substring(i + 3, endIdx - i - 3));
                }
            }
        }

        var transmits = kgpSequences.Where(s => s.StartsWith("a=T")).ToList();
        var puts = kgpSequences.Where(s => s.StartsWith("a=p")).ToList();

        Assert.True(transmits.Count + puts.Count >= 1,
            $"Expected KGP transmit/put. Found {kgpSequences.Count} seqs: [{string.Join("], [", kgpSequences.Select(s => s.Length > 60 ? s[..60] + "..." : s))}]");

        // Verify transmit format
        if (transmits.Count > 0)
        {
            var t = transmits[0];
            Assert.Contains("f=32", t);  // RGBA format
            Assert.Contains("s=4", t);   // width
            Assert.Contains("v=4", t);   // height
            Assert.Contains("i=", t);    // image ID
            Assert.Contains(";", t);     // base64 separator
        }
    }

    [Fact]
    public void KgpNode_DoubleNestedComposite_SurvivesClipping()
    {
        // Simulates Window → VStack → KGP with two levels of composite
        var pixelData = MakePixelData();
        var rootSurface = new Surface(80, 24, new CellMetrics(8, 16));
        var windowSurface = new Surface(12, 8, new CellMetrics(8, 16));
        var contentSurface = new Surface(10, 5, new CellMetrics(8, 16));

        // Render KGP into innermost surface (window content)
        var contentCtx = new SurfaceRenderContext(contentSurface, 35, 10, null);
        contentCtx.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });
        contentCtx.KgpCache = new KgpImageCache();

        var node = new KittyGraphicsNode
        {
            PixelData = pixelData, PixelWidth = 4, PixelHeight = 4,
            DisplayColumns = 4, DisplayRows = 2
        };
        node.Measure(new Constraints(0, 10, 0, 5));
        node.Arrange(new Rect(36, 12, 4, 2));
        node.Render(contentCtx);
        Assert.True(contentSurface.HasKgp, "Content surface should have KGP");

        // Composite content into window surface
        windowSurface.Composite(contentSurface, 1, 2);
        Assert.True(windowSurface.HasKgp, "Window surface should have KGP after first composite");

        // Composite window into root surface
        rootSurface.Composite(windowSurface, 34, 8);
        Assert.True(rootSurface.HasKgp, "Root surface should have KGP after second composite");

        // Verify KGP appears in tokens
        var diff = SurfaceComparer.CompareToEmpty(rootSurface);
        var tokens = SurfaceComparer.ToTokens(diff, rootSurface);
        var kgpTokens = tokens.Where(t =>
            t is Hex1b.Tokens.UnrecognizedSequenceToken ust && ust.Sequence.Contains("_G") && ust.Sequence.Contains("a=T")).ToList();
        Assert.True(kgpTokens.Count >= 1, "Expected KGP transmit token after double composite");
    }

    private static byte[] MakePixelData()
    {
        var data = new byte[4 * 4 * 4];
        for (int i = 0; i < data.Length; i += 4) { data[i] = 255; data[i + 3] = 255; }
        return data;
    }
}
