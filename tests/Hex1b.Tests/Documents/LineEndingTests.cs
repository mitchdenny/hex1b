using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

/// <summary>
/// Tests for line ending handling: LF, CRLF, mixed, trailing, empty lines.
/// </summary>
[TestClass]
public class LineEndingTests
{
    // --- LF (\n) ---

    [TestMethod]
    public void LF_SingleNewline_TwoLines()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.AreEqual(2, doc.LineCount);
        Assert.AreEqual("Hello", doc.GetLineText(1));
        Assert.AreEqual("World", doc.GetLineText(2));
    }

    [TestMethod]
    public void LF_TrailingNewline_CreatesEmptyLastLine()
    {
        var doc = new Hex1bDocument("Hello\n");
        Assert.AreEqual(2, doc.LineCount);
        Assert.AreEqual("Hello", doc.GetLineText(1));
        Assert.AreEqual("", doc.GetLineText(2));
    }

    [TestMethod]
    public void LF_LeadingNewline_CreatesEmptyFirstLine()
    {
        var doc = new Hex1bDocument("\nHello");
        Assert.AreEqual(2, doc.LineCount);
        Assert.AreEqual("", doc.GetLineText(1));
        Assert.AreEqual("Hello", doc.GetLineText(2));
    }

    [TestMethod]
    public void LF_MultipleNewlines_CreatesEmptyLines()
    {
        var doc = new Hex1bDocument("A\n\n\nB");
        Assert.AreEqual(4, doc.LineCount);
        Assert.AreEqual("A", doc.GetLineText(1));
        Assert.AreEqual("", doc.GetLineText(2));
        Assert.AreEqual("", doc.GetLineText(3));
        Assert.AreEqual("B", doc.GetLineText(4));
    }

    [TestMethod]
    public void LF_OnlyNewlines_AllEmptyLines()
    {
        var doc = new Hex1bDocument("\n\n");
        Assert.AreEqual(3, doc.LineCount);
        Assert.AreEqual("", doc.GetLineText(1));
        Assert.AreEqual("", doc.GetLineText(2));
        Assert.AreEqual("", doc.GetLineText(3));
    }

    // --- CRLF (\r\n) ---

    [TestMethod]
    public void CRLF_SingleLineBreak_TwoLines()
    {
        var doc = new Hex1bDocument("Hello\r\nWorld");
        Assert.AreEqual(2, doc.LineCount);
        Assert.AreEqual("Hello", doc.GetLineText(1));
        Assert.AreEqual("World", doc.GetLineText(2));
    }

    [TestMethod]
    public void CRLF_TrailingLineBreak_CreatesEmptyLastLine()
    {
        var doc = new Hex1bDocument("Hello\r\n");
        Assert.AreEqual(2, doc.LineCount);
        Assert.AreEqual("Hello", doc.GetLineText(1));
        Assert.AreEqual("", doc.GetLineText(2));
    }

    [TestMethod]
    public void CRLF_MultipleLineBreaks()
    {
        var doc = new Hex1bDocument("A\r\nB\r\nC");
        Assert.AreEqual(3, doc.LineCount);
        Assert.AreEqual("A", doc.GetLineText(1));
        Assert.AreEqual("B", doc.GetLineText(2));
        Assert.AreEqual("C", doc.GetLineText(3));
    }

    // --- Line counting after edits ---

    [TestMethod]
    public void InsertNewline_IncrementsLineCount()
    {
        var doc = new Hex1bDocument("HelloWorld");
        Assert.AreEqual(1, doc.LineCount);

        doc.Apply(new InsertOperation(new DocumentOffset(5), "\n"));
        Assert.AreEqual(2, doc.LineCount);
        Assert.AreEqual("Hello", doc.GetLineText(1));
        Assert.AreEqual("World", doc.GetLineText(2));
    }

    [TestMethod]
    public void InsertMultipleNewlines_AtOnce()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new InsertOperation(new DocumentOffset(5), "\n\n\n"));
        Assert.AreEqual(4, doc.LineCount);
    }

    [TestMethod]
    public void DeleteNewline_DecrementsLineCount()
    {
        var doc = new Hex1bDocument("A\nB\nC");
        Assert.AreEqual(3, doc.LineCount);

        // Delete first newline
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(1), new DocumentOffset(2))));
        Assert.AreEqual("AB\nC", doc.GetText());
        Assert.AreEqual(2, doc.LineCount);
    }

    [TestMethod]
    public void ReplaceNewlineWithText_DecrementsLineCount()
    {
        var doc = new Hex1bDocument("A\nB");
        Assert.AreEqual(2, doc.LineCount);

        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(1), new DocumentOffset(2)),
            " "));
        Assert.AreEqual("A B", doc.GetText());
        Assert.AreEqual(1, doc.LineCount);
    }

    [TestMethod]
    public void ReplaceTextWithNewline_IncrementsLineCount()
    {
        var doc = new Hex1bDocument("A B");
        Assert.AreEqual(1, doc.LineCount);

        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(1), new DocumentOffset(2)),
            "\n"));
        Assert.AreEqual("A\nB", doc.GetText());
        Assert.AreEqual(2, doc.LineCount);
    }

    // --- Multi-line text insertions ---

    [TestMethod]
    public void InsertMultiLineText_UpdatesLinesCorrectly()
    {
        var doc = new Hex1bDocument("Start End");
        doc.Apply(new InsertOperation(new DocumentOffset(6), "Line1\nLine2\nLine3 "));
        Assert.AreEqual("Start Line1\nLine2\nLine3 End", doc.GetText());
        Assert.AreEqual(3, doc.LineCount);
        Assert.AreEqual("Start Line1", doc.GetLineText(1));
        Assert.AreEqual("Line2", doc.GetLineText(2));
        Assert.AreEqual("Line3 End", doc.GetLineText(3));
    }

    // --- GetLineText / GetLineLength consistency ---

    [TestMethod]
    public void GetLineText_AndGetLineLength_AreConsistent()
    {
        var doc = new Hex1bDocument("Short\nA much longer line\nX");
        for (var i = 1; i <= doc.LineCount; i++)
        {
            Assert.AreEqual(doc.GetLineText(i).Length, doc.GetLineLength(i));
        }
    }

    [TestMethod]
    public void GetLineText_EmptyMiddleLine_ReturnsEmpty()
    {
        var doc = new Hex1bDocument("A\n\nB");
        Assert.AreEqual("", doc.GetLineText(2));
        Assert.AreEqual(0, doc.GetLineLength(2));
    }

    // --- OffsetToPosition with newlines ---

    [TestMethod]
    public void OffsetToPosition_AtNewlineChar_ReportsEndOfLine()
    {
        var doc = new Hex1bDocument("AB\nCD");
        // Offset 2 = the \n character — it's still on line 1
        var pos = doc.OffsetToPosition(new DocumentOffset(2));
        Assert.AreEqual(1, pos.Line);
        Assert.AreEqual(3, pos.Column); // 1-based: 'A'=1, 'B'=2, '\n'=3
    }

    [TestMethod]
    public void OffsetToPosition_RightAfterNewline_ReportsNextLine()
    {
        var doc = new Hex1bDocument("AB\nCD");
        var pos = doc.OffsetToPosition(new DocumentOffset(3));
        Assert.AreEqual(2, pos.Line);
        Assert.AreEqual(1, pos.Column);
    }
}
