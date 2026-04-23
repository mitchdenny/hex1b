using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for mouse-driven text selection on TerminalWidgetHandle.
/// </summary>
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

    [Fact]
    public async Task MouseSelect_Down_EntersCopyModeAndStartsSelection()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "Hello World", 0);
        
        handle.MouseSelect(2, 0, MouseAction.Down, SelectionMode.Character);
        
        Assert.True(handle.IsInCopyMode);
        Assert.NotNull(handle.Selection);
        Assert.True(handle.Selection!.IsSelecting);
        Assert.Equal(SelectionMode.Character, handle.Selection.Mode);
    }

    [Fact]
    public async Task MouseSelect_Drag_ExtendsSelection()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "Hello World", 0);
        
        handle.MouseSelect(0, 0, MouseAction.Down, SelectionMode.Character);
        handle.MouseSelect(4, 0, MouseAction.Drag, SelectionMode.Character);
        
        var sel = handle.Selection!;
        // Anchor at (0,0), cursor at (0,4)
        Assert.Equal(0, sel.Start.Column);
        Assert.Equal(4, sel.Cursor.Column);
    }

    [Fact]
    public async Task MouseSelect_MultiRow_CharacterMode()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "Line one", 0);
        await WriteText(handle, "Line two", 1);
        
        handle.MouseSelect(5, 0, MouseAction.Down, SelectionMode.Character);
        handle.MouseSelect(3, 1, MouseAction.Drag, SelectionMode.Character);
        
        var sel = handle.Selection!;
        Assert.Equal(0, sel.Start.Row);
        Assert.Equal(1, sel.End.Row);
        
        // Extract text
        var text = sel.ExtractText(handle.GetVirtualCell, handle.Width);
        Assert.Contains("one", text);
        Assert.Contains("Line", text);
    }

    [Fact]
    public async Task MouseSelect_LineMode_SelectsFullRows()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "AAAA", 0);
        await WriteText(handle, "BBBB", 1);
        await WriteText(handle, "CCCC", 2);
        
        handle.MouseSelect(2, 0, MouseAction.Down, SelectionMode.Line);
        handle.MouseSelect(2, 1, MouseAction.Drag, SelectionMode.Line);
        
        var sel = handle.Selection!;
        Assert.Equal(SelectionMode.Line, sel.Mode);
        // Line mode selects full rows regardless of column
        Assert.True(sel.IsCellSelected(0, 0));
        Assert.True(sel.IsCellSelected(0, 3));
        Assert.True(sel.IsCellSelected(1, 0));
        Assert.False(sel.IsCellSelected(2, 0));
    }

    [Fact]
    public async Task MouseSelect_BlockMode_SelectsRectangle()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "ABCDEF", 0);
        await WriteText(handle, "GHIJKL", 1);
        await WriteText(handle, "MNOPQR", 2);
        
        handle.MouseSelect(1, 0, MouseAction.Down, SelectionMode.Block);
        handle.MouseSelect(3, 2, MouseAction.Drag, SelectionMode.Block);
        
        var sel = handle.Selection!;
        Assert.Equal(SelectionMode.Block, sel.Mode);
        // Block: columns 1-3, rows 0-2
        Assert.True(sel.IsCellSelected(0, 1));
        Assert.True(sel.IsCellSelected(1, 2));
        Assert.True(sel.IsCellSelected(2, 3));
        Assert.False(sel.IsCellSelected(0, 0)); // column 0 outside
        Assert.False(sel.IsCellSelected(0, 4)); // column 4 outside
    }

    [Fact]
    public async Task MouseSelect_Up_KeepsSelectionActive()
    {
        var handle = CreateHandle(20, 5);
        await WriteText(handle, "Hello", 0);
        
        handle.MouseSelect(0, 0, MouseAction.Down, SelectionMode.Character);
        handle.MouseSelect(4, 0, MouseAction.Drag, SelectionMode.Character);
        handle.MouseSelect(4, 0, MouseAction.Up, SelectionMode.Character);
        
        // Selection should still be active after mouse up
        Assert.True(handle.IsInCopyMode);
        Assert.True(handle.Selection!.IsSelecting);
    }

    [Fact]
    public void MouseSelect_Down_WhenAlreadyInCopyMode_ResetsSelection()
    {
        var handle = CreateHandle(20, 5);
        handle.EnterCopyMode();
        
        // Click to start new selection
        handle.MouseSelect(5, 2, MouseAction.Down, SelectionMode.Character);
        
        Assert.True(handle.Selection!.IsSelecting);
        Assert.Equal(2, handle.Selection.Cursor.Row);
        Assert.Equal(5, handle.Selection.Cursor.Column);
    }

    [Fact]
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
        
        Assert.Equal("World", result);
        Assert.Equal("World", copiedText);
    }

    [Fact]
    public void MouseSelect_ClampsToBufferBounds()
    {
        var handle = CreateHandle(10, 5);
        
        // Click outside bounds
        handle.MouseSelect(100, 100, MouseAction.Down, SelectionMode.Character);
        
        Assert.True(handle.IsInCopyMode);
        var pos = handle.Selection!.Cursor;
        Assert.True(pos.Column < handle.Width);
        Assert.True(pos.Row < handle.VirtualBufferHeight);
    }
}
