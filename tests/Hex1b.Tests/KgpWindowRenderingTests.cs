using System.Text;
using Hex1b.Input;
using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public partial class KgpWindowRenderingTests
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
    public async Task FullApp_KgpDirect_InspectOutput()
    {
        // Diagnostic test: capture and verify exact KGP output format
        var pixelData = MakePixelData();
        var capabilities = new TerminalCapabilities
        {
            SupportsKgp = true, SupportsTrueColor = true, Supports256Colors = true
        };

        var workload = new Hex1bAppWorkloadAdapter(capabilities);
        await workload.ResizeAsync(40, 15);

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
            ctx => ctx.VStack(v => [
                v.Text("Above"),
                v.KittyGraphics(pixelData, 4, 4).WithDisplaySize(4, 2),
                v.Text("Below")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = false }
        );

        using var appCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { await app.RunAsync(appCts.Token); } catch (OperationCanceledException) { }
        await app.DisposeAsync();
        await Task.Delay(100);
        readCts.Cancel();
        try { await readTask; } catch { }

        var text = Encoding.UTF8.GetString(allBytes.ToArray());

        // Extract KGP sequences with their params
        var kgpParams = new List<string>();
        for (int i = 0; i < text.Length - 2; i++)
        {
            if (text[i] == '\x1b' && text[i + 1] == '_' && text[i + 2] == 'G')
            {
                var endIdx = text.IndexOf("\x1b\\", i + 3, StringComparison.Ordinal);
                if (endIdx > 0)
                {
                    var seq = text.Substring(i + 3, endIdx - i - 3);
                    var parts = seq.Split(';', 2);
                    kgpParams.Add(parts[0]);
                }
            }
        }

        // Must have at least one transmit
        Assert.Contains(kgpParams, p => p.Contains("a=T"));

        // Verify the transmit has all required KGP fields
        var transmitParams = kgpParams.First(p => p.Contains("a=T"));
        
        // Parse params into dictionary
        var paramDict = transmitParams.Split(',')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1]);

        // Required for transmit+display
        Assert.True(paramDict.ContainsKey("a") && paramDict["a"] == "T", "action must be T");
        Assert.True(paramDict.ContainsKey("f"), $"format (f) missing. Params: {transmitParams}");
        Assert.True(paramDict.ContainsKey("s"), $"source width (s) missing. Params: {transmitParams}");
        Assert.True(paramDict.ContainsKey("v"), $"source height (v) missing. Params: {transmitParams}");
        Assert.True(paramDict.ContainsKey("i"), $"image id (i) missing. Params: {transmitParams}");
        Assert.True(paramDict.ContainsKey("c"), $"columns (c) missing. Params: {transmitParams}");
        Assert.True(paramDict.ContainsKey("r"), $"rows (r) missing. Params: {transmitParams}");
        
        // Verify cursor positioning before KGP
        // The KGP should be at row 2 (0-indexed), so ANSI row 2 (1-based)
        // Check for cursor position sequence immediately before the KGP
        var kgpIdx = text.IndexOf("\x1b_G", StringComparison.Ordinal);
        Assert.True(kgpIdx > 0, "KGP sequence not found in output");
        
        // Look backwards for the cursor position
        var beforeKgp = text[..kgpIdx];
        var lastCursorPos = beforeKgp.LastIndexOf("\x1b[", StringComparison.Ordinal);
        Assert.True(lastCursorPos >= 0, "No cursor position before KGP");
        
        var cursorSeq = beforeKgp[(lastCursorPos + 2)..];
        var hIdx = cursorSeq.IndexOf('H');
        if (hIdx >= 0)
        {
            var posStr = cursorSeq[..hIdx];
            // Should be "row;col" in 1-based coords
            Assert.Contains(";", posStr);
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

public partial class KgpWindowRenderingTests
{
    [Fact]
    public void TokenOrder_KgpEmittedBeforeTextAndNotOverwritten()
    {
        // Verify that KGP tokens are emitted first, and subsequent text tokens
        // don't write to cells covered by the KGP region
        var pixelData = MakePixelData();
        var surface = new Surface(20, 10, new CellMetrics(8, 16));
        
        // Write some text at row 0
        surface[0, 0] = new SurfaceCell("H", null, null);
        surface[1, 0] = new SurfaceCell("i", null, null);
        
        // Write KGP at row 1, col 0 spanning 4 cols x 2 rows
        var cache = new KgpImageCache();
        var imageId = cache.AllocateImageId();
        var payload = $"\x1b_Ga=T,f=32,s=4,v=4,i={imageId},c=4,r=2,C=1,q=2;AAAA\x1b\\";
        var kgpData = new KgpCellData(payload, 4, 2);
        surface[0, 1] = new SurfaceCell(" ", null, null, KgpData: kgpData);
        
        // Write text at row 3 (below KGP region)
        surface[0, 3] = new SurfaceCell("B", null, null);
        
        // Also put spaces at cells COVERED by KGP (rows 1-2, cols 0-3)
        // These should be skipped in the output
        for (int y = 1; y <= 2; y++)
            for (int x = 0; x < 4; x++)
                if (!(x == 0 && y == 1)) // skip anchor
                    surface[x, y] = new SurfaceCell(" ", null, null);
        
        var diff = SurfaceComparer.CompareToEmpty(surface);
        var tokens = SurfaceComparer.ToTokens(diff, surface);
        
        // Find KGP token index
        int kgpTokenIdx = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is Hex1b.Tokens.UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=T"))
            {
                kgpTokenIdx = i;
                break;
            }
        }
        Assert.True(kgpTokenIdx >= 0, "KGP token not found");
        
        // Verify there's a cursor position token right before the KGP
        Assert.True(kgpTokenIdx > 0 && tokens[kgpTokenIdx - 1] is Hex1b.Tokens.CursorPositionToken,
            "Expected cursor position before KGP token");
        
        var cursorToken = (Hex1b.Tokens.CursorPositionToken)tokens[kgpTokenIdx - 1];
        Assert.Equal(2, cursorToken.Row);  // 1-based row 2 = 0-based row 1
        Assert.Equal(1, cursorToken.Column); // 1-based col 1 = 0-based col 0
        
        // Verify NO cursor position targets rows 1-2, cols 1-3 after the KGP token
        // (those cells should be skipped as covered by KGP)
        for (int i = kgpTokenIdx + 1; i < tokens.Count; i++)
        {
            if (tokens[i] is Hex1b.Tokens.CursorPositionToken cp)
            {
                var row0 = cp.Row - 1;
                var col0 = cp.Column - 1;
                // Check if this position is within the KGP region (rows 1-2, cols 0-3)
                // but NOT the anchor (row 1, col 0)
                bool inKgpRegion = row0 >= 1 && row0 <= 2 && col0 >= 0 && col0 < 4;
                bool isAnchor = row0 == 1 && col0 == 0;
                Assert.False(inKgpRegion && !isAnchor,
                    $"Cursor positioned at ({col0},{row0}) which is inside KGP region but not anchor");
            }
        }
    }
}

