using Hex1b.Input;
using Hex1b.Layout;

namespace Hex1b;

public abstract class Hex1bNode
{
    /// <summary>
    /// The bounds assigned to this node after layout.
    /// </summary>
    public Rect Bounds { get; set; }

    /// <summary>
    /// The bounds from the previous frame, used for dirty region tracking.
    /// Before the first arrange, this will be an empty rect at (0,0).
    /// </summary>
    public Rect PreviousBounds { get; private set; }

    /// <summary>
    /// Bounds of child nodes that were removed during reconciliation.
    /// These regions need to be cleared to avoid visual artifacts.
    /// Cleared after each render frame by the framework.
    /// </summary>
    internal List<Rect>? OrphanedChildBounds { get; private set; }

    /// <summary>
    /// Adds the bounds of a removed child node to the orphaned bounds list.
    /// Call this during reconciliation when a child is removed from the tree.
    /// </summary>
    /// <param name="bounds">The bounds of the removed child.</param>
    internal void AddOrphanedChildBounds(Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        OrphanedChildBounds ??= new List<Rect>();
        OrphanedChildBounds.Add(bounds);
        MarkDirty(); // Container needs re-render to clear orphaned regions
    }

    /// <summary>
    /// Clears the list of orphaned child bounds after they've been processed.
    /// Called by the framework after clearing dirty regions.
    /// </summary>
    internal void ClearOrphanedChildBounds()
    {
        OrphanedChildBounds?.Clear();
    }

    /// <summary>
    /// Whether this node needs to be re-rendered.
    /// New nodes start dirty. The framework clears this after each render frame.
    /// </summary>
    /// <remarks>
    /// This flag is automatically managed by the framework:
    /// <list type="bullet">
    ///   <item>New nodes are created with IsDirty = true</item>
    ///   <item>Nodes are marked dirty when their bounds change during Arrange()</item>
    ///   <item>The framework calls ClearDirty() on all nodes after rendering</item>
    /// </list>
    /// Widget authors can call MarkDirty() for internal state changes that don't
    /// flow through reconciliation (e.g., animation timers, cursor blink).
    /// </remarks>
    public bool IsDirty { get; private set; } = true;

    /// <summary>
    /// Marks this node as needing re-rendering.
    /// Call this when internal state changes that don't flow through widget reconciliation.
    /// </summary>
    /// <example>
    /// <code>
    /// // In a custom node with a blinking cursor:
    /// private void OnBlinkTimer()
    /// {
    ///     _cursorVisible = !_cursorVisible;
    ///     MarkDirty();
    /// }
    /// </code>
    /// </example>
    public void MarkDirty()
    {
        IsDirty = true;
    }

    /// <summary>
    /// Clears the dirty flag after rendering. Called by the framework.
    /// </summary>
    internal void ClearDirty()
    {
        IsDirty = false;
    }

    /// <summary>
    /// Inherits bounds from a replaced node for proper dirty region clearing.
    /// When a node is replaced by a different type, we need to know the old bounds
    /// to clear the region previously occupied by the old node.
    /// </summary>
    /// <param name="replacedNode">The node being replaced.</param>
    internal void InheritBoundsFromReplacedNode(Hex1bNode replacedNode)
    {
        // Set Bounds to the replaced node's Bounds so that when Arrange() is called,
        // it will copy this to PreviousBounds before setting the new Bounds.
        // This ensures ClearDirtyRegions knows to clear the old region.
        Bounds = replacedNode.Bounds;
    }

    /// <summary>
    /// The parent node in the tree (set during reconciliation).
    /// </summary>
    public Hex1bNode? Parent { get; set; }

    /// <summary>
    /// Optional callback to configure bindings for this node.
    /// Set from the widget's WithInputBindings() call.
    /// </summary>
    internal Action<InputBindingsBuilder>? BindingsConfigurator { get; set; }

