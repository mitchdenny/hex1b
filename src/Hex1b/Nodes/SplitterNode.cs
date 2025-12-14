using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b;

public sealed class SplitterNode : Hex1bNode
{
    /// <summary>
    /// The first child (left for horizontal, top for vertical).
    /// </summary>
    public Hex1bNode? First { get; set; }
    
    /// <summary>
    /// The second child (right for horizontal, bottom for vertical).
    /// </summary>
    public Hex1bNode? Second { get; set; }
    
    /// <summary>
    /// The size of the first pane in characters (width for horizontal, height for vertical).
    /// </summary>
    public int FirstSize { get; set; } = 30;
    
    /// <summary>
    /// The orientation of the splitter.
    /// </summary>
    public SplitterOrientation Orientation { get; set; } = SplitterOrientation.Horizontal;
    
    // Legacy property aliases for backward compatibility
    public Hex1bNode? Left { get => First; set => First = value; }
    public Hex1bNode? Right { get => Second; set => Second = value; }
    public int LeftWidth { get => FirstSize; set => FirstSize = value; }
    
    private bool _isFocused;
    public override bool IsFocused { get => _isFocused; set => _isFocused = value; }
    
    /// <summary>
    /// The amount to move the splitter when pressing arrow keys.
    /// </summary>
    public int ResizeStep { get; set; } = 2;
    
    /// <summary>
    /// Minimum size for the first pane (width for horizontal, height for vertical).
    /// </summary>
    public int MinFirstSize { get; set; } = 5;
    
    // Legacy property alias
    public int MinLeftWidth { get => MinFirstSize; set => MinFirstSize = value; }
    
    private int _focusedIndex = 0;
    private List<Hex1bNode>? _focusableNodes;

    public override bool IsFocusable => true;

    /// <inheritdoc />
    public override bool ManagesChildFocus => true;

    /// <summary>
    /// The width of the divider in characters (3 for horizontal: " │ ", 1 for vertical: "─").
    /// </summary>
    private int DividerSize => Orientation == SplitterOrientation.Horizontal ? 3 : 1;

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
        var dividerSize = DividerSize;

