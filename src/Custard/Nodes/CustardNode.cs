using Custard.Input;
using Custard.Layout;

namespace Custard;

public abstract class CustardNode
{
    /// <summary>
    /// The bounds assigned to this node after layout.
    /// </summary>
    public Rect Bounds { get; set; }

    /// <summary>
    /// The parent node in the tree (set during reconciliation).
    /// </summary>
    public CustardNode? Parent { get; set; }

    /// <summary>
    /// Keyboard shortcuts for this node.
    /// </summary>
    public IReadOnlyList<Shortcut> Shortcuts { get; set; } = [];

    /// <summary>
    /// Measures the desired size of this node given the constraints.
    /// </summary>
    public abstract Size Measure(Constraints constraints);

    /// <summary>
    /// Assigns final bounds to this node and arranges children.
    /// </summary>
    public virtual void Arrange(Rect bounds)
    {
        Bounds = bounds;
    }

    /// <summary>
    /// Renders the node to the given context.
    /// </summary>
    public abstract void Render(CustardRenderContext context);

    /// <summary>
    /// Handles an input event. Returns true if the event was handled.
    /// </summary>
    public virtual bool HandleInput(CustardInputEvent evt) => false;

    /// <summary>
    /// Tries to handle the event as a shortcut, bubbling up to ancestors.
    /// Returns true if a shortcut was matched and executed.
    /// </summary>
    public bool TryHandleShortcut(CustardInputEvent evt)
    {
        if (evt is not KeyInputEvent keyEvent)
            return false;

        // Try this node's shortcuts first
        foreach (var shortcut in Shortcuts)
        {
            if (shortcut.Matches(keyEvent))
            {
                shortcut.Execute();
                return true;
            }
        }

        // Bubble up to parent
        return Parent?.TryHandleShortcut(evt) ?? false;
    }

    /// <summary>
    /// Returns true if this node can receive focus.
    /// </summary>
    public virtual bool IsFocusable => false;

    /// <summary>
    /// Gets all focusable nodes in this subtree (including this node if focusable).
    /// </summary>
    public virtual IEnumerable<CustardNode> GetFocusableNodes()
    {
        if (IsFocusable)
        {
            yield return this;
        }
    }
}
