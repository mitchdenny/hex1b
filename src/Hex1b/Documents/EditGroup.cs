namespace Hex1b.Documents;

/// <summary>
/// An atomic group of edit operations that are undone/redone together.
/// Captures cursor state before and after for full undo fidelity.
/// </summary>
public sealed class EditGroup
{
    private readonly List<EditOperation> _operations = [];
    private readonly List<EditOperation> _inverseOperations = [];

    /// <summary>Cursor positions before the group was applied.</summary>
    public CursorSetSnapshot CursorsBefore { get; }

    /// <summary>Cursor positions after the group was applied (set on commit).</summary>
    public CursorSetSnapshot? CursorsAfter { get; internal set; }

    /// <summary>Document version before the first operation in the group.</summary>
    public long VersionBefore { get; }

    /// <summary>Document version after the last operation in the group.</summary>
    public long VersionAfter { get; internal set; }

    /// <summary>Source tag for the edit (e.g., "typing", "paste", "undo").</summary>
    public string? Source { get; }

    /// <summary>The operations in this group (in application order).</summary>
    public IReadOnlyList<EditOperation> Operations => _operations;

    /// <summary>The inverse operations (in reverse application order for undo).</summary>
    public IReadOnlyList<EditOperation> InverseOperations => _inverseOperations;

    /// <summary>Whether this group can be coalesced with adjacent typing groups.</summary>
    public bool IsCoalescable { get; init; }

    /// <summary>Timestamp when the group was created (for coalescing timeout).</summary>
    public long CreatedTicks { get; } = Environment.TickCount64;

    public EditGroup(CursorSetSnapshot cursorsBefore, long versionBefore, string? source = null)
    {
        CursorsBefore = cursorsBefore;
        VersionBefore = versionBefore;
        Source = source;
    }

    /// <summary>Add an operation and its inverse to the group.</summary>
    internal void AddOperation(EditOperation operation, EditOperation inverse)
    {
        _operations.Add(operation);
        _inverseOperations.Insert(0, inverse); // Inverse operations are in reverse order
    }

    /// <summary>Whether this group has any operations.</summary>
    public bool IsEmpty => _operations.Count == 0;
}
