using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

public class Hex1bDocumentTests
{
    [Fact]
    public void Constructor_EmptyString_HasLengthZero()
    {
        var doc = new Hex1bDocument();
        Assert.Equal(0, doc.Length);
        Assert.Equal(1, doc.LineCount);
        Assert.Equal(0, doc.Version);
    }

    [Fact]
    public void Constructor_WithText_HasCorrectLength()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.Equal(5, doc.Length);
        Assert.Equal(1, doc.LineCount);
    }

    [Fact]
    public void Constructor_WithMultipleLines_HasCorrectLineCount()
    {
        var doc = new Hex1bDocument("Line 1\nLine 2\nLine 3");
        Assert.Equal(3, doc.LineCount);
    }

    [Fact]
    public void GetText_ReturnsFullText()
    {
        var doc = new Hex1bDocument("Hello world");
        Assert.Equal("Hello world", doc.GetText());
    }

    [Fact]
    public void GetText_WithRange_ReturnsSubstring()
    {
        var doc = new Hex1bDocument("Hello world");
        var range = new DocumentRange(new DocumentOffset(6), new DocumentOffset(11));
        Assert.Equal("world", doc.GetText(range));
    }

    [Fact]
    public void GetLineText_ReturnsCorrectLine()
    {
        var doc = new Hex1bDocument("Line 1\nLine 2\nLine 3");
        Assert.Equal("Line 1", doc.GetLineText(1));
        Assert.Equal("Line 2", doc.GetLineText(2));
        Assert.Equal("Line 3", doc.GetLineText(3));
    }

    [Fact]
    public void GetLineLength_ReturnsCorrectLength()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.Equal(5, doc.GetLineLength(1));
        Assert.Equal(5, doc.GetLineLength(2));
    }

    [Fact]
    public void OffsetToPosition_ConvertsCorrectly()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.Equal(new DocumentPosition(1, 1), doc.OffsetToPosition(new DocumentOffset(0)));
        Assert.Equal(new DocumentPosition(1, 6), doc.OffsetToPosition(new DocumentOffset(5)));
        Assert.Equal(new DocumentPosition(2, 1), doc.OffsetToPosition(new DocumentOffset(6)));
        Assert.Equal(new DocumentPosition(2, 3), doc.OffsetToPosition(new DocumentOffset(8)));
    }

    [Fact]
    public void PositionToOffset_ConvertsCorrectly()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.Equal(new DocumentOffset(0), doc.PositionToOffset(new DocumentPosition(1, 1)));
        Assert.Equal(new DocumentOffset(6), doc.PositionToOffset(new DocumentPosition(2, 1)));
        Assert.Equal(new DocumentOffset(8), doc.PositionToOffset(new DocumentPosition(2, 3)));
    }

    [Fact]
    public void Apply_Insert_AddsText()
    {
        var doc = new Hex1bDocument("Hello");
        doc.Apply(new InsertOperation(new DocumentOffset(5), " world"));
        Assert.Equal("Hello world", doc.GetText());
        Assert.Equal(11, doc.Length);
    }

    [Fact]
    public void Apply_InsertAtBeginning_PrependsText()
    {
        var doc = new Hex1bDocument("world");
        doc.Apply(new InsertOperation(new DocumentOffset(0), "Hello "));
        Assert.Equal("Hello world", doc.GetText());
    }

    [Fact]
    public void Apply_InsertInMiddle_SplitsPiece()
    {
        var doc = new Hex1bDocument("Helloworld");
        doc.Apply(new InsertOperation(new DocumentOffset(5), " "));
        Assert.Equal("Hello world", doc.GetText());
    }

    [Fact]
    public void Apply_Delete_RemovesText()
    {
        var doc = new Hex1bDocument("Hello world");
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(11))));
        Assert.Equal("Hello", doc.GetText());
        Assert.Equal(5, doc.Length);
    }

    [Fact]
    public void Apply_Replace_ReplacesText()
    {
        var doc = new Hex1bDocument("Hello world");
        doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(6), new DocumentOffset(11)),
            "earth"));
        Assert.Equal("Hello earth", doc.GetText());
    }

    [Fact]
    public void Apply_IncrementsVersion()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.Equal(0, doc.Version);
        doc.Apply(new InsertOperation(new DocumentOffset(5), "!"));
        Assert.Equal(1, doc.Version);
        doc.Apply(new InsertOperation(new DocumentOffset(6), "!"));
        Assert.Equal(2, doc.Version);
    }

    [Fact]
    public void Apply_ReturnsEditResult()
    {
        var doc = new Hex1bDocument("Hello");
        var result = doc.Apply(new InsertOperation(new DocumentOffset(5), " world"));
        Assert.Equal(0, result.PreviousVersion);
        Assert.Equal(1, result.NewVersion);
        Assert.Single(result.Applied);
        Assert.Single(result.Inverse);
    }

    [Fact]
    public void Apply_InverseUndoesInsert()
    {
        var doc = new Hex1bDocument("Hello");
        var result = doc.Apply(new InsertOperation(new DocumentOffset(5), " world"));
        Assert.Equal("Hello world", doc.GetText());

        // Apply inverse should undo
        doc.Apply(result.Inverse);
        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void Apply_InverseUndoesDelete()
    {
        var doc = new Hex1bDocument("Hello world");
        var result = doc.Apply(new DeleteOperation(
            new DocumentRange(new DocumentOffset(5), new DocumentOffset(11))));
        Assert.Equal("Hello", doc.GetText());

        doc.Apply(result.Inverse);
        Assert.Equal("Hello world", doc.GetText());
    }

    [Fact]
    public void Apply_InverseUndoesReplace()
    {
        var doc = new Hex1bDocument("Hello world");
        var result = doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(6), new DocumentOffset(11)),
            "earth"));
        Assert.Equal("Hello earth", doc.GetText());

        doc.Apply(result.Inverse);
        Assert.Equal("Hello world", doc.GetText());
    }

    [Fact]
    public void Changed_FiresOnEdit()
    {
        var doc = new Hex1bDocument("Hello");
        DocumentChangedEventArgs? lastEvent = null;
        doc.Changed += (_, e) => lastEvent = e;

        doc.Apply(new InsertOperation(new DocumentOffset(5), "!"));

        Assert.NotNull(lastEvent);
        Assert.Equal(1, lastEvent.Version);
        Assert.Equal(0, lastEvent.PreviousVersion);
    }

    [Fact]
    public void Changed_CarriesSourceTag()
    {
        var doc = new Hex1bDocument("Hello");
        string? receivedSource = null;
        doc.Changed += (_, e) => receivedSource = e.Source;

        doc.Apply(new InsertOperation(new DocumentOffset(5), "!"), source: "test-agent");

        Assert.Equal("test-agent", receivedSource);
    }

    [Fact]
    public void Apply_MultipleOperations_Atomic()
    {
        var doc = new Hex1bDocument("Hello world");
        var result = doc.Apply([
            new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(6))),
            new InsertOperation(new DocumentOffset(5), "_"),
        ]);
        Assert.Equal("Hello_world", doc.GetText());
        Assert.Equal(2, result.Applied.Count);
        Assert.Equal(1, result.NewVersion); // single version bump
    }

    [Fact]
    public void Insert_UpdatesLineCount()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.Equal(1, doc.LineCount);

        doc.Apply(new InsertOperation(new DocumentOffset(5), "\nWorld"));
        Assert.Equal(2, doc.LineCount);
        Assert.Equal("Hello", doc.GetLineText(1));
        Assert.Equal("World", doc.GetLineText(2));
    }

    [Fact]
    public void Delete_UpdatesLineCount()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        Assert.Equal(2, doc.LineCount);

        // Delete the newline
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(6))));
        Assert.Equal(1, doc.LineCount);
        Assert.Equal("HelloWorld", doc.GetText());
    }

    [Fact]
    public void MultipleEdits_MaintainConsistency()
    {
        var doc = new Hex1bDocument("abc");

        doc.Apply(new InsertOperation(new DocumentOffset(1), "X"));
        Assert.Equal("aXbc", doc.GetText());

        doc.Apply(new InsertOperation(new DocumentOffset(3), "Y"));
        Assert.Equal("aXbYc", doc.GetText());

        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))));
        Assert.Equal("XbYc", doc.GetText());

        Assert.Equal(4, doc.Length);
        Assert.Equal(3, doc.Version);
    }

    [Fact]
    public void EmptyDocument_HasOneLine()
    {
        var doc = new Hex1bDocument("");
        Assert.Equal(1, doc.LineCount);
        Assert.Equal("", doc.GetLineText(1));
    }
}
