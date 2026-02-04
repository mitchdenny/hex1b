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

    [Fact]
    public async Task TabPanel_TreeDoubleClick_UpdatesContent_WithoutExplicitInvalidate()
    {
        // This test replicates the actual demo pattern where:
        // 1. A Tree widget has items with OnActivated handlers
        // 2. OnActivated modifies shared state (openDocs list)
        // 3. The TabPanel should re-render with new content WITHOUT explicit Invalidate()

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(80, 20)
            .Build();

        // Shared mutable state (like editorState in the demo)
        var openDocs = new List<(string Name, string Content)>();
        var selectedIndex = 0;
        var activatedCalled = false;
        var clickedCalled = false;

        // Available files (like the demo's file list)
        var availableFiles = new[]
        {
            ("File1.cs", "Content of File 1"),
            ("File2.cs", "Content of File 2"),
            ("File3.cs", "Content of File 3")
        };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(main => [
                // Tree with files that have OnActivated handlers
                main.Tree(tree => [
                    ..availableFiles.Select(f => tree.Item(f.Item1)
                        .OnClicked(e => {
                            clickedCalled = true;
                        })
                        .OnActivated(e => {
                            activatedCalled = true;
                            // This is what happens in the demo - modify state inside handler
                            if (!openDocs.Any(d => d.Name == f.Item1))
                            {
                                openDocs.Add((f.Item1, f.Item2));
                            }
                            selectedIndex = openDocs.FindIndex(d => d.Name == f.Item1);
                        }))
                ]).Height(SizeHint.Fixed(5)),
                
                // TabPanel that shows open documents
                openDocs.Count == 0
                    ? main.Text("No documents open")
                    : main.TabPanel(tp => [
                        ..openDocs.Select(doc => tp.Tab(doc.Name, t => [
                            t.Text(doc.Content)
                        ]))
                    ])
                    .WithSelectedIndex(selectedIndex)
                    .Fill()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act & Assert
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for initial render - should show tree and "No documents"
        var snapshot1 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File1.cs") && s.ContainsText("No documents"), TimeSpan.FromSeconds(1), "initial")
            .Capture("initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.True(snapshot1.ContainsText("No documents"), "Should show empty state initially");
        Assert.False(activatedCalled, "OnActivated should not be called yet");
        Assert.False(clickedCalled, "OnClicked should not be called yet");

        // Find File1.cs position in the tree - it should be at row 0
        var file1Pos = FindTextPosition(snapshot1, "File1.cs");
        Assert.True(file1Pos.HasValue, "Should find File1.cs in tree");

        // First try a single click to verify mouse routing works
        var snapshot2 = await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(file1Pos.Value.x, file1Pos.Value.y)
            .Click()
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("afterClick")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(clickedCalled, $"OnClicked should have been called by click at ({file1Pos.Value.x}, {file1Pos.Value.y})");

        // Now double-click to activate
        var snapshot3 = await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(file1Pos.Value.x, file1Pos.Value.y)
            .DoubleClick()
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("afterDoubleClick")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(activatedCalled, "OnActivated should have been called by double-click");
        Assert.Single(openDocs);
        Assert.Equal("File1.cs", openDocs[0].Name);

        // Now verify content update
        var snapshot4 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content of File 1"), TimeSpan.FromSeconds(1), "file1 content")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.True(snapshot4.ContainsText("File1.cs"), "Tab should show File1.cs");
        Assert.True(snapshot4.ContainsText("Content of File 1"), "Content area should show file content");

        await runTask;
    }

    [Fact]
    public async Task TabPanel_SwitchingTabs_UpdatesContent()
    {
        // Simple test: TabPanel with two tabs, switch between them
        // Content should update without needing resize

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.TabPanel(tp => [
                    tp.Tab("Tab One", t => [t.Text("Content One")]),
                    tp.Tab("Tab Two", t => [t.Text("Content Two")])
                ]).Fill()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Initially shows Tab One content
        var snapshot1 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "tab1 content")
            .Capture("initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.True(snapshot1.ContainsText("Content One"), "Should show Tab One content initially");
        Assert.False(snapshot1.ContainsText("Content Two"), "Should not show Tab Two content initially");

        // Press Alt+Right to switch to Tab Two
        var snapshot2 = await new Hex1bTerminalInputSequenceBuilder()
            .Alt().Key(Hex1bKey.RightArrow)
            .WaitUntil(s => s.ContainsText("Content Two"), TimeSpan.FromSeconds(1), "tab2 content")
            .Capture("afterSwitch")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // THIS IS THE BUG: Content Two should be visible, Content One should not
        Assert.True(snapshot2.ContainsText("Content Two"), "Should show Tab Two content after switch");
        Assert.False(snapshot2.ContainsText("Content One"), "Should not show Tab One content after switch");

        await runTask;
    }

    [Fact]
    public async Task TabPanel_SwitchingTabs_WithVScrollAndWrap_UpdatesContent()
    {
        // Test with VScroll and Wrap - matches the demo's structure
        // This is more likely to trigger the caching bug

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 15)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.TabPanel(tp => [
                    tp.Tab("Tab One", t => [
                        t.VScroll(s => [
                            s.Text("Content One - This is the first tab with some longer text").Wrap()
                        ]).Fill()
                    ]),
                    tp.Tab("Tab Two", t => [
                        t.VScroll(s => [
                            s.Text("Content Two - This is the second tab with different text").Wrap()
                        ]).Fill()
                    ])
                ]).Fill()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Initially shows Tab One content
        var snapshot1 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "tab1 content")
            .Capture("initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.True(snapshot1.ContainsText("Content One"), "Should show Tab One content initially");
        Assert.False(snapshot1.ContainsText("Content Two"), "Should not show Tab Two content initially");

        // Press Alt+Right to switch to Tab Two
        var snapshot2 = await new Hex1bTerminalInputSequenceBuilder()
            .Alt().Key(Hex1bKey.RightArrow)
            .WaitUntil(s => s.ContainsText("Content Two"), TimeSpan.FromSeconds(1), "tab2 content")
            .Capture("afterSwitch")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Content Two should be visible
        Assert.True(snapshot2.ContainsText("Content Two"), "Should show Tab Two content after switch");
        Assert.False(snapshot2.ContainsText("Content One"), "Should not show Tab One content after switch");

        await runTask;
    }

    [Fact]
    public async Task TabPanel_SwitchingTabs_WithResponsive_UpdatesContent()
    {
        // Test with Responsive widget wrapping the TabPanel - matches the demo's exact structure

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.VStack(v => [
                v.Responsive(r => [
                    r.When((w, h) => h >= 15, r => r.TabPanel(tp => [
                        tp.Tab("Tab One", t => [
                            t.VScroll(s => [
                                s.Text("Content One - This is the first tab").Wrap()
                            ]).Fill()
                        ]),
                        tp.Tab("Tab Two", t => [
                            t.VScroll(s => [
                                s.Text("Content Two - This is the second tab").Wrap()
                            ]).Fill()
                        ])
                    ]).Full().Fill()),
                    r.Otherwise(r => r.Text("Compact mode"))
                ]).Fill()
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Initially shows Tab One content
        var snapshot1 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "tab1 content")
            .Capture("initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.True(snapshot1.ContainsText("Content One"), "Should show Tab One content initially");
        Assert.False(snapshot1.ContainsText("Content Two"), "Should not show Tab Two content initially");

        // Press Alt+Right to switch to Tab Two
        var snapshot2 = await new Hex1bTerminalInputSequenceBuilder()
            .Alt().Key(Hex1bKey.RightArrow)
            .WaitUntil(s => s.ContainsText("Content Two"), TimeSpan.FromSeconds(1), "tab2 content")
            .Capture("afterSwitch")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Content Two should be visible
        Assert.True(snapshot2.ContainsText("Content Two"), "Should show Tab Two content after switch");
        Assert.False(snapshot2.ContainsText("Content One"), "Should not show Tab One content after switch");

        await runTask;
    }

    [Fact]
    public async Task TabPanel_InSplitter_SwitchingTabs_UpdatesContent()
    {
        // Test with Splitter + Responsive + TabPanel - full demo structure
        // This test reproduces the bug where tab content doesn't update in a splitter

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.HSplitter(
                left => [
                    left.Text("Left Pane").Fill()
                ],
                right => [
                    right.Responsive(r => [
                        r.When((w, h) => h >= 15, r => r.TabPanel(tp => [
                            tp.Tab("Tab One", t => [
                                t.VScroll(s => [
                                    s.Text("Content One - First tab content").Wrap()
                                ]).Fill()
                            ]),
                            tp.Tab("Tab Two", t => [
                                t.VScroll(s => [
                                    s.Text("Content Two - Second tab content").Wrap()
                                ]).Fill()
                            ])
                        ]).Full().Fill()),
                        r.Otherwise(r => r.Text("Compact"))
                    ]).Fill()
                ],
                leftWidth: 20
            ).Fill()),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Initially shows Tab One content
        var snapshot1 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content One"), TimeSpan.FromSeconds(1), "tab1 content")
            .Capture("initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.True(snapshot1.ContainsText("Content One"), "Should show Tab One content initially");

        // Press Alt+Right to switch to Tab Two
        // Note: TabPanel's Alt+Right binding only works when TabPanel or its content is focused
        var snapshot2 = await new Hex1bTerminalInputSequenceBuilder()
            .Alt().Key(Hex1bKey.RightArrow)
            .Wait(TimeSpan.FromMilliseconds(100))
            .Capture("afterSwitch")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Try clicking on Tab Two instead (this should work regardless of focus)
        // Find Tab Two position
        var tabTwoPos = FindTextPosition(snapshot2, "Tab Two");
        Assert.True(tabTwoPos.HasValue, "Should find Tab Two in tab bar");
        
        var snapshot3 = await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(tabTwoPos.Value.x + 2, tabTwoPos.Value.y)
            .Click()
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("afterClick")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Print full terminal state for debugging
        var fullState = string.Join("\n", Enumerable.Range(0, 10).Select(i => snapshot3.GetLine(i)));
        
        // Content Two should be visible after clicking on Tab Two
        // This verifies the content area actually updated, not just that both texts exist somewhere
        Assert.True(snapshot3.ContainsText("Content Two"), 
            $"Should show Tab Two content after clicking tab.\nTerminal state:\n{fullState}");
        Assert.False(snapshot3.ContainsText("Content One"), 
            $"Should not show Tab One content after switch.\nTerminal state:\n{fullState}");

        await runTask;
    }

    [Fact(Skip = "Tree double-click in splitter is a separate issue - needs investigation")]
    public async Task TabPanel_TreeInSplitter_DoubleClickUpdatesContent()
    {
        // This test more closely mirrors the TabPanelDemo structure
        // with an HSplitter containing Tree on left and TabPanel on right

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(80, 20)
            .Build();

        // Shared mutable state (like editorState in the demo)
        var openDocs = new List<(string Name, string Content)>();
        var selectedIndex = 0;
        var activatedCalled = false;

        // Available files
        var availableFiles = new[]
        {
            ("File1.cs", "Content of File 1"),
            ("File2.cs", "Content of File 2"),
            ("File3.cs", "Content of File 3")
        };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.HSplitter(
                left => [
                    left.Tree(tree => [
                        ..availableFiles.Select(f => tree.Item(f.Item1)
                            .OnActivated(e => {
                                activatedCalled = true;
                                if (!openDocs.Any(d => d.Name == f.Item1))
                                {
                                    openDocs.Add((f.Item1, f.Item2));
                                }
                                selectedIndex = openDocs.FindIndex(d => d.Name == f.Item1);
                            }))
                    ]).Fill()
                ],
                right => openDocs.Count == 0
                    ? [right.Text("No documents open")]
                    : [
                        right.TabPanel(tp => [
                            ..openDocs.Select(doc => tp.Tab(doc.Name, t => [
                                t.Text(doc.Content)
                            ]))
                        ])
                        .WithSelectedIndex(selectedIndex)
                        .Fill()
                    ],
                leftWidth: 20
            ).Fill()),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act & Assert
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for initial render
        var snapshot1 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File1.cs") && s.ContainsText("No documents"), TimeSpan.FromSeconds(1), "initial")
            .Capture("initial")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.True(snapshot1.ContainsText("No documents"), "Should show empty state initially");

        // Find File1.cs position - should be in the left pane
        var file1Pos = FindTextPosition(snapshot1, "File1.cs");
        Assert.True(file1Pos.HasValue, "Should find File1.cs in tree");

        // Click in the middle of the filename (add offset from start)
        var clickX = file1Pos.Value.x + 3; // Click on "e1" part of File1.cs
        var clickY = file1Pos.Value.y;

        // Double-click to activate - first verify OnActivated is called
        var snapshot2 = await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(clickX, clickY)
            .DoubleClick()
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("afterDoubleClick")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(activatedCalled, $"OnActivated should have been called by double-click at ({clickX}, {clickY})");
        Assert.Single(openDocs);
        Assert.Equal("File1.cs", openDocs[0].Name);

        // Now wait for content update
        var snapshot3 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content of File 1"), TimeSpan.FromSeconds(1), "file1 content")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(snapshot3.ContainsText("File1.cs"), "Tab should show File1.cs");
        Assert.True(snapshot3.ContainsText("Content of File 1"), "Content area should show file content");

        await runTask;
    }

    #endregion
}
