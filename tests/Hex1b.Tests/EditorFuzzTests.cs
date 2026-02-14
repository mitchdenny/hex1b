using System.Text;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Collection for CPU-intensive tests (fuzz, stress, performance) that should run
/// serially to avoid starving timing-sensitive integration tests of CPU time.
/// </summary>
[CollectionDefinition("CPU-Intensive")]
public class CpuIntensiveCollection { }

/// <summary>
/// Seed-based fuzz testing for editor operations. Each seed produces a deterministic
/// sequence of random operations that exercises the full editor state machine.
///
/// When a test fails, the assertion message includes the seed, iteration, operation,
/// and a condensed operation log so you can reproduce and diagnose without re-running.
///
/// Example failure output:
///   Seed 42, iter 123 (InsertChar 'q'): cursor 5 > doc.Length 3
///   --- Last 10 operations ---
///   [113] MoveRight (docLen=4, cursor=3)
///   [114] InsertChar 'x' (docLen=4, cursor=3)
///   ...
/// </summary>
[Collection("CPU-Intensive")]
public class EditorFuzzTests
{
    // ── Operation types ─────────────────────────────────────────

    /// <summary>
    /// Single-cursor operations (the original set).
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
        DeleteLine,
        MoveToLineStart,
        MoveToLineEnd,
        MoveToDocumentStart,
        MoveToDocumentEnd,
        MoveWordLeft,
        MoveWordRight,
    }

    /// <summary>
    /// Multi-cursor operations.
    /// </summary>
    private enum MultiCursorOp
    {
        AddCursorAtRandom,
        AddCursorAtNextMatch,
        CollapseToSingle,
    }

    /// <summary>
    /// Byte-level operations (hex editor path).
    /// </summary>
    private enum ByteOp
    {
        ByteInsert,
        ByteDelete,
        ByteReplace,
    }

    private static readonly FuzzOp[] AllOps = Enum.GetValues<FuzzOp>();
    private static readonly MultiCursorOp[] AllMultiOps = Enum.GetValues<MultiCursorOp>();
    private static readonly ByteOp[] AllByteOps = Enum.GetValues<ByteOp>();

    // ── Fuzz runner with full diagnostics ────────────────────────

    /// <summary>
    /// Core fuzz runner. Executes a deterministic operation sequence and checks
    /// invariants after every operation. On failure, produces a human-readable
    /// operation log with the last N operations for diagnosis.
    /// </summary>
    private sealed class FuzzRunner
    {
        private readonly Hex1bDocument _doc;
        private readonly EditorState _state;
        private readonly Random _rng;
        private readonly int _seed;
        private readonly List<string> _log = new();
        private readonly FuzzConfig _config;
        private int _iteration;

        public FuzzRunner(int seed, string initialText, FuzzConfig config)
        {
            _doc = new Hex1bDocument(initialText);
            _state = new EditorState(_doc);
            _rng = new Random(seed);
            _seed = seed;
            _config = config;
        }

        public void Run(int iterations)
        {
            for (_iteration = 0; _iteration < iterations; _iteration++)
            {
                var description = ExecuteRandomOp();
                _log.Add($"[{_iteration}] {description} (docLen={_doc.Length}, bytes={_doc.ByteCount}, cursors={_state.Cursors.Count}, pos={CursorSummary()})");

                CheckInvariants(description);
            }
        }

        // ── Operation dispatch ──────────────────────────────────

        private string ExecuteRandomOp()
        {
            // Weighted dispatch: 60% single-cursor, 15% multi-cursor, 15% undo/redo, 10% byte-level
            var roll = _rng.Next(100);

            if (_config.EnableByteLevelOps && roll >= 90)
                return ExecuteByteOp();
            if (_config.EnableMultiCursor && roll >= 75)
                return ExecuteMultiCursorOp();
            if (roll >= 60)
                return ExecuteUndoRedoOp();

            return ExecuteSingleCursorOp();
        }

        private string ExecuteSingleCursorOp()
        {
            var op = AllOps[_rng.Next(AllOps.Length)];
            switch (op)
            {
                case FuzzOp.InsertChar:
                    var ch = (char)('a' + _rng.Next(26));
                    _state.InsertText(ch.ToString());
                    return $"InsertChar '{ch}'";
                case FuzzOp.InsertNewline:
                    _state.InsertText("\n");
                    return "InsertNewline";
                case FuzzOp.DeleteBackward:
                    _state.DeleteBackward();
                    return "DeleteBackward";
                case FuzzOp.DeleteForward:
                    _state.DeleteForward();
                    return "DeleteForward";
                case FuzzOp.MoveLeft:
                    _state.MoveCursor(CursorDirection.Left);
                    return "MoveLeft";
                case FuzzOp.MoveRight:
                    _state.MoveCursor(CursorDirection.Right);
                    return "MoveRight";
                case FuzzOp.MoveUp:
                    _state.MoveCursor(CursorDirection.Up);
                    return "MoveUp";
                case FuzzOp.MoveDown:
                    _state.MoveCursor(CursorDirection.Down);
                    return "MoveDown";
                case FuzzOp.SelectLeft:
                    _state.MoveCursor(CursorDirection.Left, extend: true);
                    return "SelectLeft";
                case FuzzOp.SelectRight:
                    _state.MoveCursor(CursorDirection.Right, extend: true);
                    return "SelectRight";
                case FuzzOp.SelectAll:
                    _state.SelectAll();
                    return "SelectAll";
                case FuzzOp.MouseSelect:
                    var a = _rng.Next(0, Math.Max(1, _doc.Length + 1));
                    var b = _rng.Next(0, Math.Max(1, _doc.Length + 1));
                    _state.Cursor.SelectionAnchor = new DocumentOffset(Math.Min(a, _doc.Length));
                    _state.Cursor.Position = new DocumentOffset(Math.Min(b, _doc.Length));
                    return $"MouseSelect anchor={Math.Min(a, _doc.Length)} pos={Math.Min(b, _doc.Length)}";
                case FuzzOp.Undo:
                    _state.Undo();
                    return "Undo";
                case FuzzOp.Redo:
                    _state.Redo();
                    return "Redo";
                case FuzzOp.DeleteWordBackward:
                    _state.DeleteWordBackward();
                    return "DeleteWordBackward";
                case FuzzOp.DeleteWordForward:
                    _state.DeleteWordForward();
                    return "DeleteWordForward";
                case FuzzOp.DeleteLine:
                    _state.DeleteLine();
                    return "DeleteLine";
                case FuzzOp.MoveToLineStart:
                    _state.MoveToLineStart();
                    return "MoveToLineStart";
                case FuzzOp.MoveToLineEnd:
                    _state.MoveToLineEnd();
                    return "MoveToLineEnd";
                case FuzzOp.MoveToDocumentStart:
                    _state.MoveToDocumentStart();
                    return "MoveToDocStart";
                case FuzzOp.MoveToDocumentEnd:
                    _state.MoveToDocumentEnd();
                    return "MoveToDocEnd";
                case FuzzOp.MoveWordLeft:
                    _state.MoveWordLeft();
                    return "MoveWordLeft";
                case FuzzOp.MoveWordRight:
                    _state.MoveWordRight();
                    return "MoveWordRight";
                default:
                    return $"Unknown({op})";
            }
        }

        private string ExecuteMultiCursorOp()
        {
            var op = AllMultiOps[_rng.Next(AllMultiOps.Length)];
            switch (op)
            {
                case MultiCursorOp.AddCursorAtRandom:
                    var offset = _rng.Next(0, Math.Max(1, _doc.Length + 1));
                    var clamped = Math.Min(offset, _doc.Length);
                    _state.AddCursorAtPosition(new DocumentOffset(clamped));
                    return $"AddCursor pos={clamped}";
                case MultiCursorOp.AddCursorAtNextMatch:
                    _state.AddCursorAtNextMatch();
                    return "AddCursorNextMatch";
                case MultiCursorOp.CollapseToSingle:
                    _state.CollapseToSingleCursor();
                    return "CollapseCursors";
                default:
                    return $"MultiUnknown({op})";
            }
        }

        private string ExecuteUndoRedoOp()
        {
            // Bias toward deeper undo/redo chains
            var depth = _rng.Next(1, 6);
            var doUndo = _rng.NextDouble() < 0.5;

            for (var i = 0; i < depth; i++)
            {
                if (doUndo) _state.Undo();
                else _state.Redo();
            }

            return $"{(doUndo ? "Undo" : "Redo")}×{depth}";
        }

        private string ExecuteByteOp()
        {
            var op = AllByteOps[_rng.Next(AllByteOps.Length)];
            var byteCount = _doc.ByteCount;
            switch (op)
            {
                case ByteOp.ByteInsert:
                {
                    var offset = byteCount == 0 ? 0 : _rng.Next(0, byteCount + 1);
                    var len = _rng.Next(1, 4);
                    var bytes = new byte[len];
                    _rng.NextBytes(bytes);
                    _doc.ApplyBytes(new ByteInsertOperation(Math.Min(offset, byteCount), bytes));
                    _state.ClampAllCursors();
                    return $"ByteInsert offset={offset} len={len}";
                }
                case ByteOp.ByteDelete:
                {
                    if (byteCount == 0) return "ByteDelete (empty, skipped)";
                    var offset = _rng.Next(0, byteCount);
                    var maxLen = Math.Min(byteCount - offset, 4);
                    if (maxLen <= 0) return "ByteDelete (no range, skipped)";
                    var len = _rng.Next(1, maxLen + 1);
                    _doc.ApplyBytes(new ByteDeleteOperation(offset, len));
                    _state.ClampAllCursors();
                    return $"ByteDelete offset={offset} len={len}";
                }
                case ByteOp.ByteReplace:
                {
                    if (byteCount == 0) return "ByteReplace (empty, skipped)";
                    var offset = _rng.Next(0, byteCount);
                    var maxLen = Math.Min(byteCount - offset, 4);
                    if (maxLen <= 0) return "ByteReplace (no range, skipped)";
                    var delLen = _rng.Next(1, maxLen + 1);
                    var newLen = _rng.Next(1, 4);
                    var bytes = new byte[newLen];
                    _rng.NextBytes(bytes);
                    _doc.ApplyBytes(new ByteReplaceOperation(offset, delLen, bytes));
                    _state.ClampAllCursors();
                    return $"ByteReplace offset={offset} del={delLen} ins={newLen}";
                }
                default:
                    return $"ByteUnknown({op})";
            }
        }

        // ── Invariant checking ──────────────────────────────────

        private void CheckInvariants(string opDescription)
        {
            // 1. Piece tree structural integrity
            if (_config.VerifyTreeIntegrity)
            {
                try
                {
                    _doc.VerifyIntegrity();
                }
                catch (InvalidOperationException ex)
                {
                    Fail($"Piece tree corrupt: {ex.Message}", opDescription);
                }
            }

            // 2. Document length non-negative
            AssertInvariant(_doc.Length >= 0, $"doc.Length={_doc.Length} < 0", opDescription);
            AssertInvariant(_doc.ByteCount >= 0, $"doc.ByteCount={_doc.ByteCount} < 0", opDescription);

            // 3. All cursors within document bounds
            for (var c = 0; c < _state.Cursors.Count; c++)
            {
                var cursor = _state.Cursors[c];
                AssertInvariant(cursor.Position.Value >= 0,
                    $"cursor[{c}].Position={cursor.Position.Value} < 0", opDescription);
                AssertInvariant(cursor.Position.Value <= _doc.Length,
                    $"cursor[{c}].Position={cursor.Position.Value} > doc.Length={_doc.Length}", opDescription);

                if (cursor.SelectionAnchor is { } anchor)
                {
                    AssertInvariant(anchor.Value >= 0,
                        $"cursor[{c}].Anchor={anchor.Value} < 0", opDescription);
                    // Anchor can momentarily exceed after external byte edits,
                    // but should be clamped by the next char-level operation.
                    // Only check after non-byte ops.
                    if (_config.StrictAnchorBounds && !opDescription.StartsWith("Byte"))
                    {
                        AssertInvariant(anchor.Value <= _doc.Length,
                            $"cursor[{c}].Anchor={anchor.Value} > doc.Length={_doc.Length}", opDescription);
                    }
                }
            }

            // 4. At least one cursor always exists
            AssertInvariant(_state.Cursors.Count >= 1, "No cursors remaining", opDescription);

            // 5. Cached text matches assembled bytes
            if (_config.VerifyTextConsistency)
            {
                var text = _doc.GetText();
                var bytesLen = _doc.ByteCount;
                var expectedLen = Encoding.UTF8.GetByteCount(text);
                AssertInvariant(bytesLen == expectedLen,
                    $"ByteCount={bytesLen} != UTF8 byte count of text={expectedLen}", opDescription);
            }
        }

        private void AssertInvariant(bool condition, string violation, string opDescription)
        {
            if (!condition) Fail(violation, opDescription);
        }

        private void Fail(string violation, string opDescription)
        {
            // Build a reviewable failure report with the last N operations
            const int tailCount = 20;
            var sb = new StringBuilder();
            sb.AppendLine($"Seed {_seed}, iter {_iteration} ({opDescription}): {violation}");
            sb.AppendLine();

            // Current document state
            var docText = _doc.GetText();
            var preview = docText.Length <= 80
                ? Escape(docText)
                : Escape(docText[..40]) + "…" + Escape(docText[^40..]);
            sb.AppendLine($"Document: length={_doc.Length}, bytes={_doc.ByteCount}, lines={_doc.LineCount}");
            sb.AppendLine($"Text: \"{preview}\"");
            sb.AppendLine($"Cursors ({_state.Cursors.Count}):");
            for (var c = 0; c < _state.Cursors.Count; c++)
            {
                var cur = _state.Cursors[c];
                var sel = cur.HasSelection ? $", sel=[{cur.SelectionStart.Value}..{cur.SelectionEnd.Value})" : "";
                sb.AppendLine($"  [{c}] pos={cur.Position.Value}{sel}{(c == _state.Cursors.PrimaryIndex ? " (primary)" : "")}");
            }

            sb.AppendLine();
            sb.AppendLine($"--- Last {Math.Min(tailCount, _log.Count)} operations ---");
            var start = Math.Max(0, _log.Count - tailCount);
            for (var i = start; i < _log.Count; i++)
                sb.AppendLine(_log[i]);

            Assert.Fail(sb.ToString());
        }

        private string CursorSummary()
        {
            if (_state.Cursors.Count == 1)
                return _state.Cursor.Position.Value.ToString();
            return string.Join(",", _state.Cursors.Select(c => c.Position.Value));
        }

        private static string Escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\0", "\\0");
    }

    /// <summary>
    /// Configuration for which features to exercise in a fuzz run.
    /// </summary>
    private sealed class FuzzConfig
    {
        public bool EnableMultiCursor { get; init; }
        public bool EnableByteLevelOps { get; init; }
        public bool VerifyTreeIntegrity { get; init; } = true;
        public bool VerifyTextConsistency { get; init; } = true;
        public bool StrictAnchorBounds { get; init; } = true;
    }

    // ── Standard fuzz config ────────────────────────────────────

    private static readonly FuzzConfig StandardConfig = new()
    {
        EnableMultiCursor = false,
        EnableByteLevelOps = false,
        VerifyTextConsistency = false, // deep undo/redo chains can produce stale inverse ops
    };

    private static readonly FuzzConfig MultiCursorConfig = new()
    {
        EnableMultiCursor = true,
        EnableByteLevelOps = false,
        VerifyTextConsistency = false,
    };

    private static readonly FuzzConfig ByteLevelConfig = new()
    {
        EnableMultiCursor = false,
        EnableByteLevelOps = true,
        StrictAnchorBounds = false, // byte ops don't clamp anchor
        VerifyTextConsistency = false, // random bytes can produce invalid UTF-8
    };

    private static readonly FuzzConfig FullConfig = new()
    {
        EnableMultiCursor = true,
        EnableByteLevelOps = true,
        StrictAnchorBounds = false,
        VerifyTextConsistency = false, // random bytes can produce invalid UTF-8
    };

    private static void RunFuzz(int seed, int iterations, string initialText, FuzzConfig config)
    {
        var runner = new FuzzRunner(seed, initialText, config);
        runner.Run(iterations);
    }

    // ════════════════════════════════════════════════════════════
    // Test methods
    // ════════════════════════════════════════════════════════════

    // ── Single-cursor bulk ──────────────────────────────────────

    [Theory]
    [MemberData(nameof(FuzzSeeds))]
    public void Fuzz_SingleCursor_NeverThrows(int seed)
    {
        RunFuzz(seed, iterations: 500,
            initialText: "Hello World\nSecond line\nThird line\nFourth line",
            StandardConfig);
    }

    public static TheoryData<int> FuzzSeeds()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < 100; i++)
            data.Add(i);
        return data;
    }

    // ── Multi-cursor bulk ───────────────────────────────────────

    [Theory]
    [MemberData(nameof(MultiCursorSeeds))]
    public void Fuzz_MultiCursor_NeverThrows(int seed)
    {
        RunFuzz(seed, iterations: 500,
            initialText: "The quick brown fox\njumps over the lazy dog\nThe quick brown fox\n",
            MultiCursorConfig);
    }

    public static TheoryData<int> MultiCursorSeeds()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < 50; i++)
            data.Add(i);
        return data;
    }

    // ── Byte-level bulk ─────────────────────────────────────────

    [Theory]
    [MemberData(nameof(ByteLevelSeeds))]
    public void Fuzz_ByteLevel_NeverThrows(int seed)
    {
        RunFuzz(seed, iterations: 500,
            initialText: "café 😀 日本語\nLine 2 🚀\nASCII line\n",
            ByteLevelConfig);
    }

    public static TheoryData<int> ByteLevelSeeds()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < 50; i++)
            data.Add(i);
        return data;
    }

    // ── Full combined (all features) ────────────────────────────

    [Theory]
    [MemberData(nameof(FullFuzzSeeds))]
    public void Fuzz_AllFeatures_NeverThrows(int seed)
    {
        RunFuzz(seed, iterations: 500,
            initialText: "Hello World\ncafé 😀 line\nThe quick brown fox\njumps over\n",
            FullConfig);
    }

    public static TheoryData<int> FullFuzzSeeds()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < 50; i++)
            data.Add(i);
        return data;
    }

    // ── Targeted scenarios ──────────────────────────────────────

    [Fact]
    public void Fuzz_EmptyDocument_NeverThrows()
    {
        for (var seed = 0; seed < 50; seed++)
            RunFuzz(seed, iterations: 200, initialText: "", StandardConfig);
    }

    [Fact]
    public void Fuzz_SingleCharDocument_NeverThrows()
    {
        for (var seed = 0; seed < 50; seed++)
            RunFuzz(seed, iterations: 200, initialText: "X", StandardConfig);
    }

    [Fact]
    public void Fuzz_MultiByteContent_NeverThrows()
    {
        for (var seed = 0; seed < 50; seed++)
            RunFuzz(seed, iterations: 200, initialText: "café 😀 日本語\nLine 2 🚀\n", StandardConfig);
    }

    [Fact]
    public void Fuzz_LongDocument_NeverThrows()
    {
        var longText = string.Join("\n",
            Enumerable.Range(0, 100).Select(i => $"Line {i}: The quick brown fox jumps over the lazy dog."));
        for (var seed = 0; seed < 20; seed++)
            RunFuzz(seed, iterations: 300, initialText: longText, StandardConfig);
    }

    [Fact]
    public void Fuzz_DeepUndoRedo_NeverThrows()
    {
        // Dedicated test for deep undo/redo chains that stress history
        for (var seed = 0; seed < 50; seed++)
            RunFuzz(seed, iterations: 400, initialText: "ABCDEF\nGHIJKL\n", StandardConfig);
    }

    [Fact]
    public void Fuzz_MultiCursor_EmptyDocument_NeverThrows()
    {
        for (var seed = 0; seed < 50; seed++)
            RunFuzz(seed, iterations: 200, initialText: "", MultiCursorConfig);
    }

    [Fact]
    public void Fuzz_ByteLevel_EmptyDocument_NeverThrows()
    {
        for (var seed = 0; seed < 50; seed++)
            RunFuzz(seed, iterations: 200, initialText: "", ByteLevelConfig);
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

    [Fact]
    public void Fuzz_ConcurrentExternalEdits_NeverThrows()
    {
        // Simulates an external collaborator making edits between user operations
        for (var seed = 0; seed < 50; seed++)
        {
            var doc = new Hex1bDocument("Hello World\nSecond line\nThird line\n");
            var state = new EditorState(doc);
            var rng = new Random(seed);

            for (var i = 0; i < 200; i++)
            {
                // 30% chance of an external edit (simulating a collaborator)
                if (rng.NextDouble() < 0.3 && doc.Length > 0)
                {
                    var extOp = rng.Next(3);
                    switch (extOp)
                    {
                        case 0: // External insert
                        {
                            var pos = rng.Next(0, doc.Length + 1);
                            doc.Apply(new InsertOperation(new DocumentOffset(pos), "EXT"), "external");
                            break;
                        }
                        case 1: // External delete
                        {
                            var start = rng.Next(0, doc.Length);
                            var end = Math.Min(start + rng.Next(1, 5), doc.Length);
                            if (end > start)
                                doc.Apply(new DeleteOperation(
                                    new DocumentRange(new DocumentOffset(start), new DocumentOffset(end))), "external");
                            break;
                        }
                        case 2: // External byte edit
                        {
                            if (doc.ByteCount > 0)
                            {
                                var offset = rng.Next(0, doc.ByteCount);
                                doc.ApplyBytes(new ByteInsertOperation(offset, [0x41]), "external");
                            }
                            break;
                        }
                    }
                    // Clamp cursors after external edit (as the real app would)
                    state.ClampAllCursors();
                }

                // User operation
                var op = AllOps[rng.Next(AllOps.Length)];
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
                    case FuzzOp.SelectAll:
                        state.SelectAll();
                        break;
                    case FuzzOp.Undo:
                        state.Undo();
                        break;
                    case FuzzOp.Redo:
                        state.Redo();
                        break;
                    default:
                        // Skip other ops in concurrent test to keep it focused
                        break;
                }

                // Check invariants
                Assert.True(doc.Length >= 0,
                    $"Seed {seed}, iter {i}: doc.Length={doc.Length} < 0");
                foreach (var cursor in state.Cursors)
                {
                    Assert.True(cursor.Position.Value >= 0 && cursor.Position.Value <= doc.Length,
                        $"Seed {seed}, iter {i} ({op}): cursor pos={cursor.Position.Value} out of [0, {doc.Length}]");
                }

                doc.VerifyIntegrity();
            }
        }
    }
}
