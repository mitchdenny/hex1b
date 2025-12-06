using Custard.Layout;

namespace Custard;

public sealed class VStackNode : CustardNode
{
    public List<CustardNode> Children { get; set; } = new();
    public List<SizeHint> ChildHeightHints { get; set; } = new();
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

    public override Size Measure(Constraints constraints)
    {
        // VStack: take max width, sum heights
        var maxWidth = 0;
        var totalHeight = 0;

        foreach (var child in Children)
        {
            var childSize = child.Measure(Constraints.Unbounded);
            maxWidth = Math.Max(maxWidth, childSize.Width);
            totalHeight += childSize.Height;
        }

        return constraints.Constrain(new Size(maxWidth, totalHeight));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        if (Children.Count == 0) return;

        // Calculate how to distribute height among children
        var availableHeight = bounds.Height;
        var childSizes = new int[Children.Count];
        var totalFixed = 0;
        var totalWeight = 0;

        // First pass: measure content-sized and fixed children
        for (int i = 0; i < Children.Count; i++)
        {
            var hint = i < ChildHeightHints.Count ? ChildHeightHints[i] : SizeHint.Content;

            if (hint.IsFixed)
            {
                childSizes[i] = hint.FixedValue;
                totalFixed += hint.FixedValue;
            }
            else if (hint.IsContent)
            {
                var measured = Children[i].Measure(Constraints.Unbounded);
                childSizes[i] = measured.Height;
                totalFixed += measured.Height;
            }
            else if (hint.IsFill)
            {
                totalWeight += hint.FillWeight;
            }
        }

        // Second pass: distribute remaining space to fill children
        var remaining = Math.Max(0, availableHeight - totalFixed);
        if (totalWeight > 0)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                var hint = i < ChildHeightHints.Count ? ChildHeightHints[i] : SizeHint.Content;
                if (hint.IsFill)
                {
                    childSizes[i] = remaining * hint.FillWeight / totalWeight;
                }
            }
        }

        // Arrange children
        var y = bounds.Y;
        for (int i = 0; i < Children.Count; i++)
        {
            var childBounds = new Rect(bounds.X, y, bounds.Width, childSizes[i]);
            Children[i].Arrange(childBounds);
            y += childSizes[i];
        }
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
                    SetNodeFocus(focusables[_focusedIndex], false);
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
                SetNodeFocus(focusables[_focusedIndex], true);
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

    private static void SetNodeFocus(CustardNode node, bool focused)
    {
        switch (node)
        {
            case TextBoxNode textBox:
                textBox.IsFocused = focused;
                break;
            case ButtonNode button:
                button.IsFocused = focused;
                break;
            case ListNode list:
                list.IsFocused = focused;
                break;
        }
    }
}
