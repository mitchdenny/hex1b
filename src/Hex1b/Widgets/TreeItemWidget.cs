using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A focusable tree item with label and optional icon.
/// Supports lazy loading of children via OnExpanding callback.
/// </summary>
/// <param name="Label">The display label for this tree item.</param>
public sealed record TreeItemWidget(string Label) : Hex1bWidget
{
    /// <summary>
    /// Optional icon/emoji prefix displayed before the label.
    /// </summary>
    internal string? IconValue { get; init; }
    
    /// <summary>
    /// Child tree items. Empty by default.
    /// </summary>
    internal IReadOnlyList<TreeItemWidget> ChildItems { get; init; } = [];
    
    /// <summary>
    /// Whether this item is expanded to show children. Default is false (collapsed).
    /// </summary>
    public bool IsExpanded { get; init; } = false;
    
    /// <summary>
    /// Whether this item is currently loading children. Default is false.
    /// When true, displays an animated spinner instead of the expand indicator.
    /// </summary>
    public bool IsLoading { get; init; } = false;
    
    /// <summary>
    /// Whether this item is selected (for multi-select mode). Default is false.
    /// </summary>
    public bool IsSelected { get; init; } = false;
    
    /// <summary>
    /// Hint that this item has children even if Children is empty.
    /// Used to show expand indicator for lazy-loaded items.
    /// </summary>
    public bool HasChildren { get; init; } = false;
    
    /// <summary>
    /// User data value associated with this item.
    /// </summary>
    internal object? DataValue { get; init; }
    
    /// <summary>
    /// The type of the user data, for runtime validation in GetData&lt;T&gt;.
    /// </summary>
    internal Type? DataType { get; init; }

    // Lazy loading callbacks
    internal Func<TreeItemExpandingEventArgs, IEnumerable<TreeItemWidget>>? ExpandingHandler { get; init; }
    internal Func<TreeItemExpandingEventArgs, Task<IEnumerable<TreeItemWidget>>>? ExpandingAsyncHandler { get; init; }

    // Item-level event handlers
    internal Func<TreeItemActivatedEventArgs, Task>? ActivatedHandler { get; init; }
    internal Func<TreeItemClickedEventArgs, Task>? ClickedHandler { get; init; }
    internal Func<TreeItemExpandedEventArgs, Task>? ExpandedHandler { get; init; }
    internal Func<TreeItemCollapsedEventArgs, Task>? CollapsedHandler { get; init; }

    #region Fluent API

    /// <summary>
    /// Sets the icon/emoji prefix for this item.
    /// </summary>
    public TreeItemWidget Icon(string icon) => this with { IconValue = icon };

    /// <summary>
    /// Sets the child items. Also sets HasChildren to true if children are provided.
    /// </summary>
    public TreeItemWidget Children(params TreeItemWidget[] children)
        => this with { ChildItems = children, HasChildren = children.Length > 0 };

    /// <summary>
    /// Sets whether this item is initially expanded.
    /// </summary>
    public TreeItemWidget Expanded(bool expanded = true)
        => this with { IsExpanded = expanded };

    /// <summary>
    /// Sets whether this item is currently loading children.
    /// When true, displays an animated spinner instead of the expand indicator.
    /// </summary>
    public TreeItemWidget Loading(bool loading = true)
        => this with { IsLoading = loading };

    /// <summary>
    /// Sets whether this item is selected (for multi-select mode).
    /// </summary>
    public TreeItemWidget Selected(bool selected = true)
        => this with { IsSelected = selected };

    /// <summary>
    /// Associates typed data with this item. Retrieve with <see cref="TreeItemNode.GetData{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="data">The data to associate with this item.</param>
    /// <returns>A new TreeItemWidget with the data set.</returns>
    /// <example>
    /// <code>
    /// t.Item(server.Name).Data(server)
    ///     .OnActivated(e => ConnectTo(e.Item.GetData&lt;Server&gt;()))
    /// </code>
    /// </example>
    public TreeItemWidget Data<T>(T data)
        => this with { DataValue = data, DataType = typeof(T) };

    /// <summary>
    /// Sets a synchronous handler for lazy loading children when the item is expanded.
    /// Also sets HasChildren to true.
    /// </summary>
    public TreeItemWidget OnExpanding(Func<TreeItemExpandingEventArgs, IEnumerable<TreeItemWidget>> handler)
        => this with { ExpandingHandler = handler, HasChildren = true };

    /// <summary>
    /// Sets an asynchronous handler for lazy loading children when the item is expanded.
    /// Also sets HasChildren to true.
    /// </summary>
    public TreeItemWidget OnExpanding(Func<TreeItemExpandingEventArgs, Task<IEnumerable<TreeItemWidget>>> handler)
        => this with { ExpandingAsyncHandler = handler, HasChildren = true };

    /// <summary>
    /// Sets a synchronous handler called when this item is activated (Enter key).
    /// </summary>
    public TreeItemWidget OnActivated(Action<TreeItemActivatedEventArgs> handler)
        => this with { ActivatedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when this item is activated (Enter key or double-click).
    /// </summary>
    public TreeItemWidget OnActivated(Func<TreeItemActivatedEventArgs, Task> handler)
        => this with { ActivatedHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called when this item is clicked (single-click).
    /// </summary>
    public TreeItemWidget OnClicked(Action<TreeItemClickedEventArgs> handler)
        => this with { ClickedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when this item is clicked (single-click).
    /// </summary>
    public TreeItemWidget OnClicked(Func<TreeItemClickedEventArgs, Task> handler)
        => this with { ClickedHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called after this item is expanded.
    /// </summary>
    public TreeItemWidget OnExpanded(Action<TreeItemExpandedEventArgs> handler)
        => this with { ExpandedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called after this item is expanded.
    /// </summary>
    public TreeItemWidget OnExpanded(Func<TreeItemExpandedEventArgs, Task> handler)
        => this with { ExpandedHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called when this item is collapsed.
    /// </summary>
    public TreeItemWidget OnCollapsed(Action<TreeItemCollapsedEventArgs> handler)
        => this with { CollapsedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when this item is collapsed.
    /// </summary>
    public TreeItemWidget OnCollapsed(Func<TreeItemCollapsedEventArgs, Task> handler)
        => this with { CollapsedHandler = handler };

    #endregion

    // Note: TreeItemWidget doesn't implement Reconcile directly.
    // It is reconciled by the parent TreeNode which manages the tree structure.
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        throw new InvalidOperationException(
            "TreeItemWidget should not be reconciled directly. Use TreeWidget as the container.");
    }

    internal override Type GetExpectedNodeType() => typeof(TreeItemNode);
}
