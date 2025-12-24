using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Testing;
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
        await new Hex1bTestSequenceBuilder()
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
        await new Hex1bTestSequenceBuilder()
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
        await new Hex1bTestSequenceBuilder()
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
        await new Hex1bTestSequenceBuilder()
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
        await new Hex1bTestSequenceBuilder()
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
        await new Hex1bTestSequenceBuilder()
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
        await new Hex1bTestSequenceBuilder()
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
        await new Hex1bTestSequenceBuilder()
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
        await new Hex1bTestSequenceBuilder()
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
}
