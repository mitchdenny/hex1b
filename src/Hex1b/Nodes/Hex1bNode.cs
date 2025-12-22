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
    /// </summary>
    public virtual void Arrange(Rect bounds)
    {
        PreviousBounds = Bounds;
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
}
