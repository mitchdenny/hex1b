using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for <see cref="TypedListWidget{T}"/> selection-change events.
/// Carries the typed item value rather than just its string representation.
/// </summary>
/// <typeparam name="T">The item type of the list.</typeparam>
public sealed class ListSelectionChangedEventArgs<T> : WidgetEventArgs<TypedListWidget<T>, TypedListNode<T>>
{
    /// <summary>
    /// The index of the newly selected item.
    /// </summary>
    public int SelectedIndex { get; }

    /// <summary>
    /// The newly selected item.
    /// </summary>
    public T SelectedItem { get; }

    /// <summary>
    /// Convenience accessor that returns <see cref="SelectedItem"/> rendered via
    /// <see cref="object.ToString"/>, or an empty string when the item is <c>null</c>.
    /// </summary>
    public string SelectedText => SelectedItem?.ToString() ?? string.Empty;

    public ListSelectionChangedEventArgs(
        TypedListWidget<T> widget,
        TypedListNode<T> node,
        InputBindingActionContext context,
        int selectedIndex,
        T selectedItem)
        : base(widget, node, context)
    {
        SelectedIndex = selectedIndex;
        SelectedItem = selectedItem;
    }
}
