using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TextBoxState input handling.
/// </summary>
public class TextBoxStateTests
{
    [Fact]
    public void InitialState_IsEmpty()
    {
        var state = new TextBoxState();
        
        Assert.Equal("", state.Text);
        Assert.Equal(0, state.CursorPosition);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void HandleInput_CharacterKey_InsertsText()
    {
        var state = new TextBoxState();
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.H, 'h', false, false, false));
        state.HandleInput(new KeyInputEvent(ConsoleKey.I, 'i', false, false, false));
        
        Assert.Equal("hi", state.Text);
        Assert.Equal(2, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_Backspace_DeletesBeforeCursor()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.Backspace, '\b', false, false, false));
        
        Assert.Equal("hell", state.Text);
        Assert.Equal(4, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_Backspace_AtStart_DoesNothing()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 0 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.Backspace, '\b', false, false, false));
        
        Assert.Equal("hello", state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_Delete_DeletesAtCursor()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 0 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.Delete, '\0', false, false, false));
        
        Assert.Equal("ello", state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_Delete_AtEnd_DoesNothing()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.Delete, '\0', false, false, false));
        
        Assert.Equal("hello", state.Text);
        Assert.Equal(5, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_LeftArrow_MovesCursorLeft()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 3 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.LeftArrow, '\0', false, false, false));
        
        Assert.Equal(2, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_LeftArrow_AtStart_StaysAtStart()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 0 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.LeftArrow, '\0', false, false, false));
        
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_RightArrow_MovesCursorRight()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 2 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        
        Assert.Equal(3, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_RightArrow_AtEnd_StaysAtEnd()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        
        Assert.Equal(5, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_Home_MovesCursorToStart()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 3 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.Home, '\0', false, false, false));
        
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_End_MovesCursorToEnd()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 0 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.End, '\0', false, false, false));
        
        Assert.Equal(5, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_ShiftLeftArrow_StartsSelection()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 3 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.LeftArrow, '\0', true, false, false));
        
        Assert.True(state.HasSelection);
        Assert.Equal(2, state.CursorPosition);
        Assert.Equal(2, state.SelectionStart);
        Assert.Equal(3, state.SelectionEnd);
    }

    [Fact]
    public void HandleInput_ShiftRightArrow_ExtendsSelection()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 1 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.RightArrow, '\0', true, false, false));
        state.HandleInput(new KeyInputEvent(ConsoleKey.RightArrow, '\0', true, false, false));
        
        Assert.True(state.HasSelection);
        Assert.Equal(3, state.CursorPosition);
        Assert.Equal(1, state.SelectionStart);
        Assert.Equal(3, state.SelectionEnd);
        Assert.Equal("el", state.SelectedText);
    }

    [Fact]
    public void HandleInput_LeftArrow_WithSelection_MovesToSelectionStart()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 4, SelectionAnchor = 1 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.LeftArrow, '\0', false, false, false));
        
        Assert.False(state.HasSelection);
        Assert.Equal(1, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_RightArrow_WithSelection_MovesToSelectionEnd()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 1, SelectionAnchor = 4 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.RightArrow, '\0', false, false, false));
        
        Assert.False(state.HasSelection);
        Assert.Equal(4, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_Backspace_WithSelection_DeletesSelection()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 8, SelectionAnchor = 5 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.Backspace, '\b', false, false, false));
        
        // Selection from 5 to 8 is " wo", so "hello world" becomes "hellorld"
        Assert.Equal("hellorld", state.Text);
        Assert.Equal(5, state.CursorPosition);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void HandleInput_Delete_WithSelection_DeletesSelection()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 3, SelectionAnchor = 8 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.Delete, '\0', false, false, false));
        
        Assert.Equal("helrld", state.Text);
        Assert.Equal(3, state.CursorPosition);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void HandleInput_Type_WithSelection_ReplacesSelection()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 6, SelectionAnchor = 11 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.X, 'X', false, false, false));
        
        Assert.Equal("hello X", state.Text);
        Assert.Equal(7, state.CursorPosition);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void HandleInput_CtrlA_SelectsAll()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 3 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.A, 'a', false, false, true));
        
        Assert.True(state.HasSelection);
        Assert.Equal(0, state.SelectionStart);
        Assert.Equal(11, state.SelectionEnd);
        Assert.Equal("hello world", state.SelectedText);
    }

    [Fact]
    public void HandleInput_CtrlA_OnEmptyText_DoesNothing()
    {
        var state = new TextBoxState { Text = "", CursorPosition = 0 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.A, 'a', false, false, true));
        
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void HandleInput_ShiftHome_SelectsToStart()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 6 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.Home, '\0', true, false, false));
        
        Assert.True(state.HasSelection);
        Assert.Equal(0, state.SelectionStart);
        Assert.Equal(6, state.SelectionEnd);
    }

    [Fact]
    public void HandleInput_ShiftEnd_SelectsToEnd()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 3 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.End, '\0', true, false, false));
        
        Assert.True(state.HasSelection);
        Assert.Equal(3, state.SelectionStart);
        Assert.Equal(11, state.SelectionEnd);
    }

    [Fact]
    public void SelectAll_SetsCorrectAnchorAndCursor()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 2 };
        
        state.SelectAll();
        
        Assert.Equal(0, state.SelectionAnchor);
        Assert.Equal(5, state.CursorPosition);
    }

    [Fact]
    public void ClearSelection_RemovesSelection()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 4, SelectionAnchor = 1 };
        
        state.ClearSelection();
        
        Assert.False(state.HasSelection);
        Assert.Null(state.SelectionAnchor);
    }

    [Fact]
    public void InsertInMiddle_InsertsCorrectly()
    {
        var state = new TextBoxState { Text = "hllo", CursorPosition = 1 };
        
        state.HandleInput(new KeyInputEvent(ConsoleKey.E, 'e', false, false, false));
        
        Assert.Equal("hello", state.Text);
        Assert.Equal(2, state.CursorPosition);
    }
}
