using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

/// <summary>
/// Tests for piece table internal consistency — edge cases in splitting,
/// merging, and multi-piece operations that exercise the data structure.
/// </summary>
[TestClass]
public class PieceTableEdgeCaseTests
{
    // --- Insert at piece boundaries ---

    [TestMethod]
    public void Insert_AtStartOfDocument_PrependsToPieceList()
    {
        var doc = new Hex1bDocument("World");
        doc.Apply(new InsertOperation(new DocumentOffset(0), "Hello "));
        Assert.AreEqual("Hello World", doc.GetText());
        Assert.AreEqual(11, doc.Length);
    }

    [TestMethod]
    public void Insert_AtEndOfDocument_AppendsToPieceList()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new InsertOperation(new DocumentOffset(5), " World"));
        Assert.AreEqual("Hello World", doc.GetText());
    }

    [TestMethod]
    public void Insert_AtExactPieceBoundary_DoesNotCorrupt()
    {
        // Create a doc, insert in middle to create 3 pieces, then insert at boundary
        var doc = new Hex1bDocument("AABB");
        // Split original piece: "AA" | "X" | "BB"
        doc.Apply(new InsertOperation(new DocumentOffset(2), "X"));
        Assert.AreEqual("AAXBB", doc.GetText());

        // Insert at the boundary between "X" piece and "BB" piece
        doc.Apply(new InsertOperation(new DocumentOffset(3), "Y"));
        Assert.AreEqual("AAXYBB", doc.GetText());
    }

    [TestMethod]
    public void Insert_EmptyString_IsNoOp()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new InsertOperation(new DocumentOffset(2), ""));
        Assert.AreEqual("Hello", doc.GetText());
        Assert.AreEqual(5, doc.Length);
    }

    [TestMethod]
    public void Insert_IntoEmptyDocument_CreatesContent()
    {
        var doc = new Hex1bDocument("");
        doc.Apply(new InsertOperation(new DocumentOffset(0), "Hello"));
        Assert.AreEqual("Hello", doc.GetText());
        Assert.AreEqual(5, doc.Length);
    }

    // --- Delete at piece boundaries ---

    [TestMethod]
    public void Delete_EntirePiece_RemovesItCleanly()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(5))));
        Assert.AreEqual("", doc.GetText());
        Assert.AreEqual(0, doc.Length);
    }

    [TestMethod]
    public void Delete_SpanningMultiplePieces_RemovesCorrectly()
    {
        var doc = new Hex1bDocument("ABCD");
        // Insert to create: "AB" | "X" | "CD"
        doc.Apply(new InsertOperation(new DocumentOffset(2), "X"));
        Assert.AreEqual("ABXCD", doc.GetText());

        // Delete across all three pieces: "B" + "X" + "C"
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(1), new DocumentOffset(4))));
        Assert.AreEqual("AD", doc.GetText());
    }

    [TestMethod]
    public void Delete_FirstCharOfPiece_LeavesRemainder()
    {
        var doc = new Hex1bDocument("ABCDE");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))));
        Assert.AreEqual("BCDE", doc.GetText());
    }

    [TestMethod]
    public void Delete_LastCharOfPiece_LeavesRemainder()
    {
        var doc = new Hex1bDocument("ABCDE");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(4), new DocumentOffset(5))));
        Assert.AreEqual("ABCD", doc.GetText());
    }

    [TestMethod]
    public void Delete_EmptyRange_IsNoOp()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(2), new DocumentOffset(2))));
        Assert.AreEqual("Hello", doc.GetText());
    }

    [TestMethod]
    public void Delete_AllContent_LeavesEmptyDocument()
    {
        var doc = new Hex1bDocument("Hello World!");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(12))));
        Assert.AreEqual("", doc.GetText());
        Assert.AreEqual(0, doc.Length);
        Assert.AreEqual(1, doc.LineCount);
    }

    // --- Replace at piece boundaries ---

    [TestMethod]
    public void Replace_AcrossPieceBoundary_WorksCorrectly()
    {
        var doc = new Hex1bDocument("ABCD");
        doc.Apply(new InsertOperation(new DocumentOffset(2), "XY"));
        Assert.AreEqual("ABXYCD", doc.GetText());

        // Replace "XYC" with "Z"
        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(2), new DocumentOffset(5)),
            "Z"));
        Assert.AreEqual("ABZD", doc.GetText());
    }

    [TestMethod]
    public void Replace_WithLongerText_IncreasesLength()
    {
        var doc = new Hex1bDocument("Hello world");
        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(6), new DocumentOffset(11)),
            "beautiful world"));
        Assert.AreEqual("Hello beautiful world", doc.GetText());
        Assert.AreEqual(21, doc.Length);
    }

    [TestMethod]
    public void Replace_WithShorterText_DecreasesLength()
    {
        var doc = new Hex1bDocument("Hello world");
        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(0), new DocumentOffset(5)),
            "Hi"));
        Assert.AreEqual("Hi world", doc.GetText());
        Assert.AreEqual(8, doc.Length);
    }

    [TestMethod]
    public void Replace_WithEmptyText_IsDelete()
    {
        var doc = new Hex1bDocument("Hello world");
        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(5), new DocumentOffset(11)),
            ""));
        Assert.AreEqual("Hello", doc.GetText());
    }

    // --- Sequential operations creating complex piece lists ---

    [TestMethod]
    public void ManyInserts_MaintainConsistency()
    {
        var doc = new Hex1bDocument("");
        // Build "Hello World" one char at a time
        var text = "Hello World";
        for (var i = 0; i < text.Length; i++)
        {
            doc.Apply(new InsertOperation(new DocumentOffset(i), text[i].ToString()));
        }
        Assert.AreEqual(text, doc.GetText());
        Assert.AreEqual(text.Length, doc.Length);
    }

    [TestMethod]
    public void ManyInserts_AtSamePosition_PrependsCorrectly()
    {
        var doc = new Hex1bDocument("");
        // Insert chars in reverse at position 0 to build "ABCDE"
        doc.Apply(new InsertOperation(new DocumentOffset(0), "E"));
        doc.Apply(new InsertOperation(new DocumentOffset(0), "D"));
        doc.Apply(new InsertOperation(new DocumentOffset(0), "C"));
        doc.Apply(new InsertOperation(new DocumentOffset(0), "B"));
        doc.Apply(new InsertOperation(new DocumentOffset(0), "A"));
        Assert.AreEqual("ABCDE", doc.GetText());
    }

    [TestMethod]
    public void InterleavedInsertDelete_MaintainsConsistency()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new InsertOperation(new DocumentOffset(5), " World"));  // "Hello World"
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(6))));  // "World"
        doc.Apply(new InsertOperation(new DocumentOffset(0), "Brave New "));  // "Brave New World"
        Assert.AreEqual("Brave New World", doc.GetText());
        Assert.AreEqual(15, doc.Length);
    }

    [TestMethod]
    public void Delete_EntireContent_ThenInsert_Works()
    {
        var doc = new Hex1bDocument("Original");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(8))));
        Assert.AreEqual("", doc.GetText());

        doc.Apply(new InsertOperation(new DocumentOffset(0), "Replacement"));
        Assert.AreEqual("Replacement", doc.GetText());
    }

    [TestMethod]
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
        Assert.AreEqual("Test", doc.GetText());
    }

    // --- GetText range across piece boundaries ---

    [TestMethod]
    public void GetText_RangeSpanningMultiplePieces_ReturnsCorrectText()
    {
        var doc = new Hex1bDocument("ABCDEF");
        doc.Apply(new InsertOperation(new DocumentOffset(3), "XYZ"));
        // Pieces: "ABC" | "XYZ" | "DEF"
        Assert.AreEqual("ABCXYZDEF", doc.GetText());

        // Range spanning two boundaries: "CXY"
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(6));
        Assert.AreEqual("CXYZ", doc.GetText(range));
    }

    [TestMethod]
    public void GetText_EmptyRange_ReturnsEmptyString()
    {
        var doc = new Hex1bDocument("Hello");
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(2));
        Assert.AreEqual("", doc.GetText(range));
    }

    [TestMethod]
    public void GetText_FullRange_ReturnsFullText()
    {
        var doc = new Hex1bDocument("Hello World");
        var range = new DocumentRange(new DocumentOffset(0), new DocumentOffset(11));
        Assert.AreEqual("Hello World", doc.GetText(range));
    }

    [TestMethod]
    public void GetText_OutOfRange_Throws()
    {
        var doc = new Hex1bDocument("Hello");
        var range = new DocumentRange(new DocumentOffset(0), new DocumentOffset(10));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => doc.GetText(range));
    }

    // --- OffsetToPosition across pieces ---

    [TestMethod]
    public void OffsetToPosition_AfterMultipleInserts_StillCorrect()
    {
        var doc = new Hex1bDocument("AB\nCD");
        doc.Apply(new InsertOperation(new DocumentOffset(2), "XY\nZ"));
        // Content: "ABXY\nZ\nCD"
        Assert.AreEqual("ABXY\nZ\nCD", doc.GetText());
        Assert.AreEqual(3, doc.LineCount);

        Assert.AreEqual(new DocumentPosition(1, 1), doc.OffsetToPosition(new DocumentOffset(0)));
        Assert.AreEqual(new DocumentPosition(1, 5), doc.OffsetToPosition(new DocumentOffset(4))); // "Y"
        Assert.AreEqual(new DocumentPosition(2, 1), doc.OffsetToPosition(new DocumentOffset(5))); // "Z"
        Assert.AreEqual(new DocumentPosition(3, 1), doc.OffsetToPosition(new DocumentOffset(7))); // "C"
    }

    [TestMethod]
    public void OffsetToPosition_AtEndOfDocument_ReturnsLastLineLastColumn()
    {
        var doc = new Hex1bDocument("AB\nCD");
        var pos = doc.OffsetToPosition(new DocumentOffset(5));
        Assert.AreEqual(new DocumentPosition(2, 3), pos);
    }

    [TestMethod]
    public void OffsetToPosition_BeyondLength_Throws()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            doc.OffsetToPosition(new DocumentOffset(6)));
    }

    // --- PositionToOffset edge cases ---

    [TestMethod]
    public void PositionToOffset_InvalidLine_Throws()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            doc.PositionToOffset(new DocumentPosition(3, 1)));
    }

    [TestMethod]
    public void PositionToOffset_ZeroLine_Throws()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new DocumentPosition(0, 1));
    }

    // --- Line operations edge cases ---

    [TestMethod]
    public void GetLineText_InvalidLine_Throws()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => doc.GetLineText(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => doc.GetLineText(2));
    }

    [TestMethod]
    public void GetLineLength_InvalidLine_Throws()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => doc.GetLineLength(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => doc.GetLineLength(2));
    }
}
