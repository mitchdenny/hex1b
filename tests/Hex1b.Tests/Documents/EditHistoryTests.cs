using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

/// <summary>
/// Tests for EditHistory: undo/redo stacks, grouping, typing coalescing, cursor snapshots.
/// NOTE: Coalescing timeout tests use the default CoalesceTimeoutMs (1000ms).
/// These tests may need updates if coalescing behavior changes.
/// </summary>
[TestClass]
public class EditHistoryTests
{
    private static CursorSet MakeCursors(params int[] positions)
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(positions[0]);
        for (var i = 1; i < positions.Length; i++)
        {
            set.Add(new DocumentOffset(positions[i]));
        }
        return set;
    }

    // ── Basic record/undo/redo ──────────────────────────────────

    [TestMethod]
    public void RecordEdit_PushesToUndoStack()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);

        Assert.IsTrue(history.CanUndo);
        Assert.AreEqual(1, history.UndoCount);
    }

    [TestMethod]
    public void Undo_ReturnsGroup_ClearsFromUndoStack()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);

        var group = history.Undo();

        Assert.IsNotNull(group);
        TestSeq.Single(group!.Operations);
        Assert.IsFalse(history.CanUndo);
        Assert.IsTrue(history.CanRedo);
    }

    [TestMethod]
    public void Redo_ReturnsGroup()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);

        history.Undo();
        var group = history.Redo();

        Assert.IsNotNull(group);
        TestSeq.Single(group!.Operations);
        Assert.IsTrue(history.CanUndo);
        Assert.IsFalse(history.CanRedo);
    }

    [TestMethod]
    public void NewEdit_ClearsRedoStack()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);

        history.Undo();
        Assert.IsTrue(history.CanRedo);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "b"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 1, 2);

        Assert.IsFalse(history.CanRedo);
    }

    [TestMethod]
    public void Undo_ReturnsNull_WhenEmpty()
    {
        var history = new EditHistory();
        Assert.IsNull(history.Undo());
    }

    [TestMethod]
    public void Redo_ReturnsNull_WhenEmpty()
    {
        var history = new EditHistory();
        Assert.IsNull(history.Redo());
    }

    // ── Cursor snapshots ────────────────────────────────────────

    [TestMethod]
    public void UndoGroup_HasCursorsBefore()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(5);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(5), "x"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(6))),
            cursors, 0, 1);

        var group = history.Undo();
        Assert.IsNotNull(group);
        Assert.AreEqual(5, group!.CursorsBefore.Entries[0].Position.Value);
    }

    [TestMethod]
    public void UndoGroup_HasCursorsAfter()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(5);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(5), "x"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(6))),
            cursors, 0, 1);

        // cursors snapshot captures current state at recording time
        var group = history.Undo();
        Assert.IsNotNull(group!.CursorsAfter);
    }

    // ── Explicit grouping ───────────────────────────────────────

    [TestMethod]
    public void ExplicitGroup_CollectsMultipleOps()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.BeginGroup(cursors, 0, "batch");
        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);
        history.RecordEdit(
            new InsertOperation(new DocumentOffset(1), "b"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(1), new DocumentOffset(2))),
            cursors, 1, 2);
        history.CommitGroup(cursors, 2);

        Assert.AreEqual(1, history.UndoCount);
        var group = history.Undo();
        Assert.AreEqual(2, group!.Operations.Count);
        Assert.AreEqual(2, group.InverseOperations.Count);
    }

    [TestMethod]
    public void ExplicitGroup_Nested_OnlyOuterCommits()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.BeginGroup(cursors, 0);
        history.BeginGroup(cursors, 0); // Nested

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);

        history.CommitGroup(cursors, 1); // Inner commit — no push yet
        Assert.AreEqual(0, history.UndoCount); // Not committed yet

        history.CommitGroup(cursors, 1); // Outer commit
        Assert.AreEqual(1, history.UndoCount);
    }

    [TestMethod]
    public void CancelGroup_DropsOperations()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.BeginGroup(cursors, 0);
        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);
        history.CancelGroup();

        Assert.IsFalse(history.CanUndo);
    }

    // ── Typing coalescing ───────────────────────────────────────

    [TestMethod]
    public void Coalescing_MergesAdjacentSingleCharInserts()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        // Type "abc" one character at a time
        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1, coalescable: true);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(1), "b"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(1), new DocumentOffset(2))),
            cursors, 1, 2, coalescable: true);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(2), "c"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(2), new DocumentOffset(3))),
            cursors, 2, 3, coalescable: true);

        // All three should be in one group
        Assert.AreEqual(1, history.UndoCount);
        var group = history.Undo();
        Assert.AreEqual(3, group!.Operations.Count);
    }

    [TestMethod]
    public void Coalescing_DoesNotMerge_NonAdjacentInserts()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1, coalescable: true);

        // Non-adjacent: insert at offset 5 instead of 1
        history.RecordEdit(
            new InsertOperation(new DocumentOffset(5), "b"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(6))),
            cursors, 1, 2, coalescable: true);

        Assert.AreEqual(2, history.UndoCount);
    }

    [TestMethod]
    public void Coalescing_DoesNotMerge_MultiCharInserts()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "ab"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(2))),
            cursors, 0, 1, coalescable: true);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(2), "c"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(2), new DocumentOffset(3))),
            cursors, 1, 2, coalescable: true);

        // Multi-char insert doesn't coalesce (first op has text.Length > 1)
        Assert.AreEqual(2, history.UndoCount);
    }

    [TestMethod]
    public void Coalescing_DoesNotMerge_NonCoalescable()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1, coalescable: true);

        // Not coalescable
        history.RecordEdit(
            new InsertOperation(new DocumentOffset(1), "b"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(1), new DocumentOffset(2))),
            cursors, 1, 2, coalescable: false);

        Assert.AreEqual(2, history.UndoCount);
    }

    // ── Clear ───────────────────────────────────────────────────

    [TestMethod]
    public void Clear_RemovesAllHistory()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);

        history.Undo();
        history.Clear();

        Assert.IsFalse(history.CanUndo);
        Assert.IsFalse(history.CanRedo);
        Assert.AreEqual(0, history.UndoCount);
        Assert.AreEqual(0, history.RedoCount);
    }

    // ── GetGroupsSince ──────────────────────────────────────────

    [TestMethod]
    public void GetGroupsSince_ReturnsGroupsAfterVersion()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(1), "b"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(1), new DocumentOffset(2))),
            cursors, 1, 2, coalescable: false);

        var since = history.GetGroupsSince(0);
        Assert.AreEqual(2, since.Count);

        var sinceLast = history.GetGroupsSince(1);
        TestSeq.Single(sinceLast);
    }

    // ── InverseOperations order ─────────────────────────────────

    [TestMethod]
    public void InverseOperations_AreInReverseOrder()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.BeginGroup(cursors, 0);
        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);
        history.RecordEdit(
            new InsertOperation(new DocumentOffset(1), "b"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(1), new DocumentOffset(2))),
            cursors, 1, 2);
        history.CommitGroup(cursors, 2);

        var group = history.Undo()!;

        // Inverse should be in reverse: delete "b" first, then delete "a"
        TestSeq.IsType<DeleteOperation>(group.InverseOperations[0]);
        TestSeq.IsType<DeleteOperation>(group.InverseOperations[1]);
        var delB = (DeleteOperation)group.InverseOperations[0];
        var delA = (DeleteOperation)group.InverseOperations[1];
        Assert.AreEqual(1, delB.Range.Start.Value);
        Assert.AreEqual(0, delA.Range.Start.Value);
    }

    // ── Multiple undo/redo cycles ───────────────────────────────

    [TestMethod]
    public void MultipleUndoRedo_MaintainsCorrectState()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        for (var i = 0; i < 5; i++)
        {
            history.RecordEdit(
                new InsertOperation(new DocumentOffset(i), ((char)('a' + i)).ToString()),
                new DeleteOperation(new DocumentRange(new DocumentOffset(i), new DocumentOffset(i + 1))),
                cursors, i, i + 1, coalescable: false);
        }

        Assert.AreEqual(5, history.UndoCount);

        // Undo all
        for (var i = 0; i < 5; i++)
        {
            Assert.IsNotNull(history.Undo());
        }
        Assert.AreEqual(0, history.UndoCount);
        Assert.AreEqual(5, history.RedoCount);

        // Redo all
        for (var i = 0; i < 5; i++)
        {
            Assert.IsNotNull(history.Redo());
        }
        Assert.AreEqual(5, history.UndoCount);
        Assert.AreEqual(0, history.RedoCount);
    }
}
