using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for mouse-driven text selection on TerminalWidgetHandle.
/// </summary>
[TestClass]
public class TerminalMouseSelectionTests
{
    private static TerminalWidgetHandle CreateHandle(int width = 80, int height = 24)
    {
        return new TerminalWidgetHandle(width, height);
    }

    private static async Task WriteText(TerminalWidgetHandle handle, string text, int row, int startCol = 0)
    {
        var impacts = new List<CellImpact>();
        for (int i = 0; i < text.Length; i++)
        {
            impacts.Add(new CellImpact(startCol + i, row,
                new TerminalCell(text[i].ToString(), null, null)));
        }
        var tokens = new List<AppliedToken>
        {
            new(new TextToken(text), impacts, startCol, row, startCol + text.Length, row)
        };
        await handle.WriteOutputWithImpactsAsync(tokens);
    }

    [TestMethod]
    public async Task MouseSelect_DownOnly_DoesNotEnterCopyMode()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "Hello World", 0);
        
        handle.MouseSelect(2, 0, MouseAction.Down, SelectionMode.Character);
        
        // Single click without drag should NOT enter copy mode
        Assert.IsFalse(handle.IsInCopyMode);
    }

    [TestMethod]
    public async Task MouseSelect_DownThenDrag_EntersCopyModeAndStartsSelection()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "Hello World", 0);
        
        handle.MouseSelect(2, 0, MouseAction.Down, SelectionMode.Character);
        handle.MouseSelect(5, 0, MouseAction.Drag, SelectionMode.Character);
        
        Assert.IsTrue(handle.IsInCopyMode);
        Assert.IsNotNull(handle.Selection);
        Assert.IsTrue(handle.Selection!.IsSelecting);
        Assert.AreEqual(SelectionMode.Character, handle.Selection.Mode);
    }

    [TestMethod]
    public async Task MouseSelect_Drag_ExtendsSelection()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "Hello World", 0);
        
        handle.MouseSelect(0, 0, MouseAction.Down, SelectionMode.Character);
        handle.MouseSelect(4, 0, MouseAction.Drag, SelectionMode.Character);
        
        var sel = handle.Selection!;
        // Anchor at (0,0), cursor at (0,4)
        Assert.AreEqual(0, sel.Start.Column);
        Assert.AreEqual(4, sel.Cursor.Column);
    }

    [TestMethod]
    public async Task MouseSelect_MultiRow_CharacterMode()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "Line one", 0);
        await WriteText(handle, "Line two", 1);
        
        handle.MouseSelect(5, 0, MouseAction.Down, SelectionMode.Character);
        handle.MouseSelect(3, 1, MouseAction.Drag, SelectionMode.Character);
        
        var sel = handle.Selection!;
        Assert.AreEqual(0, sel.Start.Row);
        Assert.AreEqual(1, sel.End.Row);
        
        // Extract text
        var text = sel.ExtractText(handle.GetVirtualCell, handle.Width);
        Assert.Contains("one", text);
        Assert.Contains("Line", text);
    }

    [TestMethod]
    public async Task MouseSelect_LineMode_SelectsFullRows()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "AAAA", 0);
        await WriteText(handle, "BBBB", 1);
        await WriteText(handle, "CCCC", 2);
        
        handle.MouseSelect(2, 0, MouseAction.Down, SelectionMode.Line);
        handle.MouseSelect(2, 1, MouseAction.Drag, SelectionMode.Line);
        
        var sel = handle.Selection!;
        Assert.AreEqual(SelectionMode.Line, sel.Mode);
        // Line mode selects full rows regardless of column
        Assert.IsTrue(sel.IsCellSelected(0, 0));
        Assert.IsTrue(sel.IsCellSelected(0, 3));
        Assert.IsTrue(sel.IsCellSelected(1, 0));
        Assert.IsFalse(sel.IsCellSelected(2, 0));
    }

    [TestMethod]
    public async Task MouseSelect_BlockMode_SelectsRectangle()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "ABCDEF", 0);
        await WriteText(handle, "GHIJKL", 1);
        await WriteText(handle, "MNOPQR", 2);
        
        handle.MouseSelect(1, 0, MouseAction.Down, SelectionMode.Block);
        handle.MouseSelect(3, 2, MouseAction.Drag, SelectionMode.Block);
        
        var sel = handle.Selection!;
        Assert.AreEqual(SelectionMode.Block, sel.Mode);
        // Block: columns 1-3, rows 0-2
        Assert.IsTrue(sel.IsCellSelected(0, 1));
        Assert.IsTrue(sel.IsCellSelected(1, 2));
        Assert.IsTrue(sel.IsCellSelected(2, 3));
        Assert.IsFalse(sel.IsCellSelected(0, 0)); // column 0 outside
        Assert.IsFalse(sel.IsCellSelected(0, 4)); // column 4 outside
    }

    [TestMethod]
    public async Task MouseSelect_Up_KeepsSelectionActive()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "Hello", 0);
        
        handle.MouseSelect(0, 0, MouseAction.Down, SelectionMode.Character);
        handle.MouseSelect(4, 0, MouseAction.Drag, SelectionMode.Character);
        handle.MouseSelect(4, 0, MouseAction.Up, SelectionMode.Character);
        
        // Selection should still be active after mouse up
        Assert.IsTrue(handle.IsInCopyMode);
        Assert.IsTrue(handle.Selection!.IsSelecting);
    }

    [TestMethod]
    public void MouseSelect_DragWhenAlreadyInCopyMode_SetsNewSelection()
    {
        var handle = CreateHandle(20, 5);
        handle.EnterCopyMode();
        
        // Click and drag to start new selection
        handle.MouseSelect(5, 2, MouseAction.Down, SelectionMode.Character);
        handle.MouseSelect(8, 2, MouseAction.Drag, SelectionMode.Character);
        
        Assert.IsTrue(handle.Selection!.IsSelecting);
        Assert.AreEqual(2, handle.Selection.Anchor.Row);
        Assert.AreEqual(5, handle.Selection.Anchor.Column);
    }

    [TestMethod]
    public async Task MouseSelect_CopySelection_ExtractsCorrectText()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "Hello World!", 0);
        
        string? copiedText = null;
        handle.TextCopied += text => copiedText = text;
        
        // Select "World"
        handle.MouseSelect(6, 0, MouseAction.Down, SelectionMode.Character);
        handle.MouseSelect(10, 0, MouseAction.Drag, SelectionMode.Character);
        
        var result = handle.CopySelection();
        
        Assert.AreEqual("World", result);
        Assert.AreEqual("World", copiedText);
    }

    [TestMethod]
    public void MouseSelect_ClampsToBufferBounds()
    {
        var handle = CreateHandle(10, 5);
        
        // Click and drag outside bounds
        handle.MouseSelect(100, 100, MouseAction.Down, SelectionMode.Character);
        handle.MouseSelect(100, 100, MouseAction.Drag, SelectionMode.Character);
        
        Assert.IsTrue(handle.IsInCopyMode);
        var pos = handle.Selection!.Cursor;
        Assert.IsTrue(pos.Column < handle.Width);
        Assert.IsTrue(pos.Row < handle.VirtualBufferHeight);
    }
}
