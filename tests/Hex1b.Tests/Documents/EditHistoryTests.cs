using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

/// <summary>
/// Tests for EditHistory: undo/redo stacks, grouping, typing coalescing, cursor snapshots.
/// NOTE: Coalescing timeout tests use the default CoalesceTimeoutMs (1000ms).
/// These tests may need updates if coalescing behavior changes.
/// </summary>
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

    [Fact]
    public void RecordEdit_PushesToUndoStack()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);

        Assert.True(history.CanUndo);
        Assert.Equal(1, history.UndoCount);
    }

    [Fact]
    public void Undo_ReturnsGroup_ClearsFromUndoStack()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);

        var group = history.Undo();

        Assert.NotNull(group);
        Assert.Single(group!.Operations);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
    }

    [Fact]
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

        Assert.NotNull(group);
        Assert.Single(group!.Operations);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void NewEdit_ClearsRedoStack()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(0);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "a"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 0, 1);

        history.Undo();
        Assert.True(history.CanRedo);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(0), "b"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(0), new DocumentOffset(1))),
            cursors, 1, 2);

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Undo_ReturnsNull_WhenEmpty()
    {
        var history = new EditHistory();
        Assert.Null(history.Undo());
    }

    [Fact]
    public void Redo_ReturnsNull_WhenEmpty()
    {
        var history = new EditHistory();
        Assert.Null(history.Redo());
    }

    // ── Cursor snapshots ────────────────────────────────────────

    [Fact]
    public void UndoGroup_HasCursorsBefore()
    {
        var history = new EditHistory();
        var cursors = MakeCursors(5);

        history.RecordEdit(
            new InsertOperation(new DocumentOffset(5), "x"),
            new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(6))),
            cursors, 0, 1);

        var group = history.Undo();
        Assert.NotNull(group);
        Assert.Equal(5, group!.CursorsBefore.Entries[0].Position.Value);
    }

    [Fact]
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
        Assert.NotNull(group!.CursorsAfter);
    }

    // ── Explicit grouping ───────────────────────────────────────

    [Fact]
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

        Assert.Equal(1, history.UndoCount);
        var group = history.Undo();
        Assert.Equal(2, group!.Operations.Count);
        Assert.Equal(2, group.InverseOperations.Count);
    }

    [Fact]
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
        Assert.Equal(0, history.UndoCount); // Not committed yet

        history.CommitGroup(cursors, 1); // Outer commit
        Assert.Equal(1, history.UndoCount);
    }

    [Fact]
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

        Assert.False(history.CanUndo);
    }

    // ── Typing coalescing ───────────────────────────────────────

    [Fact]
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
        Assert.Equal(1, history.UndoCount);
        var group = history.Undo();
        Assert.Equal(3, group!.Operations.Count);
    }

    [Fact]
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

        Assert.Equal(2, history.UndoCount);
    }

    [Fact]
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
        Assert.Equal(2, history.UndoCount);
    }

    [Fact]
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

        Assert.Equal(2, history.UndoCount);
    }

    // ── Clear ───────────────────────────────────────────────────

    [Fact]
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

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
    }

    // ── GetGroupsSince ──────────────────────────────────────────

    [Fact]
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
        Assert.Equal(2, since.Count);

        var sinceLast = history.GetGroupsSince(1);
        Assert.Single(sinceLast);
    }

    // ── InverseOperations order ─────────────────────────────────

    [Fact]
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
        Assert.IsType<DeleteOperation>(group.InverseOperations[0]);
        Assert.IsType<DeleteOperation>(group.InverseOperations[1]);
        var delB = (DeleteOperation)group.InverseOperations[0];
        var delA = (DeleteOperation)group.InverseOperations[1];
        Assert.Equal(1, delB.Range.Start.Value);
        Assert.Equal(0, delA.Range.Start.Value);
    }

    // ── Multiple undo/redo cycles ───────────────────────────────

    [Fact]
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

        Assert.Equal(5, history.UndoCount);

        // Undo all
        for (var i = 0; i < 5; i++)
        {
            Assert.NotNull(history.Undo());
        }
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(5, history.RedoCount);

        // Redo all
        for (var i = 0; i < 5; i++)
        {
            Assert.NotNull(history.Redo());
        }
        Assert.Equal(5, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
    }
}
