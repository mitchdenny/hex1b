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

    #region Selector Dropdown Tests

    [Fact]
    public async Task TabPanel_SelectorDropdown_AppearsNearTabBar()
    {
        // Arrange - Create a demo-like layout with menu bar, splitter, and tab panel
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(main => [
                // Menu bar at top
                main.MenuBar(m => [
                    m.Menu("File", m => [m.MenuItem("New")]),
                    m.Menu("Edit", m => [m.MenuItem("Copy")])
                ]),
                // Main content with splitter
                main.HSplitter(
                    left => [
                        left.Text("Tree View").Fill()
                    ],
                    right => [
                        right.TabPanel(tp => [
                            tp.Tab("Document1.cs", t => [t.Text("Content of Document1")]),
                            tp.Tab("Document2.cs", t => [t.Text("Content of Document2")]),
                            tp.Tab("README.md", t => [t.Text("Content of README")])
                        ])
                        .Selector()
                        .Fill()
                    ],
                    leftWidth: 20
                ).Fill(),
                // Status bar at bottom
                main.InfoBar(s => [s.Section("Ready")])
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Wait for render, then click on the selector dropdown
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // First, capture the initial state to find the selector button position
        var initialSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Document1.cs") && s.ContainsText("▼"), TimeSpan.FromSeconds(1), "tab panel with selector rendered")
            .Capture("initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Find the ▼ selector button position
        var selectorPosition = FindTextPosition(initialSnapshot, "▼");
        Assert.True(selectorPosition.HasValue, "Should find selector button ▼ in the rendered output");
        
        var (selectorX, selectorY) = selectorPosition!.Value;
        
        // Click on the selector button
        var afterClickSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(selectorX, selectorY)
            .Wait(TimeSpan.FromMilliseconds(100))
            .Capture("afterClick")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert - The dropdown list should appear near the tab bar (within a few rows of the selector)
        // It should NOT appear at the very bottom of the screen
        var dropdownPosition = FindTextPosition(afterClickSnapshot, "Document1.cs", startAfterRow: selectorY);
        
        // The dropdown should contain all tab titles in a list format
        Assert.True(afterClickSnapshot.ContainsText("Document1.cs"), "Dropdown should contain Document1.cs");
        Assert.True(afterClickSnapshot.ContainsText("Document2.cs"), "Dropdown should contain Document2.cs");
        Assert.True(afterClickSnapshot.ContainsText("README.md"), "Dropdown should contain README.md");
        
        // The dropdown should appear close to the selector (within 5 rows)
        if (dropdownPosition.HasValue)
        {
            var dropdownY = dropdownPosition.Value.y;
            Assert.True(dropdownY <= selectorY + 5, 
                $"Dropdown should appear near selector (y={selectorY}), but appeared at y={dropdownY}");
        }
    }

    /// <summary>
    /// Helper to find the position of text in a snapshot.
    /// </summary>
    private static (int x, int y)? FindTextPosition(Hex1bTerminalSnapshot snapshot, string text, int startAfterRow = 0)
    {
        for (int y = startAfterRow; y < 50; y++) // Check up to 50 rows
        {
            try
            {
                var line = snapshot.GetLine(y);
                var x = line.IndexOf(text, StringComparison.Ordinal);
                if (x >= 0)
                {
                    return (x, y);
                }
            }
            catch
            {
                // Row doesn't exist
                break;
            }
        }
        return null;
    }

    #endregion

    #region Dynamic Tab Content Tests

    [Fact]
    public async Task TabPanel_DynamicallyAddingTab_UpdatesContent()
    {
        // Arrange - Simulate the demo pattern with external state
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 15)
            .Build();

        // Mutable state that changes during the test
        var openDocs = new List<(string Name, string Content)>();
        var selectedIndex = 0;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                openDocs.Count == 0
                    ? ctx.Text("No documents open")
                    : ctx.TabPanel(tp => [
                        ..openDocs.Select(doc => tp.Tab(doc.Name, t => [
                            t.Text(doc.Content)
                        ]))
                    ])
                    .WithSelectedIndex(selectedIndex)
                    .Fill()
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act & Assert
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Initially no documents
        var snapshot1 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("No documents"), TimeSpan.FromSeconds(1), "empty state")
            .Capture("initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.True(snapshot1.ContainsText("No documents"), "Should show empty state initially");

        // Add a document (simulate what happens when user clicks in tree)
        openDocs.Add(("File1.cs", "Content of File1"));
        selectedIndex = 0;
        app.Invalidate(); // Force re-render

        var snapshot2 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content of File1"), TimeSpan.FromSeconds(1), "first file content")
            .Capture("afterAdd1")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.True(snapshot2.ContainsText("File1.cs"), "Should show first tab");
        Assert.True(snapshot2.ContainsText("Content of File1"), "Should show first file content");

        // Add another document and select it
        openDocs.Add(("File2.cs", "Content of File2"));
        selectedIndex = 1;
        app.Invalidate();

        var snapshot3 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content of File2"), TimeSpan.FromSeconds(1), "second file content")
            .Capture("afterAdd2")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.True(snapshot3.ContainsText("File2.cs"), "Should show second tab");
        Assert.True(snapshot3.ContainsText("Content of File2"), "Should show second file content");

        await runTask;
    }

    #endregion
}
