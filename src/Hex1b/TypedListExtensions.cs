namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building and configuring <see cref="TypedListWidget{T}"/>.
/// </summary>
public static class TypedListExtensions
{
    /// <summary>
    /// Creates a typed list bound to <paramref name="items"/>. Use <see cref="ItemTemplate{T}"/>
    /// to render each row as a custom widget tree.
    /// </summary>
    public static TypedListWidget<T> TypedList<TParent, T>(
        this WidgetContext<TParent> context,
        IReadOnlyList<T> items)
        where TParent : Hex1bWidget
        => new(items);

    /// <summary>
    /// Sets the per-row template used to render each item. The list itself stops
    /// drawing the selector and selected/hover background — the template owns all
    /// row chrome and styles itself from <see cref="ListItemContext{T}"/>.
    /// </summary>
    public static TypedListWidget<T> ItemTemplate<T>(
        this TypedListWidget<T> widget,
        Func<ListItemContext<T>, Hex1bWidget> builder)
        => widget with { Template = builder };

    /// <summary>
    /// Sets the fixed row height (in terminal rows) used for every item.
    /// Defaults to 1. Values below 1 are clamped to 1.
    /// </summary>
    public static TypedListWidget<T> ItemHeight<T>(
        this TypedListWidget<T> widget,
        int rows)
        => widget with { ItemHeight = Math.Max(1, rows) };

    /// <summary>
    /// Sets a key selector so reused per-row template subtrees follow items by
    /// identity even after reorder or filter.
    /// </summary>
    public static TypedListWidget<T> ItemKey<T>(
        this TypedListWidget<T> widget,
        Func<T, object> keySelector)
        => widget with { ItemKeySelector = keySelector };

    /// <summary>
    /// Sets the index that should be selected when the list is first created.
    /// Ignored on subsequent reconciliations.
    /// </summary>
    public static TypedListWidget<T> InitialSelectedIndex<T>(
        this TypedListWidget<T> widget,
        int index)
        => widget with { InitialSelectedIndex = index };

    /// <summary>
    /// Sets a synchronous handler invoked when the selection changes.
    /// </summary>
    public static TypedListWidget<T> OnSelectionChanged<T>(
        this TypedListWidget<T> widget,
        Action<ListSelectionChangedEventArgs<T>> handler)
        => widget with { SelectionChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler invoked when the selection changes.
    /// </summary>
    public static TypedListWidget<T> OnSelectionChanged<T>(
        this TypedListWidget<T> widget,
        Func<ListSelectionChangedEventArgs<T>, Task> handler)
        => widget with { SelectionChangedHandler = handler };

    /// <summary>
    /// Sets a synchronous handler invoked when an item is activated
    /// (Enter, Space, or click).
    /// </summary>
    public static TypedListWidget<T> OnItemActivated<T>(
        this TypedListWidget<T> widget,
        Action<ListItemActivatedEventArgs<T>> handler)
        => widget with { ItemActivatedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler invoked when an item is activated
    /// (Enter, Space, or click).
    /// </summary>
    public static TypedListWidget<T> OnItemActivated<T>(
        this TypedListWidget<T> widget,
        Func<ListItemActivatedEventArgs<T>, Task> handler)
        => widget with { ItemActivatedHandler = handler };
}
