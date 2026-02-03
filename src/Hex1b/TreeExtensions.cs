using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating Tree widgets.
/// </summary>
public static class TreeExtensions
{
    /// <summary>
    /// Creates a Tree using a builder callback with TreeContext.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the recommended way to create trees. The callback receives a <see cref="TreeContext"/>
    /// that provides methods for creating tree items with children.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.Tree(t => [
    ///     t.Item("Root", root => [
    ///         root.Item("Child 1"),
    ///         root.Item("Child 2")
    ///     ]).Expanded().Icon("📁")
    /// ])
    /// </code>
    /// </example>
    public static TreeWidget Tree<TParent>(
        this WidgetContext<TParent> ctx,
        Func<TreeContext, IEnumerable<TreeItemWidget>> builder)
        where TParent : Hex1bWidget
    {
        var treeContext = new TreeContext();
        var items = builder(treeContext).ToList();
        return new TreeWidget(items);
    }

    /// <summary>
    /// Creates a Tree with the specified root items.
    /// </summary>
    public static TreeWidget Tree<TParent>(
        this WidgetContext<TParent> ctx,
        params TreeItemWidget[] items)
        where TParent : Hex1bWidget
        => new(items);

    /// <summary>
    /// Creates a Tree with the specified root items.
    /// </summary>
    public static TreeWidget Tree<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<TreeItemWidget> items)
        where TParent : Hex1bWidget
        => new(items);

    /// <summary>
    /// Creates a Tree bound to a data source with selectors for label and children.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <typeparam name="T">The data item type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="items">The root data items.</param>
    /// <param name="labelSelector">Function to get the label from a data item.</param>
    /// <param name="childrenSelector">Function to get children from a data item.</param>
    /// <param name="iconSelector">Optional function to get the icon from a data item.</param>
    /// <param name="isExpandedSelector">Optional function to determine if an item is expanded.</param>
    public static TreeWidget Tree<TParent, T>(
        this WidgetContext<TParent> ctx,
        IEnumerable<T> items,
        Func<T, string> labelSelector,
        Func<T, IEnumerable<T>> childrenSelector,
        Func<T, string>? iconSelector = null,
        Func<T, bool>? isExpandedSelector = null)
        where TParent : Hex1bWidget
    {
        var treeItems = BuildTreeItems(items, labelSelector, childrenSelector, iconSelector, isExpandedSelector);
        return new TreeWidget(treeItems);
    }

    /// <summary>
    /// Creates a Tree bound to a data source with lazy-loaded children.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <typeparam name="T">The data item type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="items">The root data items.</param>
    /// <param name="labelSelector">Function to get the label from a data item.</param>
    /// <param name="hasChildrenSelector">Function to determine if an item has children to load.</param>
    /// <param name="childrenLoader">Async function to load children when an item is expanded.</param>
    /// <param name="iconSelector">Optional function to get the icon from a data item.</param>
    public static TreeWidget Tree<TParent, T>(
        this WidgetContext<TParent> ctx,
        IEnumerable<T> items,
        Func<T, string> labelSelector,
        Func<T, bool> hasChildrenSelector,
        Func<T, Task<IEnumerable<T>>> childrenLoader,
        Func<T, string>? iconSelector = null)
        where TParent : Hex1bWidget
    {
        var treeItems = BuildLazyTreeItems(items, labelSelector, hasChildrenSelector, childrenLoader, iconSelector);
        return new TreeWidget(treeItems);
    }

    private static IReadOnlyList<TreeItemWidget> BuildTreeItems<T>(
        IEnumerable<T> items,
        Func<T, string> labelSelector,
        Func<T, IEnumerable<T>> childrenSelector,
        Func<T, string>? iconSelector,
        Func<T, bool>? isExpandedSelector)
    {
        var result = new List<TreeItemWidget>();
        
        foreach (var item in items)
        {
            var children = childrenSelector(item);
            var childWidgets = BuildTreeItems(children, labelSelector, childrenSelector, iconSelector, isExpandedSelector);
            
            var widget = new TreeItemWidget(labelSelector(item))
            {
                IconValue = iconSelector?.Invoke(item),
                ChildItems = childWidgets,
                HasChildren = childWidgets.Count > 0,
                IsExpanded = isExpandedSelector?.Invoke(item) ?? false,
                DataValue = item,
                DataType = typeof(T)
            };
            
            result.Add(widget);
        }
        
        return result;
    }

    private static IReadOnlyList<TreeItemWidget> BuildLazyTreeItems<T>(
        IEnumerable<T> items,
        Func<T, string> labelSelector,
        Func<T, bool> hasChildrenSelector,
        Func<T, Task<IEnumerable<T>>> childrenLoader,
        Func<T, string>? iconSelector)
    {
        var result = new List<TreeItemWidget>();
        
        foreach (var item in items)
        {
            var hasChildren = hasChildrenSelector(item);
            
            var widget = new TreeItemWidget(labelSelector(item))
            {
                IconValue = iconSelector?.Invoke(item),
                HasChildren = hasChildren,
                DataValue = item,
                DataType = typeof(T)
            };
            
            if (hasChildren)
            {
                // Capture item for closure - use closure instead of retrieving from node
                var capturedItem = item;
                widget = widget.OnExpanding(async args =>
                {
                    var children = await childrenLoader(capturedItem);
                    return BuildLazyTreeItems(children, labelSelector, hasChildrenSelector, childrenLoader, iconSelector);
                });
            }
            
            result.Add(widget);
        }
        
        return result;
    }
}
