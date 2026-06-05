namespace Hex1b.Events;

/// <summary>
/// The reason a <see cref="ListSelectionChangedEventArgs{T}"/> was raised.
/// Surfaced via <see cref="ListSelectionChangedEventArgs{T}.Reason"/> so
/// handlers can distinguish a user-toggle from a select-all from a
/// programmatic mutation.
/// </summary>
public enum ListSelectionChangeReason
{
    /// <summary>
    /// A single row's checked state was toggled (the default Space binding).
    /// </summary>
    Toggle,

    /// <summary>
    /// A contiguous range of rows was added to or removed from the selection
    /// (the default Shift+Arrow / Shift+Home / Shift+End bindings).
    /// </summary>
    ExtendRange,

    /// <summary>
    /// Every row was added to the selection (Ctrl+A when not already
    /// all-selected).
    /// </summary>
    SelectAll,

    /// <summary>
    /// Every row was removed from the selection (Ctrl+A when already
    /// all-selected).
    /// </summary>
    DeselectAll,

    /// <summary>
    /// The selection set was replaced via the controlled
    /// <c>SelectedIndices</c> widget property rather than user input.
    /// </summary>
    Programmatic,
}
