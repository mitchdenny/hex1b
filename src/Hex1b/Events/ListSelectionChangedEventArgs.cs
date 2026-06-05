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
    private readonly bool _isMultiSelect;
    private readonly int _cursorIndex;
    private readonly T? _cursorItem;

    /// <summary>
    /// True when this event was raised from a multi-select list
    /// (<c>widget.MultiSelect()</c>). When <c>false</c>, the event reflects a
    /// cursor move and only the scalar accessors
    /// (<see cref="SelectedIndex"/>, <see cref="SelectedItem"/>,
    /// <see cref="SelectedText"/>) are valid; <see cref="SelectedIndices"/>
    /// and friends throw.
    /// </summary>
    public bool IsMultiSelect => _isMultiSelect;

    /// <summary>
    /// The full checked set after this change, in ascending index order.
    /// Only valid when <see cref="IsMultiSelect"/> is <c>true</c>; throws
    /// otherwise.
    /// </summary>
    public IReadOnlyList<int> SelectedIndices
        => _isMultiSelect
            ? _selectedIndices!
            : throw new InvalidOperationException(
                "SelectedIndices is only valid in multi-select mode. " +
                "Enable multi-select on the list with .MultiSelect() or use the scalar accessors (SelectedIndex/SelectedItem/SelectedText).");

    /// <summary>
    /// Materialised items for the indices in <see cref="SelectedIndices"/>.
    /// In virtualized mode, items that are not yet loaded are skipped —
    /// always cross-reference via <see cref="SelectedIndices"/>.Count when
    /// you need an authoritative count. Only valid when
    /// <see cref="IsMultiSelect"/> is <c>true</c>; throws otherwise.
    /// </summary>
    public IReadOnlyList<T> SelectedItems
        => _isMultiSelect
            ? _selectedItems!
            : throw new InvalidOperationException(
                "SelectedItems is only valid in multi-select mode. " +
                "Enable multi-select on the list with .MultiSelect() or use the scalar accessors.");

    /// <summary>
    /// The zero-based index of the row whose state just changed (multi-select)
    /// or the cursor row (single-select).
    /// In multi-select mode this is <c>-1</c> when the change affected every
    /// row (<see cref="ListSelectionChangeReason.SelectAll"/>,
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
    /// In single-select (cursor) events this is always <c>true</c>.
    /// </summary>
    public bool IsSelected { get; }

    /// <summary>What caused the selection set to change.</summary>
    public ListSelectionChangeReason Reason { get; }

    // -------------------- Legacy scalar accessors --------------------
    // Pre-multi-select OnSelectionChanged fired on cursor moves and exposed
    // these. Kept source-compatible by returning the cursor row when the list
    // is in single-select mode; throw in multi-select so callers don't silently
    // misread the meaning.

    /// <summary>
    /// The index of the currently focused (cursor) row. Source-compatible
    /// with the pre-multi-select event shape — throws when the list is in
    /// multi-select mode (use <see cref="SelectedIndices"/> there).
    /// </summary>
    public int SelectedIndex
        => _isMultiSelect
            ? throw new InvalidOperationException(
                "SelectedIndex is ambiguous in multi-select mode. Use SelectedIndices, ToggledIndex, or subscribe to OnFocusChanged for cursor moves.")
            : _cursorIndex;

    /// <summary>
    /// The item at the currently focused (cursor) row. Source-compatible
    /// with the pre-multi-select event shape — throws when the list is in
    /// multi-select mode (use <see cref="SelectedItems"/> there).
    /// </summary>
    public T? SelectedItem
        => _isMultiSelect
            ? throw new InvalidOperationException(
                "SelectedItem is ambiguous in multi-select mode. Use SelectedItems, ToggledItem, or subscribe to OnFocusChanged for cursor moves.")
            : _cursorItem;

    /// <summary>
    /// The string form of the focused (cursor) row's item (via
    /// <see cref="object.ToString"/>; empty string for <c>null</c>).
    /// Source-compatible with the pre-multi-select event shape — throws when
    /// the list is in multi-select mode.
    /// </summary>
    public string SelectedText
        => _isMultiSelect
            ? throw new InvalidOperationException(
                "SelectedText is ambiguous in multi-select mode. Project SelectedItems through .ToString() yourself, or subscribe to OnFocusChanged for cursor moves.")
            : _cursorItem?.ToString() ?? string.Empty;

    private readonly IReadOnlyList<int>? _selectedIndices;
    private readonly IReadOnlyList<T>? _selectedItems;

    /// <summary>
    /// Multi-select constructor — used when the checked set changes via
    /// Toggle / ExtendRange / SelectAll / DeselectAll / Programmatic.
    /// </summary>
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
        _isMultiSelect = true;
        _selectedIndices = selectedIndices;
        _selectedItems = selectedItems;
        ToggledIndex = toggledIndex;
        ToggledItem = toggledItem;
        IsSelected = isSelected;
        Reason = reason;
        _cursorIndex = toggledIndex;
        _cursorItem = toggledItem;
    }

    /// <summary>
    /// Single-select constructor — used when the cursor moves on a list that
    /// has not opted in to <c>.MultiSelect()</c>. Source-compatibility shim
    /// so existing <c>OnSelectionChanged</c> subscribers keep firing on
    /// cursor moves as they did pre-multi-select. The scalar accessors are
    /// the only valid surface on this shape.
    /// </summary>
    public ListSelectionChangedEventArgs(
        ListWidget<T> widget,
        ListNode<T> node,
        InputBindingActionContext context,
        int cursorIndex,
        T? cursorItem)
        : base(widget, node, context)
    {
        _isMultiSelect = false;
        _cursorIndex = cursorIndex;
        _cursorItem = cursorItem;
        ToggledIndex = cursorIndex;
        ToggledItem = cursorItem;
        IsSelected = true;
        Reason = ListSelectionChangeReason.Programmatic;
    }
}
