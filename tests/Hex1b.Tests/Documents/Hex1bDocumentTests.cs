using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

[TestClass]
public class Hex1bDocumentTests
{
    [TestMethod]
    public void Constructor_EmptyString_HasLengthZero()
    {
        var doc = new Hex1bDocument();
        Assert.AreEqual(0, doc.Length);
        Assert.AreEqual(1, doc.LineCount);
        Assert.AreEqual(0, doc.Version);
    }

    [TestMethod]
    public void Constructor_WithText_HasCorrectLength()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.AreEqual(5, doc.Length);
        Assert.AreEqual(1, doc.LineCount);
    }

    [TestMethod]
    public void Constructor_WithMultipleLines_HasCorrectLineCount()
    {
        var doc = new Hex1bDocument("Line 1\nLine 2\nLine 3");
        Assert.AreEqual(3, doc.LineCount);
    }

    [TestMethod]
    public void GetText_ReturnsFullText()
    {
        var doc = new Hex1bDocument("Hello world");
        Assert.AreEqual("Hello world", doc.GetText());
    }

    [TestMethod]
    public void GetText_WithRange_ReturnsSubstring()
    {
        var doc = new Hex1bDocument("Hello world");
        var range = new DocumentRange(new DocumentOffset(6), new DocumentOffset(11));
        Assert.AreEqual("world", doc.GetText(range));
    }

    [TestMethod]
    public void GetLineText_ReturnsCorrectLine()
    {
        var doc = new Hex1bDocument("Line 1\nLine 2\nLine 3");
        Assert.AreEqual("Line 1", doc.GetLineText(1));
        Assert.AreEqual("Line 2", doc.GetLineText(2));
        Assert.AreEqual("Line 3", doc.GetLineText(3));
    }

    [TestMethod]
    public void GetLineLength_ReturnsCorrectLength()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.AreEqual(5, doc.GetLineLength(1));
        Assert.AreEqual(5, doc.GetLineLength(2));
    }

    [TestMethod]
    public void OffsetToPosition_ConvertsCorrectly()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.AreEqual(new DocumentPosition(1, 1), doc.OffsetToPosition(new DocumentOffset(0)));
        Assert.AreEqual(new DocumentPosition(1, 6), doc.OffsetToPosition(new DocumentOffset(5)));
        Assert.AreEqual(new DocumentPosition(2, 1), doc.OffsetToPosition(new DocumentOffset(6)));
        Assert.AreEqual(new DocumentPosition(2, 3), doc.OffsetToPosition(new DocumentOffset(8)));
    }

    [TestMethod]
    public void PositionToOffset_ConvertsCorrectly()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.AreEqual(new DocumentOffset(0), doc.PositionToOffset(new DocumentPosition(1, 1)));
        Assert.AreEqual(new DocumentOffset(6), doc.PositionToOffset(new DocumentPosition(2, 1)));
        Assert.AreEqual(new DocumentOffset(8), doc.PositionToOffset(new DocumentPosition(2, 3)));
    }

    [TestMethod]
    public void Apply_Insert_AddsText()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new InsertOperation(new DocumentOffset(5), " world"));
        Assert.AreEqual("Hello world", doc.GetText());
        Assert.AreEqual(11, doc.Length);
    }

    [TestMethod]
    public void Apply_InsertAtBeginning_PrependsText()
    {
        var doc = new Hex1bDocument("world");
        doc.Apply(new InsertOperation(new DocumentOffset(0), "Hello "));
        Assert.AreEqual("Hello world", doc.GetText());
    }

    [TestMethod]
    public void Apply_InsertInMiddle_SplitsPiece()
    {
        var doc = new Hex1bDocument("Helloworld");
        doc.Apply(new InsertOperation(new DocumentOffset(5), " "));
        Assert.AreEqual("Hello world", doc.GetText());
    }

    [TestMethod]
    public void Apply_Delete_RemovesText()
    {
        var doc = new Hex1bDocument("Hello world");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(11))));
        Assert.AreEqual("Hello", doc.GetText());
        Assert.AreEqual(5, doc.Length);
    }

    [TestMethod]
    public void Apply_Replace_ReplacesText()
    {
        var doc = new Hex1bDocument("Hello world");
        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(6), new DocumentOffset(11)),
            "earth"));
        Assert.AreEqual("Hello earth", doc.GetText());
    }

    [TestMethod]
    public void Apply_IncrementsVersion()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.AreEqual(0, doc.Version);
        doc.Apply(new InsertOperation(new DocumentOffset(5), "!"));
        Assert.AreEqual(1, doc.Version);
        doc.Apply(new InsertOperation(new DocumentOffset(6), "!"));
        Assert.AreEqual(2, doc.Version);
    }

    [TestMethod]
    public void Apply_ReturnsEditResult()
    {
        var doc = new Hex1bDocument("Hello");
        var result = doc.Apply(new InsertOperation(new DocumentOffset(5), " world"));
        Assert.AreEqual(0, result.PreviousVersion);
        Assert.AreEqual(1, result.NewVersion);
        TestSeq.Single(result.Applied);
        TestSeq.Single(result.Inverse);
    }

    [TestMethod]
    public void Apply_InverseUndoesInsert()
    {
        var doc = new Hex1bDocument("Hello");
        var result = doc.Apply(new InsertOperation(new DocumentOffset(5), " world"));
        Assert.AreEqual("Hello world", doc.GetText());

        // Apply inverse should undo
        doc.Apply(result.Inverse);
        Assert.AreEqual("Hello", doc.GetText());
    }

    [TestMethod]
    public void Apply_InverseUndoesDelete()
    {
        var doc = new Hex1bDocument("Hello world");
        var result = doc.Apply(new DeleteOperation(
            new DocumentRange(new DocumentOffset(5), new DocumentOffset(11))));
        Assert.AreEqual("Hello", doc.GetText());

        doc.Apply(result.Inverse);
        Assert.AreEqual("Hello world", doc.GetText());
    }

    [TestMethod]
    public void Apply_InverseUndoesReplace()
    {
        var doc = new Hex1bDocument("Hello world");
        var result = doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(6), new DocumentOffset(11)),
            "earth"));
        Assert.AreEqual("Hello earth", doc.GetText());

        doc.Apply(result.Inverse);
        Assert.AreEqual("Hello world", doc.GetText());
    }

    [TestMethod]
    public void Changed_FiresOnEdit()
    {
        var doc = new Hex1bDocument("Hello");
        DocumentChangedEventArgs? lastEvent = null;
        doc.Changed += (_, e) => lastEvent = e;

        doc.Apply(new InsertOperation(new DocumentOffset(5), "!"));

        Assert.IsNotNull(lastEvent);
        Assert.AreEqual(1, lastEvent.Version);
        Assert.AreEqual(0, lastEvent.PreviousVersion);
    }

    [TestMethod]
    public void Changed_CarriesSourceTag()
    {
        var doc = new Hex1bDocument("Hello");
        string? receivedSource = null;
        doc.Changed += (_, e) => receivedSource = e.Source;

        doc.Apply(new InsertOperation(new DocumentOffset(5), "!"), source: "test-agent");

        Assert.AreEqual("test-agent", receivedSource);
    }

    [TestMethod]
    public void Apply_MultipleOperations_Atomic()
    {
        var doc = new Hex1bDocument("Hello world");
        var result = doc.Apply([
            new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(6))),
            new InsertOperation(new DocumentOffset(5), "_"),
        ]);
        Assert.AreEqual("Hello_world", doc.GetText());
        Assert.AreEqual(2, result.Applied.Count);
        Assert.AreEqual(1, result.NewVersion); // single version bump
    }

    [TestMethod]
    public void Insert_UpdatesLineCount()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.AreEqual(1, doc.LineCount);

        doc.Apply(new InsertOperation(new DocumentOffset(5), "\nWorld"));
        Assert.AreEqual(2, doc.LineCount);
        Assert.AreEqual("Hello", doc.GetLineText(1));
        Assert.AreEqual("World", doc.GetLineText(2));
    }

    [TestMethod]
    public void Delete_UpdatesLineCount()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.AreEqual(2, doc.LineCount);

        // Delete the newline
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(6))));
        Assert.AreEqual(1, doc.LineCount);
        Assert.AreEqual("HelloWorld", doc.GetText());
    }

    [TestMethod]
    public void MultipleEdits_MaintainConsistency()
    {
        var doc = new Hex1bDocument("abc");

        doc.Apply(new InsertOperation(new DocumentOffset(1), "X"));
        Assert.AreEqual("aXbc", doc.GetText());

        doc.Apply(new InsertOperation(new DocumentOffset(3), "Y"));
        Assert.AreEqual("aXbYc", doc.GetText());

        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))));
        Assert.AreEqual("XbYc", doc.GetText());

        Assert.AreEqual(4, doc.Length);
        Assert.AreEqual(3, doc.Version);
    }

    [TestMethod]
    public void EmptyDocument_HasOneLine()
    {
        var doc = new Hex1bDocument("");
        Assert.AreEqual(1, doc.LineCount);
        Assert.AreEqual("", doc.GetLineText(1));
    }

    // ── Byte API ────────────────────────────────────────────────

    [TestMethod]
    public void ByteCount_AsciiText_EqualsByteLength()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.AreEqual(5, doc.ByteCount);
    }

    [TestMethod]
    public void ByteCount_MultiByte_ExceedsCharLength()
    {
        // © = C2 A9 (2 bytes), Length=1
        var doc = new Hex1bDocument("©");
        Assert.AreEqual(1, doc.Length);
        Assert.AreEqual(2, doc.ByteCount);
    }

    [TestMethod]
    public void GetBytes_ReturnsUtf8Encoding()
    {
        var doc = new Hex1bDocument("AB");
        var bytes = doc.GetBytes().ToArray();
        TestSeq.AreEqual(new byte[] { 0x41, 0x42 }, bytes);
    }

    [TestMethod]
    public void GetBytes_Slice_ReturnsCorrectRange()
    {
        var doc = new Hex1bDocument("ABCD");
        var bytes = doc.GetBytes(1, 2).ToArray();
        TestSeq.AreEqual(new byte[] { 0x42, 0x43 }, bytes);
    }

    [TestMethod]
    public void ByteConstructor_StoresRawBytes()
    {
        var raw = new byte[] { 0xAA, 0xBB, 0xCC };
        var doc = new Hex1bDocument(raw);
        Assert.AreEqual(3, doc.ByteCount);
        var bytes = doc.GetBytes().ToArray();
        TestSeq.AreEqual(raw, bytes);
    }

    [TestMethod]
    public void ByteConstructor_InvalidUtf8_TextHasReplacementChars()
    {
        var raw = new byte[] { 0xAA, 0xBB };
        var doc = new Hex1bDocument(raw);
        // Invalid UTF-8 bytes produce U+FFFD replacement characters
        Assert.Contains('\uFFFD', doc.GetText());
        // But raw bytes are preserved
        TestSeq.AreEqual(raw, doc.GetBytes().ToArray());
    }

    [TestMethod]
    public void ApplyBytes_ReplaceSingleByte()
    {
        var doc = new Hex1bDocument("ABC");
        // Replace 'B' (0x42) with 0xAA
        doc.ApplyBytes(new ByteReplaceOperation(1, 1, [0xAA]));

        var bytes = doc.GetBytes().ToArray();
        Assert.AreEqual(3, bytes.Length);
        Assert.AreEqual(0x41, bytes[0]); // 'A'
        Assert.AreEqual(0xAA, bytes[1]); // replaced
        Assert.AreEqual(0x43, bytes[2]); // 'C'
    }

    [TestMethod]
    public void ApplyBytes_InsertBytes()
    {
        var doc = new Hex1bDocument("AC");
        doc.ApplyBytes(new ByteInsertOperation(1, [0xBB, 0xCC]));

        var bytes = doc.GetBytes().ToArray();
        Assert.AreEqual(4, bytes.Length);
        TestSeq.AreEqual(new byte[] { 0x41, 0xBB, 0xCC, 0x43 }, bytes);
    }

    [TestMethod]
    public void ApplyBytes_DeleteBytes()
    {
        var doc = new Hex1bDocument("ABCD");
        doc.ApplyBytes(new ByteDeleteOperation(1, 2));

        var bytes = doc.GetBytes().ToArray();
        TestSeq.AreEqual(new byte[] { 0x41, 0x44 }, bytes);
        Assert.AreEqual("AD", doc.GetText());
    }

    [TestMethod]
    public void ApplyBytes_FiresChangedEvent()
    {
        var doc = new Hex1bDocument("AB");
        var fired = false;
        doc.Changed += (_, _) => fired = true;

        doc.ApplyBytes(new ByteReplaceOperation(0, 1, [0xFF]));
        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void ApplyBytes_IncrementsVersion()
    {
        var doc = new Hex1bDocument("AB");
        var v1 = doc.Version;
        doc.ApplyBytes(new ByteReplaceOperation(0, 1, [0xFF]));
        Assert.IsTrue(doc.Version > v1);
    }

    [TestMethod]
    public void TextApi_WorksAfterByteEdit()
    {
        var doc = new Hex1bDocument("Hello");
        doc.ApplyBytes(new ByteReplaceOperation(0, 1, [0x4A])); // H → J
        Assert.AreEqual("Jello", doc.GetText());
        Assert.AreEqual(5, doc.Length);
        Assert.AreEqual(1, doc.LineCount);
    }

    // ── Diagnostic info ─────────────────────────────────────────

    [TestMethod]
    public void GetDiagnosticInfo_NewDocument_ReturnsOnePiece()
    {
        var doc = new Hex1bDocument("Hello");
        var info = doc.GetDiagnosticInfo();

        Assert.IsNotNull(info);
        Assert.AreEqual(0, info.Version);
        Assert.AreEqual(5, info.CharCount);
        Assert.AreEqual(5, info.ByteCount);
        Assert.AreEqual(1, info.LineCount);
        Assert.AreEqual(5, info.OriginalBufferSize);
        Assert.AreEqual(0, info.AddBufferSize);
        TestSeq.Single(info.Pieces);

        var piece = info.Pieces[0];
        Assert.AreEqual(0, piece.Index);
        Assert.AreEqual("Original", piece.Source);
        Assert.AreEqual(0, piece.Start);
        Assert.AreEqual(5, piece.Length);
        Assert.AreEqual("Hello", piece.PreviewText);
    }

    [TestMethod]
    public void GetDiagnosticInfo_AfterEdit_ShowsMultiplePieces()
    {
        var doc = new Hex1bDocument("Hello World");
        doc.Apply(new InsertOperation(new DocumentOffset(5), " Beautiful"));
        var info = doc.GetDiagnosticInfo();

        Assert.IsNotNull(info);
        Assert.AreEqual(1, info.Version);
        Assert.IsTrue(info.Pieces.Count > 1, "Should have multiple pieces after edit");
        Assert.IsTrue(info.AddBufferSize > 0, "Add buffer should have content after insert");

        // At least one piece should be from the Added buffer
        Assert.IsTrue(info.Pieces.Any(p => p.Source == "Added"));
    }

    [TestMethod]
    public void GetDiagnosticInfo_EmptyDocument_ReturnsNoPieces()
    {
        var doc = new Hex1bDocument();
        var info = doc.GetDiagnosticInfo();

        Assert.IsNotNull(info);
        Assert.AreEqual(0, info.CharCount);
        Assert.AreEqual(0, info.ByteCount);
        Assert.IsEmpty(info.Pieces);
    }

    [TestMethod]
    public void GetDiagnosticInfo_MultiByte_ByteCountDiffersFromCharCount()
    {
        var doc = new Hex1bDocument("café");
        var info = doc.GetDiagnosticInfo();

        Assert.IsNotNull(info);
        Assert.AreEqual(4, info.CharCount); // c, a, f, é
        Assert.IsTrue(info.ByteCount > info.CharCount, "Multi-byte chars should increase byte count");
        Assert.AreEqual(5, info.ByteCount); // é is 2 bytes in UTF-8
    }
}
