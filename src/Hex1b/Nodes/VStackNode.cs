using Hex1b.Layout;

namespace Hex1b;

public sealed class VStackNode : Hex1bNode
{
    public List<Hex1bNode> Children { get; set; } = new();
    public List<SizeHint> ChildHeightHints { get; set; } = new();
    private int _focusedIndex = 0;
    private List<Hex1bNode>? _focusableNodes;

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        foreach (var child in Children)
        {
            foreach (var focusable in child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    private List<Hex1bNode> GetFocusableNodesList()
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

    public override void Render(Hex1bRenderContext context)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            // Position cursor at child's bounds
            context.SetCursorPosition(Children[i].Bounds.X, Children[i].Bounds.Y);
            Children[i].Render(context);
        }
    }

    public override bool HandleInput(Hex1bInputEvent evt)
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

    private static void SetNodeFocus(Hex1bNode node, bool focused)
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

    /// <summary>
    /// Syncs _focusedIndex to match which child node has IsFocused = true.
    /// Call this after externally setting focus on a child node.
    /// </summary>
    public void SyncFocusIndex()
    {
        var focusables = GetFocusableNodesList();
        for (int i = 0; i < focusables.Count; i++)
        {
            var node = focusables[i];
            bool isFocused = node switch
            {
                TextBoxNode textBox => textBox.IsFocused,
                ButtonNode button => button.IsFocused,
                ListNode list => list.IsFocused,
                _ => false
            };
            if (isFocused)
            {
                _focusedIndex = i;
                return;
            }
        }
    }
}
