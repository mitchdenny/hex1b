using Hex1b.Input;
using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Tokens;
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

    private static void SendKgp(Hex1bTerminal terminal, string escapeSequence)
    {
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(escapeSequence));
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
                            img => img.Text("[fallback]"),
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
                                img => img.Text("[fallback]"),
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
                                        img => img.Text("[fallback]"),
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
                        img => img.Text("[img1]"),
                        width: 10, height: 4),
                    outer.KgpImage(image2, 24, 24,
                        img => img.Text("[img2]"),
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
                                img => img.Text("[fallback]"),
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
        var placementsBeforeMenu = beforeMenu.KgpPlacements.Count;

        // Open the File menu via Alt+F, then capture — image should still be visible
        // but sliced around the menu popup area
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

        // The menu popup should occlude part of the image, causing the occlusion
        // solver to slice it into more fragments than the original single placement.
        Assert.True(afterMenu.KgpPlacements.Count > placementsBeforeMenu,
            $"Expected image to be sliced by menu occluder: before={placementsBeforeMenu}, after={afterMenu.KgpPlacements.Count}");
    }

    [Fact]
    public async Task WindowPanel_KgpWindowDragThenResize_WindowSurvives()
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
            .WithMouse()
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
                                    v.Text("KGP-Content"),
                                    v.KgpImage(imageData, 16, 16,
                                        img => img.Text("[fallback]"),
                                        width: 10, height: 3)
                                ]))
                            .Title("DragMe")
                            .Size(20, 8)
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

        // Open window and wait for it
        var beforeDrag = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("DragMe"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("before-drag")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(windowOpened, "Window should have been opened");
        Assert.NotNull(beforeDrag);
        Assert.True(beforeDrag.ContainsText("DragMe"), "Window title should be visible before drag");
        Assert.True(beforeDrag.ContainsText("KGP-Content"), "Window content should be visible before drag");

        // Drag the window title bar from center to a new position (move right 5, down 3)
        // Window is centered at roughly (20, 6) on 60x20, title bar is first row
        var afterDrag = await new Hex1bTerminalInputSequenceBuilder()
            .Drag(30, 7, 35, 10) // Drag from center-ish to right+down
            .Wait(TimeSpan.FromMilliseconds(300))
            .Capture("after-drag")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.NotNull(afterDrag);
        Assert.True(afterDrag.ContainsText("DragMe"), "Window title should be visible after drag");
        Assert.True(afterDrag.ContainsText("KGP-Content"), "Window content should be visible after drag");
        var imageIdBeforeResize = afterDrag.KgpPlacements.Select(p => p.ImageId).Distinct().Single();

        // Now resize the terminal by 1 row. Simulate a Kitty-style resize that
        // invalidates terminal-side KGP state so the app must fully rehydrate it.
        terminal.Resize(60, 19);
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=d,d=A,q=2"));
        Assert.Empty(terminal.KgpPlacements);
        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
        await workload.ResizeAsync(60, 19, TestContext.Current.CancellationToken);

        var afterResize = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Width == 60
                            && s.Height == 19
                            && s.ContainsText("DragMe")
                            && s.ContainsText("KGP-Content")
                            && terminal.KgpPlacements.Count > 0
                            && terminal.KgpImageStore.ImageCount > 0,
                TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(300))
            .Capture("after-resize")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.NotNull(afterResize);

        // The window should still be visible after resize!
        Assert.True(afterResize.ContainsText("DragMe"),
            $"Window title 'DragMe' should be visible after drag+resize. Screen content:\n{afterResize.GetText()}");
        Assert.True(afterResize.ContainsText("KGP-Content"),
            $"Window content 'KGP-Content' should be visible after drag+resize. Screen content:\n{afterResize.GetText()}");

        // KGP placements should exist after resize
        Assert.True(afterResize.KgpPlacements.Count >= 1,
            $"Expected KGP placement after drag+resize, got {afterResize.KgpPlacements.Count}");
        var imageIdAfterResize = afterResize.KgpPlacements.Select(p => p.ImageId).Distinct().Single();
        Assert.NotEqual(imageIdBeforeResize, imageIdAfterResize);

        app.RequestStop();
        await runTask;
    }

    [Fact]
    public async Task WindowPanel_NonKgpWindowDragThenResize_WindowSurvives()
    {
        // Control test: same as above but without KGP image to isolate whether
        // the bug is KGP-specific or a general window issue
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
        });

        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
            .WithMouse()
            .WithDimensions(60, 20)
            .Build();

        var windowOpened = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer =>
                [
                    outer.Button("Open").OnClick(e =>
                    {
                        var window = e.Windows.Window(w =>
                                w.Text("PlainContent"))
                            .Title("DragMe")
                            .Size(20, 8)
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

        // Open window
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("DragMe"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(200))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(windowOpened);

        // Drag then resize
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(30, 7, 35, 10)
            .Wait(TimeSpan.FromMilliseconds(300))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        terminal.Resize(60, 19);
        await workload.ResizeAsync(60, 19, TestContext.Current.CancellationToken);

        var afterResize = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Width == 60 && s.Height == 19, TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(300))
            .Capture("after-resize-no-kgp")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.NotNull(afterResize);
        Assert.True(afterResize.ContainsText("DragMe"),
            $"Window title should survive drag+resize (no KGP). Screen:\n{afterResize.GetText()}");
        Assert.True(afterResize.ContainsText("PlainContent"),
            $"Window content should survive drag+resize (no KGP). Screen:\n{afterResize.GetText()}");

        app.RequestStop();
        await runTask;
    }

    [Fact]
    public async Task WindowPanel_KgpWindowDragCloseReopen_WindowRendersAgain()
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
            .WithMouse()
            .WithDimensions(60, 20)
            .Build();

        var imageData = CreateTestImage(16, 16);
        WindowHandle? currentWindow = null;
        var openCount = 0;
        var closeCount = 0;
        var statusText = "Ready";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer =>
                [
                    outer.Text(statusText),
                    outer.WindowPanel().Fill()
                ]).WithInputBindings(bindings =>
                {
                    bindings.Ctrl().Key(Hex1bKey.O).Action(e =>
                    {
                        if (currentWindow is not null)
                            return Task.CompletedTask;

                        var window = e.Windows.Window(w =>
                                w.VStack(v =>
                                [
                                    v.Text("KGP-Content"),
                                    v.KgpImage(imageData, 16, 16,
                                        img => img.Text("[fallback]"),
                                        width: 10, height: 3)
                                ]))
                            .Title("DragMe")
                            .Size(20, 8)
                            .Position(new WindowPositionSpec(WindowPosition.Center))
                            .OnClose(() =>
                            {
                                currentWindow = null;
                                closeCount++;
                                statusText = $"Closed {closeCount}";
                            });

                        currentWindow = window;
                        e.Windows.Open(window);
                        openCount++;
                        statusText = $"Opened {openCount}";
                        return Task.CompletedTask;
                    }, "Open KGP window");

                    bindings.Ctrl().Key(Hex1bKey.X).Action(_ =>
                    {
                        currentWindow?.Cancel();
                        return Task.CompletedTask;
                    }, "Close KGP window");
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var initial = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(5), "app ready")
            .Key(Hex1bKey.O, Hex1bModifiers.Control)
            .WaitUntil(s => s.ContainsText("Opened 1")
                            && s.ContainsText("DragMe")
                            && s.ContainsText("KGP-Content")
                            && terminal.KgpPlacements.Count > 0,
                TimeSpan.FromSeconds(5), "window opened")
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("before-drag-close-reopen")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(1, openCount);
        Assert.NotNull(initial);
        Assert.True(initial.KgpPlacements.Count >= 1, "Expected KGP placement after initial open.");

        var afterDrag = await new Hex1bTerminalInputSequenceBuilder()
            .Drag(30, 7, 35, 10)
            .WaitUntil(s => s.ContainsText("DragMe") && s.ContainsText("KGP-Content"),
                TimeSpan.FromSeconds(5), "window dragged")
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("after-drag-before-close")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.NotNull(afterDrag);
        Assert.True(afterDrag.KgpPlacements.Count >= 1, "Expected KGP placement after drag.");

        var afterClose = await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.X, Hex1bModifiers.Control)
            .WaitUntil(s => s.ContainsText("Closed 1")
                            && !s.ContainsText("DragMe")
                            && terminal.KgpPlacements.Count == 0,
                TimeSpan.FromSeconds(5), "window closed")
            .Capture("after-close-before-reopen")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(1, closeCount);
        Assert.NotNull(afterClose);
        Assert.False(afterClose.ContainsText("DragMe"));
        Assert.Empty(afterClose.KgpPlacements);

        var afterReopen = await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.O, Hex1bModifiers.Control)
            .WaitUntil(s => s.ContainsText("Opened 2")
                            && s.ContainsText("DragMe")
                            && s.ContainsText("KGP-Content")
                            && terminal.KgpPlacements.Count > 0,
                TimeSpan.FromSeconds(5), "window reopened")
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("after-reopen")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal(2, openCount);
        Assert.Equal(1, closeCount);
        Assert.NotNull(afterReopen);
        Assert.True(afterReopen.ContainsText("DragMe"));
        Assert.True(afterReopen.ContainsText("KGP-Content"));
        Assert.True(afterReopen.KgpPlacements.Count >= 1,
            $"Expected KGP placement after reopen, got {afterReopen.KgpPlacements.Count}");
    }

    [Fact]
    public async Task ResizeFrame_WithKgp_DeletesPlacementsBeforeClear_ThenRetransmitsOnFollowUpFrame()
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
        });

        await workload.ResizeAsync(60, 20, TestContext.Current.CancellationToken);

        var imageData = CreateTestImage(64, 64);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.WindowPanel()
                    .Background(bg =>
                        bg.KgpImage(imageData, 64, 64,
                            img => img.Text("[fallback]"),
                            width: 20, height: 8))
                    .Fill()
            ),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                EnableInputCoalescing = false,
            }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var initialFrameOutput = await ReadNextOutputContainingAsync(
            workload,
            "\x1b_Ga=t,",
            TestContext.Current.CancellationToken);
        var initialFrameText = System.Text.Encoding.UTF8.GetString(initialFrameOutput.Span);
        var initialImageId = ExtractFirstTransmitImageId(initialFrameText);

        await DrainWorkloadOutputAsync(workload, TestContext.Current.CancellationToken);

        await workload.ResizeAsync(60, 19, TestContext.Current.CancellationToken);

        var deleteOutput = await workload.ReadOutputAsync(TestContext.Current.CancellationToken);
        var clearOutput = await workload.ReadOutputAsync(TestContext.Current.CancellationToken);
        var postResizeOutput = await ReadNextNonEmptyOutputAsync(workload, TestContext.Current.CancellationToken);

        var deleteText = System.Text.Encoding.UTF8.GetString(deleteOutput.Span);
        var clearText = System.Text.Encoding.UTF8.GetString(clearOutput.Span);
        var postResizeText = System.Text.Encoding.UTF8.GetString(postResizeOutput.Span);

        string retransmitText;
        if (postResizeText.Contains("\x1b_Ga=t,", StringComparison.Ordinal))
        {
            retransmitText = postResizeText;
        }
        else
        {
            Assert.DoesNotContain("\x1b_Ga=t,", postResizeText);
            Assert.DoesNotContain("\x1b_Ga=p,", postResizeText);

            var retransmitOutput = await ReadNextNonEmptyOutputAsync(workload, TestContext.Current.CancellationToken);
            retransmitText = System.Text.Encoding.UTF8.GetString(retransmitOutput.Span);
        }

        Assert.Equal("\x1b_Ga=d,d=a,q=2\x1b\\", deleteText);
        Assert.Equal("\x1b[0m\x1b[2J", clearText);
        Assert.DoesNotContain("\x1b_Ga=d,d=i,", postResizeText);
        Assert.Contains("\x1b_Ga=t,", retransmitText);
        Assert.Contains("\x1b_Ga=p,", retransmitText);
        Assert.Contains($"\x1b_Ga=d,d=I,i={initialImageId},q=2\x1b\\", retransmitText);
        Assert.NotEqual(initialImageId, ExtractFirstTransmitImageId(retransmitText));

        app.RequestStop();
        await runTask;
    }

    private static async Task<ReadOnlyMemory<byte>> ReadNextNonEmptyOutputAsync(
        Hex1bAppWorkloadAdapter workload,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var output = await workload.ReadOutputAsync(cancellationToken);
            if (!output.IsEmpty)
                return output;
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadNextOutputContainingAsync(
        Hex1bAppWorkloadAdapter workload,
        string expectedText,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var output = await ReadNextNonEmptyOutputAsync(workload, cancellationToken);
            var text = System.Text.Encoding.UTF8.GetString(output.Span);
            if (text.Contains(expectedText, StringComparison.Ordinal))
                return output;
        }
    }

    private static async Task DrainWorkloadOutputAsync(Hex1bAppWorkloadAdapter workload, CancellationToken cancellationToken)
    {
        while (true)
        {
            var drainedAny = false;
            while (workload.TryReadOutput(out _))
            {
                drainedAny = true;
            }

            if (!drainedAny && workload.OutputQueueDepth == 0)
                return;

            await Task.Delay(50, cancellationToken);
        }
    }

    private static uint ExtractFirstTransmitImageId(string text)
    {
        var transmitIndex = text.IndexOf("\x1b_Ga=t,", StringComparison.Ordinal);
        Assert.True(transmitIndex >= 0, "Expected KGP output to contain a transmit sequence.");

        var index = text.IndexOf("i=", transmitIndex, StringComparison.Ordinal);
        Assert.True(index >= 0, "Expected KGP transmit output to contain an image id.");

        index += 2;
        var end = index;
        while (end < text.Length && char.IsDigit(text[end]))
            end++;

        Assert.True(end > index, "Expected KGP image id digits after `i=`.");
        return uint.Parse(text[index..end], System.Globalization.CultureInfo.InvariantCulture);
    }
}
