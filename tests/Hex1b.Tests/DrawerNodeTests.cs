using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for DrawerNode rendering and input handling.
/// </summary>
public class DrawerNodeTests
{
    #region Measurement Tests - Collapsed State

    [Fact]
    public void Measure_Collapsed_LeftPosition_ReturnsToggleWidth()
    {
        var header = new TextBlockNode { Text = "Files" };
        var content = new TextBlockNode { Text = "Content" };
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = false,
            Position = DrawerPosition.Left
        };

        var size = node.Measure(new Constraints(0, 100, 0, 20));

        // Indicator (2) + header length
        Assert.Equal(2 + 5, size.Width); // "▶ " + "Files"
        Assert.Equal(20, size.Height); // Fills available height for left/right
    }

    [Fact]
    public void Measure_Collapsed_TopPosition_ReturnsToggleHeight()
    {
        var header = new TextBlockNode { Text = "Settings" };
        var content = new TextBlockNode { Text = "Content" };
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = false,
            Position = DrawerPosition.Top
        };

        var size = node.Measure(new Constraints(0, 100, 0, 20));

        Assert.Equal(100, size.Width); // Fills available width for top/bottom
        Assert.Equal(1, size.Height); // Just the toggle row
    }

    #endregion

    #region Measurement Tests - Expanded State

    [Fact]
    public void Measure_Expanded_LeftPosition_IncludesContent()
    {
        var header = new TextBlockNode { Text = "Files" };
        var content = new TextBlockNode { Text = "Content here" };
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = true,
            Position = DrawerPosition.Left,
            ExpandedSize = 25
        };

        var size = node.Measure(new Constraints(0, 100, 0, 20));

        // Toggle width (7) + expanded size (25)
        Assert.Equal(7 + 25, size.Width);
        Assert.Equal(20, size.Height);
    }

    [Fact]
    public void Measure_Expanded_TopPosition_IncludesContent()
    {
        var header = new TextBlockNode { Text = "Panel" };
        var content = new VStackNode();
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = true,
            Position = DrawerPosition.Top,
            ExpandedSize = 10
        };

        var size = node.Measure(new Constraints(0, 100, 0, 30));

        Assert.Equal(100, size.Width);
        Assert.Equal(1 + 10, size.Height); // Toggle row + expanded size
    }

    [Fact]
    public void Measure_Expanded_WithDefaultSize_UsesContentSize()
    {
        var header = new TextBlockNode { Text = "Menu" };
        var content = new TextBlockNode { Text = "Item 1" };
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = true,
            Position = DrawerPosition.Left,
            ExpandedSize = null // Use content size
        };

        var size = node.Measure(new Constraints(0, 100, 0, 20));

        // Toggle width + content width
        Assert.True(size.Width > 7); // More than just toggle
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_Collapsed_SetsCorrectBounds()
    {
        var header = new TextBlockNode { Text = "Header" };
        var content = new TextBlockNode { Text = "Content" };
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = false,
            Position = DrawerPosition.Left
        };
        
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 20, 24));

        Assert.Equal(new Rect(0, 0, 20, 24), node.Bounds);
    }

    [Fact]
    public void Arrange_Expanded_LeftPosition_ArrangesContentCorrectly()
    {
        var header = new TextBlockNode { Text = "Nav" };
        var content = new VStackNode();
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = true,
            Position = DrawerPosition.Left,
            ExpandedSize = 20
        };
        
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 25, 24));

        // Content should be positioned after toggle
        Assert.True(content.Bounds.X > 0);
    }

    [Fact]
    public void Arrange_Expanded_RightPosition_ContentOnLeft()
    {
        var header = new TextBlockNode { Text = "Props" };
        var content = new VStackNode();
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = true,
            Position = DrawerPosition.Right,
            ExpandedSize = 20
        };
        
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 30, 24));

        // Content should be on left side
        Assert.Equal(0, content.Bounds.X);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new DrawerNode();

        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void IsFocused_WhenSet_MarksDirty()
    {
        var node = new DrawerNode();
        node.ClearDirty();

        node.IsFocused = true;

        Assert.True(node.IsDirty);
    }

    [Fact]
    public void GetFocusableNodes_Collapsed_ReturnsOnlyToggle()
    {
        var header = new TextBlockNode { Text = "Header" };
        var contentWithButton = new ButtonNode { Label = "Click" };
        var content = new VStackNode { Children = [contentWithButton] };
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = false
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Same(node, focusables[0]);
    }

    [Fact]
    public void GetFocusableNodes_Expanded_IncludesContentFocusables()
    {
        var header = new TextBlockNode { Text = "Header" };
        var button = new ButtonNode { Label = "Click" };
        var content = new VStackNode { Children = [button] };
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = true
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Equal(2, focusables.Count);
        Assert.Same(node, focusables[0]);
        Assert.Same(button, focusables[1]);
    }

    #endregion

    #region Input Handling Tests

    [Fact]
    public async Task HandleInput_Enter_TriggersToggleAction()
    {
        var toggled = false;
        var node = new DrawerNode
        {
            IsExpanded = false,
            IsFocused = true,
            ToggleAction = _ => { toggled = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node, 
            new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), 
            null, null, 
            TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(toggled);
    }

    [Fact]
    public async Task HandleInput_Space_TriggersToggleAction()
    {
        var toggled = false;
        var node = new DrawerNode
        {
            IsExpanded = false,
            IsFocused = true,
            ToggleAction = _ => { toggled = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node, 
            new Hex1bKeyEvent(Hex1bKey.Spacebar, ' ', Hex1bModifiers.None), 
            null, null, 
            TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(toggled);
    }

    [Fact]
    public async Task HandleInput_Escape_TriggersToggleWhenExpanded()
    {
        var toggled = false;
        var node = new DrawerNode
        {
            IsExpanded = true,
            IsFocused = true,
            ToggleAction = _ => { toggled = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node, 
            new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None), 
            null, null, 
            TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(toggled);
    }

    [Fact]
    public async Task HandleInput_Escape_DoesNotToggleWhenCollapsed()
    {
        var toggled = false;
        var node = new DrawerNode
        {
            IsExpanded = false,
            IsFocused = true,
            ToggleAction = _ => { toggled = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node, 
            new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None), 
            null, null, 
            TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.False(toggled);
    }

    #endregion

    #region Position Tests

    [Theory]
    [InlineData(DrawerPosition.Left)]
    [InlineData(DrawerPosition.Right)]
    [InlineData(DrawerPosition.Top)]
    [InlineData(DrawerPosition.Bottom)]
    public void Position_IsStoredCorrectly(DrawerPosition position)
    {
        var node = new DrawerNode { Position = position };

        Assert.Equal(position, node.Position);
    }

    #endregion

    #region Mode Tests

    [Theory]
    [InlineData(DrawerMode.Docked)]
    [InlineData(DrawerMode.Overlay)]
    public void Mode_IsStoredCorrectly(DrawerMode mode)
    {
        var node = new DrawerNode { Mode = mode };

        Assert.Equal(mode, node.Mode);
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void Render_Collapsed_ShowsCollapsedIndicator()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        
        var header = new TextBlockNode { Text = "Files" };
        var node = new DrawerNode
        {
            Header = header,
            Content = new TextBlockNode { Text = "Content" },
            IsExpanded = false,
            Position = DrawerPosition.Left
        };
        
        node.Measure(new Constraints(0, 40, 0, 5));
        node.Arrange(new Rect(0, 0, 40, 5));
        header.Measure(new Constraints(0, 38, 0, 1));
        header.Arrange(new Rect(2, 0, 5, 1));

        node.Render(context);

        var snapshot = terminal.CreateSnapshot();
        // Should contain the collapsed indicator for left position
        Assert.True(snapshot.ContainsText("▶") || snapshot.ContainsText("Files"));
    }

    [Fact]
    public void Render_Expanded_ShowsExpandedIndicator()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        
        var header = new TextBlockNode { Text = "Panel" };
        var node = new DrawerNode
        {
            Header = header,
            Content = new TextBlockNode { Text = "Content" },
            IsExpanded = true,
            Position = DrawerPosition.Left,
            ExpandedSize = 20
        };
        
        node.Measure(new Constraints(0, 40, 0, 5));
        node.Arrange(new Rect(0, 0, 40, 5));
        header.Measure(new Constraints(0, 38, 0, 1));
        header.Arrange(new Rect(2, 0, 5, 1));

        node.Render(context);

        var snapshot = terminal.CreateSnapshot();
        // Should contain the expanded indicator
        Assert.True(snapshot.ContainsText("▼") || snapshot.ContainsText("Panel"));
    }

    [Fact]
    public void Render_Focused_HasDifferentStyle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        
        var header = new TextBlockNode { Text = "Menu" };
        var node = new DrawerNode
        {
            Header = header,
            Content = new TextBlockNode { Text = "Content" },
            IsExpanded = false,
            IsFocused = true
        };
        
        node.Measure(new Constraints(0, 40, 0, 5));
        node.Arrange(new Rect(0, 0, 40, 5));
        header.Measure(new Constraints(0, 38, 0, 1));
        header.Arrange(new Rect(2, 0, 4, 1));

        node.Render(context);

        var snapshot = terminal.CreateSnapshot();
        // Should have styling applied when focused
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor());
    }

    #endregion

    #region Children Tests

    [Fact]
    public void GetChildren_Collapsed_ReturnsOnlyHeader()
    {
        var header = new TextBlockNode { Text = "Header" };
        var content = new VStackNode();
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = false
        };

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(header, children[0]);
    }

    [Fact]
    public void GetChildren_Expanded_IncludesContent()
    {
        var header = new TextBlockNode { Text = "Header" };
        var content = new VStackNode();
        var node = new DrawerNode 
        { 
            Header = header,
            Content = content,
            IsExpanded = true
        };

        var children = node.GetChildren().ToList();

        Assert.Equal(2, children.Count);
        Assert.Same(header, children[0]);
        Assert.Same(content, children[1]);
    }

    #endregion

    #region ManagesChildFocus Tests

    [Fact]
    public void ManagesChildFocus_ReturnsTrue()
    {
        var node = new DrawerNode();

        Assert.True(node.ManagesChildFocus);
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_Drawer_RendersViaHex1bApp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);

        var isExpanded = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Drawer(
                    isExpanded: isExpanded,
                    onToggle: expanded => isExpanded = expanded,
                    header: ctx.Text("📁 Files"),
                    content: ctx.VStack(v => [
                        v.Text("Documents"),
                        v.Text("Downloads")
                    ])
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Files"), TimeSpan.FromSeconds(2))
            .Capture("drawer-collapsed")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Files"));
    }

    [Fact]
    public async Task Integration_Drawer_Enter_TogglesExpanded()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);

        var isExpanded = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Drawer(
                    isExpanded: isExpanded,
                    onToggle: expanded => isExpanded = expanded,
                    header: ctx.Text("Menu"),
                    content: ctx.VStack(v => [
                        v.Text("Option 1"),
                        v.Text("Option 2")
                    ])
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Menu"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("drawer-expanded")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(isExpanded);
    }

    [Fact]
    public async Task Integration_Drawer_Expanded_ShowsContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);

        var isExpanded = true;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Drawer(
                    isExpanded: isExpanded,
                    onToggle: expanded => isExpanded = expanded,
                    header: ctx.Text("Settings"),
                    content: ctx.VStack(v => [
                        v.Text("Theme: Dark"),
                        v.Text("Language: EN")
                    ])
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Settings"), TimeSpan.FromSeconds(2))
            .Capture("drawer-with-content")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Settings"));
        Assert.True(snapshot.ContainsText("Theme: Dark"));
    }

    [Fact]
    public async Task Integration_Drawer_WithPosition_Left()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);

        var isExpanded = true;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Drawer(
                    isExpanded: isExpanded,
                    onToggle: expanded => isExpanded = expanded,
                    header: ctx.Text("Explorer"),
                    content: ctx.VStack(v => [
                        v.Text("src/"),
                        v.Text("tests/")
                    ]),
                    position: DrawerPosition.Left
                ).WithExpandedSize(25)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Explorer"), TimeSpan.FromSeconds(2))
            .Capture("drawer-left-position")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Explorer"));
        Assert.True(snapshot.ContainsText("src/"));
    }

    [Fact]
    public async Task Integration_Drawer_Escape_Collapses()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);

        var isExpanded = true;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Drawer(
                    isExpanded: isExpanded,
                    onToggle: expanded => isExpanded = expanded,
                    header: ctx.Text("Panel"),
                    content: ctx.Text("Content")
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Panel"), TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.Escape)
            .Capture("drawer-after-escape")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(isExpanded);
    }

    [Fact]
    public async Task Integration_Drawer_WithButtonContent_CanFocusButton()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);

        var isExpanded = true;
        var buttonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Drawer(
                    isExpanded: isExpanded,
                    onToggle: expanded => isExpanded = expanded,
                    header: ctx.Text("Actions"),
                    content: ctx.VStack(v => [
                        v.Button("Click Me").OnClick(_ => { buttonClicked = true; return Task.CompletedTask; })
                    ])
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Actions"), TimeSpan.FromSeconds(2))
            .Tab() // Move focus from drawer toggle to button
            .Enter() // Click the button
            .Capture("drawer-button-clicked")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(buttonClicked);
    }

    #endregion
}
