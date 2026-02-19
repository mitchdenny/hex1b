using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

/// <summary>
/// Tests for line ending handling: LF, CRLF, mixed, trailing, empty lines.
/// </summary>
public class LineEndingTests
{
    // --- LF (\n) ---

    [Fact]
    public void LF_SingleNewline_TwoLines()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.Equal(2, doc.LineCount);
        Assert.Equal("Hello", doc.GetLineText(1));
        Assert.Equal("World", doc.GetLineText(2));
    }

    [Fact]
    public void LF_TrailingNewline_CreatesEmptyLastLine()
    {
        var doc = new Hex1bDocument("Hello\n");
        Assert.Equal(2, doc.LineCount);
        Assert.Equal("Hello", doc.GetLineText(1));
        Assert.Equal("", doc.GetLineText(2));
    }

    [Fact]
    public void LF_LeadingNewline_CreatesEmptyFirstLine()
    {
        var doc = new Hex1bDocument("\nHello");
        Assert.Equal(2, doc.LineCount);
        Assert.Equal("", doc.GetLineText(1));
        Assert.Equal("Hello", doc.GetLineText(2));
    }

    [Fact]
    public void LF_MultipleNewlines_CreatesEmptyLines()
    {
        var doc = new Hex1bDocument("A\n\n\nB");
        Assert.Equal(4, doc.LineCount);
        Assert.Equal("A", doc.GetLineText(1));
        Assert.Equal("", doc.GetLineText(2));
        Assert.Equal("", doc.GetLineText(3));
        Assert.Equal("B", doc.GetLineText(4));
    }

    [Fact]
    public void LF_OnlyNewlines_AllEmptyLines()
    {
        var doc = new Hex1bDocument("\n\n");
        Assert.Equal(3, doc.LineCount);
        Assert.Equal("", doc.GetLineText(1));
        Assert.Equal("", doc.GetLineText(2));
        Assert.Equal("", doc.GetLineText(3));
    }

    // --- CRLF (\r\n) ---

    [Fact]
    public void CRLF_SingleLineBreak_TwoLines()
    {
        var doc = new Hex1bDocument("Hello\r\nWorld");
        Assert.Equal(2, doc.LineCount);
        Assert.Equal("Hello", doc.GetLineText(1));
        Assert.Equal("World", doc.GetLineText(2));
    }

    [Fact]
    public void CRLF_TrailingLineBreak_CreatesEmptyLastLine()
    {
        var doc = new Hex1bDocument("Hello\r\n");
        Assert.Equal(2, doc.LineCount);
        Assert.Equal("Hello", doc.GetLineText(1));
        Assert.Equal("", doc.GetLineText(2));
    }

    [Fact]
    public void CRLF_MultipleLineBreaks()
    {
        var doc = new Hex1bDocument("A\r\nB\r\nC");
        Assert.Equal(3, doc.LineCount);
        Assert.Equal("A", doc.GetLineText(1));
        Assert.Equal("B", doc.GetLineText(2));
        Assert.Equal("C", doc.GetLineText(3));
    }

    // --- Line counting after edits ---

    [Fact]
    public void InsertNewline_IncrementsLineCount()
    {
        var doc = new Hex1bDocument("HelloWorld");
        Assert.Equal(1, doc.LineCount);

        doc.Apply(new InsertOperation(new DocumentOffset(5), "\n"));
        Assert.Equal(2, doc.LineCount);
        Assert.Equal("Hello", doc.GetLineText(1));
        Assert.Equal("World", doc.GetLineText(2));
    }

    [Fact]
    public void InsertMultipleNewlines_AtOnce()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new InsertOperation(new DocumentOffset(5), "\n\n\n"));
        Assert.Equal(4, doc.LineCount);
    }

    [Fact]
    public void DeleteNewline_DecrementsLineCount()
    {
        var doc = new Hex1bDocument("A\nB\nC");
        Assert.Equal(3, doc.LineCount);

        // Delete first newline
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(1), new DocumentOffset(2))));
        Assert.Equal("AB\nC", doc.GetText());
        Assert.Equal(2, doc.LineCount);
    }

    [Fact]
    public void ReplaceNewlineWithText_DecrementsLineCount()
    {
        var doc = new Hex1bDocument("A\nB");
        Assert.Equal(2, doc.LineCount);

        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(1), new DocumentOffset(2)),
            " "));
        Assert.Equal("A B", doc.GetText());
        Assert.Equal(1, doc.LineCount);
    }

    [Fact]
    public void ReplaceTextWithNewline_IncrementsLineCount()
    {
        var doc = new Hex1bDocument("A B");
        Assert.Equal(1, doc.LineCount);

        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(1), new DocumentOffset(2)),
            "\n"));
        Assert.Equal("A\nB", doc.GetText());
        Assert.Equal(2, doc.LineCount);
    }

    // --- Multi-line text insertions ---

    [Fact]
    public void InsertMultiLineText_UpdatesLinesCorrectly()
    {
        var doc = new Hex1bDocument("Start End");
        doc.Apply(new InsertOperation(new DocumentOffset(6), "Line1\nLine2\nLine3 "));
        Assert.Equal("Start Line1\nLine2\nLine3 End", doc.GetText());
        Assert.Equal(3, doc.LineCount);
        Assert.Equal("Start Line1", doc.GetLineText(1));
        Assert.Equal("Line2", doc.GetLineText(2));
        Assert.Equal("Line3 End", doc.GetLineText(3));
    }

    // --- GetLineText / GetLineLength consistency ---

    [Fact]
    public void GetLineText_AndGetLineLength_AreConsistent()
    {
        var doc = new Hex1bDocument("Short\nA much longer line\nX");
        for (var i = 1; i <= doc.LineCount; i++)
        {
            Assert.Equal(doc.GetLineText(i).Length, doc.GetLineLength(i));
        }
    }

    [Fact]
    public void GetLineText_EmptyMiddleLine_ReturnsEmpty()
    {
        var doc = new Hex1bDocument("A\n\nB");
        Assert.Equal("", doc.GetLineText(2));
        Assert.Equal(0, doc.GetLineLength(2));
    }

    // --- OffsetToPosition with newlines ---

    [Fact]
    public void OffsetToPosition_AtNewlineChar_ReportsEndOfLine()
    {
        var doc = new Hex1bDocument("AB\nCD");
        // Offset 2 = the \n character — it's still on line 1
        var pos = doc.OffsetToPosition(new DocumentOffset(2));
        Assert.Equal(1, pos.Line);
        Assert.Equal(3, pos.Column); // 1-based: 'A'=1, 'B'=2, '\n'=3
    }

    [Fact]
    public void OffsetToPosition_RightAfterNewline_ReportsNextLine()
    {
        var doc = new Hex1bDocument("AB\nCD");
        var pos = doc.OffsetToPosition(new DocumentOffset(3));
        Assert.Equal(2, pos.Line);
        Assert.Equal(1, pos.Column);
    }
}
