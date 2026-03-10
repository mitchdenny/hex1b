using Hex1b.Input;
using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for KGP occlusion through the full Hex1bApp pipeline:
/// widget tree → render → registry → solver → tracker → terminal placement verification.
/// </summary>
public class KgpOcclusionIntegrationTests
{
    private static byte[] CreateTestImage(int width = 8, int height = 8)
    {
        var data = new byte[width * height * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = 255;     // R
            data[i + 1] = 128; // G
            data[i + 2] = 0;   // B
            data[i + 3] = 255; // A
        }
        return data;
    }

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
    public async Task WindowPanel_KgpBackground_NoWindows_FullImagePlaced()
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
            .WithDimensions(40, 12)
            .Build();

        var imageData = CreateTestImage(32, 32);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.WindowPanel()
                    .Background(bg =>
                        bg.KgpImage(imageData, 32, 32,
                            bg.Text("[fallback]"),
                            width: 10, height: 5))
                    .Fill()
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("no-windows")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // With no windows, the KGP image should be fully visible (1 placement)
        Assert.NotNull(snapshot);
        Assert.True(snapshot.KgpPlacements.Count >= 1,
            $"Expected at least 1 KGP placement, got {snapshot.KgpPlacements.Count}");
    }

    [Fact]
    public async Task WindowPanel_TextWindowOverKgpBackground_ImageShredded()
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
            .WithDimensions(60, 20)
            .Build();

        var imageData = CreateTestImage(64, 64);
        var windowOpened = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer =>
                [
                    outer.Button("Open Window").OnClick(e =>
                    {
                        var window = e.Windows.Window(w =>
                                w.Text("  This window occludes the KGP image"))
                            .Title("Occluder")
                            .Size(20, 8)
                            .Position(new WindowPositionSpec(WindowPosition.Center));
                        e.Windows.Open(window);
                        windowOpened = true;
                    }),
                    outer.WindowPanel()
                        .Background(bg =>
                            bg.KgpImage(imageData, 64, 64,
                                bg.Text("[fallback]"),
                                width: 30, height: 15))
                        .Fill()
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // First wait for render, then click the button to open a window
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open Window"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Occluder"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("with-window")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(windowOpened, "Window should have been opened");
        Assert.NotNull(snapshot);

        // With an occluding window, the image should be shredded into multiple placements
        // (up to 4 strips: top, bottom, left, right of the occluder)
        Assert.True(snapshot.KgpPlacements.Count >= 2,
            $"Expected at least 2 KGP placements (image shredded around window), got {snapshot.KgpPlacements.Count}");
    }

    [Fact]
    public async Task WindowPanel_KgpInsideWindow_PlacedCorrectly()
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
            .WithDimensions(60, 20)
            .Build();

        var imageData = CreateTestImage(16, 16);
        var windowOpened = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer =>
                [
                    outer.Button("Open").OnClick(e =>
                    {
                        var window = e.Windows.Window(w =>
                                w.VStack(v =>
                                [
                                    v.Text(" Window with KGP:"),
                                    v.KgpImage(imageData, 16, 16,
                                        v.Text("[fallback]"),
                                        width: 10, height: 5)
                                ]))
                            .Title("KGP Window")
                            .Size(20, 10)
                            .Position(new WindowPositionSpec(WindowPosition.Center));
                        e.Windows.Open(window);
                        windowOpened = true;
                    }),
                    outer.WindowPanel().Fill()
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("KGP Window"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("kgp-in-window")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(windowOpened, "Window should have been opened");
        Assert.NotNull(snapshot);

        // KGP image inside the window should have at least one placement
        Assert.True(snapshot.KgpPlacements.Count >= 1,
            $"Expected at least 1 KGP placement for image inside window, got {snapshot.KgpPlacements.Count}");
    }

    [Fact]
    public async Task WindowPanel_MultipleKgpImages_BothRendered()
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
            .WithDimensions(60, 20)
            .Build();

        var image1 = CreateTestImage(16, 16);
        var image2 = CreateTestImage(24, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer =>
                [
                    outer.KgpImage(image1, 16, 16,
                        outer.Text("[img1]"),
                        width: 10, height: 4),
                    outer.KgpImage(image2, 24, 24,
                        outer.Text("[img2]"),
                        width: 12, height: 5)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("multi-kgp")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.NotNull(snapshot);

        // Both images should have placements
        var imageIds = snapshot.KgpPlacements.Select(p => p.ImageId).Distinct().ToList();
        Assert.True(imageIds.Count >= 2,
            $"Expected at least 2 distinct image IDs, got {imageIds.Count}: [{string.Join(", ", imageIds)}]");
    }

    [Fact]
    public void OcclusionSolver_FullPipeline_RegistryToFragments()
    {
        // Unit-level pipeline test: registry → solver → tracker → commands
        var imageData = MakeKgpData(42, 20, 10, 100, 100);

        // Frame 1: Image alone — should produce 1 fragment
        var registry = new KgpImageRegistry();
        registry.RegisterImage(imageData, 5, 2);
        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        Assert.Single(fragments);
        Assert.Equal(42u, fragments[0].ImageId);
        Assert.Equal(5, fragments[0].AbsoluteX);
        Assert.Equal(2, fragments[0].AbsoluteY);
        Assert.Equal(20, fragments[0].CellWidth);
        Assert.Equal(10, fragments[0].CellHeight);

        // Feed to tracker
        var tracker = new KgpPlacementTracker();
        var (before1, after1) = tracker.GenerateCommands(fragments);

        // Should have transmit + placement commands
        Assert.True(before1.Count > 0 || after1.Count > 0,
            "Frame 1 should produce transmit + place commands");

        // Frame 2: Same image with an occluder — should produce multiple fragments
        registry.Clear();
        registry.RegisterImage(imageData, 5, 2);
        registry.PushLayer();
        registry.RegisterOccluder(10, 4, 8, 4); // Window at (10,4) size 8x4

        var fragments2 = KgpOcclusionSolver.ComputeFragments(registry);

        // Occluder partially overlaps image — should produce multiple fragments
        Assert.True(fragments2.Count > 1,
            $"Expected multiple fragments with partial occlusion, got {fragments2.Count}");

        // Total visible area should be less than original
        var totalVisibleCells = fragments2.Sum(f => f.CellWidth * f.CellHeight);
        Assert.True(totalVisibleCells < 20 * 10,
            $"Visible area ({totalVisibleCells}) should be less than full image (200)");

        // Feed to tracker — should produce delete + re-place commands
        var (before2, after2) = tracker.GenerateCommands(fragments2);
        Assert.True(before2.Count > 0 || after2.Count > 0,
            "Frame 2 should produce delete + re-place commands for changed fragments");

        // Frame 3: Same fragments — no changes needed
        registry.Clear();
        registry.RegisterImage(imageData, 5, 2);
        registry.PushLayer();
        registry.RegisterOccluder(10, 4, 8, 4);

        var fragments3 = KgpOcclusionSolver.ComputeFragments(registry);
        var (before3, after3) = tracker.GenerateCommands(fragments3);

        Assert.Empty(before3);
        Assert.Empty(after3);
    }

    [Fact]
    public void OcclusionSolver_ImageFullyCovered_NoFragments()
    {
        var imageData = MakeKgpData(10, 10, 5, 50, 50);

        var registry = new KgpImageRegistry();
        registry.RegisterImage(imageData, 5, 5);
        registry.PushLayer();
        // Occluder completely covers the image
        registry.RegisterOccluder(0, 0, 50, 50);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);
        Assert.Empty(fragments);

        // Tracker should delete the previously placed image
        var tracker = new KgpPlacementTracker();

        // First, pretend the image was visible last frame
        var fullFragments = new List<KgpFragment>
        {
            new(10, 5, 5, 10, 5, 0, 0, 0, 0, imageData)
        };
        tracker.GenerateCommands(fullFragments);

        // Now send empty fragments — should produce delete command
        var (before, after) = tracker.GenerateCommands(fragments);
        Assert.True(before.Count > 0,
            "Fully occluded image should produce delete command");
    }

    [Fact]
    public void OcclusionSolver_SameLayerImages_NoOcclusion()
    {
        var data1 = MakeKgpData(1, 10, 5, 50, 50);

        var data2 = MakeKgpData(2, 10, 5, 50, 50);

        // Both images at same layer, overlapping positions
        var registry = new KgpImageRegistry();
        registry.RegisterImage(data1, 0, 0);
        registry.RegisterImage(data2, 5, 2); // Overlaps with first image

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        // Same layer = no occlusion, each image gets exactly 1 fragment
        Assert.Equal(2, fragments.Count);
        Assert.Contains(fragments, f => f.ImageId == 1);
        Assert.Contains(fragments, f => f.ImageId == 2);
    }

    [Fact]
    public void OcclusionSolver_ClipCoordinates_MappedCorrectly()
    {
        // Image: 200x100 pixels displayed in 20x10 cells
        var imageData = MakeKgpData(5, 20, 10, 200, 100);

        var registry = new KgpImageRegistry();
        registry.RegisterImage(imageData, 0, 0);
        registry.PushLayer();
        // Occluder covers middle: columns 5-15, rows 3-7
        registry.RegisterOccluder(5, 3, 10, 4);

        var fragments = KgpOcclusionSolver.ComputeFragments(registry);

        // Should produce 4 strips
        Assert.Equal(4, fragments.Count);

        // Top strip: (0,0) 20x3 → clip (0,0) 200x30
        var top = fragments.First(f => f.AbsoluteY == 0);
        Assert.Equal(0, top.AbsoluteX);
        Assert.Equal(20, top.CellWidth);
        Assert.Equal(3, top.CellHeight);
        Assert.Equal(0, top.ClipX);
        Assert.Equal(0, top.ClipY);
        Assert.Equal(200, top.ClipW);
        Assert.Equal(30, top.ClipH);

        // Bottom strip: (0,7) 20x3 → clip (0,70) 200x30
        var bottom = fragments.First(f => f.AbsoluteY == 7);
        Assert.Equal(0, bottom.AbsoluteX);
        Assert.Equal(20, bottom.CellWidth);
        Assert.Equal(3, bottom.CellHeight);
        Assert.Equal(0, bottom.ClipX);
        Assert.Equal(70, bottom.ClipY);
        Assert.Equal(200, bottom.ClipW);
        Assert.Equal(30, bottom.ClipH);

        // Left strip: (0,3) 5x4 → clip (0,30) 50x40
        var left = fragments.First(f => f.AbsoluteX == 0 && f.AbsoluteY == 3);
        Assert.Equal(5, left.CellWidth);
        Assert.Equal(4, left.CellHeight);
        Assert.Equal(0, left.ClipX);
        Assert.Equal(30, left.ClipY);
        Assert.Equal(50, left.ClipW);
        Assert.Equal(40, left.ClipH);

        // Right strip: (15,3) 5x4 → clip (150,30) 50x40
        var right = fragments.First(f => f.AbsoluteX == 15 && f.AbsoluteY == 3);
        Assert.Equal(5, right.CellWidth);
        Assert.Equal(4, right.CellHeight);
        Assert.Equal(150, right.ClipX);
        Assert.Equal(30, right.ClipY);
        Assert.Equal(50, right.ClipW);
        Assert.Equal(40, right.ClipH);
    }

    [Fact]
    public async Task MenuPopup_DoesNotOccludeBackgroundKgpImage()
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
            .WithDimensions(60, 20)
            .Build();

        var imageData = CreateTestImage(64, 64);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.MenuBar(m => [
                        m.Menu("File", menu => [
                            menu.MenuItem("New"),
                            menu.MenuItem("Open"),
                            menu.MenuItem("Exit")
                        ])
                    ]),
                    outer.WindowPanel()
                        .Background(bg =>
                            bg.KgpImage(imageData, 64, 64,
                                bg.Text("[fallback]"),
                                width: 30, height: 15))
                        .Fill()
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Capture before opening menu — image should be visible
        var beforeMenu = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("before-menu")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.NotNull(beforeMenu);
        Assert.True(beforeMenu.KgpPlacements.Count >= 1,
            $"Expected KGP placement before menu open, got {beforeMenu.KgpPlacements.Count}");

        // Open the File menu via Alt+F, then capture — image should still be visible
        var afterMenu = await new Hex1bTerminalInputSequenceBuilder()
            .Alt().Key(Hex1bKey.F)
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("after-menu")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.NotNull(afterMenu);
        Assert.True(afterMenu.KgpPlacements.Count >= 1,
            $"Expected KGP placement with menu open, got {afterMenu.KgpPlacements.Count}");
    }
}
