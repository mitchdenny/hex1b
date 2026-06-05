using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for <see cref="ListWidget{T}"/> focus-change events (the
/// keyboard-navigation cursor moved to a new row). Carries the typed item
/// value rather than just its string representation.
/// </summary>
/// <typeparam name="T">The item type of the list.</typeparam>
public sealed class ListFocusChangedEventArgs<T> : WidgetEventArgs<ListWidget<T>, ListNode<T>>
{
    /// <summary>
    /// The index of the newly focused (cursor) item.
    /// </summary>
    public int FocusedIndex { get; }

    /// <summary>
    /// The newly focused (cursor) item.
    /// </summary>
    public T FocusedItem { get; }

    /// <summary>
    /// Convenience accessor that returns <see cref="FocusedItem"/> rendered via
    /// <see cref="object.ToString"/>, or an empty string when the item is <c>null</c>.
    /// </summary>
    public string FocusedText => FocusedItem?.ToString() ?? string.Empty;

    public ListFocusChangedEventArgs(
        ListWidget<T> widget,
        ListNode<T> node,
        InputBindingActionContext context,
        int focusedIndex,
        T focusedItem)
        : base(widget, node, context)
    {
        FocusedIndex = focusedIndex;
        FocusedItem = focusedItem;
    }
}
