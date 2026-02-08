using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

/// <summary>
/// Tests for piece table internal consistency — edge cases in splitting,
/// merging, and multi-piece operations that exercise the data structure.
/// </summary>
public class PieceTableEdgeCaseTests
{
    // --- Insert at piece boundaries ---

    [Fact]
    public void Insert_AtStartOfDocument_PrependsToPieceList()
    {
        var doc = new Hex1bDocument("World");
        doc.Apply(new InsertOperation(new DocumentOffset(0), "Hello "));
        Assert.Equal("Hello World", doc.GetText());
        Assert.Equal(11, doc.Length);
    }

    [Fact]
    public void Insert_AtEndOfDocument_AppendsToPieceList()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new InsertOperation(new DocumentOffset(5), " World"));
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public void Insert_AtExactPieceBoundary_DoesNotCorrupt()
    {
        // Create a doc, insert in middle to create 3 pieces, then insert at boundary
        var doc = new Hex1bDocument("AABB");
        // Split original piece: "AA" | "X" | "BB"
        doc.Apply(new InsertOperation(new DocumentOffset(2), "X"));
        Assert.Equal("AAXBB", doc.GetText());

        // Insert at the boundary between "X" piece and "BB" piece
        doc.Apply(new InsertOperation(new DocumentOffset(3), "Y"));
        Assert.Equal("AAXYBB", doc.GetText());
    }

    [Fact]
    public void Insert_EmptyString_IsNoOp()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new InsertOperation(new DocumentOffset(2), ""));
        Assert.Equal("Hello", doc.GetText());
        Assert.Equal(5, doc.Length);
    }

    [Fact]
    public void Insert_IntoEmptyDocument_CreatesContent()
    {
        var doc = new Hex1bDocument("");
        doc.Apply(new InsertOperation(new DocumentOffset(0), "Hello"));
        Assert.Equal("Hello", doc.GetText());
        Assert.Equal(5, doc.Length);
    }

    // --- Delete at piece boundaries ---

    [Fact]
    public void Delete_EntirePiece_RemovesItCleanly()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(5))));
        Assert.Equal("", doc.GetText());
        Assert.Equal(0, doc.Length);
    }

    [Fact]
    public void Delete_SpanningMultiplePieces_RemovesCorrectly()
    {
        var doc = new Hex1bDocument("ABCD");
        // Insert to create: "AB" | "X" | "CD"
        doc.Apply(new InsertOperation(new DocumentOffset(2), "X"));
        Assert.Equal("ABXCD", doc.GetText());

        // Delete across all three pieces: "B" + "X" + "C"
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(1), new DocumentOffset(4))));
        Assert.Equal("AD", doc.GetText());
    }

    [Fact]
    public void Delete_FirstCharOfPiece_LeavesRemainder()
    {
        var doc = new Hex1bDocument("ABCDE");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))));
        Assert.Equal("BCDE", doc.GetText());
    }

    [Fact]
    public void Delete_LastCharOfPiece_LeavesRemainder()
    {
        var doc = new Hex1bDocument("ABCDE");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(4), new DocumentOffset(5))));
        Assert.Equal("ABCD", doc.GetText());
    }

    [Fact]
    public void Delete_EmptyRange_IsNoOp()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(2), new DocumentOffset(2))));
        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void Delete_AllContent_LeavesEmptyDocument()
    {
        var doc = new Hex1bDocument("Hello World!");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(12))));
        Assert.Equal("", doc.GetText());
        Assert.Equal(0, doc.Length);
        Assert.Equal(1, doc.LineCount);
    }

    // --- Replace at piece boundaries ---

    [Fact]
    public void Replace_AcrossPieceBoundary_WorksCorrectly()
    {
        var doc = new Hex1bDocument("ABCD");
        doc.Apply(new InsertOperation(new DocumentOffset(2), "XY"));
        Assert.Equal("ABXYCD", doc.GetText());

        // Replace "XYC" with "Z"
        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(2), new DocumentOffset(5)),
            "Z"));
        Assert.Equal("ABZD", doc.GetText());
    }

    [Fact]
    public void Replace_WithLongerText_IncreasesLength()
    {
        var doc = new Hex1bDocument("Hello world");
        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(6), new DocumentOffset(11)),
            "beautiful world"));
        Assert.Equal("Hello beautiful world", doc.GetText());
        Assert.Equal(21, doc.Length);
    }

    [Fact]
    public void Replace_WithShorterText_DecreasesLength()
    {
        var doc = new Hex1bDocument("Hello world");
        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(0), new DocumentOffset(5)),
            "Hi"));
        Assert.Equal("Hi world", doc.GetText());
        Assert.Equal(8, doc.Length);
    }

    [Fact]
    public void Replace_WithEmptyText_IsDelete()
    {
        var doc = new Hex1bDocument("Hello world");
        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(5), new DocumentOffset(11)),
            ""));
        Assert.Equal("Hello", doc.GetText());
    }

    // --- Sequential operations creating complex piece lists ---

    [Fact]
    public void ManyInserts_MaintainConsistency()
    {
        var doc = new Hex1bDocument("");
        // Build "Hello World" one char at a time
        var text = "Hello World";
        for (var i = 0; i < text.Length; i++)
        {
            doc.Apply(new InsertOperation(new DocumentOffset(i), text[i].ToString()));
        }
        Assert.Equal(text, doc.GetText());
        Assert.Equal(text.Length, doc.Length);
    }

    [Fact]
    public void ManyInserts_AtSamePosition_PrependsCorrectly()
    {
        var doc = new Hex1bDocument("");
        // Insert chars in reverse at position 0 to build "ABCDE"
        doc.Apply(new InsertOperation(new DocumentOffset(0), "E"));
        doc.Apply(new InsertOperation(new DocumentOffset(0), "D"));
        doc.Apply(new InsertOperation(new DocumentOffset(0), "C"));
        doc.Apply(new InsertOperation(new DocumentOffset(0), "B"));
        doc.Apply(new InsertOperation(new DocumentOffset(0), "A"));
        Assert.Equal("ABCDE", doc.GetText());
    }

    [Fact]
    public void InterleavedInsertDelete_MaintainsConsistency()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new InsertOperation(new DocumentOffset(5), " World"));  // "Hello World"
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(6))));  // "World"
        doc.Apply(new InsertOperation(new DocumentOffset(0), "Brave New "));  // "Brave New World"
        Assert.Equal("Brave New World", doc.GetText());
        Assert.Equal(15, doc.Length);
    }

    [Fact]
    public void Delete_EntireContent_ThenInsert_Works()
    {
        var doc = new Hex1bDocument("Original");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(8))));
        Assert.Equal("", doc.GetText());

        doc.Apply(new InsertOperation(new DocumentOffset(0), "Replacement"));
        Assert.Equal("Replacement", doc.GetText());
    }

    [Fact]
    public void RapidInsertDeleteCycle_MaintainsConsistency()
    {
        var doc = new Hex1bDocument("Test");
        for (var i = 0; i < 50; i++)
        {
            doc.Apply(new InsertOperation(new DocumentOffset(doc.Length), "X"));
            doc.Apply(new DeleteOperation(new DocumentRange(
                new DocumentOffset(doc.Length - 1),
                new DocumentOffset(doc.Length))));
        }
        Assert.Equal("Test", doc.GetText());
    }

    // --- GetText range across piece boundaries ---

    [Fact]
    public void GetText_RangeSpanningMultiplePieces_ReturnsCorrectText()
    {
        var doc = new Hex1bDocument("ABCDEF");
        doc.Apply(new InsertOperation(new DocumentOffset(3), "XYZ"));
        // Pieces: "ABC" | "XYZ" | "DEF"
        Assert.Equal("ABCXYZDEF", doc.GetText());

        // Range spanning two boundaries: "CXY"
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(6));
        Assert.Equal("CXYZ", doc.GetText(range));
    }

    [Fact]
    public void GetText_EmptyRange_ReturnsEmptyString()
    {
        var doc = new Hex1bDocument("Hello");
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(2));
        Assert.Equal("", doc.GetText(range));
    }

    [Fact]
    public void GetText_FullRange_ReturnsFullText()
    {
        var doc = new Hex1bDocument("Hello World");
        var range = new DocumentRange(new DocumentOffset(0), new DocumentOffset(11));
        Assert.Equal("Hello World", doc.GetText(range));
    }

    [Fact]
    public void GetText_OutOfRange_Throws()
    {
        var doc = new Hex1bDocument("Hello");
        var range = new DocumentRange(new DocumentOffset(0), new DocumentOffset(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetText(range));
    }

    // --- OffsetToPosition across pieces ---

    [Fact]
    public void OffsetToPosition_AfterMultipleInserts_StillCorrect()
    {
        var doc = new Hex1bDocument("AB\nCD");
        doc.Apply(new InsertOperation(new DocumentOffset(2), "XY\nZ"));
        // Content: "ABXY\nZ\nCD"
        Assert.Equal("ABXY\nZ\nCD", doc.GetText());
        Assert.Equal(3, doc.LineCount);

        Assert.Equal(new DocumentPosition(1, 1), doc.OffsetToPosition(new DocumentOffset(0)));
        Assert.Equal(new DocumentPosition(1, 5), doc.OffsetToPosition(new DocumentOffset(4))); // "Y"
        Assert.Equal(new DocumentPosition(2, 1), doc.OffsetToPosition(new DocumentOffset(5))); // "Z"
        Assert.Equal(new DocumentPosition(3, 1), doc.OffsetToPosition(new DocumentOffset(7))); // "C"
    }

    [Fact]
    public void OffsetToPosition_AtEndOfDocument_ReturnsLastLineLastColumn()
    {
        var doc = new Hex1bDocument("AB\nCD");
        var pos = doc.OffsetToPosition(new DocumentOffset(5));
        Assert.Equal(new DocumentPosition(2, 3), pos);
    }

    [Fact]
    public void OffsetToPosition_BeyondLength_Throws()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            doc.OffsetToPosition(new DocumentOffset(6)));
    }

    // --- PositionToOffset edge cases ---

    [Fact]
    public void PositionToOffset_InvalidLine_Throws()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            doc.PositionToOffset(new DocumentPosition(3, 1)));
    }

    [Fact]
    public void PositionToOffset_ZeroLine_Throws()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DocumentPosition(0, 1));
    }

    // --- Line operations edge cases ---

    [Fact]
    public void GetLineText_InvalidLine_Throws()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetLineText(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetLineText(2));
    }

    [Fact]
    public void GetLineLength_InvalidLine_Throws()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetLineLength(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetLineLength(2));
    }
}