        if (Orientation == SplitterOrientation.Horizontal)
        {
            return MeasureHorizontal(constraints, dividerSize);
        }
        else
        {
            return MeasureVertical(constraints, dividerSize);
        }
    }

    private Size MeasureHorizontal(Constraints constraints, int dividerWidth)
    {
        if (constraints.MaxWidth == int.MaxValue && constraints.MaxHeight == int.MaxValue)
        {
            // Unbounded measure: keep legacy behavior.
            var firstSizeUnbounded = First?.Measure(Constraints.Unbounded) ?? Size.Zero;
            var secondSizeUnbounded = Second?.Measure(Constraints.Unbounded) ?? Size.Zero;

            var widthUnbounded = FirstSize + dividerWidth + secondSizeUnbounded.Width;
            var heightUnbounded = Math.Max(firstSizeUnbounded.Height, secondSizeUnbounded.Height);
            return constraints.Constrain(new Size(widthUnbounded, heightUnbounded));
        }

        var maxWidth = constraints.MaxWidth;
        var maxHeight = constraints.MaxHeight;

        var firstMaxWidth = Math.Max(0, Math.Min(FirstSize, maxWidth));
        var secondMaxWidth = Math.Max(0, maxWidth - FirstSize - dividerWidth);

        var firstConstraints = new Constraints(0, firstMaxWidth, 0, maxHeight);
        var secondConstraints = new Constraints(0, secondMaxWidth, 0, maxHeight);

        var firstSize = First?.Measure(firstConstraints) ?? Size.Zero;
        var secondSize = Second?.Measure(secondConstraints) ?? Size.Zero;

        var width = FirstSize + dividerWidth + secondSize.Width;
        var height = Math.Max(firstSize.Height, secondSize.Height);
        return constraints.Constrain(new Size(width, height));
    }

    private Size MeasureVertical(Constraints constraints, int dividerHeight)
    {
        if (constraints.MaxWidth == int.MaxValue && constraints.MaxHeight == int.MaxValue)
        {
            // Unbounded measure
            var firstSizeUnbounded = First?.Measure(Constraints.Unbounded) ?? Size.Zero;
            var secondSizeUnbounded = Second?.Measure(Constraints.Unbounded) ?? Size.Zero;

            var widthUnbounded = Math.Max(firstSizeUnbounded.Width, secondSizeUnbounded.Width);
            var heightUnbounded = FirstSize + dividerHeight + secondSizeUnbounded.Height;
            return constraints.Constrain(new Size(widthUnbounded, heightUnbounded));
        }

        var maxWidth = constraints.MaxWidth;
        var maxHeight = constraints.MaxHeight;

        var firstMaxHeight = Math.Max(0, Math.Min(FirstSize, maxHeight));
        var secondMaxHeight = Math.Max(0, maxHeight - FirstSize - dividerHeight);

        var firstConstraints = new Constraints(0, maxWidth, 0, firstMaxHeight);
        var secondConstraints = new Constraints(0, maxWidth, 0, secondMaxHeight);

        var firstSize = First?.Measure(firstConstraints) ?? Size.Zero;
        var secondSize = Second?.Measure(secondConstraints) ?? Size.Zero;

        var width = Math.Max(firstSize.Width, secondSize.Width);
        var height = FirstSize + dividerHeight + secondSize.Height;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        if (Orientation == SplitterOrientation.Horizontal)
        {
            ArrangeHorizontal(bounds);
        }
        else
        {
            ArrangeVertical(bounds);
        }
    }

    private void ArrangeHorizontal(Rect bounds)
    {
        // First pane gets FirstSize width
        if (First != null)
        {
            First.Arrange(new Rect(bounds.X, bounds.Y, FirstSize, bounds.Height));
        }
        
        // Second pane gets remaining width (minus 3 for divider)
        if (Second != null)
        {
            var secondX = bounds.X + FirstSize + 3;
            var secondWidth = Math.Max(0, bounds.Width - FirstSize - 3);
            Second.Arrange(new Rect(secondX, bounds.Y, secondWidth, bounds.Height));
        }
    }

    private void ArrangeVertical(Rect bounds)
    {
        // First pane gets FirstSize height
        if (First != null)
        {
            First.Arrange(new Rect(bounds.X, bounds.Y, bounds.Width, FirstSize));
        }
        
        // Second pane gets remaining height (minus 1 for divider)
        if (Second != null)
        {
            var secondY = bounds.Y + FirstSize + 1;
            var secondHeight = Math.Max(0, bounds.Height - FirstSize - 1);
            Second.Arrange(new Rect(bounds.X, secondY, bounds.Width, secondHeight));
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (First != null)
        {
            foreach (var focusable in First.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
        
        // The splitter itself is focusable (for resizing with arrow keys)
        yield return this;
        
        if (Second != null)
        {
            foreach (var focusable in Second.GetFocusableNodes())
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
        
        // Render first pane
        if (First != null)
        {
            context.SetCursorPosition(First.Bounds.X, First.Bounds.Y);
            First.Render(context);
        }
        
        if (Orientation == SplitterOrientation.Horizontal)
        {
            RenderHorizontalDivider(context, dividerFg, dividerBg, theme);
        }
        else
        {
            RenderVerticalDivider(context, dividerFg, dividerBg, theme);
        }
        
        // Render second pane
        if (Second != null)
        {
            context.SetCursorPosition(Second.Bounds.X, Second.Bounds.Y);
            Second.Render(context);
        }
    }

    private void RenderHorizontalDivider(Hex1bRenderContext context, Hex1bColor dividerFg, Hex1bColor dividerBg, Hex1bTheme theme)
    {
        var dividerChar = theme.Get(SplitterTheme.DividerCharacter);
        var dividerX = Bounds.X + FirstSize + 1;
        
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
    }

    private void RenderVerticalDivider(Hex1bRenderContext context, Hex1bColor dividerFg, Hex1bColor dividerBg, Hex1bTheme theme)
    {
        var dividerChar = theme.Get(SplitterTheme.HorizontalDividerCharacter);
        var dividerY = Bounds.Y + FirstSize;
        
        context.SetCursorPosition(Bounds.X, dividerY);
        var dividerLine = new string(dividerChar[0], Bounds.Width);
        
        if (IsFocused)
        {
            context.Write($"{dividerFg.ToForegroundAnsi()}{dividerBg.ToBackgroundAnsi()}{dividerLine}\x1b[0m");
        }
        else
        {
            context.Write($"{dividerFg.ToForegroundAnsi()}{dividerLine}\x1b[0m");
        }
    }

    public override InputResult HandleInput(Hex1bKeyEvent keyEvent)
    {
        // Handle Escape to jump focus back to first focusable (e.g., master list)
        if (keyEvent.Key == Hex1bKey.Escape)
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
                return InputResult.Handled;
            }
        }
        
        // Handle Tab to move focus across all focusable nodes
        if (keyEvent.Key == Hex1bKey.Tab)
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

        // Handle splitter resize when the splitter itself is focused
        if (IsFocused)
        {
            return HandleResizeInput(keyEvent);
        }

        return InputResult.NotHandled;
    }

    private InputResult HandleResizeInput(Hex1bKeyEvent keyEvent)
    {
        if (Orientation == SplitterOrientation.Horizontal)
        {
            if (keyEvent.Key == Hex1bKey.LeftArrow)
            {
                // Decrease first pane width
                FirstSize = Math.Max(MinFirstSize, FirstSize - ResizeStep);
                return InputResult.Handled;
            }
            else if (keyEvent.Key == Hex1bKey.RightArrow)
            {
                // Increase first pane width (respect overall bounds)
                var maxFirstSize = Bounds.Width - 3 - MinFirstSize; // 3 for divider, MinFirstSize for second pane
                FirstSize = Math.Min(maxFirstSize, FirstSize + ResizeStep);
                return InputResult.Handled;
            }
        }
        else // Vertical
        {
            if (keyEvent.Key == Hex1bKey.UpArrow)
            {
                // Decrease first pane height
                FirstSize = Math.Max(MinFirstSize, FirstSize - ResizeStep);
                return InputResult.Handled;
            }
            else if (keyEvent.Key == Hex1bKey.DownArrow)
            {
                // Increase first pane height (respect overall bounds)
                var maxFirstSize = Bounds.Height - 1 - MinFirstSize; // 1 for divider, MinFirstSize for second pane
                FirstSize = Math.Min(maxFirstSize, FirstSize + ResizeStep);
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
        First?.SyncFocusIndex();
        Second?.SyncFocusIndex();
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (First != null) yield return First;
        if (Second != null) yield return Second;
    }
}
