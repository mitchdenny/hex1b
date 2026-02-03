namespace Hex1b.Widgets;

using Hex1b.Events;

/// <summary>
/// Provides a fluent API context for building tree structures.
/// This context exposes the Item method to create tree items with optional children.
/// </summary>
/// <remarks>
/// <para>
/// TreeContext is passed to the tree builder callback and provides the <see cref="Item(string)"/>
/// method for creating tree items. Items can have children specified via a nested callback.
/// </para>
/// <para>
/// For async lazy loading, use the async children callback overload. When the async callback
/// is pending, the tree automatically shows a spinner in place of the expand indicator.
/// </para>
/// </remarks>
/// <example>
/// <para>Basic static tree:</para>
/// <code>
/// ctx.Tree(t => [
///     t.Item("Root", root => [
///         root.Item("Child 1"),
///         root.Item("Child 2")
///     ]).Expanded().Icon("📁")
/// ])
/// </code>
/// <para>Async lazy loading:</para>
/// <code>
/// ctx.Tree(t => [
///     t.Item("Server", async children => {
///         var data = await LoadAsync();
///         return data.Select(d => children.Item(d.Name));
///     })
/// ])
/// </code>
/// </example>
public readonly struct TreeContext
{
    /// <summary>
    /// Creates a tree item with the specified label and no children.
    /// </summary>
    /// <param name="label">The display label for the tree item.</param>
    /// <returns>A TreeItemWidget that can be further configured with fluent methods.</returns>
    public TreeItemWidget Item(string label)
        => new(label);

    /// <summary>
    /// Creates a tree item with the specified label and static children.
    /// </summary>
    /// <param name="label">The display label for the tree item.</param>
    /// <param name="children">A function that returns child items.</param>
    /// <returns>A TreeItemWidget configured with children.</returns>
    public TreeItemWidget Item(string label, Func<TreeContext, IEnumerable<TreeItemWidget>> children)
    {
        var childContext = new TreeContext();
        var childList = children(childContext).ToList();
        return new TreeItemWidget(label)
        {
            Children = childList,
            HasChildren = childList.Count > 0
        };
    }

    /// <summary>
    /// Creates a tree item with the specified label and async lazy-loaded children.
    /// When the item is expanded and the async callback is running, a spinner is shown.
    /// </summary>
    /// <param name="label">The display label for the tree item.</param>
    /// <param name="asyncChildren">An async function that returns child items when the item is expanded.</param>
    /// <returns>A TreeItemWidget configured for async lazy loading.</returns>
    public TreeItemWidget Item(string label, Func<TreeContext, Task<IEnumerable<TreeItemWidget>>> asyncChildren)
    {
        // Wrap the simple async loader into the existing OnExpanding pattern
        return new TreeItemWidget(label)
        {
            HasChildren = true,
            ExpandingAsyncHandler = async args =>
            {
                var childContext = new TreeContext();
                return await asyncChildren(childContext);
            }
        };
    }
}
