using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

/// <summary>
/// Tests that undo/redo roundtrips via inverse operations preserve document integrity.
/// This is critical for data safety — users must never lose data through undo/redo.
/// </summary>
public class InverseRoundtripTests
{
    [Fact]
    public void Insert_ThenUndo_RestoresOriginal()
    {
        var doc = new Hex1bDocument("Hello");
        var result = doc.Apply(new InsertOperation(new DocumentOffset(5), " World"));
        Assert.Equal("Hello World", doc.GetText());

        doc.Apply(result.Inverse);
        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void Delete_ThenUndo_RestoresOriginal()
    {
        var doc = new Hex1bDocument("Hello World");
        var result = doc.Apply(new DeleteOperation(
            new DocumentRange(new DocumentOffset(5), new DocumentOffset(11))));
        Assert.Equal("Hello", doc.GetText());

        doc.Apply(result.Inverse);
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public void Replace_ThenUndo_RestoresOriginal()
    {
        var doc = new Hex1bDocument("Hello World");
        var result = doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(6), new DocumentOffset(11)),
            "Earth"));
        Assert.Equal("Hello Earth", doc.GetText());

        doc.Apply(result.Inverse);
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public void MultipleEdits_UndoInReverseOrder_RestoresOriginal()
    {
        var original = "The quick brown fox";
        var doc = new Hex1bDocument(original);

        // Edit 1: Insert
        var r1 = doc.Apply(new InsertOperation(new DocumentOffset(doc.Length), " jumps"));
        Assert.Equal("The quick brown fox jumps", doc.GetText());

        // Edit 2: Replace
        var r2 = doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(4), new DocumentOffset(9)),
            "slow"));
        Assert.Equal("The slow brown fox jumps", doc.GetText());

        // Edit 3: Delete
        var r3 = doc.Apply(new DeleteOperation(
            new DocumentRange(new DocumentOffset(9), new DocumentOffset(15))));
        Assert.Equal("The slow fox jumps", doc.GetText());

        // Undo in reverse order
        doc.Apply(r3.Inverse);
        Assert.Equal("The slow brown fox jumps", doc.GetText());

        doc.Apply(r2.Inverse);
        Assert.Equal("The quick brown fox jumps", doc.GetText());

        doc.Apply(r1.Inverse);
        Assert.Equal(original, doc.GetText());
    }

    [Fact]
    public void Redo_AfterUndo_RestoresEdit()
    {
        var doc = new Hex1bDocument("Hello");

        // Edit
        var editResult = doc.Apply(new InsertOperation(new DocumentOffset(5), " World"));
        Assert.Equal("Hello World", doc.GetText());

        // Undo
        var undoResult = doc.Apply(editResult.Inverse);
        Assert.Equal("Hello", doc.GetText());

        // Redo (undo the undo)
        doc.Apply(undoResult.Inverse);
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public void MultipleUndoRedo_Cycles_MaintainIntegrity()
    {
        var doc = new Hex1bDocument("ABCDE");

        for (var cycle = 0; cycle < 10; cycle++)
        {
            var editResult = doc.Apply(new InsertOperation(
                new DocumentOffset(doc.Length), "X"));
            var undoResult = doc.Apply(editResult.Inverse);
            doc.Apply(undoResult.Inverse);
        }

        // Should have "ABCDE" + 10 "X"s
        Assert.Equal("ABCDE" + new string('X', 10), doc.GetText());
    }

    [Fact]
    public void BatchOperation_Undo_RestoresOriginal()
    {
        var doc = new Hex1bDocument("Hello World");

        var result = doc.Apply([
            new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(6))),
            new InsertOperation(new DocumentOffset(5), "_"),
        ]);

        Assert.Equal("Hello_World", doc.GetText());

        // Undo the batch — must undo in reverse order
        var inverseOps = result.Inverse.Reverse().ToList();
        doc.Apply(inverseOps);
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public void Replace_WithMultiLineText_UndoRestoresOriginal()
    {
        var original = "Line 1\nLine 2\nLine 3";
        var doc = new Hex1bDocument(original);

        var result = doc.Apply(new ReplaceOperation(
            new DocumentRange(new DocumentOffset(7), new DocumentOffset(13)),
            "New L2\nNew L3\nNew L4"));

        Assert.Equal("Line 1\nNew L2\nNew L3\nNew L4\nLine 3", doc.GetText());
        Assert.Equal(5, doc.LineCount);

        doc.Apply(result.Inverse);
        Assert.Equal(original, doc.GetText());
        Assert.Equal(3, doc.LineCount);
    }

    [Fact]
    public void Delete_MultiLine_UndoRestoresLines()
    {
        var original = "A\nB\nC\nD\nE";
        var doc = new Hex1bDocument(original);

        // Delete "B\nC\nD\n"
        var result = doc.Apply(new DeleteOperation(
            new DocumentRange(new DocumentOffset(2), new DocumentOffset(8))));
        Assert.Equal("A\nE", doc.GetText());
        Assert.Equal(2, doc.LineCount);

        doc.Apply(result.Inverse);
        Assert.Equal(original, doc.GetText());
        Assert.Equal(5, doc.LineCount);
    }

    [Fact]
    public void Insert_EmptyText_InversIsEmptyDelete()
    {
        var doc = new Hex1bDocument("Hello");
        var result = doc.Apply(new InsertOperation(new DocumentOffset(2), ""));
        Assert.Equal("Hello", doc.GetText());

        // Inverse should be a no-op delete (empty range)
        doc.Apply(result.Inverse);
        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void LongChainOfEdits_FullUndoChain_RestoresOriginal()
    {
        var original = "Start";
        var doc = new Hex1bDocument(original);
        var results = new List<EditResult>();

        // 20 random-ish edits
        for (var i = 0; i < 20; i++)
        {
            var text = $"_{i}_";
            var pos = Math.Min(i, doc.Length);
            var result = doc.Apply(new InsertOperation(new DocumentOffset(pos), text));
            results.Add(result);
        }

        Assert.NotEqual(original, doc.GetText());

        // Undo all in reverse
        for (var i = results.Count - 1; i >= 0; i--)
        {
            doc.Apply(results[i].Inverse);
        }

        Assert.Equal(original, doc.GetText());
    }

    [Fact]
    public void Version_IncreasesMonotonically_ThroughUndoRedo()
    {
        var doc = new Hex1bDocument("Hello");
        Assert.Equal(0, doc.Version);

        doc.Apply(new InsertOperation(new DocumentOffset(5), " World"));
        Assert.Equal(1, doc.Version);

        // Undo still increments version (it's a new edit)
        var result = doc.Apply(new DeleteOperation(
            new DocumentRange(new DocumentOffset(5), new DocumentOffset(11))));
        Assert.Equal(2, doc.Version);

        // Redo
        doc.Apply(result.Inverse);
        Assert.Equal(3, doc.Version);
    }
}
