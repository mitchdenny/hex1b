using Hex1b.Composition;
using Hex1b.Data;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Widget for displaying a selectable list of items.
/// Selection state is owned by the node and preserved across reconciliation.
/// </summary>
/// <remarks>
/// <para>
/// This non-generic widget is preserved for back-compat with existing code that
/// rebinds <see cref="MoveUp"/> / <see cref="MoveDown"/> / <see cref="Activate"/>
/// action ids or constructs <c>new ListWidget(items)</c> directly. New code
/// should prefer <see cref="ListWidget{T}"/>, which supports custom per-row
/// templates and typed event args. The <c>context.List(items)</c> extension
/// now returns <see cref="ListWidget{T}"/> of <see cref="string"/>.
/// </para>
/// </remarks>
[Obsolete(
    "ListWidget is deprecated. Use ListWidget<T> instead — context.List(items) " +
    "now returns ListWidget<string>. The non-generic ListWidget is retained for " +
    "back-compat and will be removed in a future release.",
    DiagnosticId = "HEX1B0100",
    UrlFormat = "https://github.com/mitchdenny/hex1b/blob/main/docs/diagnostics/{0}.md")]
public sealed record ListWidget(IReadOnlyList<string> Items) : Hex1bWidget
{
    /// <summary>Rebindable action: Move selection up.</summary>
    public static readonly ActionId MoveUp = new($"{nameof(ListWidget)}.{nameof(MoveUp)}");
    /// <summary>Rebindable action: Move selection down.</summary>
    public static readonly ActionId MoveDown = new($"{nameof(ListWidget)}.{nameof(MoveDown)}");
    /// <summary>Rebindable action: Activate the selected item.</summary>
    public static readonly ActionId Activate = new($"{nameof(ListWidget)}.{nameof(Activate)}");
    /// <summary>Rebindable action: Scroll up.</summary>
    public static readonly ActionId ScrollUp = new($"{nameof(ListWidget)}.{nameof(ScrollUp)}");
    /// <summary>Rebindable action: Scroll down.</summary>
    public static readonly ActionId ScrollDown = new($"{nameof(ListWidget)}.{nameof(ScrollDown)}");
    /// <summary>Rebindable action: Move selection to the first item.</summary>
    public static readonly ActionId MoveToFirst = new($"{nameof(ListWidget)}.{nameof(MoveToFirst)}");
    /// <summary>Rebindable action: Move selection to the last item.</summary>
    public static readonly ActionId MoveToLast = new($"{nameof(ListWidget)}.{nameof(MoveToLast)}");
    /// <summary>Rebindable action: Move selection up by one viewport.</summary>
    public static readonly ActionId PageUp = new($"{nameof(ListWidget)}.{nameof(PageUp)}");
    /// <summary>Rebindable action: Move selection down by one viewport.</summary>
    public static readonly ActionId PageDown = new($"{nameof(ListWidget)}.{nameof(PageDown)}");

    /// <summary>
    /// The initial selected index when the list is first created.
    /// Defaults to 0 (first item). Only applied when the node is new.
    /// </summary>
    public int InitialSelectedIndex { get; init; } = 0;
    
    /// <summary>
    /// Internal handler for selection changed events.
    /// </summary>
    internal Func<ListSelectionChangedEventArgs, Task>? SelectionChangedHandler { get; init; }

