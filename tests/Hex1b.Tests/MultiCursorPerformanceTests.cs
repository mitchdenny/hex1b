using System.Diagnostics;
using System.Text;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Performance tests for multi-cursor editing on large documents.
/// These tests reproduce the scenario of selecting ~70 words across a 100K-line
/// document (e.g., via Ctrl+D or find-and-replace) and replacing them all at once.
///
/// Root cause of slowness: Hex1bDocument.RebuildCaches() is O(n) and called once
/// per cursor edit. For 70 cursors on a 100K-line doc (~9MB), this is:
///   70 × (AssembleBytes O(n) + UTF8.GetString O(n) + RebuildLineStarts O(n))
///   ≈ 70 × 3 × 9MB ≈ 1.9GB of processing
///
/// The fix: batch all piece tree mutations, call RebuildCaches() once at the end.
/// </summary>
[Collection("CPU-Intensive")]
public class MultiCursorPerformanceTests
{
    /// <summary>
    /// Builds a document with repeating lines, each containing a target word,
    /// then sets up N cursors each selecting that word on a different line.
    /// </summary>
    private static (EditorState State, int CursorCount) SetupMultiCursorScenario(
        int lineCount, int cursorCount, string targetWord = "TARGET")
    {
        // Build document: each line is "Line NNNNN has TARGET word and some padding text here\n"
        var sb = new StringBuilder(lineCount * 60);
        var wordPositions = new List<int>();
        for (int i = 0; i < lineCount; i++)
        {
            var lineStart = sb.Length;
            var prefix = $"Line {i:D5} has ";
            sb.Append(prefix);
            wordPositions.Add(sb.Length); // position of TARGET
            sb.Append(targetWord);
            sb.Append(" word and some padding text here");
            if (i < lineCount - 1)
                sb.Append('\n');
        }

        var doc = new Hex1bDocument(sb.ToString());
        var state = new EditorState(doc);

        // Place cursors selecting TARGET on evenly spaced lines
        var step = Math.Max(1, lineCount / cursorCount);
        var placed = 0;
        for (int i = 0; i < lineCount && placed < cursorCount; i += step)
        {
            var pos = wordPositions[i];
            if (placed == 0)
            {
                state.Cursor.Position = new DocumentOffset(pos + targetWord.Length);
                state.Cursor.SelectionAnchor = new DocumentOffset(pos);
            }
            else
            {
                var idx = state.Cursors.Add(new DocumentOffset(pos + targetWord.Length),
                    new DocumentOffset(pos));
            }
            placed++;
        }

        return (state, placed);
    }

    [Fact]
    public void MultiCursorReplace_70Cursors_100KLines_Correctness()
    {
        // Smaller doc for correctness check (faster in CI)
        var (state, cursorCount) = SetupMultiCursorScenario(
            lineCount: 1000, cursorCount: 70, targetWord: "TARGET");

        Assert.Equal(70, cursorCount);

        // Verify all cursors have selections
        foreach (var cursor in state.Cursors)
        {
            Assert.True(cursor.HasSelection, "Each cursor should have a selection");
        }

        // Replace all selections with "X"
        state.InsertText("X");

        // Verify: no "TARGET" should remain on lines that had cursors
        var text = state.Document.GetText();
        var lines = text.Split('\n');

        // Count how many lines still have TARGET vs X
        var replacedCount = 0;
        foreach (var line in lines)
        {
            if (line.Contains("X word"))
                replacedCount++;
        }

        Assert.Equal(70, replacedCount);

        // Verify cursors are valid
        foreach (var cursor in state.Cursors)
        {
            Assert.True(cursor.Position.Value >= 0);
            Assert.True(cursor.Position.Value <= state.Document.Length);
            Assert.False(cursor.HasSelection, "Selections should be cleared after replace");
        }
    }

    [Fact]
    public void MultiCursorReplace_70Cursors_100KLines_Performance()
    {
        var (state, cursorCount) = SetupMultiCursorScenario(
            lineCount: 100_000, cursorCount: 70, targetWord: "TARGET");

        Assert.Equal(70, cursorCount);

        // Warm up: verify document is ready
        Assert.True(state.Document.Length > 0);
        Assert.True(state.Document.LineCount >= 100_000);

        // Measure the replace operation
        var sw = Stopwatch.StartNew();
        state.InsertText("X");
        sw.Stop();

        // Log timing for diagnostics
        var ms = sw.ElapsedMilliseconds;

        // With batch mode: single RebuildCaches call, typically <100ms.
        // Use 500ms threshold for CI headroom.
        Assert.True(ms < 500,
            $"Multi-cursor replace with {cursorCount} cursors on 100K-line doc " +
            $"took {ms}ms — expected <500ms. " +
            $"This likely means RebuildCaches is being called per-cursor instead of once.");
    }

