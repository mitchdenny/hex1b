using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

/// <summary>
/// Tests for CursorSet: sorted ordering, merging, snapshot/restore, multi-cursor operations.
/// NOTE: These tests may need updates when selection rendering or multi-cursor behaviors change.
/// </summary>
public class CursorSetTests
{
    // ── Construction ─────────────────────────────────────────────

    [Fact]
    public void Constructor_CreatesOneCursorAtOrigin()
    {
        var set = new CursorSet();
        Assert.Single(set);
        Assert.Equal(0, set.Primary.Position.Value);
        Assert.Equal(0, set.PrimaryIndex);
    }

    // ── Add ─────────────────────────────────────────────────────

    [Fact]
    public void Add_InsertsInSortedOrder()
    {
        var set = new CursorSet();
        set.Add(new DocumentOffset(10));
        set.Add(new DocumentOffset(5));

        Assert.Equal(3, set.Count);
        Assert.Equal(0, set[0].Position.Value);
        Assert.Equal(5, set[1].Position.Value);
        Assert.Equal(10, set[2].Position.Value);
    }

    [Fact]
    public void Add_AdjustsPrimaryIndex_WhenInsertedBefore()
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(10);
        set.Add(new DocumentOffset(5));

        // Primary was at index 0 (pos 10), add at index 0 (pos 5) pushes primary to index 1
        Assert.Equal(1, set.PrimaryIndex);
        Assert.Equal(10, set.Primary.Position.Value);
    }

    [Fact]
    public void Add_WithSelection()
    {
        var set = new CursorSet();
        var idx = set.Add(new DocumentOffset(10), new DocumentOffset(5));
        var cursor = set[idx];

        Assert.Equal(10, cursor.Position.Value);
        Assert.NotNull(cursor.SelectionAnchor);
        Assert.Equal(5, cursor.SelectionAnchor!.Value.Value);
    }

    [Fact]
    public void Add_MultipleCursors_MaintainsSortOrder()
    {
        var set = new CursorSet();
        set.Add(new DocumentOffset(20));
        set.Add(new DocumentOffset(10));
        set.Add(new DocumentOffset(30));
        set.Add(new DocumentOffset(15));

        Assert.Equal(5, set.Count);
        for (var i = 1; i < set.Count; i++)
        {
            Assert.True(set[i].Position >= set[i - 1].Position);
        }
    }

    // ── CollapseToSingle ────────────────────────────────────────

    [Fact]
    public void CollapseToSingle_KeepsPrimary()
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(5);
        set.Add(new DocumentOffset(10));
        set.Add(new DocumentOffset(20));

        set.CollapseToSingle();

        Assert.Single(set);
        Assert.Equal(5, set.Primary.Position.Value);
    }

    [Fact]
    public void CollapseToSingle_NoOp_WhenAlreadySingle()
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(5);
        set.CollapseToSingle();

        Assert.Single(set);
        Assert.Equal(5, set.Primary.Position.Value);
    }

    // ── MergeOverlapping ────────────────────────────────────────

    [Fact]
    public void MergeOverlapping_MergesSamePosition()
    {
        var set = new CursorSet();
        set.Add(new DocumentOffset(0)); // Duplicate of primary

        Assert.Equal(2, set.Count);
        set.MergeOverlapping();
        Assert.Single(set);
    }

    [Fact]
    public void MergeOverlapping_PreservesNonOverlapping()
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(5);
        set.Add(new DocumentOffset(10));
        set.Add(new DocumentOffset(20));

        set.MergeOverlapping();

        Assert.Equal(3, set.Count);
        Assert.Equal(5, set[0].Position.Value);
        Assert.Equal(10, set[1].Position.Value);
        Assert.Equal(20, set[2].Position.Value);
    }

    [Fact]
    public void MergeOverlapping_MergesOverlappingSelections()
    {
        var set = new CursorSet();
        // Cursor 0: selection 0..10
        set.Primary.Position = new DocumentOffset(10);
        set.Primary.SelectionAnchor = new DocumentOffset(0);
        // Add cursor with selection 5..15 (overlaps)
        set.Add(new DocumentOffset(15), new DocumentOffset(5));

        set.MergeOverlapping();

        Assert.Single(set);
        // Merged selection should span 0..15
    }

    [Fact]
    public void MergeOverlapping_PrefersPrimary()
    {
        var set = new CursorSet();
        var primary = set.Primary;
        primary.Position = new DocumentOffset(5);
        set.Add(new DocumentOffset(5)); // Same position

        set.MergeOverlapping();

        Assert.Single(set);
        Assert.Same(primary, set.Primary);
    }

    // ── InReverseOrder ──────────────────────────────────────────

    [Fact]
    public void InReverseOrder_IteratesHighestFirst()
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(5);
        set.Add(new DocumentOffset(10));
        set.Add(new DocumentOffset(20));

        var positions = new List<int>();
        foreach (var (cursor, index) in set.InReverseOrder())
        {
            positions.Add(cursor.Position.Value);
        }

        Assert.Equal(3, positions.Count);
        Assert.Equal(20, positions[0]);
        Assert.Equal(10, positions[1]);
        Assert.Equal(5, positions[2]);
    }

    [Fact]
    public void InReverseOrder_ProvicesCorrectIndices()
    {
        var set = new CursorSet();
        set.Add(new DocumentOffset(10));
        set.Add(new DocumentOffset(20));

        var indices = new List<int>();
        foreach (var (_, index) in set.InReverseOrder())
        {
            indices.Add(index);
        }

        Assert.Equal([2, 1, 0], indices);
    }

    // ── Snapshot/Restore ────────────────────────────────────────

    [Fact]
    public void Snapshot_CapturesAllCursors()
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(5);
        set.Primary.SelectionAnchor = new DocumentOffset(2);
        set.Add(new DocumentOffset(10));

        var snap = set.Snapshot();

        Assert.Equal(2, snap.Entries.Count);
        Assert.Equal(0, snap.PrimaryIndex);
        Assert.Equal(5, snap.Entries[0].Position.Value);
        Assert.Equal(new DocumentOffset(2), snap.Entries[0].SelectionAnchor);
        Assert.Equal(10, snap.Entries[1].Position.Value);
        Assert.Null(snap.Entries[1].SelectionAnchor);
    }

    [Fact]
    public void Restore_ReplacesAllCursors()
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(99);
        set.Add(new DocumentOffset(50));

        var snap = new CursorSetSnapshot(
            [
                new CursorSnapshotEntry(new DocumentOffset(3), null),
                new CursorSnapshotEntry(new DocumentOffset(7), new DocumentOffset(5))
            ],
            1);

        set.Restore(snap);

        Assert.Equal(2, set.Count);
        Assert.Equal(1, set.PrimaryIndex);
        Assert.Equal(3, set[0].Position.Value);
        Assert.Equal(7, set[1].Position.Value);
        Assert.Equal(new DocumentOffset(5), set[1].SelectionAnchor);
    }

    [Fact]
    public void Snapshot_Restore_Roundtrip()
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(5);
        set.Add(new DocumentOffset(10));
        set.Add(new DocumentOffset(15));

        var snap = set.Snapshot();

        // Mutate
        set.Primary.Position = new DocumentOffset(99);
        set.CollapseToSingle();

        // Restore
        set.Restore(snap);

        Assert.Equal(3, set.Count);
        Assert.Equal(5, set[0].Position.Value);
        Assert.Equal(10, set[1].Position.Value);
        Assert.Equal(15, set[2].Position.Value);
    }

    // ── ClampAll ────────────────────────────────────────────────

    [Fact]
    public void ClampAll_ClampsAllCursors()
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(50);
        set.Add(new DocumentOffset(100));

        set.ClampAll(30);

        Assert.Equal(30, set[0].Position.Value);
        Assert.Equal(30, set[1].Position.Value);
    }

    [Fact]
    public void ClampAll_ClampsSelectionAnchors()
    {
        var set = new CursorSet();
        set.Primary.Position = new DocumentOffset(10);
        set.Primary.SelectionAnchor = new DocumentOffset(50);

        set.ClampAll(20);

        Assert.Equal(10, set.Primary.Position.Value);
        Assert.Equal(20, set.Primary.SelectionAnchor!.Value.Value);
    }

    // ── Sort ────────────────────────────────────────────────────

    [Fact]
    public void Sort_ReordersCursorsAndTracksPrimary()
    {
        var set = new CursorSet();
        set.Add(new DocumentOffset(10));
        // Move primary (originally at 0) to position 15
        set.Primary.Position = new DocumentOffset(15);

        // Before sort: [primary(15), other(10)] — unsorted
        set.Sort();

        // After sort: [other(10), primary(15)]
        Assert.Equal(10, set[0].Position.Value);
        Assert.Equal(15, set[1].Position.Value);
        Assert.Equal(1, set.PrimaryIndex);
    }

    // ── IReadOnlyList ───────────────────────────────────────────

    [Fact]
    public void Enumeration_ReturnsAllCursors()
    {
        var set = new CursorSet();
        set.Add(new DocumentOffset(10));
        set.Add(new DocumentOffset(20));

        var count = 0;
        foreach (var cursor in set)
        {
            count++;
        }
        Assert.Equal(3, count);
    }

    [Fact]
    public void Count_ReflectsAdditions()
    {
        var set = new CursorSet();
        Assert.Single(set);
        set.Add(new DocumentOffset(10));
        Assert.Equal(2, set.Count);
        set.Add(new DocumentOffset(20));
        Assert.Equal(3, set.Count);
    }
}
