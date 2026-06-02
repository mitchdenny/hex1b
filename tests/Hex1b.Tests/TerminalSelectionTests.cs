namespace Hex1b.Tests;

/// <summary>
/// Tests for the TerminalSelection model — coordinate math, selection modes,
/// text extraction with soft wraps and wide characters.
/// </summary>
[TestClass]
public class TerminalSelectionTests
{
    [TestMethod]
    public void BufferPosition_CompareTo_OrdersByRowThenColumn()
    {
        var a = new BufferPosition(0, 0);
        var b = new BufferPosition(0, 5);
        var c = new BufferPosition(1, 0);
        
        Assert.IsTrue(a < b);
        Assert.IsTrue(b < c);
        Assert.IsTrue(a < c);
        Assert.IsFalse(c < a);
    }

    [TestMethod]
    public void BufferPosition_Equality()
    {
        var a = new BufferPosition(3, 7);
        var b = new BufferPosition(3, 7);
        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void Selection_InitialState_NotSelecting()
    {
        var sel = new TerminalSelection(new BufferPosition(5, 10));
        Assert.IsFalse(sel.IsSelecting);
        Assert.AreEqual(new BufferPosition(5, 10), sel.Cursor);
    }

    [TestMethod]
    public void MoveCursor_UpdatesCursorPosition()
    {
        var sel = new TerminalSelection(new BufferPosition(0, 0));
        sel.MoveCursor(new BufferPosition(3, 5));
        Assert.AreEqual(new BufferPosition(3, 5), sel.Cursor);
    }

    [TestMethod]
    public void StartSelection_SetsAnchorAndSelecting()
    {
        var sel = new TerminalSelection(new BufferPosition(0, 0));
        sel.MoveCursor(new BufferPosition(2, 3));
        sel.StartSelection(SelectionMode.Character);
        
        Assert.IsTrue(sel.IsSelecting);
        Assert.AreEqual(new BufferPosition(2, 3), sel.Anchor);
        Assert.AreEqual(SelectionMode.Character, sel.Mode);
    }

    [TestMethod]
    public void ClearSelection_StopsSelecting()
    {
        var sel = new TerminalSelection(new BufferPosition(0, 0));
        sel.StartSelection();
        Assert.IsTrue(sel.IsSelecting);
        
        sel.ClearSelection();
        Assert.IsFalse(sel.IsSelecting);
    }

    [TestMethod]
    public void Start_End_NormalizedRegardlessOfDirection()
    {
        var sel = new TerminalSelection(new BufferPosition(5, 10));
        sel.StartSelection();
        sel.MoveCursor(new BufferPosition(2, 3)); // cursor before anchor
        
        Assert.AreEqual(new BufferPosition(2, 3), sel.Start);
        Assert.AreEqual(new BufferPosition(5, 10), sel.End);
    }

    [TestMethod]
    public void ToggleMode_SwitchesBetweenModes()
    {
        var sel = new TerminalSelection(new BufferPosition(0, 0));
        sel.StartSelection(SelectionMode.Character);
        
        sel.ToggleMode(SelectionMode.Line);
        Assert.AreEqual(SelectionMode.Line, sel.Mode);
        
        // Toggle same mode goes back to character
        sel.ToggleMode(SelectionMode.Line);
        Assert.AreEqual(SelectionMode.Character, sel.Mode);
    }

    // === IsCellSelected tests ===

    [TestMethod]
    public void IsCellSelected_Character_SingleRow()
    {
        var sel = new TerminalSelection(new BufferPosition(3, 2));
        sel.StartSelection(SelectionMode.Character);
        sel.MoveCursor(new BufferPosition(3, 7));
        
        Assert.IsFalse(sel.IsCellSelected(3, 1));
        Assert.IsTrue(sel.IsCellSelected(3, 2));
        Assert.IsTrue(sel.IsCellSelected(3, 5));
        Assert.IsTrue(sel.IsCellSelected(3, 7));
        Assert.IsFalse(sel.IsCellSelected(3, 8));
        Assert.IsFalse(sel.IsCellSelected(2, 5));
    }

    [TestMethod]
    public void IsCellSelected_Character_MultipleRows()
    {
        var sel = new TerminalSelection(new BufferPosition(1, 5));
        sel.StartSelection(SelectionMode.Character);
        sel.MoveCursor(new BufferPosition(3, 3));
        
        // Row 1: from column 5 to end
        Assert.IsFalse(sel.IsCellSelected(1, 4));
        Assert.IsTrue(sel.IsCellSelected(1, 5));
        Assert.IsTrue(sel.IsCellSelected(1, 79));
        
        // Row 2: fully selected
        Assert.IsTrue(sel.IsCellSelected(2, 0));
        Assert.IsTrue(sel.IsCellSelected(2, 79));
        
        // Row 3: from start to column 3
        Assert.IsTrue(sel.IsCellSelected(3, 0));
        Assert.IsTrue(sel.IsCellSelected(3, 3));
        Assert.IsFalse(sel.IsCellSelected(3, 4));
    }

    [TestMethod]
    public void IsCellSelected_Line_SelectsEntireRows()
    {
        var sel = new TerminalSelection(new BufferPosition(2, 5));
        sel.StartSelection(SelectionMode.Line);
        sel.MoveCursor(new BufferPosition(4, 10));
        
        Assert.IsFalse(sel.IsCellSelected(1, 0));
        Assert.IsTrue(sel.IsCellSelected(2, 0));
        Assert.IsTrue(sel.IsCellSelected(2, 79));
        Assert.IsTrue(sel.IsCellSelected(3, 0));
        Assert.IsTrue(sel.IsCellSelected(4, 0));
        Assert.IsTrue(sel.IsCellSelected(4, 79));
        Assert.IsFalse(sel.IsCellSelected(5, 0));
    }

    [TestMethod]
    public void IsCellSelected_Block_SelectsRectangle()
    {
        var sel = new TerminalSelection(new BufferPosition(1, 3));
        sel.StartSelection(SelectionMode.Block);
        sel.MoveCursor(new BufferPosition(4, 8));
        
        // Inside rectangle
        Assert.IsTrue(sel.IsCellSelected(1, 3));
        Assert.IsTrue(sel.IsCellSelected(2, 5));
        Assert.IsTrue(sel.IsCellSelected(4, 8));
        
        // Outside rectangle
        Assert.IsFalse(sel.IsCellSelected(1, 2));
        Assert.IsFalse(sel.IsCellSelected(2, 9));
        Assert.IsFalse(sel.IsCellSelected(0, 5));
        Assert.IsFalse(sel.IsCellSelected(5, 5));
    }

    [TestMethod]
    public void IsCellSelected_WhenNotSelecting_ReturnsFalse()
    {
        var sel = new TerminalSelection(new BufferPosition(3, 5));
        Assert.IsFalse(sel.IsCellSelected(3, 5));
    }

    // === ExtractText tests ===

    private static TerminalCell MakeCell(string ch, bool softWrap = false)
    {
        var attrs = softWrap ? CellAttributes.SoftWrap : CellAttributes.None;
        return new TerminalCell(ch, null, null, attrs);
    }

    [TestMethod]
    public void ExtractText_Character_SingleRow()
    {
        // Buffer: "Hello World     " (row 0, 16 wide)
        var row = "Hello World     ".ToCharArray();
        TerminalCell? GetCell(int r, int c) => r == 0 && c < 16 ? MakeCell(row[c].ToString()) : null;

        var sel = new TerminalSelection(new BufferPosition(0, 0));
        sel.StartSelection(SelectionMode.Character);
        sel.MoveCursor(new BufferPosition(0, 10));

        var text = sel.ExtractText(GetCell, 16);
        Assert.AreEqual("Hello World", text);
    }

    [TestMethod]
    public void ExtractText_Character_MultipleRows_NoSoftWrap()
    {
        var rows = new[] { "Line one   ", "Line two   " };
        TerminalCell? GetCell(int r, int c) =>
            r < rows.Length && c < rows[r].Length ? MakeCell(rows[r][c].ToString()) : null;

        var sel = new TerminalSelection(new BufferPosition(0, 0));
        sel.StartSelection(SelectionMode.Character);
        sel.MoveCursor(new BufferPosition(1, 7));

        var text = sel.ExtractText(GetCell, 11);
        // No soft wrap → newline between rows, trailing spaces trimmed
        Assert.AreEqual("Line one" + Environment.NewLine + "Line two", text);
    }

    [TestMethod]
    public void ExtractText_Character_WithSoftWrap_JoinsRows()
    {
        // Row 0 has soft wrap at position 9 (last cell), row 1 continues
        var row0 = "This is a ".ToCharArray();
        var row1 = "long line  ".ToCharArray();
        
        TerminalCell? GetCell(int r, int c)
        {
            if (r == 0 && c < 10)
            {
                bool sw = c == 9; // last cell of row has SoftWrap
                return MakeCell(row0[c].ToString(), sw);
            }
            if (r == 1 && c < 11)
                return MakeCell(row1[c].ToString());
            return null;
        }

        var sel = new TerminalSelection(new BufferPosition(0, 0));
        sel.StartSelection(SelectionMode.Character);
        sel.MoveCursor(new BufferPosition(1, 8));

        var text = sel.ExtractText(GetCell, 10);
        // Soft wrap → no newline between rows
        Assert.AreEqual("This is a long line", text);
    }

    [TestMethod]
    public void ExtractText_Line_SelectsFullRows()
    {
        var rows = new[] { "AAA   ", "BBB   ", "CCC   " };
        TerminalCell? GetCell(int r, int c) =>
            r < rows.Length && c < rows[r].Length ? MakeCell(rows[r][c].ToString()) : null;

        var sel = new TerminalSelection(new BufferPosition(0, 2));
        sel.StartSelection(SelectionMode.Line);
        sel.MoveCursor(new BufferPosition(1, 0));

        var text = sel.ExtractText(GetCell, 6);
        Assert.AreEqual("AAA" + Environment.NewLine + "BBB", text);
    }

    [TestMethod]
    public void ExtractText_Block_SelectsRectangle()
    {
        var rows = new[] { "ABCDEF", "GHIJKL", "MNOPQR" };
        TerminalCell? GetCell(int r, int c) =>
            r < rows.Length && c < rows[r].Length ? MakeCell(rows[r][c].ToString()) : null;

        var sel = new TerminalSelection(new BufferPosition(0, 1));
        sel.StartSelection(SelectionMode.Block);
        sel.MoveCursor(new BufferPosition(2, 3));

        var text = sel.ExtractText(GetCell, 6);
        Assert.AreEqual("BCD" + Environment.NewLine + "HIJ" + Environment.NewLine + "NOP", text);
    }

    [TestMethod]
    public void ExtractText_WhenNotSelecting_ReturnsNull()
    {
        var sel = new TerminalSelection(new BufferPosition(0, 0));
        var text = sel.ExtractText((_, _) => MakeCell("A"), 10);
        Assert.IsNull(text);
    }

    [TestMethod]
    public void ExtractText_SkipsWidePaddingCells()
    {
        // Simulate a wide character: "W" at column 0, empty string at column 1 (padding)
        TerminalCell? GetCell(int r, int c)
        {
            if (r != 0) return null;
            return c switch
            {
                0 => MakeCell("Ｗ"),  // fullwidth W
                1 => MakeCell(""),    // padding cell
                2 => MakeCell("x"),
                _ => null
            };
        }

        var sel = new TerminalSelection(new BufferPosition(0, 0));
        sel.StartSelection(SelectionMode.Character);
        sel.MoveCursor(new BufferPosition(0, 2));

        var text = sel.ExtractText(GetCell, 3);
        Assert.AreEqual("Ｗx", text);
    }
}
