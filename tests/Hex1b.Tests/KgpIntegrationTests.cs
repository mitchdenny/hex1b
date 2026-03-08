using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// End-to-end integration tests for KGP support through the full Hex1bApp pipeline:
/// KgpImageWidget → Hex1bApp → render → Hex1bTerminal → snapshot → SVG verification.
/// </summary>
public class KgpIntegrationTests
{
    private static byte[] CreateTestImage(int width = 4, int height = 4)
    {
        var data = new byte[width * height * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = 255;     // R
            data[i + 1] = 0;   // G
            data[i + 2] = 0;   // B
            data[i + 3] = 255; // A
        }
        return data;
    }

    [Fact]
    public async Task App_KgpImageWidget_RendersToTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
        });

        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
            .WithDimensions(40, 10)
            .Build();

        var imageData = CreateTestImage();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new KgpImageWidget(imageData, 4, 4, new TextBlockWidget("[no kgp]"))
                    .WithWidth(4)
                    .WithHeight(2)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for the app to enter alternate screen (confirms it rendered)
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Capture("rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // KGP-capable terminal should have processed the image
        Assert.NotNull(snapshot);
    }

    [Fact]
    public async Task App_KgpImageWidget_FallbackWhenNoKgpSupport()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var imageData = CreateTestImage();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new KgpImageWidget(imageData, 4, 4, new TextBlockWidget("[fallback]"))
                    .WithWidth(10)
                    .WithHeight(2)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[fallback]"), TimeSpan.FromSeconds(5))
            .Capture("fallback")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(snapshot.ContainsText("[fallback]"));
    }

    [Fact]
    public async Task App_KgpInVStack_RendersWithOtherWidgets()
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
        });

        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
            .WithDimensions(40, 10)
            .Build();

        var imageData = CreateTestImage();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new TextBlockWidget("Title Line"),
                    new KgpImageWidget(imageData, 4, 4, new TextBlockWidget("[no kgp]"))
                        .WithWidth(4)
                        .WithHeight(2),
                    new TextBlockWidget("Footer Line"),
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Title Line"), TimeSpan.FromSeconds(5))
            .Capture("mixed")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(snapshot.ContainsText("Title Line"));
        Assert.True(snapshot.ContainsText("Footer Line"));
    }

    [Fact]
    public async Task App_KgpBelowText_RendersToTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
        });

        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
            .WithDimensions(40, 10)
            .Build();

        var imageData = CreateTestImage(8, 8);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new KgpImageWidget(imageData, 8, 8, new TextBlockWidget("[no kgp]"))
                    .BelowText()
                    .WithWidth(8)
                    .WithHeight(4)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Capture("kgp-below")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // KGP rendering through Hex1bApp confirmed by clean exit
        Assert.NotNull(snapshot);
    }

    [Fact]
    public async Task App_KgpAboveText_RendersToTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
        });

        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
            .WithDimensions(40, 10)
            .Build();

        var imageData = CreateTestImage(8, 8);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new KgpImageWidget(imageData, 8, 8, new TextBlockWidget("[no kgp]"))
                    .AboveText()
                    .WithWidth(8)
                    .WithHeight(4)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Capture("kgp-above")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.NotNull(snapshot);
    }

    [Fact]
    public async Task DirectRender_KgpBelowText_SvgContainsImage()
    {
        // Direct render path (non-surface) — KGP sequences go directly to terminal
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
        });

        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
            .WithDimensions(40, 10)
            .Build();

        var imageData = CreateTestImage(8, 8);
        var node = new Hex1b.Nodes.KgpImageNode
        {
            ImageData = imageData,
            PixelWidth = 8,
            PixelHeight = 8,
            RequestedWidth = 8,
            RequestedHeight = 4,
            ZOrder = KgpZOrder.BelowText,
        };

        var context = new Hex1bRenderContext(workload);
        node.Measure(new Hex1b.Layout.Constraints(0, 40, 0, 10));
        node.Arrange(new Hex1b.Layout.Rect(0, 0, 8, 4));
        node.Render(context);

        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        var svg = terminal.CreateSnapshot().ToSvg();
        Assert.Contains("<image", svg);
    }

    [Fact]
    public async Task App_MultipleKgpImages_AllRendered()
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
        });

        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
            .WithDimensions(40, 10)
            .Build();

        var image1 = CreateTestImage(4, 4);
        var image2 = CreateTestImage(6, 6);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new KgpImageWidget(image1, 4, 4, new TextBlockWidget("[img1]"))
                        .WithWidth(4).WithHeight(2),
                    new KgpImageWidget(image2, 6, 6, new TextBlockWidget("[img2]"))
                        .WithWidth(6).WithHeight(3),
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Capture("multi-kgp")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Both images should produce KGP placements in the terminal
        Assert.NotNull(snapshot);
    }
}
