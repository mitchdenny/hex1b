using Hex1b.Layout;

namespace Hex1b;

public sealed class HStackNode : Hex1bNode
{
    public List<Hex1bNode> Children { get; set; } = new();
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
        if (_focusableNodes == null)
        {
            _focusableNodes = GetFocusableNodes().ToList();
            // Auto-focus the first focusable node if none are focused
            if (_focusableNodes.Count > 0 && _focusedIndex == 0)
            {
                SetNodeFocus(_focusableNodes[0], true);
            }
        }
        return _focusableNodes;
    }

    public void InvalidateFocusCache()
    {
        _focusableNodes = null;
    }

    public override Size Measure(Constraints constraints)
    {
        // HStack: sum widths, take max height
        // Pass height constraint to children so they can size appropriately
        var totalWidth = 0;
        var maxHeight = 0;

        foreach (var child in Children)
        {
            // Children get the parent's height constraint but unbounded width
            var childConstraints = new Constraints(0, int.MaxValue, 0, constraints.MaxHeight);
            var childSize = child.Measure(childConstraints);
            totalWidth += childSize.Width;
            maxHeight = Math.Max(maxHeight, childSize.Height);
        }

        return constraints.Constrain(new Size(totalWidth, maxHeight));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        if (Children.Count == 0) return;

        // Calculate how to distribute width among children
        var availableWidth = bounds.Width;
        var childSizes = new int[Children.Count];
        var totalFixed = 0;
        var totalWeight = 0;

        // First pass: measure content-sized and fixed children
        for (int i = 0; i < Children.Count; i++)
        {
            var hint = Children[i].WidthHint ?? SizeHint.Content;

            if (hint.IsFixed)
            {
                childSizes[i] = hint.FixedValue;
                totalFixed += hint.FixedValue;
            }
            else if (hint.IsContent)
            {
                var measured = Children[i].Measure(Constraints.Unbounded);
                childSizes[i] = measured.Width;
                totalFixed += measured.Width;
            }
            else if (hint.IsFill)
            {
                totalWeight += hint.FillWeight;
            }
        }

        // Second pass: distribute remaining space to fill children
        var remaining = Math.Max(0, availableWidth - totalFixed);
        if (totalWeight > 0)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                var hint = Children[i].WidthHint ?? SizeHint.Content;
                if (hint.IsFill)
                {
                    childSizes[i] = remaining * hint.FillWeight / totalWeight;
                }
            }
        }

        // Arrange children
        var x = bounds.X;
        for (int i = 0; i < Children.Count; i++)
        {
            var childBounds = new Rect(x, bounds.Y, childSizes[i], bounds.Height);
            Children[i].Arrange(childBounds);
            x += childSizes[i];
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Render children at their positioned bounds
        for (int i = 0; i < Children.Count; i++)
        {
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
            case SplitterNode splitter:
                splitter.IsFocused = focused;
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
                SplitterNode splitter => splitter.IsFocused,
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
