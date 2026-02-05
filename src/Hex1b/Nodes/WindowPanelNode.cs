using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="WindowPanelWidget"/>.
/// A container that hosts floating windows rendered on top of main content.
/// </summary>
public sealed class WindowPanelNode : Hex1bNode, IWindowHost, ILayoutProvider
{
    /// <summary>
    /// Optional name for this panel. Used when multiple WindowPanels exist.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The main content node displayed behind windows.
    /// </summary>
    public Hex1bNode? Content { get; set; }

    /// <summary>
    /// The window nodes, in z-order (bottom to top).
    /// </summary>
    public List<WindowNode> WindowNodes { get; } = [];

    /// <summary>
    /// The window manager for this panel.
    /// </summary>
    public WindowManager Windows { get; } = new();

    /// <summary>
    /// Whether windows can be moved outside the panel bounds.
    /// When true, scrollbars appear to allow panning to out-of-bounds windows.
    /// </summary>
    public bool AllowOutOfBounds { get; set; }

    /// <summary>
    /// The clip mode for the panel's content. Defaults to Clip.
    /// </summary>
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    /// <summary>
    /// The clipping scope for this panel's content.
    /// </summary>
    public ClipScope ClipScopeValue { get; set; } = ClipScope.Parent;

    /// <summary>
    /// Resolved clip rectangle based on ClipScopeValue.
    /// Computed during Arrange.
    /// </summary>
    private Rect _resolvedClipRect;

    /// <summary>
    /// Current horizontal scroll offset (for panning to out-of-bounds windows).
    /// </summary>
    public int ScrollX { get; private set; }

    /// <summary>
    /// Current vertical scroll offset (for panning to out-of-bounds windows).
    /// </summary>
    public int ScrollY { get; private set; }

    /// <summary>
    /// The virtual bounds encompassing all windows (may extend beyond panel bounds).
    /// </summary>
    private Rect _virtualBounds;

    /// <summary>
    /// Whether horizontal scrollbar is needed.
    /// </summary>
    public bool NeedsHorizontalScroll => _virtualBounds.Width > Bounds.Width;

    /// <summary>
    /// Whether vertical scrollbar is needed.
    /// </summary>
    public bool NeedsVerticalScroll => _virtualBounds.Height > Bounds.Height;

    #region ILayoutProvider Implementation

