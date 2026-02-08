using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

/// <summary>
/// Stress tests for the piece table to verify correctness under heavy use.
/// These tests exercise patterns that produce many small pieces and verify
/// the document remains consistent after complex operation sequences.
/// </summary>
public class PieceTableStressTests
{
    [Fact]
    public void Insert_OneCharAtATime_BuildsStringCorrectly()
    {
        var expected = "The quick brown fox jumps over the lazy dog";
        var doc = new Hex1bDocument("");

        for (var i = 0; i < expected.Length; i++)
        {
            doc.Apply(new InsertOperation(new DocumentOffset(i), expected[i].ToString()));
        }

        Assert.Equal(expected, doc.GetText());
        Assert.Equal(expected.Length, doc.Length);
    }

    [Fact]
    public void Insert_OneCharAtATime_AtBeginning_BuildsReversedCorrectly()
    {
        // Insert each char at position 0 — result should be reversed
        var input = "ABCDE";
        var doc = new Hex1bDocument("");

        foreach (var c in input)
        {
            doc.Apply(new InsertOperation(new DocumentOffset(0), c.ToString()));
        }

        Assert.Equal("EDCBA", doc.GetText());
    }

    [Fact]
    public void Delete_OneCharAtATime_FromEnd_EmptiesDocument()
    {
        var text = "Hello World! This is a test document.";
        var doc = new Hex1bDocument(text);

        for (var i = text.Length; i > 0; i--)
        {
            doc.Apply(new DeleteOperation(
                new DocumentRange(new DocumentOffset(i - 1), new DocumentOffset(i))));
        }

        Assert.Equal("", doc.GetText());
        Assert.Equal(0, doc.Length);
    }

