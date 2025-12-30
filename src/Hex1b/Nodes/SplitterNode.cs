using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b;

public sealed class SplitterNode : Hex1bNode, IChildLayoutProvider
{
    /// <summary>
    /// The first child (left for horizontal, top for vertical).
    /// </summary>
    public Hex1bNode? First { get; set; }
    
    /// <summary>
    /// The second child (right for horizontal, bottom for vertical).
    /// </summary>
    public Hex1bNode? Second { get; set; }
    
    private int _firstSize = 30;
    
    /// <summary>
    /// The size of the first pane in characters (width for horizontal, height for vertical).
    /// </summary>
    public int FirstSize 
    { 
        get => _firstSize;
        set
        {
            if (_firstSize != value)
            {
                _firstSize = value;
                MarkDirty();
                // Also mark children dirty since their bounds will change
                First?.MarkDirty();
                Second?.MarkDirty();
            }
        }
    }
    
    private SplitterOrientation _orientation = SplitterOrientation.Horizontal;
    /// <summary>
    /// The orientation of the splitter.
    /// </summary>
    public SplitterOrientation Orientation 
    { 
        get => _orientation; 
        set 
        {
            if (_orientation != value)
            {
                _orientation = value;
                MarkDirty();
            }
        }
    }
    
    // Legacy property aliases for backward compatibility
    public Hex1bNode? Left { get => First; set => First = value; }
    public Hex1bNode? Right { get => Second; set => Second = value; }
    public int LeftWidth { get => FirstSize; set => FirstSize = value; }
    