    /// <summary>
    /// Internal handler for item activated events.
    /// </summary>
    internal Func<ListItemActivatedEventArgs, Task>? ItemActivatedHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler called when the selection changes.
    /// </summary>
    public ListWidget OnSelectionChanged(Action<ListSelectionChangedEventArgs> handler)
        => this with { SelectionChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the selection changes.
    /// </summary>
    public ListWidget OnSelectionChanged(Func<ListSelectionChangedEventArgs, Task> handler)
        => this with { SelectionChangedHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called when an item is activated (Enter, Space, or click).
    /// </summary>
    public ListWidget OnItemActivated(Action<ListItemActivatedEventArgs> handler)
        => this with { ItemActivatedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when an item is activated (Enter, Space, or click).
    /// </summary>
    public ListWidget OnItemActivated(Func<ListItemActivatedEventArgs, Task> handler)
        => this with { ItemActivatedHandler = handler };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ListNode ?? new ListNode();
        var isNewNode = existingNode == null;
        node.Items = Items;
        
        // Apply initial selection for new nodes
        if (isNewNode && Items.Count > 0)
        {
            node.FocusedIndex = Math.Clamp(InitialSelectedIndex, 0, Items.Count - 1);
        }
        // Clamp selection if items changed
        else if (node.FocusedIndex >= Items.Count && Items.Count > 0)
        {
            node.FocusedIndex = Items.Count - 1;
        }
        else if (Items.Count == 0)
        {
            node.FocusedIndex = 0;
        }
        
        // Set up event handlers
        if (SelectionChangedHandler != null)
        {
            node.FocusChangedAction = ctx =>
            {
                if (node.FocusedText != null)
                {
                    var args = new ListSelectionChangedEventArgs(this, node, ctx, node.FocusedIndex, node.FocusedText);
                    return SelectionChangedHandler(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.FocusChangedAction = null;
        }

        if (ItemActivatedHandler != null)
        {
            node.ItemActivatedAction = ctx =>
            {
                if (node.FocusedText != null)
                {
                    var args = new ListItemActivatedEventArgs(this, node, ctx, node.FocusedIndex, node.FocusedText);
                    return ItemActivatedHandler(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.ItemActivatedAction = null;
        }
        
        // Set initial focus if this is a new node (ListNode is always focusable)
        if (context.IsNew)
        {
            node.IsFocused = true;
        }
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(ListNode);
}

/// <summary>
/// Widget for displaying a selectable list of typed items. Selection state is
/// owned by the node and preserved across reconciliation. Supports per-row
/// custom rendering via <c>ItemTemplate</c> — see <see cref="ListItemContext{T}"/>.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public record ListWidget<T>(IReadOnlyList<T>? Items) : Hex1bWidget
{
    /// <summary>Rebindable action: Move selection up.</summary>
    public static readonly ActionId MoveUp = new($"{nameof(ListWidget<T>)}.{nameof(MoveUp)}");
    /// <summary>Rebindable action: Move selection down.</summary>
    public static readonly ActionId MoveDown = new($"{nameof(ListWidget<T>)}.{nameof(MoveDown)}");
    /// <summary>Rebindable action: Activate the selected item.</summary>
    public static readonly ActionId Activate = new($"{nameof(ListWidget<T>)}.{nameof(Activate)}");
    /// <summary>Rebindable action: Scroll up.</summary>
    public static readonly ActionId ScrollUp = new($"{nameof(ListWidget<T>)}.{nameof(ScrollUp)}");
    /// <summary>Rebindable action: Scroll down.</summary>
    public static readonly ActionId ScrollDown = new($"{nameof(ListWidget<T>)}.{nameof(ScrollDown)}");
    /// <summary>Rebindable action: Move selection to the first item.</summary>
    public static readonly ActionId MoveToFirst = new($"{nameof(ListWidget<T>)}.{nameof(MoveToFirst)}");
    /// <summary>Rebindable action: Move selection to the last item.</summary>
    public static readonly ActionId MoveToLast = new($"{nameof(ListWidget<T>)}.{nameof(MoveToLast)}");
    /// <summary>Rebindable action: Move selection up by one viewport.</summary>
    public static readonly ActionId PageUp = new($"{nameof(ListWidget<T>)}.{nameof(PageUp)}");
    /// <summary>Rebindable action: Move selection down by one viewport.</summary>
    public static readonly ActionId PageDown = new($"{nameof(ListWidget<T>)}.{nameof(PageDown)}");

    /// <summary>
    /// The initial focused index when the list is first created. Defaults to 0.
    /// Only applied when the node is new.
    /// </summary>
    public int InitialFocusedIndex { get; init; } = 0;

    /// <summary>
    /// When set, drives the focused index on every reconciliation rather than only
    /// at creation time. Use this to build "controlled" lists whose cursor lives
    /// in an owning composite's state (e.g. a search-filtered selection prompt where
    /// the textbox forwards Up/Down to the list). Out-of-range values are clamped to
    /// the current item range.
    /// </summary>
    public int? ControlledFocusedIndex { get; init; }

    /// <summary>
    /// The fixed row height in terminal rows for each item. Defaults to 1.
    /// Templates with content shorter than this are padded; taller content is clipped.
    /// </summary>
    public int ItemHeight { get; init; } = 1;

    /// <summary>
    /// Optional per-row template. When set, each row is rendered as the widget tree
    /// returned by the callback; the list itself draws no selector / background /
    /// hover chrome and leaves all styling to the template.
    /// </summary>
    internal Func<ListItemContext<T>, Hex1bWidget>? Template { get; init; }

    /// <summary>
    /// Optional key selector for stable per-row identity across reconciliations.
    /// When provided, item child nodes are reused across frames by matching keys
    /// rather than positions — important for stateful templates and a prerequisite
    /// for future search / filter features.
    /// </summary>
    internal Func<T, object>? ItemKeySelector { get; init; }

    internal Func<ListFocusChangedEventArgs<T>, Task>? FocusChangedHandler { get; init; }
    internal Func<ListItemActivatedEventArgs<T>, Task>? ItemActivatedHandler { get; init; }

    /// <summary>
    /// Optional virtualized data source. When set, <see cref="Items"/> is
    /// ignored; the node fetches only a window of items around the visible
    /// viewport on each frame and re-fetches when the user scrolls or the
    /// source raises
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged"/>.
    /// Use <see cref="ListDataSource{T}"/> to wrap an in-memory list, or
    /// implement <see cref="IListDataSource{T}"/> for a remote or paged
    /// source.
    /// </summary>
    internal IListDataSource<T>? DataSource { get; init; }

    /// <summary>
    /// Optional builder for an "empty state" widget shown when the list resolves
    /// to zero items after data has loaded. When a <see cref="DataSource"/> is
    /// set the empty widget is suppressed until the initial item-count load
    /// completes so it doesn't flash while data is still in flight.
    /// </summary>
    internal Func<RootContext, Hex1bWidget>? EmptyBuilder { get; init; }

    /// <summary>
    /// Configures an empty-state widget rendered when the list has no items.
    /// Mirrors <c>TableWidget&lt;TRow&gt;.Empty(...)</c> — pass a builder that
    /// returns the widget tree to render in place of the list contents.
    /// </summary>
    /// <param name="builder">A function that builds the empty-state widget.</param>
    public ListWidget<T> Empty(Func<RootContext, Hex1bWidget> builder)
        => this with { EmptyBuilder = builder };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ListNode<T> ?? CreateNode();
        var isNewNode = existingNode is null || existingNode.GetType() != GetExpectedNodeType();

        // Wire the invalidate callback BEFORE swapping in a data source so the
        // initial INotifyCollectionChanged subscription has somewhere to dispatch.
        node.InvalidateCallback = context.InvalidateCallback;
        node.DataSource = DataSource;

        ApplyState(node, context, isNewNode);

        if (DataSource is not null)
        {
            await EnsureDataLoadedAsync(node, context).ConfigureAwait(false);
        }

        await ReconcileItemNodesAsync(node, context).ConfigureAwait(false);
        await ReconcileEmptyChildAsync(node, context).ConfigureAwait(false);

        if (context.IsNew)
        {
            node.IsFocused = true;
        }

        return node;
    }

    /// <summary>
    /// Reconciles the optional empty-state child widget. Renders only when the
    /// list is genuinely empty after data has loaded (see <see cref="ListNode{T}.ShouldShowEmptyState"/>);
    /// otherwise the prior empty child is dropped and its bounds reported as
    /// orphaned so any painted region is cleared.
    /// </summary>
    private async Task ReconcileEmptyChildAsync(ListNode<T> node, ReconcileContext context)
    {
        var shouldShow = EmptyBuilder is not null && node.ShouldShowEmptyState;
        if (!shouldShow)
        {
            if (node.EmptyChildNode is { } orphan)
            {
                if (orphan.Bounds.Width > 0 && orphan.Bounds.Height > 0)
                {
                    node.AddOrphanedChildBounds(orphan.Bounds);
                }
                node.EmptyChildNode = null;
            }
            return;
        }

        var widget = EmptyBuilder!(new RootContext());
        var childContext = context.WithChildPosition(0, 1);
        var childNode = await widget.ReconcileAsync(node.EmptyChildNode, childContext).ConfigureAwait(false);
        node.EmptyChildNode = childNode;
    }

    /// <summary>
    /// Loads the visible window from the data source so the next render has
    /// data on hand. Mirrors <c>TableWidget</c>'s first-load + visible-window
    /// fetch pattern.
    /// </summary>
    private static async Task EnsureDataLoadedAsync(ListNode<T> node, ReconcileContext context)
    {
        // First load — count + initial window.
        if (node.EffectiveItemCount == 0)
        {
            await node.LoadDataAsync(0, 50, context.CancellationToken).ConfigureAwait(false);
        }

        var totalCount = node.EffectiveItemCount;
        if (totalCount == 0) return;

        var (windowStart, windowEnd) = node.GetVisibleWindow(totalCount);
        var rangeCount = Math.Max(50, windowEnd - windowStart);
        await node.LoadDataAsync(windowStart, rangeCount, context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates the concrete node type. Derived widgets override this to return
    /// their specialized node so casts in user-facing tests and event args stay
    /// strongly typed.
    /// </summary>
    private protected virtual ListNode<T> CreateNode() => new();

    private protected void ApplyState(ListNode<T> node, ReconcileContext context, bool isNewNode)
    {
        node.SourceWidget = this;
        // When a DataSource is set the node manages its own items from the cache;
        // don't overwrite with the (unused) Items facade.
        if (DataSource is null)
        {
            node.Items = Items ?? Array.Empty<T>();
        }
        node.ItemHeight = ItemHeight;
        node.ItemTemplate = Template;
        node.ItemKeySelector = ItemKeySelector;
        node.EmptyBuilder = EmptyBuilder;

        var count = node.EffectiveItemCount;

        // Apply initial selection for new nodes; clamp otherwise.
        if (isNewNode && count > 0)
        {
            node.FocusedIndex = Math.Clamp(InitialFocusedIndex, 0, count - 1);
        }
        else if (node.FocusedIndex >= count && count > 0)
        {
            node.FocusedIndex = count - 1;
        }
        else if (count == 0)
        {
            node.FocusedIndex = 0;
        }

        // Controlled focus wins after the clamp pass so the owning composite
        // can override the node's persisted choice on every frame.
        if (ControlledFocusedIndex is int controlled && count > 0)
        {
            node.FocusedIndex = Math.Clamp(controlled, 0, count - 1);
        }

        if (FocusChangedHandler is { } focChanged)
        {
            node.FocusChangedAction = ctx =>
            {
                if (node.FocusedIndex >= 0 && node.FocusedIndex < node.EffectiveItemCount &&
                    node.TryGetEffectiveItem(node.FocusedIndex, out var item))
                {
                    var args = new ListFocusChangedEventArgs<T>(this, node, ctx, node.FocusedIndex, item);
                    return focChanged(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.FocusChangedAction = null;
        }

        if (ItemActivatedHandler is { } activated)
        {
            node.ItemActivatedAction = ctx =>
            {
                if (node.FocusedIndex >= 0 && node.FocusedIndex < node.EffectiveItemCount &&
                    node.TryGetEffectiveItem(node.FocusedIndex, out var item))
                {
                    var args = new ListItemActivatedEventArgs<T>(this, node, ctx, node.FocusedIndex, item);
                    return activated(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.ItemActivatedAction = null;
        }
    }

    private protected async Task ReconcileItemNodesAsync(ListNode<T> node, ReconcileContext context)
    {
        if (Template is null)
        {
            // Template was removed (or never set). Drop any prior item children and
            // record their bounds so the framework clears the painted region.
            if (node.ItemNodes.Count > 0)
            {
                foreach (var orphan in node.ItemNodes)
                {
                    if (orphan.Bounds.Width > 0 && orphan.Bounds.Height > 0)
                    {
                        node.AddOrphanedChildBounds(orphan.Bounds);
                    }
                }
                node.ItemNodes = new List<Hex1bNode>();
                node.ItemNodesWindowStart = 0;
            }
            return;
        }

        var template = Template;
        var snapshotSelected = node.FocusedIndex;
        var snapshotFocused = node.IsFocused;
        var snapshotHovered = node.IsHovered ? node.HoveredItemIndex : -1;

        // Compute the range of absolute indices we will materialise this frame.
        // Non-virtualized: the entire list. Virtualized: visible + buffer.
        int windowStart, windowEnd;
        var totalCount = node.EffectiveItemCount;
        if (DataSource is null)
        {
            windowStart = 0;
            windowEnd = totalCount;
        }
        else
        {
            (windowStart, windowEnd) = node.GetVisibleWindow(totalCount);
        }
        var windowCount = Math.Max(0, windowEnd - windowStart);

        // Build a key -> old-node map when a key selector is supplied so reused
        // nodes preserve template-local state even after reorder/filter.
        Dictionary<object, Hex1bNode>? oldByKey = null;
        var oldNodes = node.ItemNodes;
        var oldWindowStart = node.ItemNodesWindowStart;
        if (ItemKeySelector is not null && oldNodes.Count > 0)
        {
            oldByKey = new Dictionary<object, Hex1bNode>(oldNodes.Count);
            for (int i = 0; i < oldNodes.Count; i++)
            {
                var absoluteIndex = oldWindowStart + i;
                if (!node.TryGetEffectiveItem(absoluteIndex, out var oldItem)) continue;
                var oldKey = ItemKeySelector(oldItem);
                oldByKey.TryAdd(oldKey, oldNodes[i]);
            }
        }

        var newNodes = new List<Hex1bNode>(windowCount);
        var reused = new HashSet<Hex1bNode>(ReferenceEqualityComparer.Instance);

        for (int windowOffset = 0; windowOffset < windowCount; windowOffset++)
        {
            var absoluteIndex = windowStart + windowOffset;
            var isLoaded = node.TryGetEffectiveItem(absoluteIndex, out var item);

            Hex1bNode? existingChild = null;
            if (ItemKeySelector is not null && oldByKey is not null && isLoaded)
            {
                var key = ItemKeySelector(item);
                if (oldByKey.TryGetValue(key, out var match))
                {
                    existingChild = match;
                }
            }
            else
            {
                var oldOffset = absoluteIndex - oldWindowStart;
                if (oldOffset >= 0 && oldOffset < oldNodes.Count)
                {
                    existingChild = oldNodes[oldOffset];
                }
            }

            var itemContext = new ListItemContext<T>(
                item!,
                absoluteIndex,
                isFocused: absoluteIndex == snapshotSelected,
                ownerHasFocus: snapshotFocused,
                isHovered: absoluteIndex == snapshotHovered,
                isLoaded: isLoaded);

            var itemWidget = template(itemContext);
            var positionedContext = context.WithChildPosition(windowOffset, windowCount);
            var reconciled = await positionedContext.ReconcileChildAsync(existingChild, itemWidget, node).ConfigureAwait(false);
            if (reconciled is not null)
            {
                newNodes.Add(reconciled);
                reused.Add(reconciled);
            }
        }

        // Drop any old children that weren't reused.
        foreach (var oldChild in oldNodes)
        {
            if (!reused.Contains(oldChild) && oldChild.Bounds.Width > 0 && oldChild.Bounds.Height > 0)
            {
                node.AddOrphanedChildBounds(oldChild.Bounds);
            }
        }

        node.ItemNodes = newNodes;
        node.ItemNodesWindowStart = windowStart;
    }

    internal override Type GetExpectedNodeType() => typeof(ListNode<T>);
}
