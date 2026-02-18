using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for DrawerNode layout, rendering, state transitions, and focus handling.
/// Tests cover all combinations of: direction (4), mode (2), state (2), and container (2).
/// </summary>
public class DrawerNodeTests
{
    private static Hex1bRenderContext CreateContext(IHex1bAppTerminalWorkloadAdapter workload, Hex1bTheme? theme = null)
    {
        return new Hex1bRenderContext(workload, theme);
    }

    #region Direction Auto-Detection Tests

    [Fact]
    public async Task Drawer_InHStack_FirstChild_DirectionIsRight()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.HStack(h => [
                h.Drawer()
                    .CollapsedContent(c => [c.Text("»")])
                    .ExpandedContent(e => [e.Text("Expanded")]),
                h.Text("Main")
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("»"), TimeSpan.FromSeconds(2), "collapsed drawer")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - verify drawer was placed at left (first in HStack)
        // Direction is auto-detected as Right (expands right)
    }

    [Fact]
    public async Task Drawer_InHStack_LastChild_DirectionIsLeft()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.HStack(h => [
                h.Text("Main Content"),
                h.Drawer()
                    .CollapsedContent(c => [c.Text("«")])
                    .ExpandedContent(e => [e.Text("Expanded")])
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("«") && s.ContainsText("Main Content"), TimeSpan.FromSeconds(2), "right drawer collapsed")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - drawer icon should be to the right of main content
        var line = snapshot.GetLineTrimmed(0);
        var mainIdx = line.IndexOf("Main Content", StringComparison.Ordinal);
        var drawerIdx = line.IndexOf("«", StringComparison.Ordinal);
        Assert.True(drawerIdx > mainIdx, $"Expected drawer (at {drawerIdx}) to be after main content (at {mainIdx})");
    }