    [Fact]
    public void MultiCursorReplace_200Cursors_100KLines_Performance()
    {
        // Stress test: 200 cursors (simulates large find-and-replace)
        var (state, cursorCount) = SetupMultiCursorScenario(
            lineCount: 100_000, cursorCount: 200, targetWord: "TARGET");

        Assert.True(cursorCount >= 190); // May be slightly less due to line spacing

        var sw = Stopwatch.StartNew();
        state.InsertText("REPLACEMENT");
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;

        // With batch mode: single RebuildCaches call, typically <100ms.
        Assert.True(ms < 500,
            $"Multi-cursor replace with {cursorCount} cursors on 100K-line doc " +
            $"took {ms}ms — expected <500ms.");
    }

    [Fact]
    public void MultiCursorDelete_70Cursors_100KLines_Performance()
    {
        // Test deletion (Backspace with selections = delete selected text)
        var (state, cursorCount) = SetupMultiCursorScenario(
            lineCount: 100_000, cursorCount: 70, targetWord: "TARGET");

        var sw = Stopwatch.StartNew();
        state.DeleteBackward(); // Deletes selected text for all 70 cursors
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;
        Assert.True(ms < 500,
            $"Multi-cursor delete with {cursorCount} cursors on 100K-line doc " +
            $"took {ms}ms — expected <500ms.");
    }

    [Fact]
    public void MultiCursorInsert_70Cursors_NoSelection_100KLines_Performance()
    {
        // Test insertion without selection (just 70 cursor positions, type a char)
        var (state, cursorCount) = SetupMultiCursorScenario(
            lineCount: 100_000, cursorCount: 70, targetWord: "TARGET");

        // Clear selections (keep cursor positions at start of TARGET)
        foreach (var cursor in state.Cursors)
        {
            cursor.Position = new DocumentOffset(cursor.SelectionStart.Value);
            cursor.ClearSelection();
        }

        var sw = Stopwatch.StartNew();
        state.InsertText("Z");
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;
        Assert.True(ms < 500,
            $"Multi-cursor insert with {cursorCount} cursors on 100K-line doc " +
            $"took {ms}ms — expected <500ms.");
    }

    [Fact]
    public void SingleCursorReplace_100KLines_Baseline()
    {
        // Baseline: single cursor should be fast regardless
        var (state, _) = SetupMultiCursorScenario(
            lineCount: 100_000, cursorCount: 1, targetWord: "TARGET");

        var sw = Stopwatch.StartNew();
        state.InsertText("X");
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;
        Assert.True(ms < 500,
            $"Single-cursor replace on 100K-line doc took {ms}ms — expected <500ms.");
    }

    /// <summary>
    /// Directly measures RebuildCaches cost to quantify the per-cursor overhead.
    /// </summary>
    [Fact]
    public void RebuildCaches_100KLineDoc_MeasureSingleCallCost()
    {
        var sb = new StringBuilder(100_000 * 60);
        for (int i = 0; i < 100_000; i++)
        {
            sb.Append($"Line {i:D5} has TARGET word and some padding text here");
            if (i < 99_999) sb.Append('\n');
        }

        var doc = new Hex1bDocument(sb.ToString());

        // Force a single edit + rebuild to measure
        var sw = Stopwatch.StartNew();
        doc.Apply(new InsertOperation(new DocumentOffset(0), "X"));
        sw.Stop();

        var singleRebuildMs = sw.ElapsedMilliseconds;

        // A single rebuild should be <100ms. Multiplied by 70 cursors = ~7 seconds.
        // This test documents the cost to help reason about batching gains.
        Assert.True(singleRebuildMs < 500,
            $"Single Apply (including RebuildCaches) took {singleRebuildMs}ms");
    }

    [Fact]
    public void MultiCursorUndo_70Cursors_100KLines_Performance()
    {
        var (state, cursorCount) = SetupMultiCursorScenario(
            lineCount: 100_000, cursorCount: 70, targetWord: "TARGET");

        // Perform the edit first
        state.InsertText("X");
        Assert.Contains("X word", state.Document.GetText());

        // Measure undo
        var sw = Stopwatch.StartNew();
        state.Undo();
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;
        Assert.True(ms < 500,
            $"Undo of 70-cursor replace on 100K-line doc took {ms}ms — expected <500ms.");

        // Verify undo restored the text
        Assert.Contains("TARGET", state.Document.GetText());
    }

