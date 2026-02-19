namespace Hex1b.Documents;

/// <summary>
/// Represents a cursor position in a document with optional selection.
/// </summary>
public class DocumentCursor
{
    private DocumentOffset _position;

    /// <summary>The cursor position as an absolute document offset.</summary>
    public DocumentOffset Position
    {
        get => _position;
        set => _position = value;
    }

    /// <summary>
    /// The selection anchor. When non-null, text between anchor and position is selected.
    /// </summary>
    public DocumentOffset? SelectionAnchor { get; set; }

    public bool HasSelection => SelectionAnchor is not null && SelectionAnchor.Value != Position;

    public DocumentOffset SelectionStart =>
        HasSelection ? (SelectionAnchor!.Value < Position ? SelectionAnchor.Value : Position) : Position;

    public DocumentOffset SelectionEnd =>
        HasSelection ? (SelectionAnchor!.Value > Position ? SelectionAnchor.Value : Position) : Position;

    public DocumentRange SelectionRange => new(SelectionStart, SelectionEnd);

    public void ClearSelection() => SelectionAnchor = null;

    /// <summary>
    /// Sets the selection anchor to the current position if not already set.
    /// Used by the extend pattern: first Shift+move sets anchor, subsequent moves extend.
    /// </summary>
    public void EnsureSelectionAnchor()
    {
        SelectionAnchor ??= Position;
    }

    /// <summary>Clamp cursor and anchor to valid range.</summary>
    public void Clamp(int documentLength)
    {
        if (Position.Value > documentLength)
            Position = new DocumentOffset(documentLength);
        if (SelectionAnchor is not null && SelectionAnchor.Value.Value > documentLength)
            SelectionAnchor = new DocumentOffset(documentLength);
    }
}
