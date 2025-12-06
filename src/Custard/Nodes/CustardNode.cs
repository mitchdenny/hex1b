namespace Custard;

public abstract class CustardNode
{
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
