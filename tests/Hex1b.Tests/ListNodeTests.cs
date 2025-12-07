using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ListNode rendering and input handling.
/// </summary>
public class ListNodeTests
{
    private static Hex1bRenderContext CreateContext(Hex1bTerminal terminal)
    {
        return new Hex1bRenderContext(terminal);
    }

    private static ListState CreateListState(params string[] items)
    {
        return new ListState
        {
            Items = items.Select((text, i) => new ListItem($"item{i}", text)).ToList()
        };
    }

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var node = new ListNode 
        { 
            State = CreateListState("Short", "Longer Item", "Med")
        };
        
        var size = node.Measure(Constraints.Unbounded);
        
        // Width = max item length + 2 (indicator), Height = item count
        Assert.Equal(13, size.Width); // "Longer Item" = 11 + 2
        Assert.Equal(3, size.Height);
    }

    [Fact]
    public void Measure_EmptyList_HasMinHeight()
    {
        var node = new ListNode { State = new ListState { Items = [] } };
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Render_ShowsAllItems()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var node = new ListNode 
        { 
            State = CreateListState("Item 1", "Item 2", "Item 3")
        };
        
        node.Render(context);
        
        Assert.Contains("Item 1", terminal.GetScreenText());
        Assert.Contains("Item 2", terminal.GetScreenText());
        Assert.Contains("Item 3", terminal.GetScreenText());
    }

    [Fact]
    public void Render_SelectedItem_HasIndicator()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = CreateContext(terminal);
        var state = CreateListState("Item 1", "Item 2");
        state.SelectedIndex = 1;
        var node = new ListNode 
        { 
            State = state,
            IsFocused = true
        };
        
        node.Render(context);
        
        // The raw output should contain ANSI codes for the selected item
        Assert.Contains("\x1b[", terminal.RawOutput);
    }

    [Fact]
    public void HandleInput_DownArrow_MovesSelection()
    {
        var state = CreateListState("Item 1", "Item 2", "Item 3");
        state.SelectedIndex = 0;
        var node = new ListNode { State = state, IsFocused = true };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        
        Assert.True(handled);
        Assert.Equal(1, state.SelectedIndex);
    }

    [Fact]
    public void HandleInput_UpArrow_MovesSelection()
    {
        var state = CreateListState("Item 1", "Item 2", "Item 3");
        state.SelectedIndex = 2;
        var node = new ListNode { State = state, IsFocused = true };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.UpArrow, '\0', false, false, false));
        
        Assert.True(handled);
        Assert.Equal(1, state.SelectedIndex);
    }

    [Fact]
    public void HandleInput_DownArrow_WrapsAround()
    {
        var state = CreateListState("Item 1", "Item 2");
        state.SelectedIndex = 1;
        var node = new ListNode { State = state, IsFocused = true };
        
        node.HandleInput(new KeyInputEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        
        Assert.Equal(0, state.SelectedIndex);
    }

    [Fact]
    public void HandleInput_UpArrow_WrapsAround()
    {
        var state = CreateListState("Item 1", "Item 2");
        state.SelectedIndex = 0;
        var node = new ListNode { State = state, IsFocused = true };
        
        node.HandleInput(new KeyInputEvent(ConsoleKey.UpArrow, '\0', false, false, false));
        
        Assert.Equal(1, state.SelectedIndex);
    }

    [Fact]
    public void HandleInput_Enter_InvokesOnSelectionChanged()
    {
        var state = CreateListState("Item 1", "Item 2");
        ListItem? changedItem = null;
        state.OnSelectionChanged = item => changedItem = item;
        state.SelectedIndex = 1;
        var node = new ListNode { State = state, IsFocused = true };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.Enter, '\r', false, false, false));
        
        Assert.True(handled);
        Assert.NotNull(changedItem);
        Assert.Equal("Item 2", changedItem.Text);
    }

    [Fact]
    public void HandleInput_NotFocused_DoesNotHandle()
    {
        var state = CreateListState("Item 1", "Item 2");
        state.SelectedIndex = 0;
        var node = new ListNode { State = state, IsFocused = false };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        
        Assert.False(handled);
        Assert.Equal(0, state.SelectedIndex);
    }

    [Fact]
    public void HandleInput_OtherKey_DoesNotHandle()
    {
        var state = CreateListState("Item 1", "Item 2");
        var node = new ListNode { State = state, IsFocused = true };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.A, 'a', false, false, false));
        
        Assert.False(handled);
    }

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new ListNode();
        
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new ListNode { State = CreateListState("Test") };
        var bounds = new Rect(0, 0, 40, 10);
        
        node.Arrange(bounds);
        
        Assert.Equal(bounds, node.Bounds);
    }
}
