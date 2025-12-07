using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ListState navigation and selection.
/// </summary>
public class ListStateTests
{
    private static ListState CreateListWithItems(params string[] items)
    {
        return new ListState
        {
            Items = items.Select((text, i) => new ListItem($"item{i}", text)).ToList()
        };
    }

    [Fact]
    public void InitialState_HasEmptyItems()
    {
        var state = new ListState();
        
        Assert.Empty(state.Items);
        Assert.Equal(0, state.SelectedIndex);
        Assert.Null(state.SelectedItem);
    }

    [Fact]
    public void SelectedItem_ReturnsCorrectItem()
    {
        var state = CreateListWithItems("Item 1", "Item 2", "Item 3");
        state.SelectedIndex = 1;
        
        Assert.Equal("Item 2", state.SelectedItem?.Text);
    }

    [Fact]
    public void MoveDown_IncrementsIndex()
    {
        var state = CreateListWithItems("Item 1", "Item 2", "Item 3");
        state.SelectedIndex = 0;
        
        state.MoveDown();
        
        Assert.Equal(1, state.SelectedIndex);
    }

    [Fact]
    public void MoveDown_WrapsToStart()
    {
        var state = CreateListWithItems("Item 1", "Item 2", "Item 3");
        state.SelectedIndex = 2;
        
        state.MoveDown();
        
        Assert.Equal(0, state.SelectedIndex);
    }

    [Fact]
    public void MoveUp_DecrementsIndex()
    {
        var state = CreateListWithItems("Item 1", "Item 2", "Item 3");
        state.SelectedIndex = 2;
        
        state.MoveUp();
        
        Assert.Equal(1, state.SelectedIndex);
    }

    [Fact]
    public void MoveUp_WrapsToEnd()
    {
        var state = CreateListWithItems("Item 1", "Item 2", "Item 3");
        state.SelectedIndex = 0;
        
        state.MoveUp();
        
        Assert.Equal(2, state.SelectedIndex);
    }

    [Fact]
    public void MoveDown_EmptyList_DoesNothing()
    {
        var state = new ListState { Items = [] };
        
        state.MoveDown();
        
        Assert.Equal(0, state.SelectedIndex);
    }

    [Fact]
    public void MoveUp_EmptyList_DoesNothing()
    {
        var state = new ListState { Items = [] };
        
        state.MoveUp();
        
        Assert.Equal(0, state.SelectedIndex);
    }

    [Fact]
    public void MoveDown_InvokesOnSelectionChanged()
    {
        var state = CreateListWithItems("Item 1", "Item 2");
        ListItem? changedItem = null;
        state.OnSelectionChanged = item => changedItem = item;
        
        state.MoveDown();
        
        Assert.NotNull(changedItem);
        Assert.Equal("Item 2", changedItem.Text);
    }

    [Fact]
    public void MoveUp_InvokesOnSelectionChanged()
    {
        var state = CreateListWithItems("Item 1", "Item 2");
        state.SelectedIndex = 1;
        ListItem? changedItem = null;
        state.OnSelectionChanged = item => changedItem = item;
        
        state.MoveUp();
        
        Assert.NotNull(changedItem);
        Assert.Equal("Item 1", changedItem.Text);
    }

    [Fact]
    public void SelectedItem_OutOfRange_ReturnsNull()
    {
        var state = CreateListWithItems("Item 1");
        state.SelectedIndex = 5;
        
        Assert.Null(state.SelectedItem);
    }

    [Fact]
    public void SelectedItem_NegativeIndex_ReturnsNull()
    {
        var state = CreateListWithItems("Item 1");
        state.SelectedIndex = -1;
        
        Assert.Null(state.SelectedItem);
    }
}
