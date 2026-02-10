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
}
