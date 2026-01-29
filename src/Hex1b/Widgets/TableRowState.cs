namespace Hex1b.Widgets;

/// <summary>
/// Provides state information about a table row during rendering.
/// </summary>
public record TableRowState
{
    /// <summary>
    /// True if this row currently has keyboard focus (navigation cursor is here).
    /// </summary>
    public bool IsFocused { get; init; }

    /// <summary>
    /// True if this row is in the selection set (marked/chosen).
    /// </summary>
    public bool IsSelected { get; init; }

    /// <summary>
    /// Zero-based index of this row in the data source.
    /// </summary>
    public int RowIndex { get; init; }

    /// <summary>
    /// True if this is the first row in the data.
    /// </summary>
    public bool IsFirst { get; init; }

    /// <summary>
    /// True if this is the last row in the data.
    /// </summary>
    public bool IsLast { get; init; }

    /// <summary>
    /// The key value for this row (from WithRowKey selector, or row index if not specified).
    /// </summary>
    public object RowKey { get; init; } = null!;

    /// <summary>
    /// True if the row index is even (useful for alternating row styles/zebra striping).
    /// </summary>
    public bool IsEven => RowIndex % 2 == 0;

    /// <summary>
    /// True if the row index is odd.
    /// </summary>
    public bool IsOdd => !IsEven;
}