public partial class KgpWindowRenderingTests
{
    [Fact]
    public void MultiChunk_ContinuationChunks_NoLeadingComma()
    {
        // 64x64 RGBA = 16384 bytes → ~21848 base64 chars → 6 chunks
        var pixelData = new byte[64 * 64 * 4];
        for (int i = 0; i < pixelData.Length; i += 4) { pixelData[i] = 255; pixelData[i + 3] = 255; }

        var surface = new Surface(40, 20, new CellMetrics(8, 16));
        var ctx = new SurfaceRenderContext(surface, 0, 0, null);
        ctx.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });
        ctx.KgpCache = new KgpImageCache();

        var node = new KittyGraphicsNode
        {
            PixelData = pixelData, PixelWidth = 64, PixelHeight = 64,
            DisplayColumns = 16, DisplayRows = 8
        };
        node.Measure(new Constraints(0, 40, 0, 20));
        node.Arrange(new Rect(0, 0, 16, 8));
        node.Render(ctx);

        var cell = surface[0, 0];
        Assert.True(cell.HasKgp, "Cell should have KGP data");
        var payload = cell.KgpData!.Payload;

        // Split into individual APC sequences
        var sequences = new List<string>();
        int idx = 0;
        while (idx < payload.Length)
        {
            var start = payload.IndexOf("\x1b_G", idx, StringComparison.Ordinal);
            if (start < 0) break;
            var end = payload.IndexOf("\x1b\\", start + 3, StringComparison.Ordinal);
            if (end < 0) break;
            sequences.Add(payload.Substring(start + 3, end - start - 3));
            idx = end + 2;
        }

        Assert.True(sequences.Count > 1, $"Expected multiple chunks, got {sequences.Count}");

        // First chunk should have full metadata + m=1
        Assert.StartsWith("a=T,", sequences[0]);
        Assert.Contains(",m=1", sequences[0]);

        // Continuation chunks must NOT have a leading comma
        for (int i = 1; i < sequences.Count; i++)
        {
            Assert.False(sequences[i].StartsWith(","),
                $"Chunk {i} has leading comma: {sequences[i][..Math.Min(20, sequences[i].Length)]}");
            Assert.StartsWith("m=", sequences[i]);
        }

        // Last chunk must have m=0
        Assert.StartsWith("m=0", sequences[^1]);
    }
}
