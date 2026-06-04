namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for configuring <see cref="ListWidget{T}"/>.
/// </summary>
public static class TypedListExtensions
{
    /// <summary>
    /// Creates a typed list bound to <paramref name="items"/>. Use
    /// <see cref="ItemTemplate{T}"/> to render each row as a custom widget tree.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="ListExtensions.List{TParent,T}"/>; this entry point is
    /// retained for back-compat with code that called <c>TypedList(...)</c>
    /// before the rename.
    /// </remarks>
    [Obsolete(
        "TypedList(...) was renamed. Use List<T>(...) (via context.List(items)) " +
        "instead; it returns the same ListWidget<T>.",
        DiagnosticId = "HEX1B0101",
        UrlFormat = "https://github.com/mitchdenny/hex1b/blob/main/docs/diagnostics/{0}.md")]
    public static ListWidget<T> TypedList<TParent, T>(
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
