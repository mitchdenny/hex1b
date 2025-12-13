using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b;

public sealed class SplitterNode : Hex1bNode
{
    public Hex1bNode? Left { get; set; }
    public Hex1bNode? Right { get; set; }
    public int LeftWidth { get; set; } = 30;
    
    private bool _isFocused;
    public override bool IsFocused { get => _isFocused; set => _isFocused = value; }
    
    /// <summary>
    /// The amount to move the splitter when pressing left/right arrow keys.
    /// </summary>
    public int ResizeStep { get; set; } = 2;
    
    /// <summary>
    /// Minimum width for the left pane.
    /// </summary>
    public int MinLeftWidth { get; set; } = 5;
    
    private int _focusedIndex = 0;
    private List<Hex1bNode>? _focusableNodes;

    public override bool IsFocusable => true;

    /// <inheritdoc />
    public override bool ManagesChildFocus => true;

    /// <summary>
    /// Computes a contrasting color (black or white) based on the luminance of the input color.
    /// </summary>
    private static Hex1bColor GetContrastingColor(Hex1bColor color)
    {
        // Calculate relative luminance using the formula for sRGB
        // https://www.w3.org/TR/WCAG20/#relativeluminancedef
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return luminance > 0.5 ? Hex1bColor.Black : Hex1bColor.White;
    }

    public override Size Measure(Constraints constraints)
    {
        // Splitter: left width + divider (3 chars " │ ") + right content.
        // IMPORTANT: propagate bounded width constraints to children so text wrapping can work.
        const int dividerWidth = 3;

        if (constraints.MaxWidth == int.MaxValue && constraints.MaxHeight == int.MaxValue)
        {
            // Unbounded measure: keep legacy behavior.
            var leftSizeUnbounded = Left?.Measure(Constraints.Unbounded) ?? Size.Zero;
            var rightSizeUnbounded = Right?.Measure(Constraints.Unbounded) ?? Size.Zero;

            var widthUnbounded = LeftWidth + dividerWidth + rightSizeUnbounded.Width;
            var heightUnbounded = Math.Max(leftSizeUnbounded.Height, rightSizeUnbounded.Height);
            return constraints.Constrain(new Size(widthUnbounded, heightUnbounded));
        }

        var maxWidth = constraints.MaxWidth;
        var maxHeight = constraints.MaxHeight;

        var leftMaxWidth = Math.Max(0, Math.Min(LeftWidth, maxWidth));
        var rightMaxWidth = Math.Max(0, maxWidth - LeftWidth - dividerWidth);

        var leftConstraints = new Constraints(0, leftMaxWidth, 0, maxHeight);
        var rightConstraints = new Constraints(0, rightMaxWidth, 0, maxHeight);

        var leftSize = Left?.Measure(leftConstraints) ?? Size.Zero;
        var rightSize = Right?.Measure(rightConstraints) ?? Size.Zero;

        var width = LeftWidth + dividerWidth + rightSize.Width;
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
        
        // The splitter itself is focusable (for resizing with arrow keys)
        yield return this;
        
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
            focusables[0].IsFocused = true;
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var dividerChar = theme.Get(SplitterTheme.DividerCharacter);
        var dividerColor = theme.Get(SplitterTheme.DividerColor);
        
        // When focused, invert colors: divider color becomes background, use contrasting foreground
        Hex1bColor dividerFg;
        Hex1bColor dividerBg;
        
        if (IsFocused)
        {
            // Use divider color as background, compute contrasting foreground
            dividerBg = dividerColor.IsDefault ? Hex1bColor.White : dividerColor;
            dividerFg = GetContrastingColor(dividerBg);
        }
        else
        {
            dividerFg = dividerColor;
            dividerBg = Hex1bColor.Default;
        }
        
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
            if (IsFocused)
            {
                context.Write($"{dividerFg.ToForegroundAnsi()}{dividerBg.ToBackgroundAnsi()}{dividerChar}\x1b[0m");
            }
            else
            {
                context.Write($"{dividerFg.ToForegroundAnsi()}{dividerChar}\x1b[0m");
            }
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
                    focusables[_focusedIndex].IsFocused = false;
                }
                
                // Jump to first focusable
                _focusedIndex = 0;
                focusables[_focusedIndex].IsFocused = true;
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
                    focusables[_focusedIndex].IsFocused = false;
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
                focusables[_focusedIndex].IsFocused = true;
                return true;
            }
        }

        // Handle splitter resize when the splitter itself is focused
        if (IsFocused && evt is KeyInputEvent resizeEvent)
        {
            if (resizeEvent.Key == ConsoleKey.LeftArrow)
            {
                // Decrease left pane width
                LeftWidth = Math.Max(MinLeftWidth, LeftWidth - ResizeStep);
                return true;
            }
            else if (resizeEvent.Key == ConsoleKey.RightArrow)
            {
                // Increase left pane width (respect overall bounds)
                var maxLeftWidth = Bounds.Width - 3 - MinLeftWidth; // 3 for divider, MinLeftWidth for right pane
                LeftWidth = Math.Min(maxLeftWidth, LeftWidth + ResizeStep);
                return true;
            }
        }

        // Dispatch to focused node for regular input handling (but not to ourselves to avoid infinite recursion)
        if (_focusedIndex >= 0 && _focusedIndex < focusablesList.Count)
        {
            var focusedNode = focusablesList[_focusedIndex];
            if (focusedNode != this)
            {
                return focusedNode.HandleInput(evt);
            }
        }

        return false;
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
        Left?.SyncFocusIndex();
        Right?.SyncFocusIndex();
    }
}
