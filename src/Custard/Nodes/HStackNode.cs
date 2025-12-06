namespace Custard;

public sealed class HStackNode : CustardNode
{
    public List<CustardNode> Children { get; set; } = new();
    public int FocusedIndex { get; set; } = 0;

    public override IEnumerable<CustardNode> GetFocusableNodes()
    {
        foreach (var child in Children)
        {
            foreach (var focusable in child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override void Render(CustardRenderContext context)
    {
        // Render children horizontally (no separator, just concatenate)
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Render(context);
        }
    }

    public override bool HandleInput(CustardInputEvent evt)
    {
        // Dispatch to focused child
        if (FocusedIndex >= 0 && FocusedIndex < Children.Count)
        {
            return Children[FocusedIndex].HandleInput(evt);
        }

        return false;
    }
}
