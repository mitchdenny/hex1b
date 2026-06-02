using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Widget for displaying a selectable list of typed items. Selection state is
/// owned by the node and preserved across reconciliation. Supports per-row
/// custom rendering via <c>ItemTemplate</c> — see <see cref="ListItemContext{T}"/>.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public record TypedListWidget<T>(IReadOnlyList<T> Items) : Hex1bWidget
{
    /// <summary>Rebindable action: Move selection up.</summary>
    public static readonly ActionId MoveUp = new($"{nameof(TypedListWidget<T>)}.{nameof(MoveUp)}");
    /// <summary>Rebindable action: Move selection down.</summary>
    public static readonly ActionId MoveDown = new($"{nameof(TypedListWidget<T>)}.{nameof(MoveDown)}");
    /// <summary>Rebindable action: Activate the selected item.</summary>
    public static readonly ActionId Activate = new($"{nameof(TypedListWidget<T>)}.{nameof(Activate)}");
    /// <summary>Rebindable action: Scroll up.</summary>
    public static readonly ActionId ScrollUp = new($"{nameof(TypedListWidget<T>)}.{nameof(ScrollUp)}");
    /// <summary>Rebindable action: Scroll down.</summary>
    public static readonly ActionId ScrollDown = new($"{nameof(TypedListWidget<T>)}.{nameof(ScrollDown)}");

    /// <summary>
    /// The initial selected index when the list is first created. Defaults to 0.
    /// Only applied when the node is new.
    /// </summary>
    public int InitialSelectedIndex { get; init; } = 0;

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

    internal Func<ListSelectionChangedEventArgs<T>, Task>? SelectionChangedHandler { get; init; }
    internal Func<ListItemActivatedEventArgs<T>, Task>? ItemActivatedHandler { get; init; }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TypedListNode<T> ?? CreateNode();
        var isNewNode = existingNode is null || existingNode.GetType() != GetExpectedNodeType();

        ApplyState(node, context, isNewNode);
        await ReconcileItemNodesAsync(node, context).ConfigureAwait(false);

        if (context.IsNew)
        {
            node.IsFocused = true;
        }

        return node;
    }

    /// <summary>
    /// Creates the concrete node type. Derived widgets (e.g. <see cref="ListWidget"/>)
    /// override this to return their specialized node so casts in user-facing tests
    /// and event args stay strongly typed.
    /// </summary>
    private protected virtual TypedListNode<T> CreateNode() => new();

    private protected void ApplyState(TypedListNode<T> node, ReconcileContext context, bool isNewNode)
    {
        node.SourceWidget = this;
        node.Items = Items;
        node.ItemHeight = ItemHeight;
        node.ItemTemplate = Template;
        node.ItemKeySelector = ItemKeySelector;

        // Apply initial selection for new nodes; clamp otherwise.
        if (isNewNode && Items.Count > 0)
        {
            node.SelectedIndex = Math.Clamp(InitialSelectedIndex, 0, Items.Count - 1);
        }
        else if (node.SelectedIndex >= Items.Count && Items.Count > 0)
        {
            node.SelectedIndex = Items.Count - 1;
        }
        else if (Items.Count == 0)
        {
            node.SelectedIndex = 0;
        }

        if (SelectionChangedHandler is { } selChanged)
        {
            node.SelectionChangedAction = ctx =>
            {
                if (node.SelectedIndex >= 0 && node.SelectedIndex < node.Items.Count)
                {
                    var args = new ListSelectionChangedEventArgs<T>(this, node, ctx, node.SelectedIndex, node.Items[node.SelectedIndex]);
                    return selChanged(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.SelectionChangedAction = null;
        }

        if (ItemActivatedHandler is { } activated)
        {
            node.ItemActivatedAction = ctx =>
            {
                if (node.SelectedIndex >= 0 && node.SelectedIndex < node.Items.Count)
                {
                    var args = new ListItemActivatedEventArgs<T>(this, node, ctx, node.SelectedIndex, node.Items[node.SelectedIndex]);
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

    private protected async Task ReconcileItemNodesAsync(TypedListNode<T> node, ReconcileContext context)
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
            }
            return;
        }

        var template = Template;
        var snapshotSelected = node.SelectedIndex;
        var snapshotFocused = node.IsFocused;
        var snapshotHovered = node.IsHovered ? node.HoveredItemIndex : -1;

        // Build a key -> old-node map when a key selector is supplied so reused
        // nodes preserve template-local state even after reorder/filter.
        Dictionary<object, Hex1bNode>? oldByKey = null;
        if (ItemKeySelector is not null && node.ItemNodes.Count > 0)
        {
            oldByKey = new Dictionary<object, Hex1bNode>(node.ItemNodes.Count);
            for (int i = 0; i < node.ItemNodes.Count && i < node.Items.Count; i++)
            {
                var oldKey = ItemKeySelector(node.Items[i]);
                // First-write-wins to be safe if a key appears more than once.
                oldByKey.TryAdd(oldKey, node.ItemNodes[i]);
            }
        }

        var oldNodes = node.ItemNodes;
        var newNodes = new List<Hex1bNode>(Items.Count);
        var reused = new HashSet<Hex1bNode>(ReferenceEqualityComparer.Instance);

        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            Hex1bNode? existingChild = null;

            if (ItemKeySelector is not null && oldByKey is not null)
            {
                var key = ItemKeySelector(item);
                if (oldByKey.TryGetValue(key, out var match))
                {
                    existingChild = match;
                }
            }
            else if (i < oldNodes.Count)
            {
                existingChild = oldNodes[i];
            }

            var itemContext = new ListItemContext<T>(
                item,
                i,
                isSelected: i == snapshotSelected,
                isFocused: snapshotFocused,
                isHovered: i == snapshotHovered);

            var itemWidget = template(itemContext);
            var positionedContext = context.WithChildPosition(i, Items.Count);
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
    }

    internal override Type GetExpectedNodeType() => typeof(TypedListNode<T>);
}