    /// <summary>
    /// The clip rectangle for child content.
    /// </summary>
    public Rect ClipRect => _resolvedClipRect;

    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);

    #endregion

    /// <summary>
    /// WindowPanel manages focus for its descendants including windows.
    /// </summary>
    public override bool ManagesChildFocus => true;

    /// <summary>
    /// Reconciles window nodes from the WindowManager.
    /// Called by WindowPanelWidget after reconciling content.
    /// </summary>
    internal async Task ReconcileWindowsAsync(ReconcileContext context)
    {
        var entries = Windows.All;
        var activeWindow = Windows.ActiveWindow;
        var newWindowNodes = new List<WindowNode>();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            // Find existing node for this entry
            var existingNode = WindowNodes.FirstOrDefault(n => n.Entry?.Id == entry.Id);

            // Create the window widget and reconcile
            var windowWidget = new WindowWidget(entry);
            var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
            var reconciledNode = await childContext.ReconcileChildAsync(existingNode, windowWidget, this) as WindowNode;

            if (reconciledNode != null)
            {
                reconciledNode.IsActive = ReferenceEquals(entry, activeWindow);
                newWindowNodes.Add(reconciledNode);
            }
        }

        // Track removed windows for bounds clearing
        foreach (var oldNode in WindowNodes)
        {
            if (!newWindowNodes.Any(n => n.Entry?.Id == oldNode.Entry?.Id))
            {
                if (oldNode.Bounds.Width > 0 && oldNode.Bounds.Height > 0)
                {
                    AddOrphanedChildBounds(oldNode.Bounds);
                }
            }
        }

        WindowNodes.Clear();
        WindowNodes.AddRange(newWindowNodes);

        // Focus management: if windows exist, focus the active window's first focusable
        if (activeWindow?.Node != null)
        {
            var focusables = activeWindow.Node.GetFocusableNodes().ToList();
            if (focusables.Count > 0 && !focusables.Any(f => f.IsFocused))
            {
                // Clear focus from other nodes first
                if (Content != null)
                {
                    foreach (var focusable in Content.GetFocusableNodes())
                    {
                        if (focusable.IsFocused)
                        {
                            ReconcileContext.SetNodeFocus(focusable, false);
                        }
                    }
                }

                ReconcileContext.SetNodeFocus(focusables[0], true);
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // If there are windows, only return focusables from the topmost window
        // (unless modal, in which case only the modal's focusables)
        if (WindowNodes.Count > 0)
        {
            // If there's a modal, only its focusables are accessible
            var modal = WindowNodes.LastOrDefault(w => w.IsModal);
            if (modal != null)
            {
                foreach (var focusable in modal.GetFocusableNodes())
                {
                    yield return focusable;
                }
                yield break;
            }

            // Otherwise, return focusables from all windows (topmost last for priority)
            // and the main content
            if (Content != null)
            {
                foreach (var focusable in Content.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }

            foreach (var windowNode in WindowNodes)
            {
                foreach (var focusable in windowNode.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
        }
        else
        {
            // No windows - just return content focusables
            if (Content != null)
            {
                foreach (var focusable in Content.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Measure content to fill available space
        var contentSize = Content?.Measure(constraints) ?? Size.Zero;

        // Measure windows (they have their own fixed sizes)
        foreach (var windowNode in WindowNodes)
        {
            windowNode.Measure(constraints);
        }

        return constraints.Constrain(contentSize);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        // Resolve the clip scope
        var parentClip = ParentLayoutProvider?.ClipRect;
        var screenBounds = new Rect(0, 0, 1000, 1000);
        _resolvedClipRect = ClipScopeValue.Resolve(bounds, parentClip, screenBounds, widgetNodeResolver: null);

        // Arrange content to fill bounds
        Content?.Arrange(bounds);

        // Arrange windows at their positions
        foreach (var windowNode in WindowNodes)
        {
            if (windowNode.Entry == null) continue;

            var entry = windowNode.Entry;
            var windowWidth = entry.Width;
            var windowHeight = entry.Height;

            // Calculate position (center if not specified)
            int windowX, windowY;
            if (entry.X.HasValue && entry.Y.HasValue)
            {
                windowX = entry.X.Value;
                windowY = entry.Y.Value;
            }
            else
            {
                // Use position spec to calculate initial position
                (windowX, windowY) = entry.PositionSpec.Calculate(
                    bounds, 
                    windowWidth, 
                    windowHeight,
                    entry.X,
                    entry.Y);

                // Store calculated position so subsequent renders use the same position
                entry.X = windowX;
                entry.Y = windowY;
            }

            // Clamp to panel bounds unless panel allows out-of-bounds windows
            if (!AllowOutOfBounds)
            {
                windowX = Math.Max(bounds.X, Math.Min(windowX, bounds.X + bounds.Width - windowWidth));
                windowY = Math.Max(bounds.Y, Math.Min(windowY, bounds.Y + bounds.Height - windowHeight));
            }

            // Apply scroll offset for rendering position
            var renderX = windowX - ScrollX;
            var renderY = windowY - ScrollY;

            var windowBounds = new Rect(renderX, renderY, windowWidth, windowHeight);
            windowNode.Arrange(windowBounds);
        }

        // Calculate virtual bounds encompassing all windows
        CalculateVirtualBounds(bounds);

        // Clamp scroll offset to valid range after virtual bounds calculation
        var maxScrollX = Math.Max(0, _virtualBounds.Width - bounds.Width);
        var maxScrollY = Math.Max(0, _virtualBounds.Height - bounds.Height);
        ScrollX = Math.Clamp(ScrollX, 0, maxScrollX);
        ScrollY = Math.Clamp(ScrollY, 0, maxScrollY);

        // Mark dirty if any window is dirty (z-order maintenance)
        if (WindowNodes.Count > 1)
        {
            foreach (var windowNode in WindowNodes)
            {
                if (windowNode.NeedsRender())
                {
                    MarkDirty();
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Calculates the virtual bounds that encompass all windows.
    /// </summary>
    private void CalculateVirtualBounds(Rect panelBounds)
    {
        if (WindowNodes.Count == 0 || !AllowOutOfBounds)
        {
            _virtualBounds = panelBounds;
            return;
        }

        int minX = panelBounds.X;
        int minY = panelBounds.Y;
        int maxX = panelBounds.X + panelBounds.Width;
        int maxY = panelBounds.Y + panelBounds.Height;

        foreach (var windowNode in WindowNodes)
        {
            if (windowNode.Entry != null)
            {
                // Use the logical position (Entry.X/Y) not the render position (Bounds)
                var entryX = windowNode.Entry.X ?? panelBounds.X;
                var entryY = windowNode.Entry.Y ?? panelBounds.Y;
                minX = Math.Min(minX, entryX);
                minY = Math.Min(minY, entryY);
                maxX = Math.Max(maxX, entryX + windowNode.Entry.Width);
                maxY = Math.Max(maxY, entryY + windowNode.Entry.Height);
            }
        }

        _virtualBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public override void Render(Hex1bRenderContext context)
    {
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;

        // Render content first (bottom layer)
        if (Content != null)
        {
            context.RenderChild(Content);
        }

        // Render windows in z-order (bottom to top)
        foreach (var windowNode in WindowNodes)
        {
            context.RenderChild(windowNode);
        }

        // Render scrollbars if needed
        var theme = context.Theme;
        if (NeedsVerticalScroll)
        {
            RenderVerticalScrollbar(context, theme);
        }
        if (NeedsHorizontalScroll)
        {
            RenderHorizontalScrollbar(context, theme);
        }

        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    private void RenderVerticalScrollbar(Hex1bRenderContext context, Theming.Hex1bTheme theme)
    {
        var trackColor = theme.Get(Theming.ScrollTheme.TrackColor);
        var thumbColor = theme.Get(Theming.ScrollTheme.ThumbColor);
        var trackChar = theme.Get(Theming.ScrollTheme.VerticalTrackCharacter);
        var thumbChar = theme.Get(Theming.ScrollTheme.VerticalThumbCharacter);

        var scrollbarX = Bounds.X + Bounds.Width - 1;
        var scrollbarHeight = Bounds.Height - (NeedsHorizontalScroll ? 1 : 0);

        // Calculate thumb size and position based on virtual bounds
        var contentHeight = _virtualBounds.Height;
        var viewportHeight = Bounds.Height;
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)viewportHeight / contentHeight * scrollbarHeight));
        
        // Calculate thumb position based on current scroll offset
        var maxScrollOffset = Math.Max(0, contentHeight - viewportHeight);
        var scrollRange = scrollbarHeight - thumbSize;
        var thumbPosition = maxScrollOffset > 0 
            ? (int)Math.Round((double)ScrollY / maxScrollOffset * scrollRange) 
            : 0;

        for (int row = 0; row < scrollbarHeight; row++)
        {
            string charToRender;
            Theming.Hex1bColor color;
            
            if (row >= thumbPosition && row < thumbPosition + thumbSize)
            {
                charToRender = thumbChar;
                color = thumbColor;
            }
            else
            {
                charToRender = trackChar;
                color = trackColor;
            }
            
            context.SetCursorPosition(scrollbarX, Bounds.Y + row);
            context.Write($"{color.ToForegroundAnsi()}{charToRender}\x1b[0m");
        }
    }

    private void RenderHorizontalScrollbar(Hex1bRenderContext context, Theming.Hex1bTheme theme)
    {
        var trackColor = theme.Get(Theming.ScrollTheme.TrackColor);
        var thumbColor = theme.Get(Theming.ScrollTheme.ThumbColor);
        var trackChar = theme.Get(Theming.ScrollTheme.HorizontalTrackCharacter);
        var thumbChar = theme.Get(Theming.ScrollTheme.HorizontalThumbCharacter);

        var scrollbarY = Bounds.Y + Bounds.Height - 1;
        var scrollbarWidth = Bounds.Width - (NeedsVerticalScroll ? 1 : 0);

        // Calculate thumb size and position based on virtual bounds
        var contentWidth = _virtualBounds.Width;
        var viewportWidth = Bounds.Width;
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)viewportWidth / contentWidth * scrollbarWidth));
        
        // Calculate thumb position based on current scroll offset
        var maxScrollOffset = Math.Max(0, contentWidth - viewportWidth);
        var scrollRange = scrollbarWidth - thumbSize;
        var thumbPosition = maxScrollOffset > 0 
            ? (int)Math.Round((double)ScrollX / maxScrollOffset * scrollRange) 
            : 0;

        for (int col = 0; col < scrollbarWidth; col++)
        {
            string charToRender;
            Theming.Hex1bColor color;
            
            if (col >= thumbPosition && col < thumbPosition + thumbSize)
            {
                charToRender = thumbChar;
                color = thumbColor;
            }
            else
            {
                charToRender = trackChar;
                color = trackColor;
            }
            
            context.SetCursorPosition(Bounds.X + col, scrollbarY);
            context.Write($"{color.ToForegroundAnsi()}{charToRender}\x1b[0m");
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Tab navigation through focusables
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
        
        // Mouse wheel scrolling for panel panning
        bindings.Mouse(Input.MouseButton.ScrollUp).Action(_ => ScrollByAmount(0, -3), "Pan up");
        bindings.Mouse(Input.MouseButton.ScrollDown).Action(_ => ScrollByAmount(0, 3), "Pan down");
        
        // Scrollbar drag
        bindings.Drag(Input.MouseButton.Left).Action(HandleScrollbarDrag, "Drag scrollbar");
    }

    /// <summary>
    /// Scrolls the panel view by the specified amount.
    /// </summary>
    public void ScrollByAmount(int deltaX, int deltaY)
    {
        if (NeedsHorizontalScroll && deltaX != 0)
        {
            var maxScrollX = _virtualBounds.Width - Bounds.Width;
            ScrollX = Math.Clamp(ScrollX + deltaX, 0, Math.Max(0, maxScrollX));
            MarkDirty();
        }
        
        if (NeedsVerticalScroll && deltaY != 0)
        {
            var maxScrollY = _virtualBounds.Height - Bounds.Height;
            ScrollY = Math.Clamp(ScrollY + deltaY, 0, Math.Max(0, maxScrollY));
            MarkDirty();
        }
    }

    private Input.DragHandler HandleScrollbarDrag(int localX, int localY)
    {
        // Check vertical scrollbar
        if (NeedsVerticalScroll)
        {
            var scrollbarX = Bounds.Width - 1;
            if (localX >= scrollbarX)
            {
                return HandleVerticalScrollbarDrag(localY);
            }
        }
        
        // Check horizontal scrollbar
        if (NeedsHorizontalScroll)
        {
            var scrollbarY = Bounds.Height - 1;
            if (localY >= scrollbarY)
            {
                return HandleHorizontalScrollbarDrag(localX);
            }
        }
        
        return new Input.DragHandler();
    }

    private Input.DragHandler HandleVerticalScrollbarDrag(int localY)
    {
        var scrollbarHeight = Bounds.Height - (NeedsHorizontalScroll ? 1 : 0);
        var contentHeight = _virtualBounds.Height;
        var viewportHeight = Bounds.Height;
        var maxScrollY = Math.Max(0, contentHeight - viewportHeight);
        
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)viewportHeight / contentHeight * scrollbarHeight));
        var scrollRange = scrollbarHeight - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)ScrollY / maxScrollY * scrollRange) 
            : 0;

        if (localY >= thumbPosition && localY < thumbPosition + thumbSize)
        {
            // Drag thumb
            var startScrollY = ScrollY;
            var contentPerPixel = maxScrollY > 0 && scrollRange > 0
                ? (double)maxScrollY / scrollRange
                : 0;
            
            return Input.DragHandler.Simple(
                onMove: (deltaX, deltaY) =>
                {
                    if (contentPerPixel > 0)
                    {
                        ScrollY = Math.Clamp((int)Math.Round(startScrollY + deltaY * contentPerPixel), 0, maxScrollY);
                        MarkDirty();
                    }
                }
            );
        }
        else if (localY < thumbPosition)
        {
            // Page up
            ScrollByAmount(0, -viewportHeight / 2);
        }
        else
        {
            // Page down
            ScrollByAmount(0, viewportHeight / 2);
        }
        
        return new Input.DragHandler();
    }

    private Input.DragHandler HandleHorizontalScrollbarDrag(int localX)
    {
        var scrollbarWidth = Bounds.Width - (NeedsVerticalScroll ? 1 : 0);
        var contentWidth = _virtualBounds.Width;
        var viewportWidth = Bounds.Width;
        var maxScrollX = Math.Max(0, contentWidth - viewportWidth);
        
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)viewportWidth / contentWidth * scrollbarWidth));
        var scrollRange = scrollbarWidth - thumbSize;
        var thumbPosition = scrollRange > 0 
            ? (int)Math.Round((double)ScrollX / maxScrollX * scrollRange) 
            : 0;

        if (localX >= thumbPosition && localX < thumbPosition + thumbSize)
        {
            // Drag thumb
            var startScrollX = ScrollX;
            var contentPerPixel = maxScrollX > 0 && scrollRange > 0
                ? (double)maxScrollX / scrollRange
                : 0;
            
            return Input.DragHandler.Simple(
                onMove: (deltaX, deltaY) =>
                {
                    if (contentPerPixel > 0)
                    {
                        ScrollX = Math.Clamp((int)Math.Round(startScrollX + deltaX * contentPerPixel), 0, maxScrollX);
                        MarkDirty();
                    }
                }
            );
        }
        else if (localX < thumbPosition)
        {
            // Page left
            ScrollByAmount(-viewportWidth / 2, 0);
        }
        else
        {
            // Page right
            ScrollByAmount(viewportWidth / 2, 0);
        }
        
        return new Input.DragHandler();
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Content != null) yield return Content;
        foreach (var windowNode in WindowNodes)
        {
            yield return windowNode;
        }
    }

    /// <summary>
    /// Handles mouse clicks, intercepting scrollbar clicks before routing to children.
    /// </summary>
    public override Input.InputResult HandleMouseClick(int localX, int localY, Input.Hex1bMouseEvent mouseEvent)
    {
        // Check if click is on scrollbar areas
        if (mouseEvent.Button == Input.MouseButton.Left)
        {
            // Check vertical scrollbar
            if (NeedsVerticalScroll && localX >= Bounds.Width - 1)
            {
                var handler = HandleVerticalScrollbarDrag(localY);
                if (handler.OnMove != null || handler.OnEnd != null)
                {
                    // Start drag - this is handled by the binding system
                    return Input.InputResult.Handled;
                }
            }
            
            // Check horizontal scrollbar
            if (NeedsHorizontalScroll && localY >= Bounds.Height - 1)
            {
                var handler = HandleHorizontalScrollbarDrag(localX);
                if (handler.OnMove != null || handler.OnEnd != null)
                {
                    return Input.InputResult.Handled;
                }
            }
        }
        
        return Input.InputResult.NotHandled;
    }
}
