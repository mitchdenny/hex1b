using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TabPanelNode layout, rendering, tab switching, and keyboard navigation.
/// </summary>
public class TabPanelNodeTests
{
    #region Basic Rendering Tests

    [Fact]
    public async Task TabPanel_Renders_TabBar_And_Content()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.TabPanel(tp => [
                    tp.Tab("Tab One", t => [t.Text("Content One")]),
                    tp.Tab("Tab Two", t => [t.Text("Content Two")])
                ]).Fill()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Tab One") && s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "tab panel rendered")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Tab bar shows both tabs, content shows first tab
        Assert.True(snapshot.ContainsText("Tab One"), "Should show first tab title");
        Assert.True(snapshot.ContainsText("Tab Two"), "Should show second tab title");
        Assert.True(snapshot.ContainsText("Content One"), "Should show first tab content");
        Assert.False(snapshot.ContainsText("Content Two"), "Should not show second tab content");
    }

    [Fact]
    public async Task TabPanel_AltRight_SwitchesToNextTab()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.TabPanel(tp => [
                    tp.Tab("Tab One", t => [t.Text("Content One")]),
                    tp.Tab("Tab Two", t => [t.Text("Content Two")])
                ]).Fill()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "first tab")
            .Alt().Key(Hex1bKey.RightArrow)
            .WaitUntil(s => s.ContainsText("Content Two"), TimeSpan.FromSeconds(1), "switched to second tab")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Should now show second tab content
        Assert.True(snapshot.ContainsText("Content Two"), "Should show second tab content after Alt+Right");
        Assert.False(snapshot.ContainsText("Content One"), "Should not show first tab content after switch");
    }

    [Fact]
    public async Task TabPanel_AltLeft_SwitchesToPreviousTab()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        // Use internal state management (no WithSelectedIndex)
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.TabPanel(tp => [
                    tp.Tab("Tab One", t => [t.Text("Content One")]),
                    tp.Tab("Tab Two", t => [t.Text("Content Two")])
                ]).Fill()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - First go to second tab, then go back
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "first tab")
            .Alt().Key(Hex1bKey.RightArrow)
            .WaitUntil(s => s.ContainsText("Content Two"), TimeSpan.FromSeconds(1), "switched to second tab")
            .Alt().Key(Hex1bKey.LeftArrow)
            .WaitUntil(s => s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "switched back to first tab")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Should now show first tab content
        Assert.True(snapshot.ContainsText("Content One"), "Should show first tab content after Alt+Left");
    }

    #endregion

    #region Orientation Tests

    [Fact]
    public async Task TabPanel_InVStack_FirstPosition_TabsOnTop()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.TabPanel(tp => [
                    tp.Tab("TopTab", t => [t.Text("Content")])
                ]).Fill(),
                v.Text("Footer")
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("TopTab"), TimeSpan.FromSeconds(1), "tab panel rendered")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Tab should be on second line (after top separator in Full mode)
        var line1 = snapshot.GetLineTrimmed(1);
        Assert.Contains("TopTab", line1);
    }

    [Fact]
    public async Task TabPanel_WithTabsOnBottom_TabsRenderAtBottom()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.Text("Header"),
                v.TabPanel(tp => [
                    tp.Tab("BottomTab", t => [t.Text("Content")])
                ]).TabsOnBottom().Fill()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("BottomTab"), TimeSpan.FromSeconds(1), "tab panel rendered")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Tab should be in bottom half of screen
        var found = false;
        for (int i = 5; i < 10; i++)
        {
            var line = snapshot.GetLineTrimmed(i);
            if (line.Contains("BottomTab"))
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Tab should be in bottom half of screen");
    }

    #endregion

    #region Selection Changed Event Tests

    [Fact]
    public async Task TabPanel_OnSelectionChanged_FiresWhenTabSwitches()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        var selectionChangedFired = false;
        var newIndex = -1;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.TabPanel(tp => [
                    tp.Tab("Tab One", t => [t.Text("Content One")]),
                    tp.Tab("Tab Two", t => [t.Text("Content Two")])
                ]).OnSelectionChanged(e =>
                {
                    selectionChangedFired = true;
                    newIndex = e.SelectedIndex;
                }).Fill()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "first tab")
            .Alt().Key(Hex1bKey.RightArrow)
            .WaitUntil(s => s.ContainsText("Content Two"), TimeSpan.FromSeconds(1), "switched")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        Assert.True(selectionChangedFired, "Selection changed event should fire");
        Assert.Equal(1, newIndex);
    }

    #endregion

    #region Tab Wrapping Tests

    [Fact]
    public async Task TabPanel_AltRight_OnLastTab_WrapsToFirst()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        // Use internal state management
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.TabPanel(tp => [
                    tp.Tab("Tab One", t => [t.Text("Content One")]),
                    tp.Tab("Tab Two", t => [t.Text("Content Two")])
                ]).Fill()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Go to last tab, then press right again to wrap
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "first tab")
            .Alt().Key(Hex1bKey.RightArrow)
            .WaitUntil(s => s.ContainsText("Content Two"), TimeSpan.FromSeconds(1), "second tab")
            .Alt().Key(Hex1bKey.RightArrow)
            .WaitUntil(s => s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "wrapped to first")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - Should wrap to first tab
        Assert.True(snapshot.ContainsText("Content One"), "Should wrap to first tab");
    }

    #endregion
}
