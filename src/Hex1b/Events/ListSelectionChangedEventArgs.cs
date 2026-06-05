using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for the legacy non-generic <see cref="ListWidget"/> selection
/// change event. Kept for back-compat with the obsolete widget.
/// </summary>
public sealed class ListSelectionChangedEventArgs : WidgetEventArgs<ListWidget, ListNode>
{
    /// <summary>
    /// The index of the newly selected item.
    /// </summary>
    public int SelectedIndex { get; }

    /// <summary>
    /// The text of the newly selected item.
    /// </summary>
    public string SelectedText { get; }

    public ListSelectionChangedEventArgs(
        ListWidget widget,
        ListNode node,
        InputBindingActionContext context,
        int selectedIndex,
        string selectedText)
        : base(widget, node, context)
    {
        SelectedIndex = selectedIndex;
        SelectedText = selectedText;
    }
}

/// <summary>
/// Event arguments raised when the multi-select set on a
/// <see cref="ListWidget{T}"/> changes (Space toggles a row, Shift+Arrow
/// extends a range, Ctrl+A selects-all, or the controlled
/// <c>SelectedIndices</c> property mutates).
///
/// <para>The cursor moving to a new row (Up/Down/Home/End/PageUp/PageDown)
/// raises <see cref="ListFocusChangedEventArgs{T}"/>, not this event —
/// focus and selection are independent concepts in multi-select mode.</para>
/// </summary>
/// <typeparam name="T">The item type of the list.</typeparam>
public sealed class ListSelectionChangedEventArgs<T> : WidgetEventArgs<ListWidget<T>, ListNode<T>>
{
    /// <summary>
    /// The full checked set after this change, in ascending index order.
    /// </summary>
    public IReadOnlyList<int> SelectedIndices { get; }

    /// <summary>
    /// Materialised items for the indices in <see cref="SelectedIndices"/>.
    /// In virtualized mode, items that are not yet loaded are skipped —
    /// always cross-reference via <see cref="SelectedIndices"/>.Count when
    /// you need an authoritative count.
    /// </summary>
    public IReadOnlyList<T> SelectedItems { get; }

    /// <summary>
    /// The zero-based index of the row whose state just changed, or <c>-1</c>
    /// when the change affected every row (<see cref="ListSelectionChangeReason.SelectAll"/>,
    /// <see cref="ListSelectionChangeReason.DeselectAll"/>, or
    /// <see cref="ListSelectionChangeReason.Programmatic"/>).
    /// </summary>
    public int ToggledIndex { get; }

    /// <summary>
    /// The item at <see cref="ToggledIndex"/>, or <c>default</c> when
    /// <see cref="ToggledIndex"/> is <c>-1</c> or the row is not yet loaded
    /// in virtualized mode.
    /// </summary>
    public T? ToggledItem { get; }

    /// <summary>
    /// The new state of <see cref="ToggledIndex"/> after the change. For
    /// <see cref="ListSelectionChangeReason.SelectAll"/> this is <c>true</c>;
    /// for <see cref="ListSelectionChangeReason.DeselectAll"/> and
    /// <see cref="ListSelectionChangeReason.Programmatic"/> the value reflects
    /// whether the post-change set is non-empty.
    /// </summary>
    public bool IsSelected { get; }

    /// <summary>What caused the selection set to change.</summary>
    public ListSelectionChangeReason Reason { get; }

    public ListSelectionChangedEventArgs(
        ListWidget<T> widget,
        ListNode<T> node,
        InputBindingActionContext context,
        IReadOnlyList<int> selectedIndices,
        IReadOnlyList<T> selectedItems,
        int toggledIndex,
        T? toggledItem,
        bool isSelected,
        ListSelectionChangeReason reason)
        : base(widget, node, context)
    {
        SelectedIndices = selectedIndices;
        SelectedItems = selectedItems;
        ToggledIndex = toggledIndex;
        ToggledItem = toggledItem;
        IsSelected = isSelected;
        Reason = reason;
    }
}
