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
    /// Optional background node that renders behind all windows.
    /// This node is purely decorative and does not receive focus or input.
    /// </summary>
    public Hex1bNode? BackgroundNode { get; set; }

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
    public int ScrollX { get; set; }

    /// <summary>
    /// Current vertical scroll offset (for panning to out-of-bounds windows).
    /// </summary>
    public int ScrollY { get; set; }

    /// <summary>
    /// The virtual bounds encompassing all windows (may extend beyond panel bounds).
    /// </summary>
    private Rect _virtualBounds;

    /// <summary>
    /// The content origin - minimum X/Y of all windows.
    /// </summary>
    private int _contentOriginX;
    private int _contentOriginY;

    /// <summary>
    /// Scroll limits calculated during arrange. Used by scrollbars and scroll methods.
    /// </summary>
    private int _minScrollX, _minScrollY, _maxScrollX, _maxScrollY;

    /// <summary>
    /// Vertical scrollbar node (created lazily when needed).
    /// </summary>
    private ScrollbarNode? _verticalScrollbar;

    /// <summary>
    /// Horizontal scrollbar node (created lazily when needed).
    /// </summary>
    private ScrollbarNode? _horizontalScrollbar;

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
    /// Called by WindowPanelWidget after reconciling background.
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
        // But ONLY if parent is not managing focus (e.g., when popups are open, ZStack manages focus)
        if (activeWindow?.Node != null && !context.ParentManagesFocus())
        {
            var focusables = activeWindow.Node.GetFocusableNodes().ToList();
            if (focusables.Count > 0 && !focusables.Any(f => f.IsFocused))
            {
                // Clear focus from other windows (not the active one)
                foreach (var windowNode in WindowNodes)
                {
                    if (!ReferenceEquals(windowNode, activeWindow.Node))
                    {
                        foreach (var focusable in windowNode.GetFocusableNodes())
                        {
                            if (focusable.IsFocused)
                            {
                                ReconcileContext.SetNodeFocus(focusable, false);
                            }
                        }
                    }
                }

                ReconcileContext.SetNodeFocus(focusables[0], true);
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // If there's a modal window, only its focusables are accessible
        var modal = WindowNodes.LastOrDefault(w => w.IsModal);
        if (modal != null)
        {
            foreach (var focusable in modal.GetFocusableNodes())
            {
                yield return focusable;
            }
            yield break;
        }

        // Return background focusables first (lower priority in hit testing)
        if (BackgroundNode != null)
        {
            foreach (var focusable in BackgroundNode.GetFocusableNodes())
            {
                yield return focusable;
            }
        }

        // Then return window focusables (topmost last for priority)
        foreach (var windowNode in WindowNodes)
        {
            foreach (var focusable in windowNode.GetFocusableNodes())
            {
                yield return focusable;
            }
        }

        // Scrollbar nodes are focusable and need to be hit-testable
        // They are yielded LAST so they are hit-tested FIRST (FocusRing searches in reverse)
        if (_horizontalScrollbar != null && _horizontalScrollbar.IsFocusable)
        {
            yield return _horizontalScrollbar;
        }
        if (_verticalScrollbar != null && _verticalScrollbar.IsFocusable)
        {
            yield return _verticalScrollbar;
        }
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        // Measure background (fills available space)
        BackgroundNode?.Measure(constraints);

        // Measure windows (they have their own fixed sizes)
        foreach (var windowNode in WindowNodes)
        {
            windowNode.Measure(constraints);
        }

        return constraints.Constrain(new Size(constraints.MaxWidth, constraints.MaxHeight));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        // Clip to our own bounds - windows should not render outside the panel
        _resolvedClipRect = bounds;

        // Arrange background to fill bounds
        BackgroundNode?.Arrange(bounds);

        // First pass: resolve window positions and calculate virtual bounds
        foreach (var windowNode in WindowNodes)
        {
            if (windowNode.Entry == null) continue;

            var entry = windowNode.Entry;
            var windowWidth = entry.Width;
            var windowHeight = entry.Height;

            // Calculate position (center if not specified)
            if (!entry.X.HasValue || !entry.Y.HasValue)
            {
                // Use position spec to calculate initial position
                var (calcX, calcY) = entry.PositionSpec.Calculate(
                    bounds, 
                    windowWidth, 
                    windowHeight,
                    entry.X,
                    entry.Y);

                // Store calculated position so subsequent renders use the same position
                entry.X = calcX;
                entry.Y = calcY;
            }

            // Clamp to panel bounds unless panel allows out-of-bounds windows
            if (!AllowOutOfBounds)
            {
                entry.X = Math.Max(bounds.X, Math.Min(entry.X!.Value, bounds.X + bounds.Width - windowWidth));
                entry.Y = Math.Max(bounds.Y, Math.Min(entry.Y!.Value, bounds.Y + bounds.Height - windowHeight));
            }
        }

        // Calculate virtual bounds
        CalculateVirtualBounds(bounds);

        // NOTE: We intentionally do NOT clamp scroll offset here.
        // Clamping during arrange causes viewport shifts when content bounds change
        // (e.g., when a window moves back toward center, other windows would appear to move).
        // Scroll offset is only clamped when user actively scrolls (ScrollByAmount, scrollbar drag).
        
        // Store scroll limits for use by scrollbars and scroll methods
        _minScrollX = _virtualBounds.X - bounds.X;
        _minScrollY = _virtualBounds.Y - bounds.Y;
        _maxScrollX = Math.Max(0, _virtualBounds.X + _virtualBounds.Width - bounds.X - bounds.Width);
        _maxScrollY = Math.Max(0, _virtualBounds.Y + _virtualBounds.Height - bounds.Y - bounds.Height);

        // Second pass: arrange windows with scroll offset applied
        // Render position = world position - scroll offset
        foreach (var windowNode in WindowNodes)
        {
            if (windowNode.Entry == null) continue;

            var entry = windowNode.Entry;
            
            // Simple: screen position = world position - scroll offset
            var renderX = entry.X!.Value - ScrollX;
            var renderY = entry.Y!.Value - ScrollY;

            var windowBounds = new Rect(renderX, renderY, entry.Width, entry.Height);
            windowNode.Arrange(windowBounds);
        }

        // Arrange scrollbar nodes if needed
        ArrangeScrollbars(bounds);

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
    /// Creates/updates and arranges scrollbar nodes.
    /// </summary>
    private void ArrangeScrollbars(Rect bounds)
    {
        var needsVScroll = NeedsVerticalScroll;
        var needsHScroll = NeedsHorizontalScroll;
        
        // Update clip rect to exclude scrollbar areas
        var clipWidth = bounds.Width - (needsVScroll ? 1 : 0);
        var clipHeight = bounds.Height - (needsHScroll ? 1 : 0);
        _resolvedClipRect = new Rect(bounds.X, bounds.Y, clipWidth, clipHeight);

        // Vertical scrollbar
        if (needsVScroll)
        {
            _verticalScrollbar ??= new ScrollbarNode { Parent = this };
            _verticalScrollbar.Orientation = ScrollOrientation.Vertical;
            _verticalScrollbar.ContentSize = _virtualBounds.Height;
            _verticalScrollbar.ViewportSize = bounds.Height;
            
            // Convert panel-relative scroll to content-relative offset for scrollbar
            // Clamp the offset for display purposes (thumb position)
            var clampedScrollY = Math.Clamp(ScrollY, _minScrollY, _maxScrollY);
            _verticalScrollbar.Offset = clampedScrollY - _minScrollY;
            
            _verticalScrollbar.ScrollHandler = offset =>
            {
                // Convert back from scrollbar offset to panel-relative scroll
                ScrollY = offset + _minScrollY;
                MarkDirty();
                return Task.CompletedTask;
            };

            var vScrollHeight = bounds.Height - (needsHScroll ? 1 : 0);
            _verticalScrollbar.Measure(new Constraints(0, 1, 0, vScrollHeight));
            _verticalScrollbar.Arrange(new Rect(bounds.X + bounds.Width - 1, bounds.Y, 1, vScrollHeight));
        }
        else
        {
            _verticalScrollbar = null;
        }

        // Horizontal scrollbar
        if (needsHScroll)
        {
            _horizontalScrollbar ??= new ScrollbarNode { Parent = this };
            _horizontalScrollbar.Orientation = ScrollOrientation.Horizontal;
            _horizontalScrollbar.ContentSize = _virtualBounds.Width;
            _horizontalScrollbar.ViewportSize = bounds.Width;
            
            // Convert panel-relative scroll to content-relative offset for scrollbar
            // Clamp the offset for display purposes (thumb position)
            var clampedScrollX = Math.Clamp(ScrollX, _minScrollX, _maxScrollX);
            _horizontalScrollbar.Offset = clampedScrollX - _minScrollX;
            
            _horizontalScrollbar.ScrollHandler = offset =>
            {
                // Convert back from scrollbar offset to panel-relative scroll
                ScrollX = offset + _minScrollX;
                MarkDirty();
                return Task.CompletedTask;
            };

            var hScrollWidth = bounds.Width - (needsVScroll ? 1 : 0);
            _horizontalScrollbar.Measure(new Constraints(0, hScrollWidth, 0, 1));
            _horizontalScrollbar.Arrange(new Rect(bounds.X, bounds.Y + bounds.Height - 1, hScrollWidth, 1));
        }
        else
        {
            _horizontalScrollbar = null;
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
            _contentOriginX = panelBounds.X;
            _contentOriginY = panelBounds.Y;
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

        _contentOriginX = minX;
        _contentOriginY = minY;
        _virtualBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public override void Render(Hex1bRenderContext context)
    {
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;

        // Render background first (bottom layer - decorative only)
        if (BackgroundNode != null)
        {
            context.RenderChild(BackgroundNode);
        }

        // Render windows in z-order (bottom to top)
        foreach (var windowNode in WindowNodes)
        {
            context.RenderChild(windowNode);
        }

        // Render scrollbars on top using scrollbar nodes
        if (_verticalScrollbar != null)
        {
            context.RenderChild(_verticalScrollbar);
        }
        if (_horizontalScrollbar != null)
        {
            context.RenderChild(_horizontalScrollbar);
        }
        
        // Render corner cell when both scrollbars present
        if (_verticalScrollbar != null && _horizontalScrollbar != null)
        {
            var cornerX = Bounds.X + Bounds.Width - 1;
            var cornerY = Bounds.Y + Bounds.Height - 1;
            context.SetCursorPosition(cornerX, cornerY);
            context.Write(" ");
        }

        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Tab navigation through focusables
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
        
        // Mouse wheel scrolling for panel panning
        bindings.Mouse(Input.MouseButton.ScrollUp).Action(_ => ScrollByAmount(0, -3), "Pan up");
        bindings.Mouse(Input.MouseButton.ScrollDown).Action(_ => ScrollByAmount(0, 3), "Pan down");
    }

    /// <summary>
    /// Scrolls the panel view by the specified amount.
    /// </summary>
    public void ScrollByAmount(int deltaX, int deltaY)
    {
        if (NeedsHorizontalScroll && deltaX != 0)
        {
            // Use stored limits, clamp on user scroll action
            ScrollX = Math.Clamp(ScrollX + deltaX, _minScrollX, _maxScrollX);
            MarkDirty();
        }
        
        if (NeedsVerticalScroll && deltaY != 0)
        {
            // Use stored limits, clamp on user scroll action
            ScrollY = Math.Clamp(ScrollY + deltaY, _minScrollY, _maxScrollY);
            MarkDirty();
        }
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// Scrollbars are returned LAST so they are hit-tested FIRST (reverse order in InputRouter).
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        foreach (var windowNode in WindowNodes)
        {
            yield return windowNode;
        }
        // Scrollbars last = hit-tested first (InputRouter processes in reverse)
        if (_horizontalScrollbar != null) yield return _horizontalScrollbar;
        if (_verticalScrollbar != null) yield return _verticalScrollbar;
    }
}
