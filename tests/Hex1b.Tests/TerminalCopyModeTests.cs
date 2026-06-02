using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for copy mode on TerminalWidgetHandle — output queuing, enter/exit lifecycle,
/// selection integration, and text extraction.
/// </summary>
[TestClass]
public class TerminalCopyModeTests
{
    private static TerminalWidgetHandle CreateHandle(int width = 80, int height = 24)
    {
        return new TerminalWidgetHandle(width, height);
    }

    private static IReadOnlyList<AppliedToken> MakeCellImpacts(string text, int row = 0, int startCol = 0)
    {
        var impacts = new List<CellImpact>();
        for (int i = 0; i < text.Length; i++)
        {
            impacts.Add(new CellImpact(startCol + i, row,
                new TerminalCell(text[i].ToString(), null, null)));
        }
        
        return new List<AppliedToken>
        {
            new(new TextToken(text), impacts, startCol, row, startCol + text.Length, row)
        };
    }

    [TestMethod]
    public void EnterCopyMode_SetsIsInCopyMode()
    {
        var handle = CreateHandle();
        Assert.IsFalse(handle.IsInCopyMode);
        
        handle.EnterCopyMode();
        Assert.IsTrue(handle.IsInCopyMode);
    }

    [TestMethod]
    public void EnterCopyMode_CreatesSelection()
    {
        var handle = CreateHandle(80, 24);
        handle.EnterCopyMode();
        
        Assert.IsNotNull(handle.Selection);
        // Cursor should be at terminal's current cursor position (0,0 for fresh handle)
        Assert.AreEqual(0, handle.Selection!.Cursor.Row);
        Assert.AreEqual(0, handle.Selection.Cursor.Column);
    }

    [TestMethod]
    public void ExitCopyMode_ClearsState()
    {
        var handle = CreateHandle();
        handle.EnterCopyMode();
        handle.ExitCopyMode();
        
        Assert.IsFalse(handle.IsInCopyMode);
        Assert.IsNull(handle.Selection);
    }

    [TestMethod]
    public void EnterCopyMode_DoubleEntry_NoOp()
    {
        var handle = CreateHandle();
        handle.EnterCopyMode();
        var selection1 = handle.Selection;
        
        handle.EnterCopyMode(); // should be no-op
        Assert.AreSame(selection1, handle.Selection);
    }

    [TestMethod]
    public async Task OutputQueuing_WhenInCopyMode_DoesNotApplyToBuffer()
    {
        var handle = CreateHandle(10, 5);
        
        // Write initial content
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("Hello", row: 0));
        Assert.AreEqual("H", handle.GetCell(0, 0).Character);
        
        // Enter copy mode
        handle.EnterCopyMode();
        
        // Write new content — should be queued, not applied
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("World", row: 0));
        
        // Buffer should still show original content
        Assert.AreEqual("H", handle.GetCell(0, 0).Character);
    }

    [TestMethod]
    public async Task OutputQueuing_OnExit_FlushesQueuedOutput()
    {
        var handle = CreateHandle(10, 5);
        
        // Write initial content
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("Hello", row: 0));
        
        // Enter copy mode and write new content
        handle.EnterCopyMode();
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("World", row: 0));
        
        // Exit copy mode — queued output should be flushed
        handle.ExitCopyMode();
        
        Assert.AreEqual("W", handle.GetCell(0, 0).Character);
        Assert.AreEqual("o", handle.GetCell(1, 0).Character);
    }

    [TestMethod]
    public async Task OutputQueuing_MultipleChunks_AllFlushedInOrder()
    {
        var handle = CreateHandle(10, 5);
        handle.EnterCopyMode();
        
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("AA", row: 0));
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("BB", row: 0)); // overwrites
        
        handle.ExitCopyMode();
        
        // Second write should overwrite first
        Assert.AreEqual("B", handle.GetCell(0, 0).Character);
        Assert.AreEqual("B", handle.GetCell(1, 0).Character);
    }

    [TestMethod]
    public void CopyModeChanged_FiredOnEnterAndExit()
    {
        var handle = CreateHandle();
        var events = new List<bool>();
        handle.CopyModeChanged += (value) => events.Add(value);
        
        handle.EnterCopyMode();
        handle.ExitCopyMode();
        
        Assert.AreEqual(2, events.Count);
        Assert.IsTrue(events[0]);   // enter
        Assert.IsFalse(events[1]);  // exit
    }

    [TestMethod]
    public async Task CopySelection_ExtractsTextAndExits()
    {
        var handle = CreateHandle(10, 3);
        
        // Write content
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("Hello!", row: 0));
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("World!", row: 1));
        
        // Enter copy mode
        handle.EnterCopyMode();
        
        // Select "Hello!" on row 0
        var sel = handle.Selection!;
        sel.MoveCursor(new BufferPosition(0, 0));
        sel.StartSelection(SelectionMode.Character);
        sel.MoveCursor(new BufferPosition(0, 5));
        
        string? copiedText = null;
        handle.TextCopied += (text) => copiedText = text;
        
        var result = handle.CopySelection();
        
        Assert.AreEqual("Hello!", result);
        Assert.AreEqual("Hello!", copiedText);
        Assert.IsFalse(handle.IsInCopyMode);
    }

    [TestMethod]
    public void CopySelection_NoSelection_ReturnsNull()
    {
        var handle = CreateHandle();
        handle.EnterCopyMode();
        // Don't start selection
        
        var result = handle.CopySelection();
        Assert.IsNull(result);
        Assert.IsFalse(handle.IsInCopyMode);
    }

    [TestMethod]
    public void VirtualBufferHeight_ReturnsScrollbackPlusScreen()
    {
        var handle = CreateHandle(80, 24);
        // No scrollback configured (no terminal attached), so scrollback count = 0
        Assert.AreEqual(24, handle.VirtualBufferHeight);
    }

    [TestMethod]
    public async Task GetVirtualCell_ScreenRegion_ReturnsCorrectCell()
    {
        var handle = CreateHandle(10, 5);
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("ABC", row: 2));
        
        // Screen rows start at scrollbackCount (0 when no terminal attached)
        var cell = handle.GetVirtualCell(2, 0);
        Assert.IsNotNull(cell);
        Assert.AreEqual("A", cell!.Value.Character);
        
        var cell2 = handle.GetVirtualCell(2, 2);
        Assert.IsNotNull(cell2);
        Assert.AreEqual("C", cell2!.Value.Character);
    }

    [TestMethod]
    public void GetVirtualCell_OutOfBounds_ReturnsNull()
    {
        var handle = CreateHandle(10, 5);
        
        Assert.IsNull(handle.GetVirtualCell(-1, 0));
        Assert.IsNull(handle.GetVirtualCell(0, -1));
        Assert.IsNull(handle.GetVirtualCell(0, 10)); // width = 10
        Assert.IsNull(handle.GetVirtualCell(5, 0));  // height = 5
    }

    [TestMethod]
    public void CopyModeCursorPosition_WhenNotInCopyMode_ReturnsNull()
    {
        var handle = CreateHandle();
        Assert.IsNull(handle.CopyModeCursorPosition);
    }

    [TestMethod]
    public void CopyModeCursorPosition_WhenInCopyMode_ReturnsCursorPos()
    {
        var handle = CreateHandle(80, 24);
        handle.EnterCopyMode();
        
        var pos = handle.CopyModeCursorPosition;
        Assert.IsNotNull(pos);
        // Fresh handle has cursor at (0,0)
        Assert.AreEqual(0, pos!.Value.Row);
        Assert.AreEqual(0, pos.Value.Column);
    }
}
