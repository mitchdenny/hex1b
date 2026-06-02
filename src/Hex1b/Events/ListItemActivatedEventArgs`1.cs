using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for <see cref="TypedListWidget{T}"/> item-activation events
/// (Enter, Space, or click).
/// </summary>
/// <typeparam name="T">The item type of the list.</typeparam>
public sealed class ListItemActivatedEventArgs<T> : WidgetEventArgs<TypedListWidget<T>, TypedListNode<T>>
{
    /// <summary>
    /// The index of the activated item.
    /// </summary>
    public int ActivatedIndex { get; }

    /// <summary>
    /// The activated item.
    /// </summary>
    public T ActivatedItem { get; }

    /// <summary>
    /// Convenience accessor that returns <see cref="ActivatedItem"/> rendered via
    /// <see cref="object.ToString"/>, or an empty string when the item is <c>null</c>.
    /// </summary>
    public string ActivatedText => ActivatedItem?.ToString() ?? string.Empty;

    public ListItemActivatedEventArgs(
        TypedListWidget<T> widget,
        TypedListNode<T> node,
        InputBindingActionContext context,
        int activatedIndex,
        T activatedItem)
        : base(widget, node, context)
    {
        ActivatedIndex = activatedIndex;
        ActivatedItem = activatedItem;
    }
}
