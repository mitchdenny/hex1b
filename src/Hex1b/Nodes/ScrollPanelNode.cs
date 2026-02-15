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
public sealed class ScrollPanelNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// The child node to scroll.
    /// </summary>
    private Hex1bNode? _child;
    public Hex1bNode? Child 
    { 
        get => _child;
        set
        {
            if (_child != value)
            {
                _child = value;
                MarkDirty();
            }
        }
    }
    
    /// <summary>
    /// The source widget that created this node.
    /// Used to create event arguments for scroll events.
    /// </summary>
    public ScrollPanelWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// The current scroll offset (in characters).
    /// For vertical scrolling, this is the row offset.
    /// For horizontal scrolling, this is the column offset.
    /// </summary>
    public int Offset { get; private set; }
    
    /// <summary>
    /// The size of the content being scrolled (in characters).
    /// This is set during layout.
    /// </summary>
    public int ContentSize { get; private set; }
    
    /// <summary>
    /// The size of the visible viewport (in characters).
    /// This is set during layout.
    /// </summary>
    public int ViewportSize { get; private set; }
    
    /// <summary>
    /// Whether the scrollbar is currently needed (content exceeds viewport).
    /// </summary>
    public bool IsScrollable => ContentSize > ViewportSize;
    
    /// <summary>
    /// The maximum scroll offset.
    /// </summary>
    public int MaxOffset => Math.Max(0, ContentSize - ViewportSize);
    
    /// <summary>
    /// The scroll action to invoke when scrolling occurs.
    /// Set during reconciliation from the ScrollPanelWidget's ScrollHandler.
    /// </summary>
    internal Func<InputBindingActionContext, int, int, int, int, Task>? ScrollAction { get; set; }
    
    /// <summary>
    /// Context for firing scroll events when not triggered by user input.
    /// </summary>
    private InputBindingActionContext? _pendingEventContext;
    
    private ScrollOrientation _orientation = ScrollOrientation.Vertical;
    /// <summary>
    /// The scroll orientation (vertical or horizontal).
    /// </summary>
    public ScrollOrientation Orientation 
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
    
    private bool _showScrollbar = true;
    /// <summary>
    /// Whether to show the scrollbar when content is scrollable.
    /// </summary>
    public bool ShowScrollbar 
    { 
        get => _showScrollbar; 
        set 
        {
            if (_showScrollbar != value)
            {
                _showScrollbar = value;
                MarkDirty();
            }
        }
    }
    
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
    
    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    /// <summary>
    /// Determines if a character at the given absolute position should be rendered.
    /// </summary>
    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    /// <summary>
    /// Clips a string that starts at the given position, returning only the visible portion.
    /// </summary>
    public (int adjustedX, string clippedText) ClipString(int x, int y, string text) 
        => LayoutProviderHelper.ClipString(this, x, y, text);

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
        bindings.Mouse(MouseButton.ScrollUp).Action(ctx => ScrollByAmount(-3, ctx), "Scroll up");
        bindings.Mouse(MouseButton.ScrollDown).Action(ctx => ScrollByAmount(3, ctx), "Scroll down");
        
        // Mouse drag on scrollbar (handles both clicks and thumb dragging)
        bindings.Drag(MouseButton.Left).Action(HandleScrollbarDrag, "Drag scrollbar");
    }

    private DragHandler HandleScrollbarDrag(int localX, int localY)
    {
        if (!IsScrollable || !ShowScrollbar)
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
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)ViewportSize / ContentSize * scrollbarHeight));
        var scrollRange = scrollbarHeight - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)Offset / MaxOffset * scrollRange) 
            : 0;
        
        // Check which part of the scrollbar was clicked (no arrows)
        if (localY >= 0 && localY < scrollbarHeight)
        {
            if (localY >= thumbPosition && localY < thumbPosition + thumbSize)
            {
                // Clicked on thumb - start drag
                var startOffset = Offset;
                var trackHeight = scrollbarHeight;
                var contentPerPixel = MaxOffset > 0 && trackHeight > thumbSize
                    ? (double)MaxOffset / (trackHeight - thumbSize)
                    : 0;
                
                return DragHandler.Simple(
                    onMove: (deltaX, deltaY) =>
                    {
                        if (contentPerPixel > 0)
                        {
                            var newOffset = (int)Math.Round(startOffset + deltaY * contentPerPixel);
                            SetOffset(Math.Clamp(newOffset, 0, MaxOffset));
                        }
                    }
                );
            }
            else if (localY < thumbPosition)
            {
                // Clicked above thumb - page up
                ScrollByPage(-1);
                return new DragHandler(); // No drag
            }
            else
            {
                // Clicked below thumb - page down
                ScrollByPage(1);
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
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)ViewportSize / ContentSize * scrollbarWidth));
        var scrollRange = scrollbarWidth - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)Offset / MaxOffset * scrollRange) 
            : 0;
        
        // Check which part of the scrollbar was clicked (no arrows)
        if (localX >= 0 && localX < scrollbarWidth)
        {
            if (localX >= thumbPosition && localX < thumbPosition + thumbSize)
            {
                // Clicked on thumb - start drag
                var startOffset = Offset;
                var trackWidth = scrollbarWidth;
                var contentPerPixel = MaxOffset > 0 && trackWidth > thumbSize
                    ? (double)MaxOffset / (trackWidth - thumbSize)
                    : 0;
                
                return DragHandler.Simple(
                    onMove: (deltaX, deltaY) =>
                    {
                        if (contentPerPixel > 0)
                        {
                            var newOffset = (int)Math.Round(startOffset + deltaX * contentPerPixel);
                            SetOffset(Math.Clamp(newOffset, 0, MaxOffset));
                        }
                    }
                );
            }
            else if (localX < thumbPosition)
            {
                // Clicked left of thumb - page left
                ScrollByPage(-1);
                return new DragHandler(); // No drag
            }
            else
            {
                // Clicked right of thumb - page right
                ScrollByPage(1);
                return new DragHandler(); // No drag
            }
        }
        
        return new DragHandler(); // No-op
    }

    #region Internal Scroll Methods
    
    /// <summary>
    /// Sets the offset directly and fires the scroll event if changed.
    /// </summary>
    internal void SetOffset(int newOffset, InputBindingActionContext? context = null)
    {
        var clampedOffset = Math.Clamp(newOffset, 0, MaxOffset);
        if (clampedOffset == Offset) return;
        
        var previousOffset = Offset;
        Offset = clampedOffset;
        MarkDirty();
        
        // Fire scroll event
        if (ScrollAction != null && context != null)
        {
            // Fire async event - we don't await it here as it's fire-and-forget in input handling
            _ = ScrollAction(context, Offset, previousOffset, ContentSize, ViewportSize);
        }
        else if (ScrollAction != null)
        {
            // Store context for deferred event firing
            _pendingEventContext = context;
        }
    }
    
    /// <summary>
    /// Scrolls by the specified amount (positive = down/right, negative = up/left).
    /// </summary>
    private void ScrollByAmount(int amount, InputBindingActionContext? context = null)
    {
        SetOffset(Offset + amount, context);
    }

    /// <summary>
    /// Adjusts Offset so the focused descendant's bounds are within the viewport.
    /// Must be called after Arrange so descendant Bounds are set.
    /// Returns true if offset changed (caller should re-arrange).
    /// </summary>
    private bool EnsureFocusedVisible(int viewportStart, int viewportSize)
    {
        // Find the focused descendant
        Hex1bNode? focused = null;
        foreach (var node in GetFocusableNodes())
        {
            if (node.IsFocused)
            {
                focused = node;
                break;
            }
        }
        if (focused is null) return false;

        int nodeStart, nodeSize;
        if (Orientation == ScrollOrientation.Vertical)
        {
            nodeStart = focused.Bounds.Y;
            nodeSize = focused.Bounds.Height;
        }
        else
        {
            nodeStart = focused.Bounds.X;
            nodeSize = focused.Bounds.Width;
        }

        var viewportEnd = viewportStart + viewportSize;
        var nodeEnd = nodeStart + nodeSize;

        int newOffset = Offset;
        if (nodeStart < viewportStart)
        {
            // Node is above/left of viewport — scroll up/left
            newOffset = Offset - (viewportStart - nodeStart);
        }
        else if (nodeEnd > viewportEnd)
        {
            // Node is below/right of viewport — scroll down/right
            newOffset = Offset + (nodeEnd - viewportEnd);
        }

        newOffset = Math.Clamp(newOffset, 0, MaxOffset);
        if (newOffset == Offset) return false;

        Offset = newOffset;
        MarkDirty();
        return true;
    }
    
    /// <summary>
    /// Scrolls by a full page (viewport size minus 1).
    /// </summary>
    private void ScrollByPage(int direction, InputBindingActionContext? context = null)
    {
        var pageSize = Math.Max(1, ViewportSize - 1);
        ScrollByAmount(direction * pageSize, context);
    }
    
    #endregion

    private void ScrollUp(InputBindingActionContext ctx)
    {
        if (!IsFocused || Orientation != ScrollOrientation.Vertical) return;
        ScrollByAmount(-1, ctx);
    }

    private void ScrollDown(InputBindingActionContext ctx)
    {
        if (!IsFocused || Orientation != ScrollOrientation.Vertical) return;
        ScrollByAmount(1, ctx);
    }

    private void ScrollLeft(InputBindingActionContext ctx)
    {
        if (!IsFocused || Orientation != ScrollOrientation.Horizontal) return;
        ScrollByAmount(-1, ctx);
    }

    private void ScrollRight(InputBindingActionContext ctx)
    {
        if (!IsFocused || Orientation != ScrollOrientation.Horizontal) return;
        ScrollByAmount(1, ctx);
    }

    private void PageUp(InputBindingActionContext ctx)
    {
        if (!IsFocused) return;
        ScrollByPage(-1, ctx);
    }

    private void PageDown(InputBindingActionContext ctx)
    {
        if (!IsFocused) return;
        ScrollByPage(1, ctx);
    }

    private void ScrollToStart(InputBindingActionContext ctx)
    {
        if (!IsFocused) return;
        SetOffset(0, ctx);
    }

    private void ScrollToEnd(InputBindingActionContext ctx)
    {
        if (!IsFocused) return;
        SetOffset(MaxOffset, ctx);
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

    protected override Size MeasureCore(Constraints constraints)
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
        
        // Update content size
        if (Orientation == ScrollOrientation.Vertical)
        {
            ContentSize = _contentSize.Height;
        }
        else
        {
            ContentSize = _contentSize.Width;
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

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        
        if (Child == null) return;

        // Determine if scrollbar is needed and visible
        var needsScrollbar = IsScrollable && ShowScrollbar;
        var scrollbarWidth = needsScrollbar && Orientation == ScrollOrientation.Vertical ? ScrollbarSize : 0;
        var scrollbarHeight = needsScrollbar && Orientation == ScrollOrientation.Horizontal ? ScrollbarSize : 0;

        // Calculate viewport size
        var viewportWidth = bounds.Width - scrollbarWidth;
        var viewportHeight = bounds.Height - scrollbarHeight;
        
        // Update viewport size
        if (Orientation == ScrollOrientation.Vertical)
        {
            ViewportSize = viewportHeight;
        }
        else
        {
            ViewportSize = viewportWidth;
        }
        
        // Clamp offset to valid range
        if (Offset > MaxOffset)
        {
            Offset = MaxOffset;
        }
        if (Offset < 0)
        {
            Offset = 0;
        }
        
        // Set up viewport rect for clipping
        _viewportRect = new Rect(bounds.X, bounds.Y, viewportWidth, viewportHeight);
        
        // Arrange child with offset applied
        // The child is positioned such that the visible portion starts at the viewport position
        if (Orientation == ScrollOrientation.Vertical)
        {
            // Child is positioned above the viewport by the scroll offset
            var childY = bounds.Y - Offset;
            Child.Arrange(new Rect(bounds.X, childY, viewportWidth, _contentSize.Height));

            // Scroll to keep the focused descendant visible
            if (EnsureFocusedVisible(bounds.Y, viewportHeight))
            {
                childY = bounds.Y - Offset;
                Child.Arrange(new Rect(bounds.X, childY, viewportWidth, _contentSize.Height));
            }
        }
        else
        {
            // Child is positioned to the left of the viewport by the scroll offset
            var childX = bounds.X - Offset;
            Child.Arrange(new Rect(childX, bounds.Y, _contentSize.Width, viewportHeight));

            if (EnsureFocusedVisible(bounds.X, viewportWidth))
            {
                childX = bounds.X - Offset;
                Child.Arrange(new Rect(childX, bounds.Y, _contentSize.Width, viewportHeight));
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        // Store ourselves as the current layout provider to enable clipping
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;
        
        // Use RenderChild for automatic caching support
        if (Child != null)
        {
            context.RenderChild(Child);
        }
        
        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
        
        // Render scrollbar if needed
        if (IsScrollable && ShowScrollbar)
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
        
        var scrollbarX = Bounds.X + Bounds.Width - ScrollbarSize;
        var scrollbarHeight = _viewportRect.Height;
        
        // Calculate thumb position and size (no arrows, use full height)
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)ViewportSize / ContentSize * scrollbarHeight));
        var scrollRange = scrollbarHeight - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)Offset / MaxOffset * scrollRange) 
            : 0;
        
        // Render scrollbar
        for (int row = 0; row < scrollbarHeight; row++)
        {
            context.SetCursorPosition(scrollbarX, Bounds.Y + row);
            
            string charToRender;
            Hex1bColor color;
            
            if (row >= thumbPosition && row < thumbPosition + thumbSize)
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
        
        var scrollbarY = Bounds.Y + Bounds.Height - ScrollbarSize;
        var scrollbarWidth = _viewportRect.Width;
        
        // Calculate thumb position and size (no arrows, use full width)
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)ViewportSize / ContentSize * scrollbarWidth));
        var scrollRange = scrollbarWidth - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)Offset / MaxOffset * scrollRange) 
            : 0;
        
        context.SetCursorPosition(Bounds.X, scrollbarY);
        
        // Render scrollbar
        for (int col = 0; col < scrollbarWidth; col++)
        {
            string charToRender;
            Hex1bColor color;
            
            if (col >= thumbPosition && col < thumbPosition + thumbSize)
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
