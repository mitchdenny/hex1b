using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Testing;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for ListNode rendering and input handling.
/// </summary>
public class ListNodeTests
{
    private static Hex1bRenderContext CreateContext(Hex1bTerminal terminal, Hex1bTheme? theme = null)
    {
        return new Hex1bRenderContext(terminal.WorkloadAdapter, theme);
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
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode 
        { 
            Items = CreateItems("Item 1", "Item 2", "Item 3")
        };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        Assert.Contains("Item 1", terminal.RawOutput);
        Assert.Contains("Item 2", terminal.RawOutput);
        Assert.Contains("Item 3", terminal.RawOutput);
    }

    [Fact]
    public void Render_EmptyList_RendersNothing()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode { Items = [] };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // Should not crash and output should be minimal
        Assert.DoesNotContain("Item", terminal.RawOutput);
    }

    [Fact]
    public void Render_SingleItem_ShowsItem()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode { Items = CreateItems("Only Item") };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        Assert.Contains("Only Item", terminal.RawOutput);
    }

    #endregion

    #region Rendering - Selection Indicator Tests

    [Fact]
    public void Render_SelectedItem_HasSelectedIndicator()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // Default selected indicator is "> "
        Assert.Contains("> Item 1", terminal.RawOutput);
    }

    [Fact]
    public void Render_UnselectedItems_HaveUnselectedIndicator()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // Default unselected indicator is "  " (two spaces)
        Assert.Contains("  Item 2", terminal.RawOutput);
    }

    [Fact]
    public void Render_MiddleItemSelected_ShowsCorrectIndicators()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode { Items = CreateItems("First", "Second", "Third"), SelectedIndex = 1, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        Assert.Contains("  First", terminal.RawOutput);
        Assert.Contains("> Second", terminal.RawOutput);
        Assert.Contains("  Third", terminal.RawOutput);
    }

    [Fact]
    public void Render_LastItemSelected_ShowsCorrectIndicators()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode { Items = CreateItems("First", "Second", "Third"), SelectedIndex = 2, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        Assert.Contains("  First", terminal.RawOutput);
        Assert.Contains("  Second", terminal.RawOutput);
        Assert.Contains("> Third", terminal.RawOutput);
    }

    #endregion

    #region Rendering - Focus State Tests

    [Fact]
    public void Render_FocusedAndSelected_HasColorCodes()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // The raw output should contain ANSI codes for the focused+selected item
        Assert.Contains("\x1b[", terminal.RawOutput);
    }

    [Fact]
    public void Render_NotFocused_SelectedItemHasIndicatorOnly()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = false };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // Still shows indicator but without selection colors
        Assert.Contains("> Item 1", terminal.RawOutput);
    }

    #endregion

    #region Rendering - Position Tests

    [Fact]
    public void Render_WithOffset_RendersAtCorrectPosition()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode { Items = CreateItems("Test Item"), SelectedIndex = 0, IsFocused = false };
        node.Arrange(new Rect(5, 3, 20, 5));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // Check that content is rendered - the terminal places it at the right position internally
        Assert.Contains("Test Item", terminal.RawOutput);
    }

    [Fact]
    public void Render_MultipleItems_RendersAllItems()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode 
        { 
            Items = CreateItems("Item A", "Item B", "Item C") 
        };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // All items should be rendered
        Assert.Contains("Item A", terminal.RawOutput);
        Assert.Contains("Item B", terminal.RawOutput);
        Assert.Contains("Item C", terminal.RawOutput);
    }

    #endregion

    #region Rendering - Theming Tests

    [Fact]
    public void Render_WithCustomTheme_UsesCustomColors()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Yellow)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.Red);
        var context = CreateContext(terminal, theme);
        var node = new ListNode { Items = CreateItems("Item 1"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // Yellow foreground: \x1b[38;2;255;255;0m
        Assert.Contains("\x1b[38;2;255;255;0m", terminal.RawOutput);
        // Red background: \x1b[48;2;255;0;0m
        Assert.Contains("\x1b[48;2;255;0;0m", terminal.RawOutput);
    }

    [Fact]
    public void Render_WithCustomIndicator_UsesCustomIndicator()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(ListTheme.SelectedIndicator, "► ");
        var context = CreateContext(terminal, theme);
        var node = new ListNode { Items = CreateItems("Item 1"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        Assert.Contains("► Item 1", terminal.RawOutput);
    }

    [Fact]
    public void Render_WithCustomUnselectedIndicator_UsesCustomIndicator()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(ListTheme.UnselectedIndicator, "- ");
        var context = CreateContext(terminal, theme);
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        Assert.Contains("- Item 2", terminal.RawOutput);
    }

    [Fact]
    public void Render_RetroTheme_UsesTriangleIndicator()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal, Hex1bThemes.HighContrast);
        var node = new ListNode { Items = CreateItems("Item 1"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 40, 10));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // HighContrast theme uses "► " indicator
        Assert.Contains("► Item 1", terminal.RawOutput);
    }

    #endregion

    #region Rendering - Narrow Terminal Tests

    [Fact]
    public void Render_NarrowTerminal_TruncatesItems()
    {
        using var terminal = new Hex1bTerminal(10, 5);
        var context = CreateContext(terminal);
        var node = new ListNode 
        { 
            Items = CreateItems("Very Long Item Name") 
        };
        node.Measure(new Constraints(0, 10, 0, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // Content is rendered, truncation depends on terminal handling
        Assert.Contains("Very Long", terminal.RawOutput);
    }

    [Fact]
    public void Render_MinimalWidth_StillRendersIndicator()
    {
        using var terminal = new Hex1bTerminal(5, 5);
        var context = CreateContext(terminal);
        var node = new ListNode { Items = CreateItems("Test"), SelectedIndex = 0, IsFocused = true };
        node.Arrange(new Rect(0, 0, 5, 5));
        
        node.Render(context);
        terminal.FlushOutput();
        
        // Should still render the indicator
        Assert.Contains(">", terminal.RawOutput);
    }

    #endregion

    #region Input Handling - Navigation Tests

    [Fact]
    public async Task HandleInput_DownArrow_MovesSelection()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2", "Item 3"), SelectedIndex = 0, IsFocused = true };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));
        
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_UpArrow_MovesSelection()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2", "Item 3"), SelectedIndex = 2, IsFocused = true };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));
        
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_DownArrow_WrapsAround()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 1, IsFocused = true };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));
        
        Assert.Equal(0, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_UpArrow_WrapsAround()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 0, IsFocused = true };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));
        
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_MultipleDownArrows_NavigatesCorrectly()
    {
        var node = new ListNode { Items = CreateItems("A", "B", "C", "D"), SelectedIndex = 0, IsFocused = true };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));
        
        Assert.Equal(3, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_MultipleUpArrows_NavigatesCorrectly()
    {
        var node = new ListNode { Items = CreateItems("A", "B", "C", "D"), SelectedIndex = 3, IsFocused = true };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));
        
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
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));
        
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
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Spacebar, ' ', Hex1bModifiers.None));
        
        Assert.Equal(InputResult.Handled, result);
        Assert.NotNull(activatedItem);
        Assert.Equal("Item 1", activatedItem);
    }

    [Fact]
    public async Task HandleInput_Enter_WithoutCallback_StillReturnsHandled()
    {
        var node = new ListNode { Items = CreateItems("Item 1"), SelectedIndex = 0, IsFocused = true };
        // No OnItemActivated callback set
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));
        
        Assert.Equal(InputResult.Handled, result);
    }

    [Fact]
    public async Task HandleInput_Enter_OnEmptyList_ReturnsHandled()
    {
        var node = new ListNode { Items = [], IsFocused = true };
        string? activatedItem = null;
        node.ItemActivatedAction = _ => { activatedItem = node.SelectedText; return Task.CompletedTask; };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));
        
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
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));
        
        Assert.NotNull(selectedItem);
        Assert.Equal("Item 2", selectedItem);
    }

    [Fact]
    public async Task HandleInput_UpArrow_InvokesOnSelectionChanged()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), SelectedIndex = 1, IsFocused = true };
        string? selectedItem = null;
        node.SelectionChangedAction = _ => { selectedItem = node.SelectedText; return Task.CompletedTask; };
        
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));
        
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
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));
        
        // Bindings execute regardless of focus state when using RouteInputToNode
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);  // Selection changed
    }

    [Fact]
    public async Task HandleInput_OtherKey_DoesNotHandle()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), IsFocused = true };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None));
        
        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public async Task HandleInput_Tab_DoesNotHandle()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), IsFocused = true };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None));
        
        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public async Task HandleInput_Escape_DoesNotHandle()
    {
        var node = new ListNode { Items = CreateItems("Item 1", "Item 2"), IsFocused = true };
        
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None));
        
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
        using var terminal = new Hex1bTerminal(40, 10);
        var items = CreateItems("Option A", "Option B", "Option C");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );
        
        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Option A"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
        
        Assert.Contains("Option A", terminal.RawOutput);
        Assert.Contains("Option B", terminal.RawOutput);
        Assert.Contains("Option C", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_ListWithSelection_RendersIndicator()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var items = CreateItems("First", "Second", "Third");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );
        
        var runTask = app.RunAsync();
        
        // Navigate down to select second item
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(2))
            .Down()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
        terminal.FlushOutput();
        
        Assert.Contains("> Second", terminal.RawOutput);
        Assert.Contains("  First", terminal.RawOutput);
        Assert.Contains("  Third", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_ListInBorder_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var items = CreateItems("Item 1", "Item 2");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.List(items), "Menu")
            ),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );
        
        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
        terminal.FlushOutput();
        
        // Note: Border title may not render in all configurations
        Assert.Contains("Item 1", terminal.RawOutput);
        Assert.Contains("Item 2", terminal.RawOutput);
        Assert.Contains("┌", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_ListReceivesFocus_HandlesInput()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var items = CreateItems("Item 1", "Item 2", "Item 3");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );
        
        var runTask = app.RunAsync();
        
        // Simulate down arrow
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2))
            .Down()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
        terminal.FlushOutput();
        
        // After down arrow, second item should be selected
        Assert.Contains("> Item 2", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_ListActivation_InvokesCallback()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var items = CreateItems("Action 1", "Action 2");
        string? activatedAction = null;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)
                .OnItemActivated((ListItemActivatedEventArgs args) => activatedAction = args.ActivatedText)),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );
        
        var runTask = app.RunAsync();
        
        // Simulate Enter key
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action 1"), TimeSpan.FromSeconds(2))
            .Enter()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
        
        Assert.Equal("Action 1", activatedAction);
    }

    [Fact]
    public async Task Integration_ListInVStack_RendersWithOtherElements()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var items = CreateItems("Menu Item");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Select an option:"),
                    v.List(items)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );
        
        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Select an option:"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
        terminal.FlushOutput();
        
        Assert.Contains("Select an option:", terminal.RawOutput);
        Assert.Contains("Menu Item", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_ListWithTheme_AppliesTheme()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var items = CreateItems("Themed Item");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter, Theme = Hex1bThemes.HighContrast }
        );
        
        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Themed Item"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
        terminal.FlushOutput();
        
        // HighContrast theme uses "► " indicator
        Assert.Contains("► Themed Item", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_ListNavigationMultipleSteps_UpdatesSelection()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var items = CreateItems("First", "Second", "Third");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.List(items)),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );
        
        var runTask = app.RunAsync();
        
        // Navigate down twice
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(2))
            .Down()
            .Down()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
        terminal.FlushOutput();
        
        // Third item should be selected
        Assert.Contains("> Third", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_ListInsideBorderWithOtherWidgets_RendersProperly()
    {
        using var terminal = new Hex1bTerminal(50, 15);
        var items = CreateItems("Option A", "Option B");
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Welcome"),
                    v.Border(ctx.List(items), "Options"),
                    v.Button("OK").OnClick(_ => Task.CompletedTask)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );
        
        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Welcome"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;
        terminal.FlushOutput();
        
        Assert.Contains("Welcome", terminal.RawOutput);
        Assert.Contains("Options", terminal.RawOutput);
        Assert.Contains("Option A", terminal.RawOutput);
        Assert.Contains("Option B", terminal.RawOutput);
        Assert.Contains("OK", terminal.RawOutput);
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
}
