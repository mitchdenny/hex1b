using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Seed-based fuzz testing for editor operations. Each seed produces a deterministic
/// sequence of random operations (insert, delete, select, move, undo, redo) that
/// exercises the full editor state machine. When a seed fails, it's fully reproducible.
/// </summary>
public class EditorFuzzTests
{
    /// <summary>
    /// Operations the fuzzer can perform. Weighted towards edits and selection
    /// since those are the most crash-prone paths.
    /// </summary>
    private enum FuzzOp
    {
        InsertChar,
        InsertNewline,
        DeleteBackward,
        DeleteForward,
        MoveLeft,
        MoveRight,
        MoveUp,
        MoveDown,
        SelectLeft,
        SelectRight,
        SelectAll,
        MouseSelect,
        Undo,
        Redo,
        DeleteWordBackward,
        DeleteWordForward,
        MoveToLineStart,
        MoveToLineEnd,
    }

    private static readonly FuzzOp[] AllOps = Enum.GetValues<FuzzOp>();

    /// <summary>
    /// Runs a single fuzz iteration with the given seed. Returns the operation log
    /// for diagnostics on failure.
    /// </summary>
    private static List<string> RunFuzz(int seed, int iterations, string initialText)
    {
        var doc = new Hex1bDocument(initialText);
        var state = new EditorState(doc);
        var rng = new Random(seed);
        var log = new List<string>(iterations);

        for (var i = 0; i < iterations; i++)
        {
            var op = AllOps[rng.Next(AllOps.Length)];
            log.Add($"[{i}] {op} (docLen={doc.Length}, cursor={state.Cursor.Position.Value}, sel={state.Cursor.HasSelection})");

            switch (op)
            {
                case FuzzOp.InsertChar:
                    state.InsertText(((char)('a' + rng.Next(26))).ToString());
                    break;
                case FuzzOp.InsertNewline:
                    state.InsertText("\n");
                    break;
                case FuzzOp.DeleteBackward:
                    state.DeleteBackward();
                    break;
                case FuzzOp.DeleteForward:
                    state.DeleteForward();
                    break;
                case FuzzOp.MoveLeft:
                    state.MoveCursor(CursorDirection.Left);
                    break;
                case FuzzOp.MoveRight:
                    state.MoveCursor(CursorDirection.Right);
                    break;
                case FuzzOp.MoveUp:
                    state.MoveCursor(CursorDirection.Up);
                    break;
                case FuzzOp.MoveDown:
                    state.MoveCursor(CursorDirection.Down);
                    break;
                case FuzzOp.SelectLeft:
                    state.MoveCursor(CursorDirection.Left, extend: true);
                    break;
                case FuzzOp.SelectRight:
                    state.MoveCursor(CursorDirection.Right, extend: true);
                    break;
                case FuzzOp.SelectAll:
                    state.SelectAll();
                    break;
                case FuzzOp.MouseSelect:
                    // Simulate a mouse drag: random anchor + random position
                    var a = rng.Next(0, Math.Max(1, doc.Length + 1));
                    var b = rng.Next(0, Math.Max(1, doc.Length + 1));
                    state.Cursor.SelectionAnchor = new DocumentOffset(Math.Min(a, doc.Length));
                    state.Cursor.Position = new DocumentOffset(Math.Min(b, doc.Length));
                    break;
                case FuzzOp.Undo:
                    state.Undo();
                    break;
                case FuzzOp.Redo:
                    state.Redo();
                    break;
                case FuzzOp.DeleteWordBackward:
                    state.DeleteWordBackward();
                    break;
                case FuzzOp.DeleteWordForward:
                    state.DeleteWordForward();
                    break;
                case FuzzOp.MoveToLineStart:
                    state.MoveToLineStart();
                    break;
                case FuzzOp.MoveToLineEnd:
                    state.MoveToLineEnd();
                    break;
            }

            // Invariants that must always hold
            Assert.True(state.Cursor.Position.Value >= 0,
                $"Seed {seed}, iteration {i} ({op}): cursor {state.Cursor.Position.Value} < 0");
            Assert.True(state.Cursor.Position.Value <= doc.Length,
                $"Seed {seed}, iteration {i} ({op}): cursor {state.Cursor.Position.Value} > doc.Length {doc.Length}");
            Assert.True(doc.Length >= 0,
                $"Seed {seed}, iteration {i} ({op}): doc.Length {doc.Length} < 0");

            if (state.Cursor.HasSelection)
            {
                Assert.True(state.Cursor.SelectionAnchor!.Value.Value >= 0,
                    $"Seed {seed}, iteration {i} ({op}): anchor < 0");
                // Note: anchor can momentarily exceed doc.Length after external edits,
                // but the next operation should clamp it
            }
        }

        return log;
    }

    // ── Bulk fuzz: many seeds, moderate iterations ──────────────

    [Theory]
    [MemberData(nameof(FuzzSeeds))]
    public void Fuzz_WithSeed_NeverThrows(int seed)
    {
        try
        {
            RunFuzz(seed, iterations: 500, initialText: "Hello World\nSecond line\nThird line\nFourth line");
        }
        catch (Exception ex)
        {
            // Re-throw with seed info for reproducibility
            throw new Exception($"Fuzz failed with seed {seed}. Re-run with this seed to reproduce.", ex);
        }
    }

    public static TheoryData<int> FuzzSeeds()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < 100; i++)
            data.Add(i);
        return data;
    }

    // ── Targeted scenarios ──────────────────────────────────────

    [Fact]
    public void Fuzz_EmptyDocument_NeverThrows()
    {
        for (var seed = 0; seed < 50; seed++)
            RunFuzz(seed, iterations: 200, initialText: "");
    }

    [Fact]
    public void Fuzz_SingleCharDocument_NeverThrows()
    {
        for (var seed = 0; seed < 50; seed++)
            RunFuzz(seed, iterations: 200, initialText: "X");
    }

    [Fact]
    public void Fuzz_MultiByteContent_NeverThrows()
    {
        for (var seed = 0; seed < 50; seed++)
            RunFuzz(seed, iterations: 200, initialText: "café 😀 日本語\nLine 2 🚀\n");
    }

    [Fact]
    public void Fuzz_LongDocument_NeverThrows()
    {
        var longText = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"Line {i}: The quick brown fox jumps over the lazy dog."));
        for (var seed = 0; seed < 20; seed++)
            RunFuzz(seed, iterations: 300, initialText: longText);
    }

    [Fact]
    public void Fuzz_StaleSelectionAfterExternalEdit_NeverThrows()
    {
        // Targeted test: selection set, document shrinks externally, then type
        var doc = new Hex1bDocument("ABCDEFGHIJ");
        var state = new EditorState(doc);

        state.Cursor.Position = new DocumentOffset(10);
        state.Cursor.SelectionAnchor = new DocumentOffset(0);

        // External edit shrinks document
        doc.Apply(new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(10))));

        // Must not throw
        state.InsertText("Z");
        Assert.True(state.Cursor.Position.Value <= doc.Length);
    }
}
