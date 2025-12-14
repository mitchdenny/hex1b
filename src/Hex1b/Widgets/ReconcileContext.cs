#pragma warning disable HEX1B001 // Navigator API is experimental - internal usage is allowed

using Hex1b.Input;

namespace Hex1b.Widgets;

/// <summary>
/// Context passed to widget reconciliation methods, providing access to
/// child reconciliation and focus management utilities.
/// </summary>
public sealed class ReconcileContext
{
    /// <summary>
    /// The parent node in the tree (used for focus management decisions).
    /// </summary>
    public Hex1bNode? Parent { get; }
    
    /// <summary>
    /// The full chain of ancestor nodes, from immediate parent to root.
    /// This is needed because during reconciliation, node.Parent links may not
    /// be set yet on intermediate nodes.
    /// </summary>
    private readonly IReadOnlyList<Hex1bNode> _ancestors;

    /// <summary>
    /// Whether this is a new node being created (vs updating an existing one).
    /// </summary>
    public bool IsNew { get; internal set; }

    private ReconcileContext(Hex1bNode? parent, IReadOnlyList<Hex1bNode>? ancestors = null)
    {
        Parent = parent;
        _ancestors = ancestors ?? Array.Empty<Hex1bNode>();
    }

    /// <summary>
    /// Creates a root reconcile context (no parent).
    /// </summary>
    internal static ReconcileContext CreateRoot() => new(null);

    /// <summary>
    /// Creates a child context with the specified parent.
    /// The new context includes the full ancestor chain.
    /// </summary>
    internal ReconcileContext WithParent(Hex1bNode parent)
    {
        // Build the new ancestor list: [parent, ...current ancestors]
        var newAncestors = new List<Hex1bNode>(_ancestors.Count + 1) { parent };
        newAncestors.AddRange(_ancestors);
        return new ReconcileContext(parent, newAncestors);
    }

    /// <summary>
    /// Reconciles a child widget, returning the updated or new node.
    /// </summary>
    public Hex1bNode? ReconcileChild(Hex1bNode? existingNode, Hex1bWidget? widget, Hex1bNode parent)
    {
        if (widget is null)
        {
            return null;
        }

        var childContext = WithParent(parent);
        childContext.IsNew = existingNode is null || existingNode.GetType() != widget.GetExpectedNodeType();
        
        var node = widget.Reconcile(existingNode, childContext);

        // Set common properties on the reconciled node
        node.Parent = parent;
        node.InputBindings = widget.InputBindings ?? [];
        node.WidthHint = widget.WidthHint;
        node.HeightHint = widget.HeightHint;

        return node;
    }

    /// <summary>
    /// Returns true if the parent node (or any ancestor) manages focus for its children.
    /// When a parent manages focus, child containers should NOT set initial focus.
    /// </summary>
    public bool ParentManagesFocus()
    {
        // Use the ancestor chain stored in the context, not node.Parent links,
        // because node.Parent links may not be set yet during reconciliation.
        foreach (var ancestor in _ancestors)
        {
            // Check the virtual property - any container can declare it manages child focus
            if (ancestor.ManagesChildFocus)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Sets focus on a node. Uses the virtual IsFocused property on Hex1bNode.
    /// </summary>
    public static void SetNodeFocus(Hex1bNode node, bool focused)
    {
        node.IsFocused = focused;
    }

    /// <summary>
    /// Checks if a node currently has focus. Uses the virtual IsFocused property on Hex1bNode.
    /// </summary>
    public static bool IsNodeFocused(Hex1bNode node)
    {
        return node.IsFocused;
    }

    /// <summary>
    /// Recursively syncs focus indices on container nodes after focus has been set.
    /// Uses the virtual SyncFocusIndex method on Hex1bNode.
    /// </summary>
    public static void SyncContainerFocusIndices(Hex1bNode node)
    {
        // Call the virtual method - containers override this to sync their internal state
        node.SyncFocusIndex();
    }
}
