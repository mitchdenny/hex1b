namespace Hex1b.Documents;

/// <summary>
/// Manages undo/redo history for an editor. Supports explicit grouping,
/// automatic typing coalescing, and cursor state restoration.
/// </summary>
public sealed class EditHistory
{
    private readonly Stack<EditGroup> _undoStack = new();
    private readonly Stack<EditGroup> _redoStack = new();
    private EditGroup? _openGroup;
    private int _nestingDepth;

    /// <summary>Maximum time in milliseconds between keystrokes for typing coalescing.</summary>
    public long CoalesceTimeoutMs { get; set; } = 1000;

    /// <summary>Whether there are operations to undo.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Whether there are operations to redo.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Number of undo groups on the stack.</summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>Number of redo groups on the stack.</summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// Begin a new explicit edit group. Nested calls increment a counter;
    /// only the outermost Commit actually pushes the group.
    /// </summary>
    public void BeginGroup(CursorSet cursors, long documentVersion, string? source = null)
    {
        if (_nestingDepth == 0)
        {
            CommitOpenGroup();
            _openGroup = new EditGroup(cursors.Snapshot(), documentVersion, source);
        }
        _nestingDepth++;
    }

    /// <summary>
    /// Commit the current edit group. Only the outermost commit pushes to undo stack.
    /// </summary>
    public void CommitGroup(CursorSet cursors, long documentVersion)
    {
        if (_nestingDepth <= 0) return;
        _nestingDepth--;

        if (_nestingDepth == 0 && _openGroup != null)
        {
            _openGroup.CursorsAfter = cursors.Snapshot();
            _openGroup.VersionAfter = documentVersion;

            if (!_openGroup.IsEmpty)
            {
                _undoStack.Push(_openGroup);
                _redoStack.Clear();
            }
            _openGroup = null;
        }
    }

    /// <summary>
    /// Cancel the current edit group. Drops all collected operations.
    /// The caller is responsible for reverting the document state.
    /// </summary>
    public void CancelGroup()
    {
        if (_nestingDepth <= 0) return;
        _nestingDepth--;

        if (_nestingDepth == 0)
        {
            _openGroup = null;
        }
    }

    /// <summary>
    /// Record an edit for potential coalescing with adjacent typing edits.
    /// If the edit can be coalesced with the previous group, it is appended.
    /// Otherwise, a new group is created.
    /// </summary>
    public void RecordEdit(
        EditOperation operation,
        EditOperation inverse,
        CursorSet cursors,
        long versionBefore,
        long versionAfter,
        bool coalescable = false)
    {
        if (_nestingDepth > 0 && _openGroup != null)
        {
            // Inside explicit group — just append
            _openGroup.AddOperation(operation, inverse);
            return;
        }

        if (coalescable && TryCoalesce(operation, inverse, cursors, versionAfter))
        {
            return;
        }

        // Start new group
        CommitOpenGroup();
        var group = new EditGroup(cursors.Snapshot(), versionBefore) { IsCoalescable = coalescable };
        group.AddOperation(operation, inverse);
        group.CursorsAfter = cursors.Snapshot();
        group.VersionAfter = versionAfter;
        _undoStack.Push(group);
        _redoStack.Clear();
    }

    /// <summary>
    /// Undo the last edit group. Returns the group for the caller to apply
    /// inverse operations and restore cursors.
    /// </summary>
    public EditGroup? Undo()
    {
        CommitOpenGroup();
        if (_undoStack.Count == 0) return null;

        var group = _undoStack.Pop();
        _redoStack.Push(group);
        return group;
    }

    /// <summary>
    /// Redo the last undone edit group. Returns the group for the caller to
    /// re-apply operations and restore cursors.
    /// </summary>
    public EditGroup? Redo()
    {
        CommitOpenGroup();
        if (_redoStack.Count == 0) return null;

        var group = _redoStack.Pop();
        _undoStack.Push(group);
        return group;
    }

    /// <summary>Clear all history.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _openGroup = null;
        _nestingDepth = 0;
    }

    /// <summary>
    /// Push a fully-formed group directly onto the undo stack.
    /// Used by EditorState for multi-operation batches.
    /// </summary>
    internal void PushGroup(EditGroup group)
    {
        if (group.IsEmpty) return;
        CommitOpenGroup();
        _undoStack.Push(group);
        _redoStack.Clear();
    }

    /// <summary>
    /// Get all groups since a specific document version (for sync/collaboration).
    /// Returns groups in chronological order.
    /// </summary>
    internal IReadOnlyList<EditGroup> GetGroupsSince(long version)
    {
        var result = new List<EditGroup>();
        foreach (var group in _undoStack.Reverse())
        {
            if (group.VersionAfter > version)
            {
                result.Add(group);
            }
        }
        return result;
    }

    // ── Private helpers ──────────────────────────────────────────

    private void CommitOpenGroup()
    {
        // Auto-commit any lingering non-explicit open group
        // This shouldn't happen in normal flow, but handles edge cases
        if (_nestingDepth > 0 && _openGroup != null)
        {
            // Force close
            _nestingDepth = 0;
            if (!_openGroup.IsEmpty)
            {
                _undoStack.Push(_openGroup);
                _redoStack.Clear();
            }
            _openGroup = null;
        }
    }

    private bool TryCoalesce(
        EditOperation operation,
        EditOperation inverse,
        CursorSet cursors,
        long versionAfter)
    {
        if (_undoStack.Count == 0) return false;

        var last = _undoStack.Peek();
        if (!last.IsCoalescable) return false;

        // Check timeout
        var elapsed = Environment.TickCount64 - last.CreatedTicks;
        if (elapsed > CoalesceTimeoutMs) return false;

        // Only coalesce single-character inserts at adjacent positions
        if (operation is InsertOperation insert && insert.Text.Length == 1
            && last.Operations.Count > 0
            && last.Operations[^1] is InsertOperation lastInsert
            && lastInsert.Text.Length == 1)
        {
            // Adjacent: new insert offset == last insert offset + last insert length
            var expectedOffset = lastInsert.Offset + lastInsert.Text.Length;
            if (insert.Offset == expectedOffset)
            {
                last.AddOperation(operation, inverse);
                last.CursorsAfter = cursors.Snapshot();
                last.VersionAfter = versionAfter;
                return true;
            }
        }

        return false;
    }
}
