using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating Tree widgets.
/// </summary>
public static class TreeExtensions
{
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
    /// Creates a TreeItem with the specified label.
    /// </summary>
    public static TreeItemWidget TreeItem<TParent>(
        this WidgetContext<TParent> ctx,
        string label)
        where TParent : Hex1bWidget
        => new(label);

    /// <summary>
    /// Creates a TreeItem with the specified label and children.
    /// </summary>
    public static TreeItemWidget TreeItem<TParent>(
        this WidgetContext<TParent> ctx,
        string label,
        params TreeItemWidget[] children)
        where TParent : Hex1bWidget
        => new(label) { Children = children, HasChildren = children.Length > 0 };

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
                Icon = iconSelector?.Invoke(item),
                Children = childWidgets,
                HasChildren = childWidgets.Count > 0,
                IsExpanded = isExpandedSelector?.Invoke(item) ?? false,
                Tag = item
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
                Icon = iconSelector?.Invoke(item),
                HasChildren = hasChildren,
                Tag = item
            };
            
            if (hasChildren)
            {
                // Set up lazy loading handler
                widget = widget.OnExpanding(async args =>
                {
                    var data = (T)args.Tag!;
                    var children = await childrenLoader(data);
                    return BuildLazyTreeItems(children, labelSelector, hasChildrenSelector, childrenLoader, iconSelector);
                });
            }
            
            result.Add(widget);
        }
        
        return result;
    }
}

/// <summary>
/// Static helper methods for creating Tree widgets without a context.
/// </summary>
public static class TreeItemBuilder
{
    /// <summary>
    /// Creates a TreeItem with the specified label.
    /// </summary>
    public static TreeItemWidget TreeItem(string label) => new(label);

    /// <summary>
    /// Creates a TreeItem with the specified label and children.
    /// </summary>
    public static TreeItemWidget TreeItem(string label, params TreeItemWidget[] children)
        => new(label) { Children = children, HasChildren = children.Length > 0 };
}
