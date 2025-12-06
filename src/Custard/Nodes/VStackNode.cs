namespace Custard;

public sealed class VStackNode : CustardNode
{
    public List<CustardNode> Children { get; set; } = new();
    private int _focusedIndex = 0;
    private List<CustardNode>? _focusableNodes;

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

    private List<CustardNode> GetFocusableNodesList()
    {
        _focusableNodes ??= GetFocusableNodes().ToList();
        return _focusableNodes;
    }

    public void InvalidateFocusCache()
    {
        _focusableNodes = null;
    }

    public override void Render(CustardRenderContext context)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Render(context);
            if (i < Children.Count - 1)
            {
                context.Write("\n");
            }
        }
    }

    public override bool HandleInput(CustardInputEvent evt)
    {
        // Handle Tab to move focus
        if (evt is KeyInputEvent keyEvent && keyEvent.Key == ConsoleKey.Tab)
        {
            var focusables = GetFocusableNodesList();
            if (focusables.Count > 0)
            {
                // Clear old focus
                if (_focusedIndex >= 0 && _focusedIndex < focusables.Count)
                {
                    if (focusables[_focusedIndex] is TextBoxNode oldFocused)
                    {
                        oldFocused.IsFocused = false;
                    }
                }

                // Move focus
                if (keyEvent.Shift)
                {
                    _focusedIndex = _focusedIndex <= 0 ? focusables.Count - 1 : _focusedIndex - 1;
                }
                else
                {
                    _focusedIndex = (_focusedIndex + 1) % focusables.Count;
                }

                // Set new focus
                if (focusables[_focusedIndex] is TextBoxNode newFocused)
                {
                    newFocused.IsFocused = true;
                }
                return true;
            }
        }

        // Dispatch to focused node
        var focusablesList = GetFocusableNodesList();
        if (_focusedIndex >= 0 && _focusedIndex < focusablesList.Count)
        {
            return focusablesList[_focusedIndex].HandleInput(evt);
        }

        return false;
    }
}