    [Fact]
    public void MultiCursorRedo_70Cursors_100KLines_Performance()
    {
        var (state, cursorCount) = SetupMultiCursorScenario(
            lineCount: 100_000, cursorCount: 70, targetWord: "TARGET");

        state.InsertText("X");
        state.Undo();

        // Measure redo
        var sw = Stopwatch.StartNew();
        state.Redo();
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;
        Assert.True(ms < 500,
            $"Redo of 70-cursor replace on 100K-line doc took {ms}ms — expected <500ms.");
    }

    // ── Single-keystroke performance tests (post lazy-cache optimization) ──

    [Fact]
    public void SingleKeystroke_100KLineFile_CompletesUnderThreshold()
    {
        // Build a 100K-line document (~5MB)
        var sb = new StringBuilder(100_000 * 50);
        for (int i = 0; i < 100_000; i++)
        {
            sb.Append($"Line {i:D6} with some content here for padding purposes xxxx\n");
        }

        var doc = new Hex1bDocument(sb.ToString());
        var state = new EditorState(doc);

        // Position cursor in the middle of the document
        state.Cursor.Position = new DocumentOffset(50_000 * 50);

        // Warm up — first edit triggers full cache build
        state.InsertText("W");
        state.DeleteBackward();

        // Measure a single character insert
        var sw = Stopwatch.StartNew();
        state.InsertText("X");
        sw.Stop();

        var ms = sw.Elapsed.TotalMilliseconds;
        // With lazy text + per-line reading, single keystroke avoids full text materialization
        // Previously ~30-50ms with full RebuildCaches; now ~10-15ms (byte assembly + line starts scan)
        Assert.True(ms < 50,
            $"Single keystroke on 100K-line doc took {ms:F1}ms — expected <50ms.");
    }

    [Fact]
    public void SingleKeystroke_DoesNotRebuildFullText()
    {
        // Build a 100K-line document
        var sb = new StringBuilder(100_000 * 50);
        for (int i = 0; i < 100_000; i++)
        {
            sb.Append($"Line {i:D6} with some content\n");
        }

        var doc = new Hex1bDocument(sb.ToString());
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(50_000 * 30);

        // Warm up
        state.InsertText("W");
        state.DeleteBackward();

        // Measure: insert + read visible lines (simulating render path)
        var sw = Stopwatch.StartNew();
        state.InsertText("Y");

        // Simulate render: read 40 visible lines (typical viewport) + OffsetToPosition
        var pos = doc.OffsetToPosition(state.Cursor.Position);
        for (int line = Math.Max(1, pos.Line - 20); line <= Math.Min(doc.LineCount, pos.Line + 20); line++)
        {
            _ = doc.GetLineText(line);
        }
        sw.Stop();

        var ms = sw.Elapsed.TotalMilliseconds;
        // Edit + render of visible lines should be <15ms total
        Assert.True(ms < 15,
            $"Edit + render-visible-lines on 100K-line doc took {ms:F1}ms — expected <15ms.");
    }

    [Fact]
    public void GetLineText_ReadsFromPiecesNotCachedText()
    {
        // Verify that GetLineText doesn't trigger full text materialization
        var sb = new StringBuilder(10_000 * 50);
        for (int i = 0; i < 10_000; i++)
        {
            sb.Append($"Line {i:D5} content\n");
        }

        var doc = new Hex1bDocument(sb.ToString());

        // Edit without reading full text
        doc.Apply(new InsertOperation(new DocumentOffset(0), "X"));

        // Reading a single line should be fast (from pieces)
        var sw = Stopwatch.StartNew();
        var lineText = doc.GetLineText(5000);
        sw.Stop();

        Assert.StartsWith("Line ", lineText);
        Assert.True(sw.Elapsed.TotalMilliseconds < 5,
            $"GetLineText on 10K-line doc took {sw.Elapsed.TotalMilliseconds:F1}ms — expected <5ms.");
    }

    [Fact]
    public void OffsetToPosition_BinarySearch_Fast()
    {
        // Verify O(log L) OffsetToPosition on large doc
        var sb = new StringBuilder(100_000 * 50);
        for (int i = 0; i < 100_000; i++)
        {
            sb.Append($"Line {i:D6} with some content here for padding\n");
        }

        var doc = new Hex1bDocument(sb.ToString());

        // Measure 10000 OffsetToPosition calls
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
        {
            doc.OffsetToPosition(new DocumentOffset(i * 40));
        }
        sw.Stop();

        var ms = sw.Elapsed.TotalMilliseconds;
        // 10K binary searches on 100K-entry list should be <10ms
        Assert.True(ms < 50,
            $"10K OffsetToPosition calls took {ms:F1}ms — expected <50ms.");
    }
}
