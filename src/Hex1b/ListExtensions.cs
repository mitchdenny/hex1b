namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building ListWidget.
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Creates a List with the specified items.
    /// </summary>
    public static ListWidget List<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<string> items)
        where TParent : Hex1bWidget
        => new(items);

    /// <summary>
    /// Creates a List with the specified items and a synchronous item activated callback.
    /// </summary>
    public static ListWidget List<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<string> items,
        Action<ListItemActivatedEventArgs> onItemActivated)
        where TParent : Hex1bWidget
        => new(items) { OnItemActivated = args => { onItemActivated(args); return Task.CompletedTask; } };

    /// <summary>
    /// Creates a List with the specified items and an async item activated callback.
    /// </summary>
    public static ListWidget List<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<string> items,
        Func<ListItemActivatedEventArgs, Task> onItemActivated)
        where TParent : Hex1bWidget
        => new(items) { OnItemActivated = onItemActivated };

    /// <summary>
    /// Creates a List with the specified items and both selection changed and item activated callbacks.
    /// </summary>
    public static ListWidget List<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<string> items,
        Action<ListSelectionChangedEventArgs>? onSelectionChanged,
        Action<ListItemActivatedEventArgs>? onItemActivated)
        where TParent : Hex1bWidget
        => new(items) 
        { 
            OnSelectionChanged = onSelectionChanged != null 
                ? args => { onSelectionChanged(args); return Task.CompletedTask; } 
                : null,
            OnItemActivated = onItemActivated != null 
                ? args => { onItemActivated(args); return Task.CompletedTask; } 
                : null
        };
}
