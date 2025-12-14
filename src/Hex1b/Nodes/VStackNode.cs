using Hex1b.Input;
using Hex1b.Layout;

namespace Hex1b;

public sealed class VStackNode : Hex1bNode
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
        // VStack: take max width, sum heights
        // Pass width constraint to children so they can wrap if needed
        var maxWidth = 0;
        var totalHeight = 0;

        foreach (var child in Children)
        {
            // Children get the parent's width constraint but unbounded height
            var childConstraints = new Constraints(0, constraints.MaxWidth, 0, int.MaxValue);
            var childSize = child.Measure(childConstraints);
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
            var hint = Children[i].HeightHint ?? SizeHint.Content;

            if (hint.IsFixed)
            {
                childSizes[i] = hint.FixedValue;
                totalFixed += hint.FixedValue;
            }
            else if (hint.IsContent)
            {
                // Content height often depends on available width (e.g., wrapped TextBlock).
                // Measure with the current bounds width so content sizing is accurate.
                var measured = Children[i].Measure(new Constraints(0, bounds.Width, 0, int.MaxValue));
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
                var hint = Children[i].HeightHint ?? SizeHint.Content;
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

    /// <summary>
    /// Checks if we're nested inside another container (HStack/VStack) that also has focusables.
    /// When nested, Tab at the boundary should bubble up, not wrap.
    /// </summary>
    private bool IsNestedInsideFocusableContainer()
    {
        var current = Parent;
        while (current != null)
        {
            if (current is HStackNode or VStackNode)
            {
                // Check if the ancestor has focusables (meaning it can navigate focus)
                if (current.GetFocusableNodes().Any())
                {
                    return true;
                }
            }
            current = current.Parent;
        }
        return false;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Don't register Tab bindings if:
        // 1. An ancestor manages focus (Splitter) - let Tab bubble up
        // 2. We have 0-1 focusables - nothing to cycle
        // 3. We're nested inside another focusable container - use HandleInput for boundary-aware navigation
        if (HasAncestorThatManagesFocus())
        {
            return; // Let Splitter handle Tab
        }
        
        var focusableCount = GetFocusableNodesList().Count;
        if (focusableCount <= 1)
        {
            return; // Nothing to cycle, let Tab bubble up
        }
        
        if (IsNestedInsideFocusableContainer())
        {
            // We're nested - don't register bindings, use HandleInput instead
            // This allows us to return NotHandled at boundaries
            return;
        }
        
        // Top-level container with multiple focusables - register Tab bindings (will cycle)
        bindings.Key(Hex1bKey.Tab).Action(FocusNext, "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(FocusPrevious, "Previous focusable");
    }

    /// <summary>
    /// Handles Tab/Shift+Tab for nested containers with boundary-aware navigation.
    /// Returns NotHandled at boundaries to let parent containers navigate.
    /// </summary>
    public override InputResult HandleInput(Hex1bKeyEvent keyEvent)
    {
        // Only handle Tab for nested containers
        if (!IsNestedInsideFocusableContainer())
        {
            return InputResult.NotHandled;
        }
        
        var focusables = GetFocusableNodesList();
        if (focusables.Count <= 1)
        {
            return InputResult.NotHandled;
        }
        
        var isTab = keyEvent.Key == Hex1bKey.Tab && keyEvent.Modifiers == Hex1bModifiers.None;
        var isShiftTab = keyEvent.Key == Hex1bKey.Tab && keyEvent.Modifiers == Hex1bModifiers.Shift;
        
        if (!isTab && !isShiftTab)
        {
            return InputResult.NotHandled;
        }
        
        // Find current focus index
        var currentIndex = -1;
        for (int i = 0; i < focusables.Count; i++)
        {
            if (focusables[i].IsFocused)
            {
                currentIndex = i;
                break;
            }
        }
        
        if (currentIndex < 0)
        {
            return InputResult.NotHandled;
        }
        
        // Check if we're at a boundary
        if (isTab && currentIndex == focusables.Count - 1)
        {
            // At last focusable, Tab should bubble up to parent
            return InputResult.NotHandled;
        }
        
        if (isShiftTab && currentIndex == 0)
        {
            // At first focusable, Shift+Tab should bubble up to parent
            return InputResult.NotHandled;
        }
        
        // Not at boundary - move focus internally
        focusables[currentIndex].IsFocused = false;
        var newIndex = isTab ? currentIndex + 1 : currentIndex - 1;
        focusables[newIndex].IsFocused = true;
        _focusedIndex = newIndex;
        
        return InputResult.Handled;
    }

    private void FocusNext()
    {
        MoveFocus(forward: true);
    }

    private void FocusPrevious()
    {
        MoveFocus(forward: false);
    }

    private void MoveFocus(bool forward)
    {
        var focusables = GetFocusableNodesList();
        if (focusables.Count == 0) return;

        // Clear old focus
        if (_focusedIndex >= 0 && _focusedIndex < focusables.Count)
        {
            focusables[_focusedIndex].IsFocused = false;
        }

        // Move focus
        if (forward)
        {
            _focusedIndex = (_focusedIndex + 1) % focusables.Count;
        }
        else
        {
            _focusedIndex = _focusedIndex <= 0 ? focusables.Count - 1 : _focusedIndex - 1;
        }

        // Set new focus
        focusables[_focusedIndex].IsFocused = true;
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
