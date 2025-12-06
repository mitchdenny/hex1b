using Custard.Layout;

namespace Custard;

public abstract class CustardNode
{
    /// <summary>
    /// The bounds assigned to this node after layout.
    /// </summary>
    public Rect Bounds { get; set; }

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
