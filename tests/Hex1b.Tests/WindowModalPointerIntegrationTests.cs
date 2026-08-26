using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class WindowModalPointerIntegrationTests
{
    [TestMethod]
    public async Task ModalWindow_ExposedBackgroundWindow_BlocksMoveResizeAndActivation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(80, 24)
            .Build();

        WindowEntry? background = null;
        WindowEntry? modal = null;
        var backgroundActivationCount = 0;
        var modalClickCount = 0;

        using var app = new Hex1bApp(
            ctx => ctx.VStack(outer =>
            [
                outer.Button("Open windows").OnClick(e =>
                {
                    var backgroundHandle = e.Windows.Window(w => w.Text("Background content"))
                        .Title("Background")
                        .Size(40, 12)
                        .Position(2, 3)
                        .Resizable()
                        .OnActivated(() => backgroundActivationCount++);
                    background = e.Windows.Open(backgroundHandle);

                    var modalHandle = e.Windows.Window(w =>
                            w.Button("Modal action").OnClick(_ => modalClickCount++))
                        .Title("Modal")
                        .Size(20, 7)
                        .Position(20, 8)
                        .Modal()
                        .Resizable();
                    modal = e.Windows.Open(modalHandle);
                }),
                outer.WindowPanel().Height(SizeHint.Fill)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open windows"), TimeSpan.FromSeconds(5),
                "window launcher rendered")
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => background?.Node != null
                    && modal?.Node != null
                    && s.ContainsText("Background")
                    && s.ContainsText("Modal action"),
                TimeSpan.FromSeconds(5), "background and modal windows rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsNotNull(background);
        Assert.IsNotNull(modal);
        Assert.IsNotNull(background.Node);
        Assert.IsNotNull(modal.Node);

        var initialBackgroundX = background.X;
        var initialBackgroundY = background.Y;
        var initialBackgroundWidth = background.Width;
        var initialBackgroundHeight = background.Height;
        var initialBackgroundZIndex = background.ZIndex;
        var backgroundBounds = background.Node.Bounds;
        var modalButton = TestSeq.Single(modal.Node.Content!.GetFocusableNodes().OfType<ButtonNode>());
        var modalButtonHitBounds = modalButton.HitTestBounds;

        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(
                backgroundBounds.Right - 1,
                backgroundBounds.Bottom - 1,
                backgroundBounds.Right + 2,
                backgroundBounds.Bottom + 1)
            .Drag(
                backgroundBounds.X + 5,
                backgroundBounds.Y + 1,
                backgroundBounds.X + 9,
                backgroundBounds.Y + 3)
            .ClickAt(backgroundBounds.X + 5, backgroundBounds.Y + 1)
            .ClickAt(
                modalButtonHitBounds.X + modalButtonHitBounds.Width / 2,
                modalButtonHitBounds.Y + modalButtonHitBounds.Height / 2)
            .WaitUntil(_ => modalClickCount == 1, TimeSpan.FromSeconds(2),
                "modal button remains interactive")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.AreEqual(initialBackgroundX, background.X, "Modal should block moving the background window.");
        Assert.AreEqual(initialBackgroundY, background.Y, "Modal should block moving the background window.");
        Assert.AreEqual(initialBackgroundWidth, background.Width, "Modal should block resizing the background window.");
        Assert.AreEqual(initialBackgroundHeight, background.Height, "Modal should block resizing the background window.");
        Assert.AreEqual(initialBackgroundZIndex, background.ZIndex, "Modal should block activating the background window.");
        Assert.AreEqual(0, backgroundActivationCount);
        Assert.AreSame(modal, background.Manager.ActiveWindow);
        Assert.AreEqual(1, modalClickCount);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }
}
