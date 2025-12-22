using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that provides scrolling capability for content that exceeds the available space.
/// Only supports one direction at a time (vertical or horizontal).
/// Implements ILayoutProvider to clip content that exceeds the visible viewport.
/// </summary>
public sealed class ScrollNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// The child node to scroll.
    /// </summary>
    public Hex1bNode? Child { get; set; }
    
    /// <summary>
    /// The scroll state (offset, content size, viewport size).
    /// </summary>
    public ScrollState State { get; set; } = new();
    
    /// <summary>
    /// The scroll orientation (vertical or horizontal).
    /// </summary>
    public ScrollOrientation Orientation { get; set; } = ScrollOrientation.Vertical;
    
    /// <summary>
    /// Whether to show the scrollbar when content is scrollable.
    /// </summary>
    public bool ShowScrollbar { get; set; } = true;
    
    private bool _isFocused;
    public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

    private bool _isHovered;
    public override bool IsHovered { get => _isHovered; set => _isHovered = value; }

    public override bool IsFocusable => true;

    /// <inheritdoc />
    public override bool ManagesChildFocus => true;

    /// <summary>
    /// The width of the scrollbar (1 character).
    /// </summary>
    private const int ScrollbarSize = 1;

    /// <summary>
    /// The viewport rectangle for clipping.
    /// </summary>
    private Rect _viewportRect;

    /// <summary>
    /// The size of the child content (measured unbounded in scroll direction).
    /// </summary>
    private Size _contentSize;
    
    private int _focusedIndex = 0;
    private List<Hex1bNode>? _focusableNodes;

    #region ILayoutProvider Implementation

    /// <summary>
    /// The effective clipping rectangle (the viewport, not including scrollbar).
    /// </summary>
    public Rect ClipRect => _viewportRect;
    
    /// <summary>
    /// The clip mode for this layout region.
    /// </summary>
    public ClipMode ClipMode => ClipMode.Clip;

    /// <summary>
    /// Determines if a character at the given absolute position should be rendered.
    /// </summary>
    public bool ShouldRenderAt(int x, int y)
    {
        return x >= _viewportRect.X && 
               x < _viewportRect.X + _viewportRect.Width &&
               y >= _viewportRect.Y && 
               y < _viewportRect.Y + _viewportRect.Height;
    }

    /// <summary>
    /// Clips a string that starts at the given position, returning only the visible portion.
    /// </summary>
    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
    {
        // If entire line is outside vertical bounds, return empty
        if (y < _viewportRect.Y || y >= _viewportRect.Y + _viewportRect.Height)
            return (x, "");
            
        var clipLeft = _viewportRect.X;
        var clipRight = _viewportRect.X + _viewportRect.Width;

        // Entirely outside horizontal bounds
        if (x >= clipRight)
            return (x, "");

        // Use visible length for proper ANSI handling
        var visibleLength = Terminal.AnsiString.VisibleLength(text);
        if (visibleLength <= 0)
            return (x, "");

        var startColumn = Math.Max(0, clipLeft - x);
        var endColumnExclusive = Math.Min(visibleLength, clipRight - x);

        if (endColumnExclusive <= 0 || endColumnExclusive <= startColumn)
            return (x, "");

        var sliceLength = endColumnExclusive - startColumn;
        
        // Use SliceByDisplayWidth to properly handle wide characters
        var (slicedText, _, paddingBefore, paddingAfter) = 
            Terminal.DisplayWidth.SliceByDisplayWidthWithAnsi(text, startColumn, sliceLength);
        
        if (slicedText.Length == 0 && paddingBefore == 0)
            return (x, "");

        var clippedText = new string(' ', paddingBefore) + slicedText + new string(' ', paddingAfter);

        // Preserve trailing escape suffix if we clipped on the right
        if (endColumnExclusive < visibleLength)
        {
            var suffix = Terminal.AnsiString.TrailingEscapeSuffix(text);
            if (!string.IsNullOrEmpty(suffix) && !clippedText.EndsWith(suffix, StringComparison.Ordinal))
                clippedText += suffix;
        }

        var adjustedX = x + startColumn;
        return (adjustedX, clippedText);
    }

    #endregion

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Navigation based on orientation
        bindings.Key(Hex1bKey.UpArrow).Action(ScrollUp, "Scroll up");
        bindings.Key(Hex1bKey.DownArrow).Action(ScrollDown, "Scroll down");
        bindings.Key(Hex1bKey.LeftArrow).Action(ScrollLeft, "Scroll left");
        bindings.Key(Hex1bKey.RightArrow).Action(ScrollRight, "Scroll right");
        bindings.Key(Hex1bKey.PageUp).Action(PageUp, "Page up");
        bindings.Key(Hex1bKey.PageDown).Action(PageDown, "Page down");
        bindings.Key(Hex1bKey.Home).Action(ScrollToStart, "Scroll to start");
        bindings.Key(Hex1bKey.End).Action(ScrollToEnd, "Scroll to end");
        
        // Focus navigation - delegated to app-level FocusRing via InputBindingActionContext
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
        bindings.Key(Hex1bKey.Escape).Action(FocusFirst, "Jump to first focusable");
        
        // Mouse wheel scrolling
        bindings.Mouse(MouseButton.ScrollUp).Action(() => { State.ScrollUp(3); MarkDirty(); }, "Scroll up");
        bindings.Mouse(MouseButton.ScrollDown).Action(() => { State.ScrollDown(3); MarkDirty(); }, "Scroll down");
        
        // Mouse drag on scrollbar (handles both clicks and thumb dragging)
        bindings.Drag(MouseButton.Left).Action(HandleScrollbarDrag, "Drag scrollbar");
    }

    private DragHandler HandleScrollbarDrag(int localX, int localY)
    {
        if (!State.IsScrollable || !ShowScrollbar)
        {
            return new DragHandler(); // No-op
        }
        
        if (Orientation == ScrollOrientation.Vertical)
        {
            return HandleVerticalScrollbarDrag(localX, localY);
        }
        else
        {
            return HandleHorizontalScrollbarDrag(localX, localY);
        }
    }

    private DragHandler HandleVerticalScrollbarDrag(int localX, int localY)
    {
        // Check if click is on the scrollbar (rightmost column)
        var scrollbarX = Bounds.Width - ScrollbarSize;
        if (localX < scrollbarX || localX >= Bounds.Width)
        {
            return new DragHandler(); // Click not on scrollbar
        }
        
        var scrollbarHeight = _viewportRect.Height;
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)State.ViewportSize / State.ContentSize * (scrollbarHeight - 2)));
        var scrollRange = scrollbarHeight - 2 - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)State.Offset / State.MaxOffset * scrollRange) 
            : 0;
        
        // Check which part of the scrollbar was clicked
        if (localY == 0)
        {
            // Up arrow clicked
            State.ScrollUp();
            MarkDirty();
            return new DragHandler(); // No drag
        }
        else if (localY == scrollbarHeight - 1)
        {
            // Down arrow clicked
            State.ScrollDown();
            MarkDirty();
            return new DragHandler(); // No drag
        }
        else if (localY > 0 && localY < scrollbarHeight - 1)
        {
            var trackY = localY - 1; // Offset by 1 for the up arrow
            
            if (trackY >= thumbPosition && trackY < thumbPosition + thumbSize)
            {
                // Clicked on thumb - start drag
                var startOffset = State.Offset;
                var trackHeight = scrollbarHeight - 2; // Exclude arrows
                var contentPerPixel = State.MaxOffset > 0 && trackHeight > thumbSize
                    ? (double)State.MaxOffset / (trackHeight - thumbSize)
                    : 0;
                
                return new DragHandler(
                    onMove: (deltaX, deltaY) =>
                    {
                        if (contentPerPixel > 0)
                        {
                            var newOffset = (int)Math.Round(startOffset + deltaY * contentPerPixel);
                            State.Offset = Math.Clamp(newOffset, 0, State.MaxOffset);
                            MarkDirty();
                        }
                    }
                );
            }
            else if (trackY < thumbPosition)
            {
                // Clicked above thumb - page up
                State.PageUp();
                MarkDirty();
                return new DragHandler(); // No drag
            }
            else
            {
                // Clicked below thumb - page down
                State.PageDown();
                MarkDirty();
                return new DragHandler(); // No drag
            }
        }
        
        return new DragHandler(); // No-op
    }

    private DragHandler HandleHorizontalScrollbarDrag(int localX, int localY)
    {
        // Check if click is on the scrollbar (bottom row)
        var scrollbarY = Bounds.Height - ScrollbarSize;
        if (localY < scrollbarY || localY >= Bounds.Height)
        {
            return new DragHandler(); // Click not on scrollbar
        }
        
        var scrollbarWidth = _viewportRect.Width;
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)State.ViewportSize / State.ContentSize * (scrollbarWidth - 2)));
        var scrollRange = scrollbarWidth - 2 - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)State.Offset / State.MaxOffset * scrollRange) 
            : 0;
        
        // Check which part of the scrollbar was clicked
        if (localX == 0)
        {
            // Left arrow clicked
            State.ScrollUp(); // ScrollUp decreases offset
            MarkDirty();
            return new DragHandler(); // No drag
        }
        else if (localX == scrollbarWidth - 1)
        {
            // Right arrow clicked
            State.ScrollDown(); // ScrollDown increases offset
            MarkDirty();
            return new DragHandler(); // No drag
        }
        else if (localX > 0 && localX < scrollbarWidth - 1)
        {
            var trackX = localX - 1; // Offset by 1 for the left arrow
            
            if (trackX >= thumbPosition && trackX < thumbPosition + thumbSize)
            {
                // Clicked on thumb - start drag
                var startOffset = State.Offset;
                var trackWidth = scrollbarWidth - 2; // Exclude arrows
                var contentPerPixel = State.MaxOffset > 0 && trackWidth > thumbSize
                    ? (double)State.MaxOffset / (trackWidth - thumbSize)
                    : 0;
                
                return new DragHandler(
                    onMove: (deltaX, deltaY) =>
                    {
                        if (contentPerPixel > 0)
                        {
                            var newOffset = (int)Math.Round(startOffset + deltaX * contentPerPixel);
                            State.Offset = Math.Clamp(newOffset, 0, State.MaxOffset);
                            MarkDirty();
                        }
                    }
                );
            }
            else if (trackX < thumbPosition)
            {
                // Clicked left of thumb - page left
                State.PageUp();
                MarkDirty();
                return new DragHandler(); // No drag
            }
            else
            {
                // Clicked right of thumb - page right
                State.PageDown();
                MarkDirty();
                return new DragHandler(); // No drag
            }
        }
        
        return new DragHandler(); // No-op
    }

    private void ScrollUp()
    {
        if (!IsFocused || Orientation != ScrollOrientation.Vertical) return;
        State.ScrollUp();
        MarkDirty();
    }

    private void ScrollDown()
    {
        if (!IsFocused || Orientation != ScrollOrientation.Vertical) return;
        State.ScrollDown();
        MarkDirty();
    }

    private void ScrollLeft()
    {
        if (!IsFocused || Orientation != ScrollOrientation.Horizontal) return;
        State.ScrollUp(); // Uses ScrollUp because it decreases offset
        MarkDirty();
    }

    private void ScrollRight()
    {
        if (!IsFocused || Orientation != ScrollOrientation.Horizontal) return;
        State.ScrollDown(); // Uses ScrollDown because it increases offset
        MarkDirty();
    }

    private void PageUp()
    {
        if (!IsFocused) return;
        State.PageUp();
        MarkDirty();
    }

    private void PageDown()
    {
        if (!IsFocused) return;
        State.PageDown();
        MarkDirty();
    }

    private void ScrollToStart()
    {
        if (!IsFocused) return;
        State.ScrollToStart();
        MarkDirty();
    }

    private void ScrollToEnd()
    {
        if (!IsFocused) return;
        State.ScrollToEnd();
        MarkDirty();
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

    public override Size Measure(Constraints constraints)
    {
        if (Child == null)
        {
            return constraints.Constrain(Size.Zero);
        }

        // Calculate available viewport size (accounting for scrollbar if shown)
        var scrollbarWidth = ShowScrollbar && Orientation == ScrollOrientation.Vertical ? ScrollbarSize : 0;
        var scrollbarHeight = ShowScrollbar && Orientation == ScrollOrientation.Horizontal ? ScrollbarSize : 0;
        
        // First, measure child with unbounded constraint in scroll direction to get content size
        var childConstraints = Orientation == ScrollOrientation.Vertical
            ? new Constraints(0, Math.Max(0, constraints.MaxWidth - scrollbarWidth), 0, int.MaxValue)
            : new Constraints(0, int.MaxValue, 0, Math.Max(0, constraints.MaxHeight - scrollbarHeight));
        
        _contentSize = Child.Measure(childConstraints);
        
        // Update state with content size
        if (Orientation == ScrollOrientation.Vertical)
        {
            State.ContentSize = _contentSize.Height;
        }
        else
        {
            State.ContentSize = _contentSize.Width;
        }
        
        // The scroll widget takes whatever space is given to it
        // Width = content width + scrollbar (for vertical scroll)
        // Height = content height + scrollbar (for horizontal scroll)
        if (Orientation == ScrollOrientation.Vertical)
        {
            var width = Math.Min(_contentSize.Width + scrollbarWidth, constraints.MaxWidth);
            var height = constraints.MaxHeight < int.MaxValue ? constraints.MaxHeight : _contentSize.Height;
            return constraints.Constrain(new Size(width, height));
        }
        else
        {
            var width = constraints.MaxWidth < int.MaxValue ? constraints.MaxWidth : _contentSize.Width;
            var height = Math.Min(_contentSize.Height + scrollbarHeight, constraints.MaxHeight);
            return constraints.Constrain(new Size(width, height));
        }
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        if (Child == null) return;

        // Determine if scrollbar is needed and visible
        var needsScrollbar = State.IsScrollable && ShowScrollbar;
        var scrollbarWidth = needsScrollbar && Orientation == ScrollOrientation.Vertical ? ScrollbarSize : 0;
        var scrollbarHeight = needsScrollbar && Orientation == ScrollOrientation.Horizontal ? ScrollbarSize : 0;

        // Calculate viewport size
        var viewportWidth = bounds.Width - scrollbarWidth;
        var viewportHeight = bounds.Height - scrollbarHeight;
        
        // Update state with viewport size
        if (Orientation == ScrollOrientation.Vertical)
        {
            State.ViewportSize = viewportHeight;
        }
        else
        {
            State.ViewportSize = viewportWidth;
        }
        
        // Clamp offset to valid range
        if (State.Offset > State.MaxOffset)
        {
            State.Offset = State.MaxOffset;
        }
        if (State.Offset < 0)
        {
            State.Offset = 0;
        }
        
        // Set up viewport rect for clipping
        _viewportRect = new Rect(bounds.X, bounds.Y, viewportWidth, viewportHeight);
        
        // Arrange child with offset applied
        // The child is positioned such that the visible portion starts at the viewport position
        if (Orientation == ScrollOrientation.Vertical)
        {
            // Child is positioned above the viewport by the scroll offset
            var childY = bounds.Y - State.Offset;
            Child.Arrange(new Rect(bounds.X, childY, viewportWidth, _contentSize.Height));
        }
        else
        {
            // Child is positioned to the left of the viewport by the scroll offset
            var childX = bounds.X - State.Offset;
            Child.Arrange(new Rect(childX, bounds.Y, _contentSize.Width, viewportHeight));
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        // Store ourselves as the current layout provider to enable clipping
        var previousLayout = context.CurrentLayoutProvider;
        context.CurrentLayoutProvider = this;
        
        // Render child (will be clipped by ILayoutProvider)
        Child?.Render(context);
        
        context.CurrentLayoutProvider = previousLayout;
        
        // Render scrollbar if needed
        if (State.IsScrollable && ShowScrollbar)
        {
            RenderScrollbar(context, theme);
        }
    }

    private void RenderScrollbar(Hex1bRenderContext context, Hex1bTheme theme)
    {
        var trackColor = theme.Get(ScrollTheme.TrackColor);
        var thumbColor = IsFocused 
            ? theme.Get(ScrollTheme.FocusedThumbColor) 
            : theme.Get(ScrollTheme.ThumbColor);

        if (Orientation == ScrollOrientation.Vertical)
        {
            RenderVerticalScrollbar(context, theme, trackColor, thumbColor);
        }
        else
        {
            RenderHorizontalScrollbar(context, theme, trackColor, thumbColor);
        }
    }

    private void RenderVerticalScrollbar(Hex1bRenderContext context, Hex1bTheme theme, Hex1bColor trackColor, Hex1bColor thumbColor)
    {
        var trackChar = theme.Get(ScrollTheme.VerticalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.VerticalThumbCharacter);
        var upArrow = theme.Get(ScrollTheme.UpArrowCharacter);
        var downArrow = theme.Get(ScrollTheme.DownArrowCharacter);
        
        var scrollbarX = Bounds.X + Bounds.Width - ScrollbarSize;
        var scrollbarHeight = _viewportRect.Height;
        
        // Calculate thumb position and size
        // Use a minimum thumb size of 1
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)State.ViewportSize / State.ContentSize * (scrollbarHeight - 2)));
        var scrollRange = scrollbarHeight - 2 - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)State.Offset / State.MaxOffset * scrollRange) 
            : 0;
        
        // Render scrollbar
        for (int row = 0; row < scrollbarHeight; row++)
        {
            context.SetCursorPosition(scrollbarX, Bounds.Y + row);
            
            string charToRender;
            Hex1bColor color;
            
            if (row == 0)
            {
                // Up arrow
                charToRender = upArrow;
                color = thumbColor;
            }
            else if (row == scrollbarHeight - 1)
            {
                // Down arrow
                charToRender = downArrow;
                color = thumbColor;
            }
            else if (row - 1 >= thumbPosition && row - 1 < thumbPosition + thumbSize)
            {
                // Thumb
                charToRender = thumbChar;
                color = thumbColor;
            }
            else
            {
                // Track
                charToRender = trackChar;
                color = trackColor;
            }
            
            context.Write($"{color.ToForegroundAnsi()}{charToRender}\x1b[0m");
        }
    }

    private void RenderHorizontalScrollbar(Hex1bRenderContext context, Hex1bTheme theme, Hex1bColor trackColor, Hex1bColor thumbColor)
    {
        var trackChar = theme.Get(ScrollTheme.HorizontalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.HorizontalThumbCharacter);
        var leftArrow = theme.Get(ScrollTheme.LeftArrowCharacter);
        var rightArrow = theme.Get(ScrollTheme.RightArrowCharacter);
        
        var scrollbarY = Bounds.Y + Bounds.Height - ScrollbarSize;
        var scrollbarWidth = _viewportRect.Width;
        
        // Calculate thumb position and size
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)State.ViewportSize / State.ContentSize * (scrollbarWidth - 2)));
        var scrollRange = scrollbarWidth - 2 - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)State.Offset / State.MaxOffset * scrollRange) 
            : 0;
        
        context.SetCursorPosition(Bounds.X, scrollbarY);
        
        // Render scrollbar
        for (int col = 0; col < scrollbarWidth; col++)
        {
            string charToRender;
            Hex1bColor color;
            
            if (col == 0)
            {
                // Left arrow
                charToRender = leftArrow;
                color = thumbColor;
            }
            else if (col == scrollbarWidth - 1)
            {
                // Right arrow
                charToRender = rightArrow;
                color = thumbColor;
            }
            else if (col - 1 >= thumbPosition && col - 1 < thumbPosition + thumbSize)
            {
                // Thumb
                charToRender = thumbChar;
                color = thumbColor;
            }
            else
            {
                // Track
                charToRender = trackChar;
                color = trackColor;
            }
            
            context.Write($"{color.ToForegroundAnsi()}{charToRender}\x1b[0m");
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // The scroll widget itself is focusable
        yield return this;
        
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
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

    /// <summary>
    /// Syncs _focusedIndex to match which child node has IsFocused = true.
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
        Child?.SyncFocusIndex();
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }
}
