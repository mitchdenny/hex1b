using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b;

public sealed class SplitterNode : Hex1bNode
{
    public Hex1bNode? Left { get; set; }
    public Hex1bNode? Right { get; set; }
    public int LeftWidth { get; set; } = 30;
    private int _focusedIndex = 0;
    private List<Hex1bNode>? _focusableNodes;

    public override Size Measure(Constraints constraints)
    {
        // Splitter: left width + divider (3 chars " │ ") + right content
        var leftSize = Left?.Measure(Constraints.Unbounded) ?? Size.Zero;
        var rightSize = Right?.Measure(Constraints.Unbounded) ?? Size.Zero;
        
        var width = LeftWidth + 3 + rightSize.Width;
        var height = Math.Max(leftSize.Height, rightSize.Height);
        
        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        // Left pane gets LeftWidth
        if (Left != null)
        {
            Left.Arrange(new Rect(bounds.X, bounds.Y, LeftWidth, bounds.Height));
        }
        
        // Right pane gets remaining width (minus 3 for divider)
        if (Right != null)
        {
            var rightX = bounds.X + LeftWidth + 3;
            var rightWidth = Math.Max(0, bounds.Width - LeftWidth - 3);
            Right.Arrange(new Rect(rightX, bounds.Y, rightWidth, bounds.Height));
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Left != null)
        {
            foreach (var focusable in Left.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
        if (Right != null)
        {
            foreach (var focusable in Right.GetFocusableNodes())
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

    public void SetInitialFocus()
    {
        var focusables = GetFocusableNodesList();
        if (focusables.Count > 0)
        {
            SetNodeFocus(focusables[0], true);
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var dividerChar = theme.Get(SplitterTheme.DividerCharacter);
        var dividerColor = theme.Get(SplitterTheme.DividerColor);
        
        // Render left pane at its bounds
        if (Left != null)
        {
            context.SetCursorPosition(Left.Bounds.X, Left.Bounds.Y);
            Left.Render(context);
        }
        
        // Render divider line for each row in our bounds
        var dividerX = Bounds.X + LeftWidth + 1;
        for (int row = 0; row < Bounds.Height; row++)
        {
            context.SetCursorPosition(dividerX, Bounds.Y + row);
            context.Write($"{dividerColor.ToForegroundAnsi()}{dividerChar}\x1b[0m");
        }
        
        // Render right pane at its bounds
        if (Right != null)
        {
            context.SetCursorPosition(Right.Bounds.X, Right.Bounds.Y);
            Right.Render(context);
        }
    }

    public override bool HandleInput(Hex1bInputEvent evt)
    {
        // First, try shortcuts on focused node (bubbles up through parents)
        var focusablesList = GetFocusableNodesList();
        if (_focusedIndex >= 0 && _focusedIndex < focusablesList.Count)
        {
            if (focusablesList[_focusedIndex].TryHandleShortcut(evt))
            {
                return true;
            }
        }

        // Handle Escape to jump focus back to first focusable (e.g., master list)
        if (evt is KeyInputEvent escapeEvent && escapeEvent.Key == ConsoleKey.Escape)
        {
            var focusables = GetFocusableNodesList();
            if (focusables.Count > 0 && _focusedIndex != 0)
            {
                // Clear old focus
                if (_focusedIndex >= 0 && _focusedIndex < focusables.Count)
                {
                    SetNodeFocus(focusables[_focusedIndex], false);
                }
                
                // Jump to first focusable
                _focusedIndex = 0;
                SetNodeFocus(focusables[_focusedIndex], true);
                return true;
            }
        }
        
        // Handle Tab to move focus across all focusable nodes
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

        // Dispatch to focused node for regular input handling
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
