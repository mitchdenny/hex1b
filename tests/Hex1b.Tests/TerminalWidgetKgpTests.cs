using System.Text;
using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class TerminalWidgetKgpTests
{
    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
    };

    [TestMethod]
    public void Render_KgpCapableParent_PropagatesCapabilitiesAndCellMetrics()
    {
        var handle = new TerminalWidgetHandle(8, 4);
        var node = CreateNode(handle, new Rect(2, 1, 8, 4));
        var context = CreateContext(12, 8, out _);

        node.Render(context);

        Assert.IsTrue(handle.Capabilities.SupportsKgp);
        Assert.AreEqual(10, handle.Capabilities.CellPixelWidth);
        Assert.AreEqual(20, handle.Capabilities.CellPixelHeight);
    }

    [TestMethod]
    public async Task WriteOutputWithImpacts_KgpOnlyToken_RaisesOutputReceived()
    {
        var handle = new TerminalWidgetHandle(8, 4);
        var token = TestSeq.Single(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildTransmitCommand(1, 1, 1, quiet: 2)));
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handle.OutputReceived += () => received.TrySetResult();

        await handle.WriteOutputWithImpactsAsync(
            [AppliedToken.WithNoCellImpacts(token, 0, 0, 0, 0)]);

        await received.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);
    }

    [TestMethod]
    public void Render_KgpCapableParent_ChildQueryReceivesOkResponse()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithDimensions(8, 4)
            .WithTerminalWidget(out var handle)
            .Build();
        var node = CreateNode(handle, new Rect(0, 0, 8, 4));
        var context = CreateContext(8, 4, out _);

        node.Render(context);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildQueryCommand(imageId: 31)));

        Assert.AreEqual("\x1b_Gi=31;OK\x1b\\", workload.ReadResponse());
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public async Task Render_ChildPlacementExceedsBounds_TranslatesAndClipsSource()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithDimensions(6, 4)
            .WithTerminalWidget(out var handle)
            .Build();
        var node = CreateNode(handle, new Rect(3, 2, 4, 3));
        var context = CreateContext(12, 8, out var registry);

        node.Render(context);

        var command =
            "\x1b[2;3H" +
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                imageId: 77,
                width: 40,
                height: 60,
                displayColumns: 4,
                displayRows: 3,
                quiet: 2);
        var applied = terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(command));
        await handle.WriteOutputWithImpactsAsync(applied);

        node.Render(context);

        var entry = TestSeq.Single(registry.Images);
        Assert.AreEqual(5, entry.AbsoluteX);
        Assert.AreEqual(3, entry.AbsoluteY);
        Assert.AreEqual(2, entry.Data.WidthInCells);
        Assert.AreEqual(2, entry.Data.HeightInCells);
        Assert.AreEqual(20, entry.Data.ClipW);
        Assert.AreEqual(40, entry.Data.ClipH);
    }

    [TestMethod]
    [DataRow(KgpFormat.Rgb24)]
    [DataRow(KgpFormat.Rgba32)]
    [DataRow(KgpFormat.Png)]
    public void RegisterKgp_PreservesFormatPayloadPlacementAndText(KgpFormat format)
    {
        var imageBytes = new byte[] { 1, 2, 3, 4, 5, 6 };
        var image = new KgpImageData(91, 0, imageBytes, 20, 20, format);
        var placement = new KgpPlacement(
            imageId: 91,
            placementId: 4,
            row: 1,
            column: 2,
            displayColumns: 2,
            displayRows: 1,
            sourceX: 3,
            sourceY: 4,
            sourceWidth: 10,
            sourceHeight: 12,
            zIndex: 7,
            cellOffsetX: 2,
            cellOffsetY: 3);
        var surface = new Surface(10, 5, new CellMetrics(10, 20));
        surface[2, 1] = new SurfaceCell("T", null, null);
        var registry = new KgpImageRegistry();
        var context = new SurfaceRenderContext(surface)
        {
            CellMetrics = new CellMetrics(10, 20),
            KgpRegistry = registry,
        };
        context.SetCapabilities(KgpCapabilities);

        context.RegisterKgp(image, placement);

        var entry = TestSeq.Single(registry.Images);
        Assert.AreEqual("T", surface[2, 1].Character);
        Assert.Contains($"f={(int)format}", entry.Data.TransmitPayload!);
        Assert.Contains(Convert.ToBase64String(imageBytes), entry.Data.TransmitPayload!);
        Assert.AreEqual(3, entry.Data.ClipX);
        Assert.AreEqual(4, entry.Data.ClipY);
        Assert.AreEqual(10, entry.Data.ClipW);
        Assert.AreEqual(12, entry.Data.ClipH);
        Assert.AreEqual(7, entry.Data.ZIndex);
        Assert.AreEqual(2u, entry.Data.CellOffsetX);
        Assert.AreEqual(3u, entry.Data.CellOffsetY);
        Assert.Contains("X=2", entry.Data.BuildPlacementPayload());
        Assert.Contains("Y=3", entry.Data.BuildPlacementPayload());
    }

    [TestMethod]
    public async Task NestedHex1bApp_KgpImage_RendersInOuterTerminal()
    {
        var imageBytes = KgpTestHelper.CreatePixelData(4, 4, fillByte: 0x5A);
        Hex1bApp? innerApp = null;
        await using var innerTerminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                _ => { },
                app =>
                {
                    innerApp = app;
                    return _ => new KgpImageWidget(
                            imageBytes,
                            4,
                            4,
                            new TextBlockWidget("[inner fallback]"))
                        .Width(4)
                        .Height(2);
                })
            .WithDimensions(8, 4)
            .WithTerminalWidget(out var handle)
            .Build();

        Hex1bApp? outerApp = null;
        await using var outerTerminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                _ => { },
                app =>
                {
                    outerApp = app;
                    return _ => new VStackWidget([
                        new TextBlockWidget("Host"),
                        new TerminalWidget(handle)
                            .Width(SizeHint.Fixed(8))
                            .Height(SizeHint.Fixed(4)),
                    ]);
                })
            .WithHeadless(KgpCapabilities)
            .WithDimensions(12, 8)
            .Build();

        var outerRunTask = outerTerminal.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                snapshot => snapshot.ContainsText("Host") && handle.Capabilities.SupportsKgp,
                TimeSpan.FromSeconds(5),
                "outer terminal mounted the KGP-capable child")
            .Build()
            .ApplyAsync(outerTerminal, TestContext.Current.CancellationToken);

        var innerRunTask = innerTerminal.RunAsync(TestContext.Current.CancellationToken);
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                current => current.KgpPlacements.Count > 0,
                TimeSpan.FromSeconds(5),
                "nested KGP placement reached the outer terminal")
            .Capture("nested-terminal-widget-kgp")
            .Build()
            .ApplyWithCaptureAsync(outerTerminal, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("[inner fallback]", snapshot.GetScreenText());
        var placement = TestSeq.Single(snapshot.KgpPlacements);
        var image = snapshot.KgpImages[placement.ImageId];
        CollectionAssert.AreEqual(imageBytes, image.Data);
        Assert.AreEqual(1, placement.Row);
        Assert.AreEqual(0, placement.Column);

        innerApp!.RequestStop();
        outerApp!.RequestStop();
        await Task.WhenAll(innerRunTask, outerRunTask);
    }

    [TestMethod]
    public async Task NestedHex1bApp_KgpPlacementMovesAndDeletesWithoutGhosts()
    {
        var state = new NestedKgpState();
        var imageBytes = KgpTestHelper.CreatePixelData(4, 4, fillByte: 0x6B);
        Hex1bApp? innerApp = null;
        await using var innerTerminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                _ => { },
                (Func<Hex1bApp, Func<RootContext, Hex1bWidget>>)(app =>
                {
                    innerApp = app;
                    return _ =>
                    {
                        if (!state.ShowImage)
                            return (Hex1bWidget)new TextBlockWidget("gone");

                        var image = new KgpImageWidget(
                                imageBytes,
                                4,
                                4,
                                new TextBlockWidget("image fallback"))
                            .Width(4)
                            .Height(2);
                        return state.MoveDown
                            ? new VStackWidget([new TextBlockWidget("moved"), image])
                            : (Hex1bWidget)image;
                    };
                }))
            .WithDimensions(8, 4)
            .WithTerminalWidget(out var handle)
            .Build();

        Hex1bApp? outerApp = null;
        await using var outerTerminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                _ => { },
                app =>
                {
                    outerApp = app;
                    return _ => new TerminalWidget(handle)
                        .Width(SizeHint.Fixed(8))
                        .Height(SizeHint.Fixed(4));
                })
            .WithHeadless(KgpCapabilities)
            .WithDimensions(8, 4)
            .Build();

        var outerRunTask = outerTerminal.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                _ => handle.Capabilities.SupportsKgp,
                TimeSpan.FromSeconds(5),
                "outer terminal propagated KGP support")
            .Build()
            .ApplyAsync(outerTerminal, TestContext.Current.CancellationToken);

        var innerRunTask = innerTerminal.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                snapshot => snapshot.KgpPlacements.Count == 1 &&
                    snapshot.KgpPlacements[0].Row == 0,
                TimeSpan.FromSeconds(5),
                "initial nested KGP placement reached the outer terminal")
            .Build()
            .ApplyAsync(outerTerminal, TestContext.Current.CancellationToken);

        state.MoveDown = true;
        innerApp!.Invalidate();
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                snapshot => snapshot.KgpPlacements.Count == 1 &&
                    snapshot.KgpPlacements[0].Row == 1,
                TimeSpan.FromSeconds(5),
                "nested KGP placement moved without leaving its old placement")
            .Build()
            .ApplyAsync(outerTerminal, TestContext.Current.CancellationToken);

        state.ShowImage = false;
        innerApp.Invalidate();
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                snapshot => snapshot.KgpPlacements.Count == 0 &&
                    snapshot.ContainsText("gone"),
                TimeSpan.FromSeconds(5),
                "nested KGP placement was deleted without leaving a ghost")
            .Build()
            .ApplyAsync(outerTerminal, TestContext.Current.CancellationToken);

        innerApp.RequestStop();
        outerApp!.RequestStop();
        await Task.WhenAll(innerRunTask, outerRunTask);
    }

    [TestMethod]
    public async Task Render_TwoChildrenReuseImageId_HostImagesRemainDistinct()
    {
        using var firstWorkload = new Hex1bAppWorkloadAdapter();
        using var firstTerminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(firstWorkload)
            .WithDimensions(4, 2)
            .WithTerminalWidget(out var firstHandle)
            .Build();
        using var secondWorkload = new Hex1bAppWorkloadAdapter();
        using var secondTerminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(secondWorkload)
            .WithDimensions(4, 2)
            .WithTerminalWidget(out var secondHandle)
            .Build();
        var firstNode = CreateNode(firstHandle, new Rect(0, 0, 4, 2));
        var secondNode = CreateNode(secondHandle, new Rect(4, 0, 4, 2));
        var context = CreateContext(8, 2, out var registry);

        firstNode.Render(context);
        secondNode.Render(context);
        var firstApplied = firstTerminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                imageId: 1,
                width: 2,
                height: 2,
                displayColumns: 1,
                displayRows: 1,
                quiet: 2,
                fillByte: 0x11)));
        var secondApplied = secondTerminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                imageId: 1,
                width: 2,
                height: 2,
                displayColumns: 1,
                displayRows: 1,
                quiet: 2,
                fillByte: 0x22)));
        await firstHandle.WriteOutputWithImpactsAsync(firstApplied);
        await secondHandle.WriteOutputWithImpactsAsync(secondApplied);

        firstNode.Render(context);
        secondNode.Render(context);

        Assert.HasCount(2, registry.Images);
        Assert.AreEqual(0, registry.Images[0].AbsoluteX);
        Assert.AreEqual(4, registry.Images[1].AbsoluteX);
        Assert.AreNotEqual(registry.Images[0].Data.ImageId, registry.Images[1].Data.ImageId);
    }

    [TestMethod]
    public async Task NestedHex1bApp_NonKgpParent_RendersFallback()
    {
        Hex1bApp? innerApp = null;
        await using var innerTerminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                _ => { },
                app =>
                {
                    innerApp = app;
                    return _ => new KgpImageWidget(
                        KgpTestHelper.CreatePixelData(2, 2),
                        2,
                        2,
                        new TextBlockWidget("fallback"));
                })
            .WithDimensions(8, 3)
            .WithTerminalWidget(out var handle)
            .Build();

        Hex1bApp? outerApp = null;
        await using var outerTerminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                _ => { },
                app =>
                {
                    outerApp = app;
                    return _ => new TerminalWidget(handle)
                        .Width(SizeHint.Fixed(8))
                        .Height(SizeHint.Fixed(3));
                })
            .WithHeadless(new TerminalCapabilities { SupportsKgp = false })
            .WithDimensions(8, 3)
            .Build();

        var outerRunTask = outerTerminal.RunAsync(TestContext.Current.CancellationToken);
        var innerRunTask = innerTerminal.RunAsync(TestContext.Current.CancellationToken);
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                current => current.ContainsText("fallback"),
                TimeSpan.FromSeconds(5),
                "nested app rendered its non-KGP fallback")
            .Build()
            .ApplyWithCaptureAsync(outerTerminal, TestContext.Current.CancellationToken);

        Assert.IsFalse(handle.Capabilities.SupportsKgp);
        Assert.IsEmpty(snapshot.KgpPlacements);

        innerApp!.RequestStop();
        outerApp!.RequestStop();
        await Task.WhenAll(innerRunTask, outerRunTask);
    }

    private static TerminalNode CreateNode(TerminalWidgetHandle handle, Rect bounds)
    {
        var node = new TerminalNode { Handle = handle };
        node.SetInvalidateCallback(() => { });
        node.Bind();
        node.Arrange(bounds);
        return node;
    }

    private static SurfaceRenderContext CreateContext(
        int width,
        int height,
        out KgpImageRegistry registry)
    {
        registry = new KgpImageRegistry();
        var context = new SurfaceRenderContext(
            new Surface(width, height, new CellMetrics(10, 20)))
        {
            CellMetrics = new CellMetrics(10, 20),
            KgpRegistry = registry,
            CachingEnabled = false,
        };
        context.SetCapabilities(KgpCapabilities);
        return context;
    }

    private sealed class NestedKgpState
    {
        private int _showImage = 1;
        private int _moveDown;

        public bool ShowImage
        {
            get => Volatile.Read(ref _showImage) != 0;
            set => Volatile.Write(ref _showImage, value ? 1 : 0);
        }

        public bool MoveDown
        {
            get => Volatile.Read(ref _moveDown) != 0;
            set => Volatile.Write(ref _moveDown, value ? 1 : 0);
        }
    }

    private sealed class RecordingWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        private readonly Queue<byte[]> _responses = new();
        private readonly object _lock = new();

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                _responses.Enqueue(data.ToArray());
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask ResizeAsync(
            int width,
            int height,
            CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        public string ReadResponse()
        {
            byte[]? response = null;
            var received = SpinWait.SpinUntil(
                () =>
                {
                    lock (_lock)
                    {
                        return _responses.TryDequeue(out response);
                    }
                },
                TimeSpan.FromSeconds(1));

            Assert.IsTrue(received, "Expected a KGP protocol response.");
            return Encoding.UTF8.GetString(response!);
        }
    }
}
