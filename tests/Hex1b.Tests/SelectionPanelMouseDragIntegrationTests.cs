using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// End-to-end tests verifying that a left-mouse drag inside a SelectionPanel
/// activates copy mode through the framework's bubble-drag routing in
/// Hex1bApp. Direct unit tests on the drag binding (in
/// <see cref="SelectionPanelNodeTests"/>) prove the binding does the right
/// thing in isolation; these tests prove it actually fires when the user
/// drags inside the demo-style layout where SelectionPanel is a non-focusable
/// descendant of a focusable ScrollPanel.
/// </summary>
public class SelectionPanelMouseDragIntegrationTests
{
    private static (Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bApp app,
        SelectionPanelNode panel, Task runTask) Setup(int width = 40, int height = 10)
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(width, height).Build();

        // Capture the SelectionPanelNode after first reconcile so tests can
        // inspect copy-mode state directly.
        SelectionPanelNode? capturedPanel = null;

        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.SelectionPanel(
                    ctx.Text(
                        "Line 0 content here\n" +
                        "Line 1 content here\n" +
                        "Line 2 content here\n" +
                        "Line 3 content here\n" +
                        "Line 4 content here"))
                .OnCopy((string _) => { })),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for first render so the node tree exists, then locate the panel.
        // We rely on the alternate-screen flag flipping as a "reconcile complete"
        // proxy, then walk the app's root.
        var waitTask = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5),
                "selection panel ready")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        waitTask.GetAwaiter().GetResult();

        capturedPanel = FindPanel(app);

        return (workload, terminal, app, capturedPanel!, runTask);
    }

    private static SelectionPanelNode? FindPanel(Hex1bApp app)
    {
        // RootNode is internal — accessible because Hex1b grants InternalsVisibleTo
        // to the test assembly.
        var root = app.RootNode;
        return root is null ? null : Walk(root);

        static SelectionPanelNode? Walk(Hex1bNode node)
        {
            if (node is SelectionPanelNode panel) return panel;
            foreach (var child in node.GetChildren())
            {
                var found = Walk(child);
                if (found is not null) return found;
            }
            return null;
        }
    }

    [Fact]
    public async Task LeftDrag_OverPanelContent_EntersCopyMode_AndAnchorsAtDownPosition()
    {
        var (workload, terminal, app, panel, _) = Setup();
        using var _w = workload; using var _t = terminal; using var _a = app;

        Assert.NotNull(panel);
        Assert.False(panel.IsInCopyMode);

        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(fromX: 2, fromY: 1, toX: 7, toY: 3)
            .WaitUntil(_ => panel.IsInCopyMode,
                TimeSpan.FromSeconds(2), "panel enters copy mode after drag")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(panel.IsInCopyMode);
        Assert.True(panel.HasSelection);
        Assert.Equal(SelectionMode.Character, panel.CursorSelectionMode);
        Assert.Equal(1, panel.AnchorRow);
        Assert.Equal(2, panel.AnchorCol);
        Assert.Equal(3, panel.CursorRow);
        Assert.Equal(7, panel.CursorCol);
    }

    [Fact]
    public async Task LeftClick_WithoutMovement_DoesNotEnterCopyMode()
    {
        var (workload, terminal, app, panel, _) = Setup();
        using var _w = workload; using var _t = terminal; using var _a = app;

        Assert.NotNull(panel);

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(5, 2)
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.False(panel.IsInCopyMode);
    }

    [Fact]
    public async Task ShiftDrag_StartsLineSelection()
    {
        var (workload, terminal, app, panel, _) = Setup();
        using var _w = workload; using var _t = terminal; using var _a = app;

        Assert.NotNull(panel);

        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Drag(fromX: 3, fromY: 1, toX: 6, toY: 2)
            .WaitUntil(_ => panel.IsInCopyMode,
                TimeSpan.FromSeconds(2), "panel enters copy mode after shift+drag")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(panel.IsInCopyMode);
        Assert.Equal(SelectionMode.Line, panel.CursorSelectionMode);
    }

    /// <summary>
    /// Ctrl+drag starts a line selection. This is the primary mouse-line
    /// modifier, matching <c>CopyModeBindingsOptions.MouseLineModifier</c>'s
    /// default in <see cref="TerminalWidget"/>. Most terminals (Windows
    /// Terminal, GNOME Terminal, iTerm2) consume Shift+mouse for native
    /// OS-level selection, so Ctrl+drag is the cross-platform reliable
    /// modifier.
    /// </summary>
    [Fact]
    public async Task CtrlDrag_StartsLineSelection()
    {
        var (workload, terminal, app, panel, _) = Setup();
        using var _w = workload; using var _t = terminal; using var _a = app;

        Assert.NotNull(panel);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Drag(fromX: 3, fromY: 1, toX: 6, toY: 2)
            .WaitUntil(_ => panel.IsInCopyMode,
                TimeSpan.FromSeconds(2), "panel enters copy mode after ctrl+drag")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(panel.IsInCopyMode);
        Assert.Equal(SelectionMode.Line, panel.CursorSelectionMode);
    }

    /// <summary>
    /// Reproduces the AgenticPromptDemo layout shape: ScrollPanel (focusable,
    /// has its own Drag binding for the scrollbar) wraps a SelectionPanel.
    /// The drag binding on ScrollPanel must return an empty handler when the
    /// click misses the scrollbar so the framework falls through and arms the
    /// SelectionPanel's bubble drag.
    /// </summary>
    [Fact]
    public async Task LeftDrag_OverPanelInsideScrollPanel_EntersCopyMode()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        using var _w = workload; using var _t = terminal;

        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VScrollPanel(sv =>
                [
                    sv.SelectionPanel(
                        sv.Text(
                            "Alpha bravo charlie\n" +
                            "Delta echo foxtrot\n" +
                            "Golf hotel india\n" +
                            "Juliet kilo lima\n" +
                            "Mike november oscar"))
                    .OnCopy((string _) => { })
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var _a = app;

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5),
                "scroll+selection panel ready")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var panel = FindPanel(app);
        Assert.NotNull(panel);
        Assert.False(panel!.IsInCopyMode);

        // Drag in the middle of the content (away from the scrollbar at the
        // far right edge).
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(fromX: 5, fromY: 1, toX: 12, toY: 3)
            .WaitUntil(_ => panel.IsInCopyMode,
                TimeSpan.FromSeconds(2), "panel enters copy mode after drag")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(panel.IsInCopyMode);
        Assert.True(panel.HasSelection);
    }

    /// <summary>
    /// Reproduces the FULL AgenticPromptDemo layout: VStack > HSplitter
    /// > [ VStack > ScrollPanel > SelectionPanel > TextBlock; Border > Editor ]
    /// + InfoBar. This exercises every interesting interaction: HSplitter's
    /// drag binding for the divider, ScrollPanel's drag binding for the
    /// scrollbar, and the focus ring containing focusables on both sides of
    /// the splitter plus the InfoBar wrapper.
    /// </summary>
    [Fact]
    public async Task LeftDrag_OverPanelInDemoStyleLayout_EntersCopyMode()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 20).Build();
        using var _w = workload; using var _t = terminal;

        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v =>
                [
                    v.HSplitter(
                        v.VStack(left =>
                        [
                            left.VScrollPanel(sv =>
                            [
                                sv.SelectionPanel(
                                    sv.VStack(inner =>
                                    [
                                        inner.Text("Line one of transcript here"),
                                        inner.Text("Line two of transcript here"),
                                        inner.Text("Line three of transcript here"),
                                        inner.Text("Line four of transcript here"),
                                    ]))
                                .OnCopy((string _) => { })
                            ], showScrollbar: true)
                            .Fill(),
                            left.TextBox(),
                        ]),
                        v.Border(
                            v.Text("(empty editor)").Fill()
                        ).Title("Copied"),
                        leftWidth: 50
                    ).Fill(),
                    v.InfoBar(s => [s.Section("hint")]),
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var _a = app;

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5),
                "demo-style layout ready")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var panel = FindPanel(app);
        Assert.NotNull(panel);

        // Drag in the middle of the LEFT side (transcript area).
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(fromX: 10, fromY: 2, toX: 20, toY: 4)
            .WaitUntil(_ => panel!.IsInCopyMode,
                TimeSpan.FromSeconds(2), "panel enters copy mode after drag")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(panel!.IsInCopyMode);
    }

    /// <summary>
    /// End-to-end: drag to select, then right-click commits via the
    /// <see cref="SelectionPanelWidget.CopyModeMouseCommit"/> binding —
    /// which only fires because Hex1bApp's capture-aware mouse routing
    /// honours <see cref="MouseBinding.OverridesCapture"/>. Without that
    /// path the right-click would route through normal hit-testing and
    /// miss the non-focusable SelectionPanel.
    /// </summary>
    [Fact]
    public async Task RightClick_AfterDragSelection_CommitsCopy_AndExitsCopyMode()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        using var _w = workload; using var _t = terminal;

        SelectionPanelCopyEventArgs? captured = null;

        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.SelectionPanel(
                    ctx.Text(
                        "Alpha bravo charlie\n" +
                        "Delta echo foxtrot\n" +
                        "Golf hotel india"))
                .OnCopy((SelectionPanelCopyEventArgs args) => { captured = args; })),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var _a = app;

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5),
                "panel ready")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var panel = FindPanel(app);
        Assert.NotNull(panel);

        // Drag to enter copy mode and create a selection.
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(fromX: 2, fromY: 0, toX: 8, toY: 1)
            .WaitUntil(_ => panel!.IsInCopyMode && panel.HasSelection,
                TimeSpan.FromSeconds(2), "panel has selection")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(panel!.IsInCopyMode);
        Assert.True(panel.HasSelection);

        // Right-click anywhere within the panel commits the copy.
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(5, 1, MouseButton.Right)
            .WaitUntil(_ => captured is not null,
                TimeSpan.FromSeconds(2), "copy handler fired")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.False(string.IsNullOrEmpty(captured!.Text));
        Assert.False(panel.IsInCopyMode);
        Assert.False(panel.HasSelection);
    }

    /// <summary>
    /// End-to-end scroll-wheel-while-dragging: the user drags from a visible
    /// row inside a SelectionPanel that is taller than its enclosing
    /// ScrollPanel viewport. While the left button is still held, scrolling
    /// the wheel must (a) actually scroll the ScrollPanel — even though a
    /// drag is active and would normally swallow all mouse input — and
    /// (b) extend the selection's cursor onto whatever cell scroll has
    /// brought under the (stationary) mouse pointer. Together this lets a
    /// user start a selection on visible content and drag it onto content
    /// that was off-screen at drag-start.
    /// </summary>
    [Fact]
    public async Task WheelDuringDrag_ScrollsContainer_AndExtendsSelectionToNewlyVisibleContent()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        using var _w = workload; using var _t = terminal;

        // 30 lines of content inside a 10-row terminal so the ScrollPanel has
        // plenty of off-screen content to bring into view via wheel scroll.
        var lines = new string[30];
        for (int i = 0; i < lines.Length; i++) lines[i] = $"Row {i:00} payload here";
        var content = string.Join('\n', lines);

        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VScrollPanel(sv =>
                [
                    sv.SelectionPanel(sv.Text(content)).OnCopy((string _) => { })
                ], showScrollbar: false)),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var _a = app;

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5),
                "scroll+selection panel ready")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var panel = FindPanel(app);
        Assert.NotNull(panel);
        var scroll = FindScrollPanel(app);
        Assert.NotNull(scroll);
        Assert.Equal(0, scroll!.Offset);

        // Press at terminal row 1 (panel-local row 1, since not scrolled yet)
        // and drag to row 3 to enter copy mode with an active drag.
        await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(5, 1)
            .MouseDown()
            .Wait(TimeSpan.FromMilliseconds(20))
            .MouseMoveTo(5, 3)
            .WaitUntil(_ => panel!.IsInCopyMode,
                TimeSpan.FromSeconds(2), "panel enters copy mode")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(panel!.IsInCopyMode);
        Assert.Equal(1, panel.AnchorRow);
        Assert.Equal(3, panel.CursorRow);

        // Mouse stays at terminal row 3. Scroll the wheel to bring later
        // content into view — the SelectionPanel slides up, and the cell
        // now under terminal row 3 is panel-local row > 3.
        await new Hex1bTerminalInputSequenceBuilder()
            .ScrollDown(5)
            .WaitUntil(_ => scroll!.Offset > 0 && panel.CursorRow > 3,
                TimeSpan.FromSeconds(2), "scroll occurred and cursor extended")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(scroll!.Offset > 0,
            $"ScrollPanel.Offset expected > 0 after wheel-down, got {scroll.Offset}");
        Assert.True(panel.CursorRow > 3,
            $"panel.CursorRow expected > 3 (anchor row + extension into newly-visible content), got {panel.CursorRow}");
        // Anchor unchanged — selection EXTENDS, not moves.
        Assert.Equal(1, panel.AnchorRow);
        // Cursor must extend at least as far as the original mouseY (3) plus
        // some scroll-driven offset, proving that scroll-during-drag actually
        // brought new content under the mouse and the synthetic drag-move
        // followed it. Exact value depends on input batching / coalescing
        // ordering between scroll ticks and is not part of the contract.
        Assert.True(panel.CursorRow >= 3 + 3,
            $"panel.CursorRow expected >= 6 (mouseY + at least one scroll tick), got {panel.CursorRow}");

        // Releasing the mouse leaves copy mode active for keyboard refinement.
        await new Hex1bTerminalInputSequenceBuilder()
            .MouseUp()
            .Wait(TimeSpan.FromMilliseconds(20))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(panel.IsInCopyMode);
        Assert.True(panel.HasSelection);
    }

    private static ScrollPanelNode? FindScrollPanel(Hex1bApp app)
    {
        var root = app.RootNode;
        return root is null ? null : Walk(root);

        static ScrollPanelNode? Walk(Hex1bNode node)
        {
            if (node is ScrollPanelNode sp) return sp;
            foreach (var child in node.GetChildren())
            {
                var found = Walk(child);
                if (found is not null) return found;
            }
            return null;
        }
    }
}
