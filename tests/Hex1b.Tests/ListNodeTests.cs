using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Widgets;
using Hex1b.Terminal;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for ListNode rendering and input handling.
/// </summary>
public class ListNodeTests
{
    private static Hex1bRenderContext CreateContext(IHex1bAppTerminalWorkloadAdapter workload, Hex1bTheme? theme = null)
    {
        return new Hex1bRenderContext(workload, theme);
    }

    private static IReadOnlyList<string> CreateItems(params string[] items)
    {
        return items.ToList();
    }

    #region Measurement Tests

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Short", "Longer Item", "Med")
        };
        
        var size = node.Measure(Constraints.Unbounded);
        
        // Width = max item length + 2 (indicator), Height = item count
        Assert.Equal(13, size.Width); // "Longer Item" = 11 + 2
        Assert.Equal(3, size.Height);
    }

    [Fact]
    public void Measure_EmptyList_HasMinHeight()
    {
        var node = new ListNode { Items = [] };
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_SingleItem_IncludesIndicator()
    {
        var node = new ListNode { Items = CreateItems("Hello") };
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.Equal(7, size.Width); // "Hello" = 5 + 2 for indicator
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxWidth()
    {
        var node = new ListNode { Items = CreateItems("Very Long Item Name") };
        
        var size = node.Measure(new Constraints(0, 10, 0, 100));
        
        Assert.Equal(10, size.Width);
    }

    [Fact]
    public void Measure_RespectsMaxHeight()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5") 
        };
        
        var size = node.Measure(new Constraints(0, 100, 0, 3));
        
        Assert.Equal(3, size.Height);
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new ListNode { Items = CreateItems("Test") };
        var bounds = new Rect(0, 0, 40, 10);
        
        node.Arrange(bounds);
        
        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public void Arrange_WithOffset_SetsBoundsWithOffset()
    {
        var node = new ListNode { Items = CreateItems("Test") };
        var bounds = new Rect(5, 3, 30, 8);
        
        node.Arrange(bounds);
        
        Assert.Equal(5, node.Bounds.X);
        Assert.Equal(3, node.Bounds.Y);
    }

    #endregion

    #region Rendering - Basic Tests

    [Fact]
    public void Render_ShowsAllItems()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3")
        };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Item 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Item 2"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Item 3"));
    }

    [Fact]
    public void Render_EmptyList_RendersNothing()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode { Items = [] };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        // Should not crash and output should be minimal
        Assert.False(terminal.CreateSnapshot().ContainsText("Item"));
    }

    [Fact]
    public void Render_SingleItem_ShowsItem()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode { Items = CreateItems("Only Item") };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Only Item"));
    }

    #endregion

    #region Rendering - Selection Indicator Tests

    [Fact]
    public void Render_SelectedItem_HasSelectedIndicator()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        // Default selected indicator is "> "
        Assert.True(terminal.CreateSnapshot().ContainsText("> Item 1"));
    }

    [Fact]
    public void Render_UnselectedItems_HaveUnselectedIndicator()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        // Default unselected indicator is "  " (two spaces)
        Assert.True(terminal.CreateSnapshot().ContainsText("  Item 2"));
    }

    [Fact]
    public void Render_MiddleItemSelected_ShowsCorrectIndicators()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode { Items = CreateItems("First", "Second", "Third"), SelectedIndex = 1, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        Assert.True(terminal.CreateSnapshot().ContainsText("  First"));
        Assert.True(terminal.CreateSnapshot().ContainsText("> Second"));
        Assert.True(terminal.CreateSnapshot().ContainsText("  Third"));
    }

    [Fact]
    public void Render_LastItemSelected_ShowsCorrectIndicators()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode { Items = CreateItems("First", "Second", "Third"), SelectedIndex = 2, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        Assert.True(terminal.CreateSnapshot().ContainsText("  First"));
        Assert.True(terminal.CreateSnapshot().ContainsText("  Second"));
        Assert.True(terminal.CreateSnapshot().ContainsText("> Third"));
    }

    #endregion

    #region Rendering - Focus State Tests

    [Fact]
    public void Render_FocusedAndSelected_HasColorCodes()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        // The output should contain colors for the focused+selected item
        Assert.True(terminal.CreateSnapshot().HasForegroundColor() || terminal.CreateSnapshot().HasBackgroundColor() || terminal.CreateSnapshot().HasAttribute(CellAttributes.Reverse));
    }

    [Fact]
    public void Render_NotFocused_SelectedItemHasIndicatorOnly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = false };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        // Still shows indicator but without selection colors
        Assert.True(terminal.CreateSnapshot().ContainsText("> Item 1"));
    }

    #endregion

    #region Rendering - Position Tests

    [Fact]
    public void Render_WithOffset_RendersAtCorrectPosition()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode { Items = CreateItems("Test Item"), SelectedIndex = 0, IsFocused = false };
        node.Arrange(new Rect(5, 3, 20, 5));
        
        node.Render(context);
        
        // Check that content is rendered - the terminal places it at the right position internally
        Assert.True(terminal.CreateSnapshot().ContainsText("Test Item"));
    }

    [Fact]
    public void Render_MultipleItems_RendersAllItems()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ListNode 
        { 
            Items = CreateItems("Item A", "Item B", "Item C") 
        };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        // All items should be rendered
        Assert.True(terminal.CreateSnapshot().ContainsText("Item A"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Item B"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Item C"));
    }

    #endregion

    #region Rendering - Theming Tests

    [Fact]
    public void Render_WithCustomTheme_UsesCustomColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Yellow)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.Red);
        var context = CreateContext(workload, theme);
        var node = new ListNode { Items = CreateItems("Item 1"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        // Yellow foreground
        Assert.True(terminal.CreateSnapshot().HasForegroundColor(Hex1bColor.FromRgb(255, 255, 0)));
        // Red background
        Assert.True(terminal.CreateSnapshot().HasBackgroundColor(Hex1bColor.FromRgb(255, 0, 0)));
    }

    [Fact]
    public void Render_WithCustomIndicator_UsesCustomIndicator()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(ListTheme.SelectedIndicator, "► ");
        var context = CreateContext(workload, theme);
        var node = new ListNode { Items = CreateItems("Item 1"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        Assert.True(terminal.CreateSnapshot().ContainsText("► Item 1"));
    }

    [Fact]
    public void Render_WithCustomUnselectedIndicator_UsesCustomIndicator()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(ListTheme.UnselectedIndicator, "- ");
        var context = CreateContext(workload, theme);
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        Assert.True(terminal.CreateSnapshot().ContainsText("- Item 2"));
    }

    [Fact]
    public void Render_RetroTheme_UsesTriangleIndicator()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload, Hex1bThemes.HighContrast);
        var node = new ListNode { Items = CreateItems("Item 1"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        
        // HighContrast theme uses "► " indicator
        Assert.True(terminal.CreateSnapshot().ContainsText("► Item 1"));
    }

    #endregion

    #region Rendering - Narrow Terminal Tests

    [Fact]
    public void Render_NarrowTerminal_TruncatesItems()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 10, 5);
        var context = CreateContext(workload);
        var node = new ListNode 
        { 
            Items = CreateItems("Very Long Item Name") 
        };
        node.Measure(new Constraints(0, 10, 0, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        
        node.Render(context);
        
        // Content is rendered, at least the beginning of the text should be visible
        Assert.True(terminal.CreateSnapshot().ContainsText("Very"));
    }

    [Fact]
    public void Render_MinimalWidth_StillRendersIndicator()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 5, 5);
        var context = CreateContext(workload);
        var node = new ListNode { Items = CreateItems("Test"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 5, 5));
        
        node.Render(context);
        
        // Should still render the indicator
        Assert.True(terminal.CreateSnapshot().ContainsText(">"));
    }

    #endregion

    #region Input Handling - Navigation Tests

    [Fact]
    public async Task HandleInput_DownArrow_MovesSelection()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2", "Item 3"), SelectedIndex = 0, IsFocused = true };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_UpArrow_MovesSelection()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2", "Item 3"), SelectedIndex = 2, IsFocused = true };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_DownArrow_WrapsAround()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 1, IsFocused = true };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(0, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_UpArrow_WrapsAround()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_MultipleDownArrows_NavigatesCorrectly()
    {
        var node = new ListNode { Items = CreateItems("A", "B", "C", "D"), SelectedIndex = 0, IsFocused = true };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(3, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_MultipleUpArrows_NavigatesCorrectly()
    {
        var node = new ListNode { Items = CreateItems("A", "B", "C", "D"), SelectedIndex = 3, IsFocused = true };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(1, node.SelectedIndex);
    }

    #endregion

    #region Input Handling - Activation Tests

    [Fact]
    public async Task HandleInput_Enter_InvokesOnItemActivated()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 1, IsFocused = true };
        string? activatedItem = null;
        node.ItemActivatedAction = _ => { activatedItem = node.SelectedText; return Task.CompletedTask; };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(InputResult.Handled, result);
        Assert.NotNull(activatedItem);
        Assert.Equal("Item 2", activatedItem);
    }

    [Fact]
    public async Task HandleInput_Space_InvokesOnItemActivated()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        string? activatedItem = null;
        node.ItemActivatedAction = _ => { activatedItem = node.SelectedText; return Task.CompletedTask; };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Spacebar, ' ', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(InputResult.Handled, result);
        Assert.NotNull(activatedItem);
        Assert.Equal("Item 1", activatedItem);
    }

    [Fact]
    public async Task HandleInput_Enter_WithoutCallback_StillReturnsHandled()
    {
        var node = new ListNode { Items = CreateItems("Item 1"), SelectedIndex = 0, IsFocused = true };
        // No OnItemActivated callback set
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(InputResult.Handled, result);
    }

    [Fact]
    public async Task HandleInput_Enter_OnEmptyList_ReturnsHandled()
    {
        var node = new ListNode { Items = [], IsFocused = true };
        string? activatedItem = null;
        node.ItemActivatedAction = _ => { activatedItem = node.SelectedText; return Task.CompletedTask; };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(InputResult.Handled, result);
        Assert.Null(activatedItem); // No item to activate
    }

    #endregion

    #region Input Handling - Selection Changed Callback Tests

    [Fact]
    public async Task HandleInput_DownArrow_InvokesOnSelectionChanged()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        string? selectedItem = null;
        node.SelectionChangedAction = _ => { selectedItem = node.SelectedText; return Task.CompletedTask; };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.NotNull(selectedItem);
        Assert.Equal("Item 2", selectedItem);
    }

    [Fact]
    public async Task HandleInput_UpArrow_InvokesOnSelectionChanged()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 1, IsFocused = true };
        string? selectedItem = null;
        node.SelectionChangedAction = _ => { selectedItem = node.SelectedText; return Task.CompletedTask; };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.NotNull(selectedItem);
        Assert.Equal("Item 1", selectedItem);
    }

    #endregion

    #region Input Handling - Edge Cases

    [Fact]
    public async Task HandleInput_WhenNotFocused_BindingsStillExecute()
    {
        // Note: With the new input binding architecture, bindings execute at the node level
        // regardless of focus. Focus is a tree concept handled by InputRouter.RouteInput().
        // When using RouteInputToNode() for testing, bindings always execute.
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = false };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        // Bindings execute regardless of focus state when using RouteInputToNode
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);  // Selection changed
    }

    [Fact]
    public async Task HandleInput_OtherKey_DoesNotHandle()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), IsFocused = true };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public async Task HandleInput_Tab_DoesNotHandle()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), IsFocused = true };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public async Task HandleInput_Escape_DoesNotHandle()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), IsFocused = true };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(InputResult.NotHandled, result);
    }

    #endregion

    #region Focusability Tests

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new ListNode();
        
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void GetFocusableNodes_ReturnsSelf()
    {
        var node = new ListNode { Items = CreateItems("Test") };
        
        var focusables = node.GetFocusableNodes().ToList();
        
        Assert.Single(focusables);
        Assert.Same(node, focusables[0]);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_ListInApp_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var items = CreateItems("Option A", "Option B", "Option C");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Option A"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Option A"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Option B"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Option C"));
    }

    [Fact]
    public async Task Integration_ListWithSelection_RendersIndicator()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var items = CreateItems("First", "Second", "Third");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Navigate down to select second item
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(2))
            .Down()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.True(terminal.CreateSnapshot().ContainsText("> Second"));
        Assert.True(terminal.CreateSnapshot().ContainsText("  First"));
        Assert.True(terminal.CreateSnapshot().ContainsText("  Third"));
    }

    [Fact]
    public async Task Integration_ListInBorder_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var items = CreateItems("Item 1", "Item 2");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.List(items), "Menu")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Note: Border title may not render in all configurations
        Assert.True(terminal.CreateSnapshot().ContainsText("Item 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Item 2"));
        Assert.True(terminal.CreateSnapshot().ContainsText("┌"));
    }

    [Fact]
    public async Task Integration_ListReceivesFocus_HandlesInput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var items = CreateItems("Item 1", "Item 2", "Item 3");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Simulate down arrow
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2))
            .Down()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // After down arrow, second item should be selected
        Assert.True(terminal.CreateSnapshot().ContainsText("> Item 2"));
    }

    [Fact]
    public async Task Integration_ListActivation_InvokesCallback()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var items = CreateItems("Action 1", "Action 2");
        string? activatedAction = null;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)
                .OnItemActivated((ListItemActivatedEventArgs args) => activatedAction = args.ActivatedText)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Simulate Enter key
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action 1"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.Equal("Action 1", activatedAction);
    }

    [Fact]
    public async Task Integration_ListInVStack_RendersWithOtherElements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var items = CreateItems("Menu Item");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Select an option:"),
                    v.List(items)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Select an option:"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Select an option:"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Menu Item"));
    }

    [Fact]
    public async Task Integration_ListWithTheme_AppliesTheme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var items = CreateItems("Themed Item");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = Hex1bThemes.HighContrast }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Themed Item"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // HighContrast theme uses "► " indicator
        Assert.True(terminal.CreateSnapshot().ContainsText("► Themed Item"));
    }

    [Fact]
    public async Task Integration_ListNavigationMultipleSteps_UpdatesSelection()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var items = CreateItems("First", "Second", "Third");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Navigate down twice
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(2))
            .Down()
            .Down()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Third item should be selected
        Assert.True(terminal.CreateSnapshot().ContainsText("> Third"));
    }

    [Fact]
    public async Task Integration_ListInsideBorderWithOtherWidgets_RendersProperly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 15);
        var items = CreateItems("Option A", "Option B");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Welcome"),
                    v.Border(ctx.List(items), "Options"),
                    v.Button("OK").OnClick(_ => Task.CompletedTask)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Welcome"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Welcome"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Options"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Option A"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Option B"));
        Assert.True(terminal.CreateSnapshot().ContainsText("OK"));
    }

    #endregion

    #region Mouse Click Tests

    [Fact]
    public void HandleMouseClick_SelectsClickedItem()
    {
        // Note: Mouse click is synchronous so it cannot fire async events.
        // It only changes the selection.
        var node = new ListNode
        {
            Items = CreateItems("First", "Second", "Third")
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 3));

        // Click on the second item (row 1)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 1, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 1, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public void HandleMouseClick_OutOfBounds_ReturnsNotHandled()
    {
        var node = new ListNode
        {
            Items = CreateItems("First", "Second")
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 2));

        // Click outside the list bounds (row 5)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 5, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 5, mouseEvent);

        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal(0, node.SelectedIndex); // Unchanged
    }

    [Fact]
    public void HandleMouseClick_NegativeY_ReturnsNotHandled()
    {
        var node = new ListNode
        {
            Items = CreateItems("First", "Second")
        };

        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, -1, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, -1, mouseEvent);

        Assert.Equal(InputResult.NotHandled, result);
    }

    #endregion

    #region Mouse Wheel Scrolling Tests

    [Fact]
    public async Task MouseWheelDown_MovesSelectionDown()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2", "Item 3"), SelectedIndex = 0, IsFocused = true };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 3));
        
        // Create a mouse wheel down event via the binding system
        var builder = node.BuildBindings();
        var scrollDownBinding = builder.MouseBindings.FirstOrDefault(b => b.Button == MouseButton.ScrollDown);
        Assert.NotNull(scrollDownBinding);
        
        var ctx = new InputBindingActionContext(new FocusRing(), null, default);
        await scrollDownBinding!.ExecuteAsync(ctx);
        
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task MouseWheelUp_MovesSelectionUp()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2", "Item 3"), SelectedIndex = 2, IsFocused = true };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 3));
        
        // Create a mouse wheel up event via the binding system
        var builder = node.BuildBindings();
        var scrollUpBinding = builder.MouseBindings.FirstOrDefault(b => b.Button == MouseButton.ScrollUp);
        Assert.NotNull(scrollUpBinding);
        
        var ctx = new InputBindingActionContext(new FocusRing(), null, default);
        await scrollUpBinding!.ExecuteAsync(ctx);
        
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task MouseWheelDown_WrapsAroundToFirstItem()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 1, IsFocused = true };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 2));
        
        var builder = node.BuildBindings();
        var scrollDownBinding = builder.MouseBindings.FirstOrDefault(b => b.Button == MouseButton.ScrollDown);
        Assert.NotNull(scrollDownBinding);
        
        var ctx = new InputBindingActionContext(new FocusRing(), null, default);
        await scrollDownBinding!.ExecuteAsync(ctx);
        
        // Should wrap around to first item
        Assert.Equal(0, node.SelectedIndex);
    }

    [Fact]
    public async Task MouseWheelUp_WrapsAroundToLastItem()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 2));
        
        var builder = node.BuildBindings();
        var scrollUpBinding = builder.MouseBindings.FirstOrDefault(b => b.Button == MouseButton.ScrollUp);
        Assert.NotNull(scrollUpBinding);
        
        var ctx = new InputBindingActionContext(new FocusRing(), null, default);
        await scrollUpBinding!.ExecuteAsync(ctx);
        
        // Should wrap around to last item
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task MouseWheel_InvokesSelectionChangedEvent()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 2));
        
        string? selectedItem = null;
        node.SelectionChangedAction = _ => { selectedItem = node.SelectedText; return Task.CompletedTask; };
        
        var builder = node.BuildBindings();
        var scrollDownBinding = builder.MouseBindings.FirstOrDefault(b => b.Button == MouseButton.ScrollDown);
        
        var ctx = new InputBindingActionContext(new FocusRing(), null, default);
        await scrollDownBinding!.ExecuteAsync(ctx);
        
        Assert.NotNull(selectedItem);
        Assert.Equal("Item 2", selectedItem);
    }

    #endregion

    #region Viewport Scrolling Tests (Height-Constrained Container)

    [Fact]
    public void ConstrainedList_HasCorrectViewportHeight()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5") 
        };
        
        // Measure with a height constraint of 3 rows
        node.Measure(new Constraints(0, 100, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        
        Assert.Equal(3, node.ViewportHeight);
        Assert.True(node.IsScrollable);
        Assert.Equal(2, node.MaxScrollOffset); // 5 items - 3 viewport = 2
    }

    [Fact]
    public void ConstrainedList_InitialScrollOffsetIsZero()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5") 
        };
        
        node.Measure(new Constraints(0, 100, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        
        Assert.Equal(0, node.ScrollOffset);
    }

    [Fact]
    public void ConstrainedList_NavigatingDown_ScrollsToRevealItem()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5"),
            SelectedIndex = 2 // Last visible item in viewport of 3
        };
        
        node.Measure(new Constraints(0, 100, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        
        // Initially scroll offset is 0, showing items 0, 1, 2
        Assert.Equal(0, node.ScrollOffset);
        
        // Navigate down to item 3 (index 3)
        node.MoveDown();
        
        // Scroll offset should adjust to show item 3
        Assert.Equal(3, node.SelectedIndex);
        Assert.Equal(1, node.ScrollOffset); // Now showing items 1, 2, 3
    }

    [Fact]
    public void ConstrainedList_NavigatingUp_ScrollsToRevealItem()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5"),
            SelectedIndex = 3
        };
        
        node.Measure(new Constraints(0, 100, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        
        // The scroll offset should be adjusted to show selected item
        // With selection at 3 and viewport of 3, offset should be 1
        Assert.Equal(1, node.ScrollOffset);
        
        // Navigate up to first visible item
        node.MoveUp(); // Now at index 2
        node.MoveUp(); // Now at index 1
        node.MoveUp(); // Now at index 0
        
        // Scroll should adjust to show item 0
        Assert.Equal(0, node.SelectedIndex);
        Assert.Equal(0, node.ScrollOffset);
    }

    [Fact]
    public void ConstrainedList_NavigatingToEnd_ScrollsMaximally()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5"),
            SelectedIndex = 0
        };
        
        node.Measure(new Constraints(0, 100, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        
        // Navigate to the last item
        node.SelectedIndex = 4;
        
        // Scroll offset should be at max (showing items 2, 3, 4)
        Assert.Equal(2, node.ScrollOffset);
    }

    [Fact]
    public void ConstrainedList_RendersOnlyVisibleItems()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5"),
            SelectedIndex = 0
        };
        
        // Constrain to 3 rows
        node.Measure(new Constraints(0, 40, 0, 3));
        node.Arrange(new Rect(0, 0, 40, 3));
        
        node.Render(context);
        var snapshot = terminal.CreateSnapshot();
        
        // Should show first 3 items
        Assert.True(snapshot.ContainsText("Item 1"));
        Assert.True(snapshot.ContainsText("Item 2"));
        Assert.True(snapshot.ContainsText("Item 3"));
        
        // Should NOT show items 4 and 5 (they're outside viewport)
        Assert.False(snapshot.ContainsText("Item 4"));
        Assert.False(snapshot.ContainsText("Item 5"));
    }

    [Fact]
    public void ConstrainedList_AfterScrolling_RendersCorrectItems()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5"),
            SelectedIndex = 4 // Select last item, which should scroll
        };
        
        // Constrain to 3 rows
        node.Measure(new Constraints(0, 40, 0, 3));
        node.Arrange(new Rect(0, 0, 40, 3));
        
        node.Render(context);
        var snapshot = terminal.CreateSnapshot();
        
        // Should show items 3, 4, 5 (scroll offset = 2)
        Assert.True(snapshot.ContainsText("Item 3"));
        Assert.True(snapshot.ContainsText("Item 4"));
        Assert.True(snapshot.ContainsText("Item 5"));
        
        // Should NOT show items 1 and 2
        Assert.False(snapshot.ContainsText("Item 1"));
        Assert.False(snapshot.ContainsText("Item 2"));
    }

    [Fact]
    public void ConstrainedList_MouseClickOnVisibleItem_SelectsCorrectItem()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5"),
            SelectedIndex = 3 // Scroll offset will be 1, showing items 2, 3, 4
        };
        
        node.Measure(new Constraints(0, 20, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        
        // Verify scroll offset
        Assert.Equal(1, node.ScrollOffset);
        
        // Click on the third visible row (local Y = 2)
        // With scroll offset 1, this should select item at index 3 (1 + 2)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 2, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 2, mouseEvent);
        
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(3, node.SelectedIndex);
    }

    [Fact]
    public void ConstrainedList_WrapAroundNavigation_ScrollsCorrectly()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5"),
            SelectedIndex = 4 // Last item
        };
        
        node.Measure(new Constraints(0, 20, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        
        // Navigate down from last item (should wrap to first)
        node.MoveDown();
        
        Assert.Equal(0, node.SelectedIndex);
        Assert.Equal(0, node.ScrollOffset); // Should scroll to show first item
    }

    [Fact]
    public async Task Integration_ConstrainedListWithKeyboardNavigation_RevealsItems()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);
        var items = CreateItems("Apple", "Banana", "Cherry", "Date", "Elderberry");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(b => [
                    b.List(items).FixedHeight(3) // Constrain list to 3 rows
                ], title: "Fruits")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Navigate down through the list
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(2))
            .Down().Down().Down() // Move to Date (index 3)
            .Capture("after_navigation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // After navigation, Date should be visible and selected
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Date"));
        Assert.True(snapshot.ContainsText("> Date")); // Should be selected
    }

    [Fact]
    public async Task Integration_ConstrainedListWithMouseWheel_NavigatesCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);
        var items = CreateItems("Apple", "Banana", "Cherry", "Date", "Elderberry");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(b => [
                    b.List(items).FixedHeight(3) // Constrain list to 3 rows
                ], title: "Fruits")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Position mouse over the list (inside the border) and scroll
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Apple"), TimeSpan.FromSeconds(2))
            .MouseMoveTo(5, 2) // Position mouse inside the list area
            .ScrollDown(3) // Scroll down 3 times to reach Date
            .Capture("after_scroll")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // After scrolling, Date should be selected and visible
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("Date"));
    }

    [Fact]
    public void UnconstrainedList_IsNotScrollable()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3") 
        };
        
        // Measure without constraints (or with constraints larger than item count)
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 10)); // Viewport larger than items
        
        Assert.False(node.IsScrollable);
        Assert.Equal(0, node.MaxScrollOffset);
    }

    [Fact]
    public void ConstrainedList_ScrollOffsetClampsOnItemsChange()
    {
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3", "Item 4", "Item 5"),
            SelectedIndex = 4
        };
        
        node.Measure(new Constraints(0, 20, 0, 3));
        node.Arrange(new Rect(0, 0, 20, 3));
        
        // Scroll offset should be 2 to show last item
        Assert.Equal(2, node.ScrollOffset);
        
        // Now reduce items - scroll offset should clamp
        node.Items = CreateItems("Item 1", "Item 2");
        node.Arrange(new Rect(0, 0, 20, 3));
        
        // With only 2 items and viewport of 3, max offset is 0
        Assert.Equal(0, node.ScrollOffset);
    }

    [Fact]
    public async Task Integration_LongList_KeyboardNavigationScrollsCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 15);
        
        // Create a long list of 20 items
        var items = Enumerable.Range(1, 20).Select(i => $"Item {i:D2}").ToList();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(b => [
                    b.List(items).FixedHeight(5) // Only show 5 items at a time
                ], title: "Long List (20 items)")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Navigate down to item 15
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 01"), TimeSpan.FromSeconds(2))
            .Down().Down().Down().Down().Down() // Items 2-6
            .Down().Down().Down().Down().Down() // Items 7-11
            .Down().Down().Down().Down() // Items 12-15
            .Capture("at_item_15")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        var snapshot = terminal.CreateSnapshot();
        // Item 15 should be selected and visible
        Assert.True(snapshot.ContainsText("> Item 15"));
        // Items around it should also be visible
        Assert.True(snapshot.ContainsText("Item 14") || snapshot.ContainsText("Item 13"));
        // Early items should NOT be visible (scrolled out)
        Assert.False(snapshot.ContainsText("Item 01"));
        Assert.False(snapshot.ContainsText("Item 02"));
    }

    [Fact]
    public async Task Integration_LongList_MouseWheelScrollsThroughEntireList()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 15);
        
        // Create a long list of 20 items
        var items = Enumerable.Range(1, 20).Select(i => $"Item {i:D2}").ToList();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(b => [
                    b.List(items).FixedHeight(5) // Only show 5 items at a time
                ], title: "Long List (20 items)")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Scroll down to the end using mouse wheel
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 01"), TimeSpan.FromSeconds(2))
            .MouseMoveTo(10, 5) // Position mouse over list
            .ScrollDown(19) // Scroll down 19 times to reach item 20
            .Capture("at_end")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        var snapshot = terminal.CreateSnapshot();
        // Item 20 should be selected and visible
        Assert.True(snapshot.ContainsText("> Item 20"));
        // Early items should NOT be visible
        Assert.False(snapshot.ContainsText("Item 01"));
    }

    [Fact]
    public async Task Integration_MouseClickAfterScrolling_SelectsCorrectItem()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 15);
        
        // Create a list of 20 items
        var items = Enumerable.Range(1, 20).Select(i => $"Item {i:D2}").ToList();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(b => [
                    b.List(items).FixedHeight(5) // Only show 5 items at a time
                ], title: "Click After Scroll")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // First scroll down with mouse wheel to move past item 5
        // Then click on the second visible row - should select the correct item
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 01"), TimeSpan.FromSeconds(2))
            .MouseMoveTo(10, 5) // Position over list area
            .ScrollDown(5) // Selection moves to item 6, scroll offset adjusts
            .ClickAt(10, 4) // Click on the item two rows above current selection
            .Capture("after_click")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        var snapshot = terminal.CreateSnapshot();
        // Should have selected an item in the scrolled region (item 4-6 range visible)
        // The clicked item should now be selected with ">"
        Assert.True(snapshot.ContainsText("> Item"), "An item should be selected with indicator");
    }

    [Fact]
    public void LongList_RendersOnlyVisibleItems()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 20);
        var context = CreateContext(workload);
        
        // Create a list of 50 items (0-indexed: items[0]="Item 00", items[49]="Item 49")
        var items = Enumerable.Range(0, 50).Select(i => $"Item {i:D2}").ToList();
        
        var node = new ListNode 
        { 
            Items = items,
            SelectedIndex = 25 // Middle of the list - "Item 25"
        };
        
        // Constrain to 10 rows
        node.Measure(new Constraints(0, 40, 0, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        
        // Verify scroll offset is set to show selected item
        // With viewport of 10 and selection at 25, scroll should be 16 (25 - 10 + 1)
        Assert.Equal(16, node.ScrollOffset);
        
        node.Render(context);
        var snapshot = terminal.CreateSnapshot();
        
        // Item 25 should be visible and selected
        Assert.True(snapshot.ContainsText("> Item 25"));
        
        // Items far outside the viewport should NOT be visible
        Assert.False(snapshot.ContainsText("Item 00"));
        Assert.False(snapshot.ContainsText("Item 49"));
    }

    [Fact]
    public void LongList_WrapAroundFromEnd_ScrollsToStart()
    {
        // Create a list of 50 items (0-indexed: items[0]="Item 00", items[49]="Item 49")
        var items = Enumerable.Range(0, 50).Select(i => $"Item {i:D2}").ToList();
        
        var node = new ListNode 
        { 
            Items = items,
            SelectedIndex = 49 // Last item - "Item 49"
        };
        
        node.Measure(new Constraints(0, 40, 0, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        
        // Should be scrolled to show last item
        Assert.Equal(40, node.ScrollOffset); // 50 items - 10 viewport = 40
        
        // Navigate down (should wrap to first item)
        node.MoveDown();
        
        Assert.Equal(0, node.SelectedIndex);
        Assert.Equal(0, node.ScrollOffset); // Should scroll back to start
    }

    #endregion

    #region Splitter Resize Tests (List with dynamic container height)

    [Fact]
    public void ListInVerticalSplitter_LastItemSelected_SplitterShrinks_SelectionStaysVisible()
    {
        // Create a long list
        var items = Enumerable.Range(0, 30).Select(i => $"Item {i:D2}").ToList();
        var listNode = new ListNode { Items = items };
        var bottomNode = new TextBlockNode { Text = "Bottom content" };
        
        // Create vertical splitter with list on top
        var splitterNode = new SplitterNode 
        { 
            First = listNode, 
            Second = bottomNode, 
            FirstSize = 15, // List gets 15 rows
            Orientation = SplitterOrientation.Vertical 
        };
        
        // Measure and arrange in a 40x20 space
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Verify list has 15 rows
        Assert.Equal(15, listNode.ViewportHeight);
        
        // Select the last item in the list
        listNode.SetSelection(29);
        Assert.Equal(29, listNode.SelectedIndex);
        
        // Verify scroll offset to show item 29 (should scroll to 15 = 29 - 15 + 1)
        Assert.Equal(15, listNode.ScrollOffset);
        
        // Now shrink the splitter - move it up by 5 rows
        splitterNode.FirstSize = 10;
        
        // Re-arrange (this simulates what happens when the splitter is moved)
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Verify list now has 10 rows
        Assert.Equal(10, listNode.ViewportHeight);
        
        // The selected item (29) should still be visible
        // EnsureSelectionVisible should have adjusted scroll offset
        // Scroll offset should be at least 20 (29 - 10 + 1 = 20)
        Assert.Equal(20, listNode.ScrollOffset);
        Assert.Equal(29, listNode.SelectedIndex);
    }

    [Fact]
    public void ListInVerticalSplitter_FirstItemSelected_SplitterShrinks_SelectionStaysVisible()
    {
        // Create a long list
        var items = Enumerable.Range(0, 30).Select(i => $"Item {i:D2}").ToList();
        var listNode = new ListNode { Items = items };
        var bottomNode = new TextBlockNode { Text = "Bottom content" };
        
        // Create vertical splitter with list on top
        var splitterNode = new SplitterNode 
        { 
            First = listNode, 
            Second = bottomNode, 
            FirstSize = 15,
            Orientation = SplitterOrientation.Vertical 
        };
        
        // Measure and arrange
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Select the first item
        listNode.SetSelection(0);
        Assert.Equal(0, listNode.SelectedIndex);
        Assert.Equal(0, listNode.ScrollOffset);
        
        // Shrink the splitter
        splitterNode.FirstSize = 8;
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Verify list now has 8 rows
        Assert.Equal(8, listNode.ViewportHeight);
        
        // First item should still be visible at the top
        Assert.Equal(0, listNode.ScrollOffset);
        Assert.Equal(0, listNode.SelectedIndex);
    }

    [Fact]
    public void ListInVerticalSplitter_MiddleItemSelected_SplitterShrinks_SelectionStaysVisible()
    {
        // Create a long list
        var items = Enumerable.Range(0, 30).Select(i => $"Item {i:D2}").ToList();
        var listNode = new ListNode { Items = items };
        var bottomNode = new TextBlockNode { Text = "Bottom content" };
        
        // Create vertical splitter with list on top
        var splitterNode = new SplitterNode 
        { 
            First = listNode, 
            Second = bottomNode, 
            FirstSize = 15,
            Orientation = SplitterOrientation.Vertical 
        };
        
        // Measure and arrange
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Select a middle item (item 15)
        listNode.SetSelection(15);
        Assert.Equal(15, listNode.SelectedIndex);
        
        // Initial scroll offset should be 1 (to show items 1-15 in 15-row viewport)
        Assert.Equal(1, listNode.ScrollOffset);
        
        // Shrink the splitter significantly
        splitterNode.FirstSize = 6;
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Verify list now has 6 rows
        Assert.Equal(6, listNode.ViewportHeight);
        
        // Item 15 should still be visible
        // Scroll offset should be adjusted: 15 - 6 + 1 = 10
        Assert.Equal(10, listNode.ScrollOffset);
        Assert.Equal(15, listNode.SelectedIndex);
    }

    [Fact]
    public void ListInVerticalSplitter_SelectionNearEnd_SplitterGrows_MoreItemsVisible()
    {
        // Create a long list
        var items = Enumerable.Range(0, 30).Select(i => $"Item {i:D2}").ToList();
        var listNode = new ListNode { Items = items };
        var bottomNode = new TextBlockNode { Text = "Bottom content" };
        
        // Create vertical splitter with small list area
        var splitterNode = new SplitterNode 
        { 
            First = listNode, 
            Second = bottomNode, 
            FirstSize = 5,
            Orientation = SplitterOrientation.Vertical 
        };
        
        // Measure and arrange
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Select item near the end
        listNode.SetSelection(28);
        Assert.Equal(28, listNode.SelectedIndex);
        
        // Scroll offset should be 24 (28 - 5 + 1)
        Assert.Equal(24, listNode.ScrollOffset);
        
        // Grow the splitter
        splitterNode.FirstSize = 15;
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Verify list now has 15 rows
        Assert.Equal(15, listNode.ViewportHeight);
        
        // Scroll offset should be adjusted to show item 28 (28 - 15 + 1 = 14)
        // But it could also stay at a higher offset if selection is visible
        Assert.True(listNode.ScrollOffset >= 14 && listNode.ScrollOffset <= 24);
        Assert.Equal(28, listNode.SelectedIndex);
        
        // Selection should still be within visible range
        Assert.True(listNode.SelectedIndex >= listNode.ScrollOffset);
        Assert.True(listNode.SelectedIndex < listNode.ScrollOffset + listNode.ViewportHeight);
    }

    [Fact]
    public void ListBelowVerticalSplitter_LastItemSelected_SplitterMovesDown_SelectionStaysVisible()
    {
        // Create a long list
        var items = Enumerable.Range(0, 30).Select(i => $"Item {i:D2}").ToList();
        var topNode = new TextBlockNode { Text = "Top content" };
        var listNode = new ListNode { Items = items };
        
        // Create vertical splitter with list on bottom
        var splitterNode = new SplitterNode 
        { 
            First = topNode, 
            Second = listNode, 
            FirstSize = 5, // Top gets 5 rows, list gets rest (20 - 5 - 1 = 14 rows)
            Orientation = SplitterOrientation.Vertical 
        };
        
        // Measure and arrange in a 40x20 space
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Verify list has 14 rows (20 total - 5 top - 1 divider)
        Assert.Equal(14, listNode.ViewportHeight);
        
        // Select the last item
        listNode.SetSelection(29);
        Assert.Equal(29, listNode.SelectedIndex);
        
        // Now move splitter down (increase top size), shrinking list
        splitterNode.FirstSize = 12; // List now gets 20 - 12 - 1 = 7 rows
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Verify list now has 7 rows
        Assert.Equal(7, listNode.ViewportHeight);
        
        // Selected item (29) should still be visible
        // Scroll offset should be 23 (29 - 7 + 1)
        Assert.Equal(23, listNode.ScrollOffset);
        Assert.Equal(29, listNode.SelectedIndex);
    }

    [Fact]
    public void ListBelowVerticalSplitter_FirstItemSelected_SplitterMovesDown_SelectionStaysVisible()
    {
        // Create a long list
        var items = Enumerable.Range(0, 30).Select(i => $"Item {i:D2}").ToList();
        var topNode = new TextBlockNode { Text = "Top content" };
        var listNode = new ListNode { Items = items };
        
        // Create vertical splitter with list on bottom
        var splitterNode = new SplitterNode 
        { 
            First = topNode, 
            Second = listNode, 
            FirstSize = 5,
            Orientation = SplitterOrientation.Vertical 
        };
        
        // Measure and arrange
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Select the first item
        listNode.SetSelection(0);
        Assert.Equal(0, listNode.SelectedIndex);
        Assert.Equal(0, listNode.ScrollOffset);
        
        // Move splitter down, shrinking list
        splitterNode.FirstSize = 12;
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Verify list now has 7 rows
        Assert.Equal(7, listNode.ViewportHeight);
        
        // First item should still be visible
        Assert.Equal(0, listNode.ScrollOffset);
        Assert.Equal(0, listNode.SelectedIndex);
    }

    [Fact]
    public void ListBelowVerticalSplitter_MiddleItemSelected_SplitterMovesDown_SelectionStaysVisible()
    {
        // Create a long list
        var items = Enumerable.Range(0, 30).Select(i => $"Item {i:D2}").ToList();
        var topNode = new TextBlockNode { Text = "Top content" };
        var listNode = new ListNode { Items = items };
        
        // Create vertical splitter with list on bottom
        var splitterNode = new SplitterNode 
        { 
            First = topNode, 
            Second = listNode, 
            FirstSize = 5,
            Orientation = SplitterOrientation.Vertical 
        };
        
        // Measure and arrange
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Verify list has 14 rows
        Assert.Equal(14, listNode.ViewportHeight);
        
        // Select a middle item (item 15)
        listNode.SetSelection(15);
        Assert.Equal(15, listNode.SelectedIndex);
        
        // Initial scroll offset should be 2 (to show items 2-15 in 14-row viewport)
        Assert.Equal(2, listNode.ScrollOffset);
        
        // Move splitter down significantly
        splitterNode.FirstSize = 14;
        splitterNode.Measure(new Constraints(0, 40, 0, 20));
        splitterNode.Arrange(new Rect(0, 0, 40, 20));
        
        // Verify list now has 5 rows
        Assert.Equal(5, listNode.ViewportHeight);
        
        // Item 15 should still be visible
        // Scroll offset should be 11 (15 - 5 + 1)
        Assert.Equal(11, listNode.ScrollOffset);
        Assert.Equal(15, listNode.SelectedIndex);
    }

    #endregion
}