    private bool _isFocused;
    public override bool IsFocused 
    { 
        get => _isFocused; 
        set 
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    public override bool IsHovered 
    { 
        get => _isHovered; 
        set 
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }
    
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
    /// Returns the bounds of just the divider for mouse hit testing.
    /// This prevents clicks on child panes from being captured by the splitter.
    /// </summary>
    public override Rect HitTestBounds
    {
        get
        {
            if (Orientation == SplitterOrientation.Horizontal)
            {
                // Divider is a vertical strip at position FirstSize
                return new Rect(Bounds.X + FirstSize, Bounds.Y, DividerSize, Bounds.Height);
            }
            else
            {
                // Divider is a horizontal strip at position FirstSize
                return new Rect(Bounds.X, Bounds.Y + FirstSize, Bounds.Width, DividerSize);
            }
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Resize bindings
        bindings.Key(Hex1bKey.LeftArrow).Action(ResizeLeft, "Resize left");
        bindings.Key(Hex1bKey.RightArrow).Action(ResizeRight, "Resize right");
        bindings.Key(Hex1bKey.UpArrow).Action(ResizeUp, "Resize up");
        bindings.Key(Hex1bKey.DownArrow).Action(ResizeDown, "Resize down");
        
        // Focus navigation - delegated to app-level FocusRing via InputBindingActionContext
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
        bindings.Key(Hex1bKey.Escape).Action(FocusFirst, "Jump to first focusable");
        
        // Mouse drag to resize
        bindings.Drag(MouseButton.Left).Action((startX, startY) =>
        {
            var startSize = FirstSize;
            return new DragHandler(
                onMove: (deltaX, deltaY) =>
                {
                    var delta = Orientation == SplitterOrientation.Horizontal ? deltaX : deltaY;
                    var maxSize = Orientation == SplitterOrientation.Horizontal 
                        ? Bounds.Width - DividerSize - MinFirstSize 
                        : Bounds.Height - DividerSize - MinFirstSize;
                    FirstSize = Math.Clamp(startSize + delta, MinFirstSize, maxSize);
                }
            );
        }, "Drag to resize");
    }

    private void FocusFirst()
    {
        var focusables = GetFocusableNodesList();
        if (focusables.Count == 0 || _focusedIndex == 0) return;

        // Clear old focus
        if (_focusedIndex >= 0 && _focusedIndex < focusables.Count)
        {
            focusables[_focusedIndex].IsFocused = false;
        }

        // Jump to first focusable
        _focusedIndex = 0;
        focusables[_focusedIndex].IsFocused = true;
    }

    private void ResizeLeft()
    {
        if (!IsFocused || Orientation != SplitterOrientation.Horizontal) return;
        FirstSize = Math.Max(MinFirstSize, FirstSize - ResizeStep);
    }

    private void ResizeRight()
    {
        if (!IsFocused || Orientation != SplitterOrientation.Horizontal) return;
        var maxFirstSize = Bounds.Width - 3 - MinFirstSize;
        FirstSize = Math.Min(maxFirstSize, FirstSize + ResizeStep);
    }

    private void ResizeUp()
    {
        if (!IsFocused || Orientation != SplitterOrientation.Vertical) return;
        FirstSize = Math.Max(MinFirstSize, FirstSize - ResizeStep);
    }

    private void ResizeDown()
    {
        if (!IsFocused || Orientation != SplitterOrientation.Vertical) return;
        var maxFirstSize = Bounds.Height - 1 - MinFirstSize;
        FirstSize = Math.Min(maxFirstSize, FirstSize + ResizeStep);
    }

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
        
        // Render first pane with clipping
        if (First != null)
        {
            var previousLayout = context.CurrentLayoutProvider;
            var firstPaneProvider = new RectLayoutProvider(First.Bounds);
            firstPaneProvider.ParentLayoutProvider = previousLayout;
            context.CurrentLayoutProvider = firstPaneProvider;
            
            context.SetCursorPosition(First.Bounds.X, First.Bounds.Y);
            First.Render(context);
            
            context.CurrentLayoutProvider = previousLayout;
        }
        
        if (Orientation == SplitterOrientation.Horizontal)
        {
            RenderHorizontalDivider(context, dividerFg, dividerBg, theme);
        }
        else
        {
            RenderVerticalDivider(context, dividerFg, dividerBg, theme);
        }
        
        // Render second pane with clipping
        if (Second != null)
        {
            var previousLayout = context.CurrentLayoutProvider;
            var secondPaneProvider = new RectLayoutProvider(Second.Bounds);
            secondPaneProvider.ParentLayoutProvider = previousLayout;
            context.CurrentLayoutProvider = secondPaneProvider;
            
            context.SetCursorPosition(Second.Bounds.X, Second.Bounds.Y);
            Second.Render(context);
            
            context.CurrentLayoutProvider = previousLayout;
        }
    }

    private void RenderHorizontalDivider(Hex1bRenderContext context, Hex1bColor dividerFg, Hex1bColor dividerBg, Hex1bTheme theme)
    {
        var dividerChar = theme.Get(SplitterTheme.DividerCharacter);
        var leftArrow = theme.Get(SplitterTheme.LeftArrowCharacter);
        var rightArrow = theme.Get(SplitterTheme.RightArrowCharacter);
        var leftArrowColor = theme.Get(SplitterTheme.LeftArrowColor);
        var rightArrowColor = theme.Get(SplitterTheme.RightArrowColor);
        var dividerX = Bounds.X + FirstSize + 1;
        
        // Calculate midpoint for arrow indicators (show arrows on 2 rows centered vertically)
        var midRow = Bounds.Height / 2;
        var topArrowRow = midRow - 1;
        var bottomArrowRow = midRow;
        
        for (int row = 0; row < Bounds.Height; row++)
        {
            context.SetCursorPosition(dividerX, Bounds.Y + row);
            
            // Determine which character and color to use for this row
            string charToRender;
            Hex1bColor fgColor;
            if (Bounds.Height >= 3 && row == topArrowRow)
            {
                charToRender = leftArrow;
                fgColor = leftArrowColor.IsDefault ? dividerFg : leftArrowColor;
            }
            else if (Bounds.Height >= 3 && row == bottomArrowRow)
            {
                charToRender = rightArrow;
                fgColor = rightArrowColor.IsDefault ? dividerFg : rightArrowColor;
            }
            else
            {
                charToRender = dividerChar;
                fgColor = dividerFg;
            }
            
            if (IsFocused)
            {
                context.Write($"{fgColor.ToForegroundAnsi()}{dividerBg.ToBackgroundAnsi()}{charToRender}\x1b[0m");
            }
            else
            {
                context.Write($"{fgColor.ToForegroundAnsi()}{charToRender}\x1b[0m");
            }
        }
    }

    private void RenderVerticalDivider(Hex1bRenderContext context, Hex1bColor dividerFg, Hex1bColor dividerBg, Hex1bTheme theme)
    {
        var dividerChar = theme.Get(SplitterTheme.HorizontalDividerCharacter);
        var upArrow = theme.Get(SplitterTheme.UpArrowCharacter);
        var downArrow = theme.Get(SplitterTheme.DownArrowCharacter);
        var upArrowColor = theme.Get(SplitterTheme.UpArrowColor);
        var downArrowColor = theme.Get(SplitterTheme.DownArrowColor);
        var dividerY = Bounds.Y + FirstSize;
        
        // Calculate midpoint for arrow indicators
        var midCol = Bounds.Width / 2;
        var leftArrowCol = midCol - 1;
        var rightArrowCol = midCol;
        
        context.SetCursorPosition(Bounds.X, dividerY);
        
        // Render character by character to insert arrows at midpoint
        for (int col = 0; col < Bounds.Width; col++)
        {
            string charToRender;
            Hex1bColor fgColor;
            if (Bounds.Width >= 4 && col == leftArrowCol)
            {
                charToRender = upArrow;
                fgColor = upArrowColor.IsDefault ? dividerFg : upArrowColor;
            }
            else if (Bounds.Width >= 4 && col == rightArrowCol)
            {
                charToRender = downArrow;
                fgColor = downArrowColor.IsDefault ? dividerFg : downArrowColor;
            }
            else
            {
                charToRender = dividerChar;
                fgColor = dividerFg;
            }
            
            if (IsFocused)
            {
                context.Write($"{fgColor.ToForegroundAnsi()}{dividerBg.ToBackgroundAnsi()}{charToRender}\x1b[0m");
            }
            else
            {
                context.Write($"{fgColor.ToForegroundAnsi()}{charToRender}\x1b[0m");
            }
        }
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

    /// <inheritdoc />
    public ILayoutProvider? GetChildLayoutProvider(Hex1bNode child)
    {
        // Each pane gets its own clipping provider based on its bounds
        if (ReferenceEquals(child, First) && First != null)
        {
            return new RectLayoutProvider(First.Bounds);
        }
        
        if (ReferenceEquals(child, Second) && Second != null)
        {
            return new RectLayoutProvider(Second.Bounds);
        }
        
        return null;
    }
}
