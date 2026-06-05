namespace Hex1b;

using Hex1b.Data;
using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building and configuring <see cref="ListWidget{T}"/>.
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Creates a typed list bound to <paramref name="items"/>. Use
    /// <see cref="ItemTemplate{T}"/> to render each row as a custom widget tree.
    /// </summary>
    /// <remarks>
    /// The non-generic <c>ListWidget</c> previously returned here has been
    /// replaced with <see cref="ListWidget{T}"/> of <typeparamref name="T"/>.
    /// Callers using only string items keep working unchanged because typed
    /// event args still expose <c>SelectedText</c> / <c>ActivatedText</c>
    /// convenience accessors. The legacy non-generic <c>ListWidget</c> remains
    /// available for direct construction (<c>new ListWidget(items)</c>) but
    /// is marked obsolete.
    /// </remarks>
    public static ListWidget<T> List<TParent, T>(
        this WidgetContext<TParent> context,
        IReadOnlyList<T> items)
        where TParent : Hex1bWidget
        => new(items);

    /// <summary>
    /// Sets the per-row template used to render each item. The list itself stops
    /// drawing the selector and selected/hover background — the template owns all
    /// row chrome and styles itself from <see cref="ListItemContext{T}"/>.
    /// </summary>
    public static ListWidget<T> ItemTemplate<T>(
        this ListWidget<T> widget,
        Func<ListItemContext<T>, Hex1bWidget> builder)
        => widget with { Template = builder };

    /// <summary>
    /// Sets the fixed row height (in terminal rows) used for every item.
    /// Defaults to 1. Values below 1 are clamped to 1.
    /// </summary>
    public static ListWidget<T> ItemHeight<T>(
        this ListWidget<T> widget,
        int rows)
        => widget with { ItemHeight = Math.Max(1, rows) };

    /// <summary>
    /// Sets a key selector so reused per-row template subtrees follow items by
    /// identity even after reorder or filter.
    /// </summary>
    public static ListWidget<T> ItemKey<T>(
        this ListWidget<T> widget,
        Func<T, object> keySelector)
        => widget with { ItemKeySelector = keySelector };

    /// <summary>
    /// Sets the index that should be selected when the list is first created.
    /// Ignored on subsequent reconciliations.
    /// </summary>
    public static ListWidget<T> InitialSelectedIndex<T>(
        this ListWidget<T> widget,
        int index)
        => widget with { InitialSelectedIndex = index };

    /// <summary>
    /// Drives the list's selected index on every reconciliation. Use this when
    /// the selection lives in an owning composite's state (e.g. a search-filtered
    /// selection prompt where a focused textbox forwards Up/Down to the list).
    /// </summary>
    public static ListWidget<T> SelectedIndex<T>(
        this ListWidget<T> widget,
        int index)
        => widget with { ControlledSelectedIndex = index };

    /// <summary>
    /// Binds the list to a virtualized <see cref="IListDataSource{T}"/> instead
    /// of an in-memory <c>Items</c> collection. The node fetches only a window
    /// of items around the visible viewport on each frame and re-fetches when
    /// the user scrolls or the source raises
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged"/>.
    /// Use this for very large datasets (search results, paged APIs, indexed
    /// files) where materialising the full list isn't practical.
    /// </summary>
    /// <remarks>
    /// When a data source is bound the widget's <c>Items</c> is ignored — pass
    /// <see cref="Array.Empty{T}()"/> at construction. Item templates work
    /// with virtualization; templates can branch on
    /// <see cref="ListItemContext{T}.IsLoaded"/> to render a placeholder for
    /// in-flight rows.
    /// </remarks>
    public static ListWidget<T> DataSource<T>(
        this ListWidget<T> widget,
        IListDataSource<T> dataSource)
        => widget with { DataSource = dataSource };

    /// <summary>
    /// Convenience overload that wraps <paramref name="items"/> in a
    /// <see cref="ListDataSource{T}"/>. Forwards
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged"/>
    /// when the inner list supports it.
    /// </summary>
    public static ListWidget<T> DataSource<T>(
        this ListWidget<T> widget,
        IReadOnlyList<T> items)
        => widget with { DataSource = new ListDataSource<T>(items) };

    /// <summary>
    /// Sets a synchronous handler invoked when the selection changes.
    /// </summary>
    public static ListWidget<T> OnSelectionChanged<T>(
        this ListWidget<T> widget,
        Action<ListSelectionChangedEventArgs<T>> handler)
        => widget with { SelectionChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler invoked when the selection changes.
    /// </summary>
    public static ListWidget<T> OnSelectionChanged<T>(
        this ListWidget<T> widget,
        Func<ListSelectionChangedEventArgs<T>, Task> handler)
        => widget with { SelectionChangedHandler = handler };

    /// <summary>
    /// Sets a synchronous handler invoked when an item is activated
    /// (Enter, Space, or click).
    /// </summary>
    public static ListWidget<T> OnItemActivated<T>(
        this ListWidget<T> widget,
        Action<ListItemActivatedEventArgs<T>> handler)
        => widget with { ItemActivatedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler invoked when an item is activated
    /// (Enter, Space, or click).
    /// </summary>
    public static ListWidget<T> OnItemActivated<T>(
        this ListWidget<T> widget,
        Func<ListItemActivatedEventArgs<T>, Task> handler)
        => widget with { ItemActivatedHandler = handler };
}
