using System.Text;
using System.Collections.Concurrent;
using Hex1b;
using Hex1b.Widgets;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Tokens;
using Hex1b.Kgp;
using Xunit;

namespace Hex1b.Tests;

public class KgpEndToEndTest
{
    [Fact]
    public void DirectApp_CapabilitiesAndDimensionsCorrect()
    {
        var capabilities = new TerminalCapabilities
        {
            SupportsMouse = false,
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true
        };
        var workload = new Hex1bAppWorkloadAdapter(capabilities);
        
        // Before resize, dimensions are 0
        Assert.Equal(0, workload.Width);
        Assert.Equal(0, workload.Height);
        Assert.True(workload.Capabilities.SupportsKgp);
        
        // After resize
        workload.ResizeAsync(20, 10).AsTask().Wait();
        Assert.Equal(20, workload.Width);
        Assert.Equal(10, workload.Height);
    }

    [Fact]
    public void KittyGraphicsNode_RendersWithSurfaceContext()
    {
        // Exactly what the app does, but manually
        var surface = new Surface(20, 10);
        var ctx = new SurfaceRenderContext(surface, Theming.Hex1bThemes.Default);
        ctx.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });

        var pixelData = new byte[16];
        for (int i = 0; i < 4; i++) { pixelData[i * 4] = 255; pixelData[i * 4 + 3] = 255; }

        // Create the widget and reconcile to get a node (mimicking the app)
        var widget = new KittyGraphicsWidget(pixelData, 2, 2) { DisplayColumns = 4, DisplayRows = 2 };

        // Create node directly
        var node = new KittyGraphicsNode
        {
            PixelData = pixelData,
            PixelWidth = 2,
            PixelHeight = 2,
            DisplayColumns = 4,
            DisplayRows = 2,
        };

        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Rect(0, 0, 20, 10));
        node.Render(ctx);

        // Verify surface has KGP data
        Assert.True(surface[0, 0].HasKgp);

        // Now the same but through the ZStack wrapper (what the app does)
        var surface2 = new Surface(20, 10);
        var ctx2 = new SurfaceRenderContext(surface2, Theming.Hex1bThemes.Default);
        ctx2.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });

        // Create a ZStack with our KGP node inside
        var zstackNode = new ZStackNode();
        var kgpNode = new KittyGraphicsNode
        {
            PixelData = pixelData,
            PixelWidth = 2,
            PixelHeight = 2,
            DisplayColumns = 4,
            DisplayRows = 2,
        };
        zstackNode.Children.Add(kgpNode);
        zstackNode.Measure(new Constraints(0, 20, 0, 10));
        zstackNode.Arrange(new Rect(0, 0, 20, 10));
        zstackNode.Render(ctx2);

        Assert.True(surface2[0, 0].HasKgp, "ZStack-wrapped KGP node should also place data on surface");
    }
}

// Separate class to test the full app output path
public class KgpAppOutputTest
{
    [Fact]
    public async Task Hex1bApp_KgpWidget_KgpInOutput()
    {
        var pixelData = new byte[16];
        for (int i = 0; i < 4; i++) { pixelData[i * 4] = 255; pixelData[i * 4 + 3] = 255; }

        var capabilities = new TerminalCapabilities
        {
            SupportsMouse = false,
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true
        };
        var workload = new Hex1bAppWorkloadAdapter(capabilities);
        await workload.ResizeAsync(20, 10);

        var options = new Hex1bAppOptions
        {
            WorkloadAdapter = workload,
            EnableMouse = false
        };

        // Start reading output before the app
        var allOutput = new ConcurrentQueue<byte[]>();
        using var readCts = new CancellationTokenSource();
        var readTask = Task.Run(async () =>
        {
            try
            {
                while (!readCts.Token.IsCancellationRequested)
                {
                    var item = await workload.ReadOutputItemAsync(readCts.Token);
                    if (!item.Bytes.IsEmpty)
                        allOutput.Enqueue(item.Bytes.ToArray());
                }
            }
            catch (OperationCanceledException) { }
        });

        var app = new Hex1bApp(ctx =>
            ctx.KittyGraphics(pixelData, 2, 2).WithDisplaySize(4, 2),
            options);

        using var appCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { await app.RunAsync(appCts.Token); }
        catch (OperationCanceledException) { }
        await app.DisposeAsync();

        await Task.Delay(200);
        readCts.Cancel();
        try { await readTask; } catch { }

        var allBytes = allOutput.SelectMany(b => b).ToArray();
        var output = System.Text.Encoding.UTF8.GetString(allBytes);

        Assert.True(output.Contains("\x1b_G"), $"Output ({allBytes.Length} bytes) should contain KGP APC sequence");
        Assert.Contains("a=T,f=32", output);
    }
}
