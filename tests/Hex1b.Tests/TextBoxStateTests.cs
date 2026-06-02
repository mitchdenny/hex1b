using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TextBoxState input handling.
/// </summary>
[TestClass]
public class TextBoxStateTests
{
    [TestMethod]
    public void InitialState_IsEmpty()
    {
        var state = new TextBoxState();
        
        Assert.AreEqual("", state.Text);
        Assert.AreEqual(0, state.CursorPosition);
        Assert.IsFalse(state.HasSelection);
    }

    [TestMethod]
    public void HandleInput_CharacterKey_InsertsText()
    {
        var state = new TextBoxState();
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.H, 'h', Hex1bModifiers.None));
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.I, 'i', Hex1bModifiers.None));
        
        Assert.AreEqual("hi", state.Text);
        Assert.AreEqual(2, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_Backspace_DeletesBeforeCursor()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None));
        
        Assert.AreEqual("hell", state.Text);
        Assert.AreEqual(4, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_Backspace_AtStart_DoesNothing()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 0 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None));
        
        Assert.AreEqual("hello", state.Text);
        Assert.AreEqual(0, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_Delete_DeletesAtCursor()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 0 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None));
        
        Assert.AreEqual("ello", state.Text);
        Assert.AreEqual(0, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_Delete_AtEnd_DoesNothing()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None));
        
        Assert.AreEqual("hello", state.Text);
        Assert.AreEqual(5, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_LeftArrow_MovesCursorLeft()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 3 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));
        
        Assert.AreEqual(2, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_LeftArrow_AtStart_StaysAtStart()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 0 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));
        
        Assert.AreEqual(0, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_RightArrow_MovesCursorRight()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 2 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));
        
        Assert.AreEqual(3, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_RightArrow_AtEnd_StaysAtEnd()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));
        
        Assert.AreEqual(5, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_Home_MovesCursorToStart()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 3 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None));
        
        Assert.AreEqual(0, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_End_MovesCursorToEnd()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 0 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.None));
        
        Assert.AreEqual(5, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_ShiftLeftArrow_StartsSelection()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 3 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.Shift));
        
        Assert.IsTrue(state.HasSelection);
        Assert.AreEqual(2, state.CursorPosition);
        Assert.AreEqual(2, state.SelectionStart);
        Assert.AreEqual(3, state.SelectionEnd);
    }

    [TestMethod]
    public void HandleInput_ShiftRightArrow_ExtendsSelection()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 1 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.Shift));
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.Shift));
        
        Assert.IsTrue(state.HasSelection);
        Assert.AreEqual(3, state.CursorPosition);
        Assert.AreEqual(1, state.SelectionStart);
        Assert.AreEqual(3, state.SelectionEnd);
        Assert.AreEqual("el", state.SelectedText);
    }

    [TestMethod]
    public void HandleInput_LeftArrow_WithSelection_MovesToSelectionStart()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 4, SelectionAnchor = 1 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));
        
        Assert.IsFalse(state.HasSelection);
        Assert.AreEqual(1, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_RightArrow_WithSelection_MovesToSelectionEnd()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 1, SelectionAnchor = 4 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));
        
        Assert.IsFalse(state.HasSelection);
        Assert.AreEqual(4, state.CursorPosition);
    }

    [TestMethod]
    public void HandleInput_Backspace_WithSelection_DeletesSelection()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 8, SelectionAnchor = 5 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None));
        
        // Selection from 5 to 8 is " wo", so "hello world" becomes "hellorld"
        Assert.AreEqual("hellorld", state.Text);
        Assert.AreEqual(5, state.CursorPosition);
        Assert.IsFalse(state.HasSelection);
    }

    [TestMethod]
    public void HandleInput_Delete_WithSelection_DeletesSelection()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 3, SelectionAnchor = 8 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None));
        
        Assert.AreEqual("helrld", state.Text);
        Assert.AreEqual(3, state.CursorPosition);
        Assert.IsFalse(state.HasSelection);
    }

    [TestMethod]
    public void HandleInput_Type_WithSelection_ReplacesSelection()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 6, SelectionAnchor = 11 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None));
        
        Assert.AreEqual("hello X", state.Text);
        Assert.AreEqual(7, state.CursorPosition);
        Assert.IsFalse(state.HasSelection);
    }

    [TestMethod]
    public void HandleInput_CtrlA_SelectsAll()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 3 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.Control));
        
        Assert.IsTrue(state.HasSelection);
        Assert.AreEqual(0, state.SelectionStart);
        Assert.AreEqual(11, state.SelectionEnd);
        Assert.AreEqual("hello world", state.SelectedText);
    }

    [TestMethod]
    public void HandleInput_CtrlA_OnEmptyText_DoesNothing()
    {
        var state = new TextBoxState { Text = "", CursorPosition = 0 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.Control));
        
        Assert.IsFalse(state.HasSelection);
    }

    [TestMethod]
    public void HandleInput_ShiftHome_SelectsToStart()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 6 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.Shift));
        
        Assert.IsTrue(state.HasSelection);
        Assert.AreEqual(0, state.SelectionStart);
        Assert.AreEqual(6, state.SelectionEnd);
    }

    [TestMethod]
    public void HandleInput_ShiftEnd_SelectsToEnd()
    {
        var state = new TextBoxState { Text = "hello world", CursorPosition = 3 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.Shift));
        
        Assert.IsTrue(state.HasSelection);
        Assert.AreEqual(3, state.SelectionStart);
        Assert.AreEqual(11, state.SelectionEnd);
    }

    [TestMethod]
    public void SelectAll_SetsCorrectAnchorAndCursor()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 2 };
        
        state.SelectAll();
        
        Assert.AreEqual(0, state.SelectionAnchor);
        Assert.AreEqual(5, state.CursorPosition);
    }

    [TestMethod]
    public void ClearSelection_RemovesSelection()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 4, SelectionAnchor = 1 };
        
        state.ClearSelection();
        
        Assert.IsFalse(state.HasSelection);
        Assert.IsNull(state.SelectionAnchor);
    }

    [TestMethod]
    public void InsertInMiddle_InsertsCorrectly()
    {
        var state = new TextBoxState { Text = "hllo", CursorPosition = 1 };
        
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.E, 'e', Hex1bModifiers.None));
        
        Assert.AreEqual("hello", state.Text);
        Assert.AreEqual(2, state.CursorPosition);
    }
}
