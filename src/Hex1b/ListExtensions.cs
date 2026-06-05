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
    /// Creates a typed list bound to <paramref name="items"/>. Pass <c>null</c>
    /// to construct an empty list (useful when a parent composite is still
    /// resolving its data or when you'll supply items later by using the
    /// <see cref="IListDataSource{T}"/> overload).
    /// Use <see cref="ItemTemplate{T}"/> to render each row as a custom widget tree.
    /// </summary>
    /// <remarks>
    /// The non-generic <c>ListWidget</c> previously returned here has been
    /// replaced with <see cref="ListWidget{T}"/> of <typeparamref name="T"/>.
    /// Callers using only string items keep working unchanged because typed
    /// event args still expose <c>FocusedText</c> / <c>ActivatedText</c>
    /// convenience accessors. The legacy non-generic <c>ListWidget</c> remains
    /// available for direct construction (<c>new ListWidget(items)</c>) but
    /// is marked obsolete.
    /// </remarks>
    public static ListWidget<T> List<T>(
        this RootContext context,
        IReadOnlyList<T>? items)
        => new(items);

    /// <summary>
    /// Creates a typed list bound to <paramref name="items"/>. Pass <c>null</c>
    /// to construct an empty list. Use <see cref="ItemTemplate{T}"/> to render
    /// each row as a custom widget tree.
    /// </summary>
    public static ListWidget<T> List<TParent, T>(
        this WidgetContext<TParent> context,
        IReadOnlyList<T>? items)
        where TParent : Hex1bWidget
        => new(items);

    /// <summary>
    /// Creates a virtualized typed list bound to an
    /// <see cref="IListDataSource{T}"/>. The node fetches only a window of items
    /// around the visible viewport on each frame and re-fetches when the user
    /// scrolls or the source raises
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged"/>.
    /// </summary>
    public static ListWidget<T> List<T>(
        this RootContext context,
        IListDataSource<T> dataSource)
        => new(null) { DataSource = dataSource };

    /// <summary>
    /// Creates a virtualized typed list bound to an
    /// <see cref="IListDataSource{T}"/>. See the <see cref="RootContext"/>
    /// overload for details.
    /// </summary>
    public static ListWidget<T> List<TParent, T>(
        this WidgetContext<TParent> context,
        IListDataSource<T> dataSource)
        where TParent : Hex1bWidget
        => new(null) { DataSource = dataSource };

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
    /// Sets the index that should be focused (cursor row) when the list is first
    /// created. Ignored on subsequent reconciliations.
    /// </summary>
    public static ListWidget<T> InitialFocusedIndex<T>(
        this ListWidget<T> widget,
        int index)
        => widget with { InitialFocusedIndex = index };

    /// <summary>
    /// Drives the list's focused (cursor) index on every reconciliation. Use this
    /// when the cursor lives in an owning composite's state (e.g. a search-filtered
    /// selection prompt where a focused textbox forwards Up/Down to the list).
    /// </summary>
    public static ListWidget<T> FocusedIndex<T>(
        this ListWidget<T> widget,
        int index)
        => widget with { ControlledFocusedIndex = index };

    /// <summary>
    /// Sets a synchronous handler invoked when the focused (cursor) row changes.
    /// </summary>
    public static ListWidget<T> OnFocusChanged<T>(
        this ListWidget<T> widget,
        Action<ListFocusChangedEventArgs<T>> handler)
        => widget with { FocusChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler invoked when the focused (cursor) row changes.
    /// </summary>
    public static ListWidget<T> OnFocusChanged<T>(
        this ListWidget<T> widget,
        Func<ListFocusChangedEventArgs<T>, Task> handler)
        => widget with { FocusChangedHandler = handler };

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

    /// <summary>
    /// Enables multi-select on this list. While enabled, Space toggles the
    /// focused row, Shift+Up/Down/Home/End extend a range from the anchor,
    /// and Ctrl+A selects (or deselects) all rows. The current checked set is
    /// surfaced through <see cref="ListItemContext{T}.IsSelected"/> for custom
    /// templates; the built-in renderer additionally draws a checkbox glyph in
    /// front of each row.
    /// </summary>
    public static ListWidget<T> MultiSelect<T>(this ListWidget<T> widget)
        => widget with { IsMultiSelectEnabled = true };

    /// <summary>
    /// Drives the multi-select checked set on every reconciliation
    /// (controlled mode). The owning composite should hold this collection in
    /// its own state and replace it inside <see cref="OnSelectionChanged{T}(ListWidget{T}, Action{ListSelectionChangedEventArgs{T}})"/>.
    /// Indices outside the current item range are silently dropped.
    /// </summary>
    public static ListWidget<T> SelectedIndices<T>(
        this ListWidget<T> widget,
        IReadOnlyList<int> indices)
        => widget with { SelectedIndices = indices };

    /// <summary>
    /// Seeds the multi-select checked set when the list is first created
    /// (uncontrolled mode). Has no effect once the node already exists; use
    /// <see cref="SelectedIndices{T}"/> for controlled state.
    /// </summary>
    public static ListWidget<T> InitialSelectedIndices<T>(
        this ListWidget<T> widget,
        IReadOnlyList<int> indices)
        => widget with { InitialSelectedIndices = indices };

    /// <summary>
    /// Sets a synchronous handler invoked when the multi-select checked set
    /// changes (toggle, range extend, or select-all). The event args expose
    /// the full <c>SelectedIndices</c>/<c>SelectedItems</c> after the change,
    /// the index whose state just flipped, and a <see cref="ListSelectionChangeReason"/>
    /// distinguishing the trigger.
    /// </summary>
    public static ListWidget<T> OnSelectionChanged<T>(
        this ListWidget<T> widget,
        Action<ListSelectionChangedEventArgs<T>> handler)
        => widget with { SelectionChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler invoked when the multi-select checked
    /// set changes.
    /// </summary>
    public static ListWidget<T> OnSelectionChanged<T>(
        this ListWidget<T> widget,
        Func<ListSelectionChangedEventArgs<T>, Task> handler)
        => widget with { SelectionChangedHandler = handler };
}
