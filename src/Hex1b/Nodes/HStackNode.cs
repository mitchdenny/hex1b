using Hex1b.Input;
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
            // Sync _focusedIndex to match any node that's already focused
            // (focus is set externally during reconciliation)
            for (int i = 0; i < _focusableNodes.Count; i++)
            {
                if (_focusableNodes[i].IsFocused)
                {
                    _focusedIndex = i;
                    break;
                }
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

    /// <summary>
    /// Checks if any ancestor node manages child focus.
    /// When an ancestor manages focus, this container should NOT handle Tab.
    /// </summary>
    private bool HasAncestorThatManagesFocus()
    {
        var current = Parent;
        while (current != null)
        {
            if (current.ManagesChildFocus)
            {
                return true;
            }
            current = current.Parent;
        }
        return false;
    }

    public override InputResult HandleInput(Hex1bKeyEvent keyEvent)
    {
        // Handle Tab to move focus only if no ancestor manages focus
        // If an ancestor (like SplitterNode) manages focus, let Tab bubble up to it
        if (keyEvent.Key == Hex1bKey.Tab && !HasAncestorThatManagesFocus())
        {
            var focusables = GetFocusableNodesList();
            if (focusables.Count > 0)
            {
                // Clear old focus
                if (_focusedIndex >= 0 && _focusedIndex < focusables.Count)
                {
                    focusables[_focusedIndex].IsFocused = false;
                }

                // Move focus
                if (keyEvent.Modifiers.HasFlag(Hex1bModifiers.Shift))
                {
                    _focusedIndex = _focusedIndex <= 0 ? focusables.Count - 1 : _focusedIndex - 1;
                }
                else
                {
                    _focusedIndex = (_focusedIndex + 1) % focusables.Count;
                }

                // Set new focus
                focusables[_focusedIndex].IsFocused = true;
                return InputResult.Handled;
            }
        }

        return InputResult.NotHandled;
    }

    /// <summary>
    /// Syncs _focusedIndex to match which child node has IsFocused = true.
    /// Call this after externally setting focus on a child node.
    /// </summary>
    public override void SyncFocusIndex()
    {
        var focusables = GetFocusableNodesList();
        for (int i = 0; i < focusables.Count; i++)
        {
            if (focusables[i].IsFocused)
            {
                _focusedIndex = i;
                break;
            }
        }
        
        // Recursively sync children
        foreach (var child in Children)
        {
            child.SyncFocusIndex();
        }
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren() => Children;
}