    /// <summary>
    /// Configures the default input bindings for this node type.
    /// Override in derived classes to add default key bindings.
    /// These bindings can be inspected and modified by the user's callback.
    /// </summary>
    /// <param name="bindings">The builder to add bindings to.</param>
    public virtual void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Base implementation does nothing - leaf nodes have no default bindings
    }

    /// <summary>
    /// Builds the complete set of bindings for this node.
    /// Called during input routing to get the trie for this node's layer.
    /// </summary>
    internal InputBindingsBuilder BuildBindings()
    {
        var builder = new InputBindingsBuilder();
        ConfigureDefaultBindings(builder);
        BindingsConfigurator?.Invoke(builder);
        return builder;
    }

    /// <summary>
    /// Hint for how this node should be sized horizontally within its parent.
    /// Used by HStack to distribute width among children.
    /// </summary>
    public SizeHint? WidthHint { get; set; }

    /// <summary>
    /// Hint for how this node should be sized vertically within its parent.
    /// Used by VStack to distribute height among children.
    /// </summary>
    public SizeHint? HeightHint { get; set; }

    /// <summary>
    /// Measures the desired size of this node given the constraints.
    /// </summary>
    public abstract Size Measure(Constraints constraints);

    /// <summary>
    /// Assigns final bounds to this node and arranges children.
    /// Saves the previous bounds before updating for dirty region tracking.
    /// Marks the node dirty if bounds changed.
    /// </summary>
    public virtual void Arrange(Rect bounds)
    {
        PreviousBounds = Bounds;
        
        // Mark dirty if position or size changed
        if (Bounds != bounds)
        {
            MarkDirty();
        }
        
        Bounds = bounds;
    }

    /// <summary>
    /// Renders the node to the given context.
    /// </summary>
    public abstract void Render(Hex1bRenderContext context);

    /// <summary>
    /// Handles a key input event (after bindings have been checked).
    /// Override this in nodes to handle input that wasn't matched by any binding.
    /// </summary>
    /// <param name="keyEvent">The key event to handle.</param>
    /// <returns>Handled if the input was consumed, NotHandled otherwise.</returns>
    public virtual InputResult HandleInput(Hex1bKeyEvent keyEvent) => InputResult.NotHandled;

    /// <summary>
    /// Handles a mouse click event (after mouse bindings have been checked).
    /// Override this in nodes to handle clicks that weren't matched by any mouse binding.
    /// The coordinates in the event are local to this node's bounds (0,0 is top-left of node).
    /// </summary>
    /// <param name="localX">The X coordinate relative to this node's bounds.</param>
    /// <param name="localY">The Y coordinate relative to this node's bounds.</param>
    /// <param name="mouseEvent">The original mouse event (with absolute coordinates).</param>
    /// <returns>Handled if the click was consumed, NotHandled otherwise.</returns>
    public virtual InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent) => InputResult.NotHandled;

    /// <summary>
    /// Returns true if this node can receive focus.
    /// </summary>
    public virtual bool IsFocusable => false;

    /// <summary>
    /// Gets the preferred cursor shape when the mouse is over this node.
    /// Override this to customize cursor appearance (e.g., text input uses a bar cursor).
    /// </summary>
    public virtual CursorShape PreferredCursorShape => CursorShape.SteadyBlock;

    /// <summary>
    /// Gets or sets whether this node is currently focused.
    /// Only meaningful for focusable nodes (where IsFocusable is true).
    /// </summary>
    public virtual bool IsFocused { get => false; set { } }

    /// <summary>
    /// Gets or sets whether the mouse is currently hovering over this node.
    /// Set by Hex1bApp based on mouse position during each frame.
    /// Only set on focusable nodes (tracked via FocusRing hit testing).
    /// </summary>
    public virtual bool IsHovered { get => false; set { } }

    /// <summary>
    /// Syncs internal focus tracking to match the current IsFocused state of child nodes.
    /// Called after externally setting focus on a child node.
    /// Container nodes should override this to update their internal focus index.
    /// </summary>
    public virtual void SyncFocusIndex() { }

    /// <summary>
    /// Returns true if this node manages focus for its children.
    /// When a parent manages focus, child containers should NOT set initial focus themselves.
    /// Container nodes like SplitterNode should override this to return true.
    /// </summary>
    public virtual bool ManagesChildFocus => false;

    /// <summary>
    /// Gets all focusable nodes in this subtree (including this node if focusable).
    /// </summary>
    public virtual IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (IsFocusable)
        {
            yield return this;
        }
    }

    /// <summary>
    /// Gets the direct children of this node. Used for input routing and tree traversal.
    /// Container nodes should override this to return their children.
    /// </summary>
    public virtual IEnumerable<Hex1bNode> GetChildren() => [];

    /// <summary>
    /// Gets the bounds used for mouse hit testing.
    /// By default, returns the full Bounds. Override in nodes where only a portion
    /// of the bounds should respond to clicks (e.g., SplitterNode's divider).
    /// </summary>
    public virtual Rect HitTestBounds => Bounds;
    
    /// <summary>
    /// Checks if this node or any of its descendants need rendering.
    /// Used to determine if a subtree can be skipped entirely.
    /// </summary>
    public bool NeedsRender()
    {
        if (IsDirty) return true;
        
        foreach (var child in GetChildren())
        {
            if (child.NeedsRender()) return true;
        }
        
        return false;
    }
}