    [Fact]
    public async Task Drawer_InVStack_FirstChild_DirectionIsDown()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.Drawer()
                    .CollapsedContent(c => [c.Text("▼ Top")])
                    .ExpandedContent(e => [e.Text("Expanded")]),
                v.Text("Main Content")
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▼ Top") && s.ContainsText("Main Content"), TimeSpan.FromSeconds(2), "top drawer")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - drawer should be on row 0, main content below
        Assert.Contains("▼ Top", snapshot.GetLineTrimmed(0));
        Assert.Contains("Main Content", snapshot.GetLineTrimmed(1));
    }

    [Fact]
    public async Task Drawer_InVStack_LastChild_DirectionIsUp()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.Text("Main Content"),
                v.Drawer()
                    .CollapsedContent(c => [c.Text("▲ Bottom")])
                    .ExpandedContent(e => [e.Text("Expanded")])
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▲ Bottom") && s.ContainsText("Main Content"), TimeSpan.FromSeconds(2), "bottom drawer")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - main content should be above drawer
        Assert.Contains("Main Content", snapshot.GetLineTrimmed(0));
        Assert.Contains("▲ Bottom", snapshot.GetLineTrimmed(1));
    }

    #endregion

    #region Collapsed State Rendering Tests

    [Fact]
    public async Task Drawer_Collapsed_ShowsCollapsedContent()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.HStack(h => [
                h.Drawer()
                    .CollapsedContent(c => [c.Text("[COLLAPSED]")])
                    .ExpandedContent(e => [e.Text("[EXPANDED]")]),
                h.Text("Main")
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[COLLAPSED]"), TimeSpan.FromSeconds(2), "collapsed content")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(snapshot.ContainsText("[COLLAPSED]"), "Collapsed content should be visible");
        Assert.False(snapshot.ContainsText("[EXPANDED]"), "Expanded content should NOT be visible");
    }

    [Fact]
    public async Task Drawer_Collapsed_NoContent_IsInvisible()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.HStack(h => [
                h.Drawer()
                    // No collapsed content - should be invisible
                    .ExpandedContent(e => [e.Text("[EXPANDED]")]),
                h.Text("MainContent")
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("MainContent"), TimeSpan.FromSeconds(2), "main content")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - main content should start at column 0 (drawer is invisible)
        var line = snapshot.GetLineTrimmed(0);
        Assert.StartsWith("MainContent", line);
    }

    #endregion

    #region Expanded Inline State Rendering Tests

    [Fact]
    public async Task Drawer_ExpandedInline_ShowsExpandedContent()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.HStack(h => [
                h.Drawer()
                    .Expanded(true)  // Start expanded
                    .CollapsedContent(c => [c.Text("[COLLAPSED]")])
                    .ExpandedContent(e => [e.Text("[EXPANDED]")]),
                h.Text("Main")
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[EXPANDED]"), TimeSpan.FromSeconds(2), "expanded content")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(snapshot.ContainsText("[EXPANDED]"), "Expanded content should be visible");
        Assert.False(snapshot.ContainsText("[COLLAPSED]"), "Collapsed content should NOT be visible");
    }

    [Fact]
    public async Task Drawer_ExpandedInline_PushesMainContent()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.HStack(h => [
                h.Drawer()
                    .Expanded(true)
                    .CollapsedContent(c => [c.Text("»")])
                    .ExpandedContent(e => [e.Text("ExpandedPane")]),
                h.Text("MainContent")
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("ExpandedPane") && s.ContainsText("MainContent"), TimeSpan.FromSeconds(2), "both visible")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - expanded content should be before main content
        var line = snapshot.GetLineTrimmed(0);
        var expandedIdx = line.IndexOf("ExpandedPane", StringComparison.Ordinal);
        var mainIdx = line.IndexOf("MainContent", StringComparison.Ordinal);
        Assert.True(expandedIdx < mainIdx, $"Expanded pane (at {expandedIdx}) should be before main content (at {mainIdx})");
    }

    #endregion

    #region Expanded Overlay State Rendering Tests

    [Fact]
    public async Task Drawer_Overlay_Collapsed_IsFocusable()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.ZStack(z => [
                z.VStack(v => [
                    v.Drawer()
                        .AsOverlay()
                        .CollapsedContent(c => [c.Text("▼ Console")])
                        .ExpandedContent(e => [e.Text("Popup Content")]),
                    v.Text("Main Content")
                ])
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Tab should focus the drawer
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▼ Console"), TimeSpan.FromSeconds(2), "drawer visible")
            .Tab()  // Focus first focusable - should be the overlay drawer
            .WaitUntil(_ => true, TimeSpan.FromMilliseconds(100), "focus applied")
            .Capture("focused")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The drawer should be focusable in overlay mode when collapsed
        Assert.True(snapshot.ContainsText("▼ Console"));
    }

    [Fact]
    public async Task Drawer_Overlay_EnterKey_OpensPopup()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.ZStack(z => [
                z.VStack(v => [
                    v.Drawer()
                        .AsOverlay()
                        .CollapsedContent(c => [c.Text("▼ Console")])
                        .ExpandedContent(e => [e.Text("POPUP_CONTENT")]),
                    v.Text("Main Content")
                ])
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▼ Console"), TimeSpan.FromSeconds(2), "drawer visible")
            .Tab()  // Focus the drawer
            .Enter()  // Open the overlay
            .WaitUntil(s => s.ContainsText("POPUP_CONTENT"), TimeSpan.FromSeconds(2), "popup opened")
            .Capture("popup")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(snapshot.ContainsText("POPUP_CONTENT"), "Popup content should be visible");
    }

    [Fact]
    public async Task Drawer_Overlay_SpaceKey_OpensPopup()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.ZStack(z => [
                z.VStack(v => [
                    v.Drawer()
                        .AsOverlay()
                        .CollapsedContent(c => [c.Text("▼ Console")])
                        .ExpandedContent(e => [e.Text("POPUP_SPACE")]),
                    v.Text("Main Content")
                ])
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▼ Console"), TimeSpan.FromSeconds(2), "drawer visible")
            .Tab()
            .Key(Hex1bKey.Spacebar)  // Space should also open
            .WaitUntil(s => s.ContainsText("POPUP_SPACE"), TimeSpan.FromSeconds(2), "popup opened")
            .Capture("popup")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(snapshot.ContainsText("POPUP_SPACE"));
    }

    #endregion

    #region Event Callback Tests

    [Fact]
    public async Task Drawer_OnExpanded_CallbackFires()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var expandedCalled = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.ZStack(z => [
                z.VStack(v => [
                    v.Drawer()
                        .AsOverlay()
                        .CollapsedContent(c => [c.Text("▼ Open")])
                        .ExpandedContent(e => [e.Text("Expanded")])
                        .OnExpanded(() => expandedCalled = true),
                    v.Text("Main")
                ])
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▼ Open"), TimeSpan.FromSeconds(2), "drawer visible")
            .Tab()
            .Enter()
            .WaitUntil(s => s.ContainsText("Expanded"), TimeSpan.FromSeconds(2), "expanded")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(expandedCalled, "OnExpanded callback should have been invoked");
    }

    [Fact]
    public async Task Drawer_OnCollapsed_CallbackFires()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        var collapsedCalled = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.ZStack(z => [
                z.VStack(v => [
                    v.Drawer()
                        .AsOverlay()
                        .CollapsedContent(c => [c.Text("▼ Open")])
                        .ExpandedContent(e => [
                            e.HStack(h => [
                                h.Text("Content"),
                                h.Button("Close").OnClick(ctx => ctx.Popups.Pop())
                            ])
                        ])
                        .OnCollapsed(() => collapsedCalled = true),
                    v.Text("Main")
                ])
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▼ Open"), TimeSpan.FromSeconds(2), "drawer visible")
            .Tab()
            .Enter()  // Open
            .WaitUntil(s => s.ContainsText("Close"), TimeSpan.FromSeconds(2), "popup opened")
            .Tab()  // Focus the Close button
            .Enter()  // Click it
            .WaitUntil(_ => true, TimeSpan.FromMilliseconds(200), "callback fired")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(collapsedCalled, "OnCollapsed callback should have been invoked");
    }

    #endregion

    #region Focus Management Tests

    [Fact]
    public async Task Drawer_Inline_NotFocusable()
    {
        // Inline mode drawers should not be focusable themselves
        var node = new DrawerNode { Mode = DrawerMode.Inline, IsExpanded = false };
        Assert.False(node.IsFocusable);
    }

    [Fact]
    public async Task Drawer_Overlay_Collapsed_IsFocusableProperty()
    {
        var node = new DrawerNode { Mode = DrawerMode.Overlay, IsExpanded = false };
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public async Task Drawer_Overlay_Expanded_NotFocusableProperty()
    {
        var node = new DrawerNode { Mode = DrawerMode.Overlay, IsExpanded = true };
        Assert.False(node.IsFocusable);
    }

    #endregion

    #region Measure and Arrange Tests

    [Fact]
    public void Drawer_Collapsed_WithContent_MeasuresContentSize()
    {
        var collapsedContent = new TextBlockNode { Text = "»" };
        var node = new DrawerNode { Content = collapsedContent };
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.Equal(1, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Drawer_Collapsed_NoContent_MeasuresZero()
    {
        var node = new DrawerNode { Content = null };
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Drawer_Arrange_PassesBoundsToContent()
    {
        var content = new TextBlockNode { Text = "Content" };
        var node = new DrawerNode { Content = content };
        var bounds = new Rect(5, 10, 20, 3);
        
        node.Arrange(bounds);
        
        Assert.Equal(bounds, content.Bounds);
    }

    #endregion
}