    [Fact]
    public void Delete_OneCharAtATime_FromBeginning_EmptiesDocument()
    {
        var text = "Hello World!";
        var doc = new Hex1bDocument(text);

        for (var i = 0; i < text.Length; i++)
        {
            doc.Apply(new DeleteOperation(
                new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))));
        }

        Assert.Equal("", doc.GetText());
        Assert.Equal(0, doc.Length);
    }

    [Fact]
    public void AlternatingInsertDelete_100Cycles_MaintainsConsistency()
    {
        var doc = new Hex1bDocument("Base");

        for (var i = 0; i < 100; i++)
        {
            doc.Apply(new InsertOperation(new DocumentOffset(doc.Length), $"_{i}"));
            if (doc.Length > 10)
            {
                doc.Apply(new DeleteOperation(
                    new DocumentRange(new DocumentOffset(0), new DocumentOffset(3))));
            }
        }

        // Just verify it doesn't throw and Length is consistent
        var text = doc.GetText();
        Assert.Equal(text.Length, doc.Length);
        Assert.True(doc.Length > 0);
    }

    [Fact]
    public void ManySmallInserts_InMiddle_CreatesFragmentedPieceList()
    {
        var doc = new Hex1bDocument("AABB");

        // Insert many chars in the middle to fragment heavily
        for (var i = 0; i < 50; i++)
        {
            var mid = doc.Length / 2;
            doc.Apply(new InsertOperation(new DocumentOffset(mid), "X"));
        }

        var text = doc.GetText();
        Assert.Equal(text.Length, doc.Length);
        Assert.Equal(54, doc.Length); // 4 + 50
        Assert.StartsWith("AA", text);
        Assert.EndsWith("BB", text);
        Assert.Contains("XXXXXXXXXX", text); // At least 10 Xs in a row
    }

    [Fact]
    public void LargeDocument_InsertAndQuery_PerformsCorrectly()
    {
        // Build a ~10K char document
        var doc = new Hex1bDocument("");
        for (var i = 0; i < 100; i++)
        {
            doc.Apply(new InsertOperation(
                new DocumentOffset(doc.Length),
                $"Line {i}: This is some content for testing.\n"));
        }

        Assert.Equal(101, doc.LineCount); // 100 lines + trailing \n creates empty line 101
        Assert.Equal($"Line 0: This is some content for testing.", doc.GetLineText(1));
        Assert.Equal($"Line 99: This is some content for testing.", doc.GetLineText(100));

        // Verify offset/position roundtrip on first 100 lines (the content lines)
        for (var line = 1; line <= 100; line++)
        {
            var offset = doc.PositionToOffset(new DocumentPosition(line, 1));
            var pos = doc.OffsetToPosition(offset);
            Assert.Equal(line, pos.Line);
            Assert.Equal(1, pos.Column);
        }
    }

    [Fact]
    public void GetText_Range_AfterManyEdits_ReturnsCorrectSlice()
    {
        var doc = new Hex1bDocument("0123456789");

        // Fragment the document
        doc.Apply(new InsertOperation(new DocumentOffset(5), "ABCDE"));
        // "01234ABCDE56789"
        doc.Apply(new InsertOperation(new DocumentOffset(3), "XY"));
        // "012XY34ABCDE56789"
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(7), new DocumentOffset(9))));
        // "012XY34BCDE56789"

        var fullText = doc.GetText();
        Assert.Equal(fullText.Length, doc.Length);

        // Verify GetText(range) matches substring for every possible range
        for (var start = 0; start < doc.Length; start++)
        {
            for (var end = start; end <= doc.Length; end++)
            {
                var range = new DocumentRange(new DocumentOffset(start), new DocumentOffset(end));
                Assert.Equal(fullText[start..end], doc.GetText(range));
            }
        }
    }

    [Fact]
    public void OffsetToPosition_PositionToOffset_Roundtrip_AllPositions()
    {
        var doc = new Hex1bDocument("Line 1\nLine 2\nLine 3\n");

        // Every valid offset should roundtrip through position and back
        for (var offset = 0; offset <= doc.Length; offset++)
        {
            var pos = doc.OffsetToPosition(new DocumentOffset(offset));
            var backToOffset = doc.PositionToOffset(pos);
            Assert.Equal(offset, backToOffset.Value);
        }
    }

    [Fact]
    public void Version_AfterManyEdits_IsCorrect()
    {
        var doc = new Hex1bDocument("Start");
        var editCount = 100;

        for (var i = 0; i < editCount; i++)
        {
            doc.Apply(new InsertOperation(new DocumentOffset(doc.Length), "."));
        }

        Assert.Equal(editCount, doc.Version);
    }

    [Fact]
    public void ChangedEvent_FiresForEveryEdit()
    {
        var doc = new Hex1bDocument("Start");
        var eventCount = 0;
        doc.Changed += (_, _) => eventCount++;

        for (var i = 0; i < 50; i++)
        {
            doc.Apply(new InsertOperation(new DocumentOffset(doc.Length), "X"));
        }

        Assert.Equal(50, eventCount);
    }

    [Fact]
    public void BatchEdit_FiresSingleEvent()
    {
        var doc = new Hex1bDocument("Hello World");
        var eventCount = 0;
        doc.Changed += (_, _) => eventCount++;

        doc.Apply([
            new InsertOperation(new DocumentOffset(0), "A"),
            new InsertOperation(new DocumentOffset(1), "B"),
            new InsertOperation(new DocumentOffset(2), "C"),
        ]);

        Assert.Equal(1, eventCount); // Single event for batch
    }

    [Fact]
    public void TypewriterSimulation_ProducesExpectedResult()
    {
        // Simulate typing "Hello\nWorld" one char at a time, 
        // then using backspace to delete "World" and retype "Earth"
        var doc = new Hex1bDocument("");

        // Type "Hello\nWorld"
        foreach (var c in "Hello\nWorld")
        {
            doc.Apply(new InsertOperation(new DocumentOffset(doc.Length), c.ToString()));
        }
        Assert.Equal("Hello\nWorld", doc.GetText());

        // Backspace 5 times to delete "World"
        for (var i = 0; i < 5; i++)
        {
            doc.Apply(new DeleteOperation(new DocumentRange(
                new DocumentOffset(doc.Length - 1),
                new DocumentOffset(doc.Length))));
        }
        Assert.Equal("Hello\n", doc.GetText());

        // Type "Earth"
        foreach (var c in "Earth")
        {
            doc.Apply(new InsertOperation(new DocumentOffset(doc.Length), c.ToString()));
        }
        Assert.Equal("Hello\nEarth", doc.GetText());
        Assert.Equal(2, doc.LineCount);
        Assert.Equal("Hello", doc.GetLineText(1));
        Assert.Equal("Earth", doc.GetLineText(2));
    }
}
