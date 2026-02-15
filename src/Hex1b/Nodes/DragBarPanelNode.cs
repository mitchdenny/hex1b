using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Node for a DragBarPanel — a container with a resize handle on one edge.
/// The handle can be dragged with the mouse or moved with arrow keys when focused.
/// </summary>
public sealed class DragBarPanelNode : Hex1bNode, IChildLayoutProvider
{
    private int _currentSize;
    
    /// <summary>
    /// The current size of the panel in characters along the drag axis.
    /// Mutated by drag/keyboard input; preserved across reconciliation.
    /// </summary>
    public int CurrentSize
    {
        get => _currentSize;
        set
        {
            var clamped = ClampSize(value);
            if (_currentSize != clamped)
            {
                _currentSize = clamped;
                MarkDirty();
                ContentChild?.MarkDirty();
            }
        }
    }
    
    /// <summary>
    /// Which edge the handle is on (set during reconciliation).
    /// </summary>
    public DragBarEdge ResolvedEdge { get; set; } = DragBarEdge.Right;
    
    /// <summary>
    /// The content child node.
    /// </summary>
    public Hex1bNode? ContentChild { get; set; }
    
    /// <summary>
    /// Minimum allowed size.
    /// </summary>
    public int MinSize { get; set; } = 5;
    
    /// <summary>
    /// Maximum allowed size (null = no maximum).
    /// </summary>
    public int? MaxSize { get; set; }
    
    /// <summary>
    /// Step size for keyboard resizing.
    /// </summary>
    public int ResizeStep { get; set; } = 2;

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

    private bool IsHorizontalEdge => ResolvedEdge is DragBarEdge.Top or DragBarEdge.Bottom;

    /// <summary>
    /// Hit test targets only the handle area, not the content.
    /// </summary>
    public override Rect HitTestBounds
    {
        get
        {
            return ResolvedEdge switch
            {
                DragBarEdge.Left => new Rect(Bounds.X, Bounds.Y, 1, Bounds.Height),
                DragBarEdge.Right => new Rect(Bounds.Right - 1, Bounds.Y, 1, Bounds.Height),
                DragBarEdge.Top => new Rect(Bounds.X, Bounds.Y, Bounds.Width, 1),
                DragBarEdge.Bottom => new Rect(Bounds.X, Bounds.Bottom - 1, Bounds.Width, 1),
                _ => Bounds
            };
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Keyboard resize
        bindings.Key(Hex1bKey.LeftArrow).Action(ResizeLeft, "Resize left");
        bindings.Key(Hex1bKey.RightArrow).Action(ResizeRight, "Resize right");
        bindings.Key(Hex1bKey.UpArrow).Action(ResizeUp, "Resize up");
        bindings.Key(Hex1bKey.DownArrow).Action(ResizeDown, "Resize down");
        
        // Focus navigation
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
        
        // Mouse drag to resize
        bindings.Drag(MouseButton.Left).Action((startX, startY) =>
        {
            var startSize = CurrentSize;
            return DragHandler.Simple(
                onMove: (deltaX, deltaY) =>
                {
                    var delta = IsHorizontalEdge ? deltaY : deltaX;
                    // Flip delta for Left/Top edges (dragging toward edge = growing)
                    if (ResolvedEdge is DragBarEdge.Left or DragBarEdge.Top)
                    {
                        delta = -delta;
                    }
                    CurrentSize = startSize + delta;
                }
            );
        }, "Drag to resize");
    }

    private void ResizeLeft()
    {
        if (!IsFocused) return;
        if (ResolvedEdge is DragBarEdge.Left or DragBarEdge.Right)
        {
            // Left/Right edge: left arrow shrinks for Right handle, grows for Left handle
            CurrentSize += ResolvedEdge == DragBarEdge.Left ? ResizeStep : -ResizeStep;
        }
    }

    private void ResizeRight()
    {
        if (!IsFocused) return;
        if (ResolvedEdge is DragBarEdge.Left or DragBarEdge.Right)
        {
            CurrentSize += ResolvedEdge == DragBarEdge.Right ? ResizeStep : -ResizeStep;
        }
    }

    private void ResizeUp()
    {
        if (!IsFocused) return;
        if (ResolvedEdge is DragBarEdge.Top or DragBarEdge.Bottom)
        {
            CurrentSize += ResolvedEdge == DragBarEdge.Top ? ResizeStep : -ResizeStep;
        }
    }

    private void ResizeDown()
    {
        if (!IsFocused) return;
        if (ResolvedEdge is DragBarEdge.Top or DragBarEdge.Bottom)
        {
            CurrentSize += ResolvedEdge == DragBarEdge.Bottom ? ResizeStep : -ResizeStep;
        }
    }

    private int ClampSize(int size)
    {
        size = Math.Max(size, MinSize);
        if (MaxSize.HasValue)
        {
            size = Math.Min(size, MaxSize.Value);
        }
        return size;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (IsHorizontalEdge)
        {
            // Drag axis is vertical (height). Width comes from content/constraints.
            var effectiveSize = _currentSize > 0 ? _currentSize : 10;
            effectiveSize = ClampSize(effectiveSize);
            
            // Measure content to get width
            var contentConstraints = new Constraints(
                constraints.MinWidth, constraints.MaxWidth,
                0, Math.Max(0, effectiveSize - 1));
            var contentSize = ContentChild?.Measure(contentConstraints) ?? Size.Zero;
            
            var width = Math.Max(contentSize.Width, constraints.MinWidth);
            return constraints.Constrain(new Size(width, effectiveSize));
        }
        else
        {
            // Drag axis is horizontal (width). Height comes from content/constraints.
            var effectiveSize = _currentSize > 0 ? _currentSize : 10;
            effectiveSize = ClampSize(effectiveSize);
            
            // Measure content to get height
            var contentConstraints = new Constraints(
                0, Math.Max(0, effectiveSize - 1),
                constraints.MinHeight, constraints.MaxHeight);
            var contentSize = ContentChild?.Measure(contentConstraints) ?? Size.Zero;
            
            var height = Math.Max(contentSize.Height, constraints.MinHeight);
            return constraints.Constrain(new Size(effectiveSize, height));
        }
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.Arrange(bounds);
        
        if (ContentChild == null) return;
        
        // Content gets bounds minus the 1-char handle
        var contentBounds = ResolvedEdge switch
        {
            DragBarEdge.Left => new Rect(bounds.X + 1, bounds.Y, Math.Max(0, bounds.Width - 1), bounds.Height),
            DragBarEdge.Right => new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - 1), bounds.Height),
            DragBarEdge.Top => new Rect(bounds.X, bounds.Y + 1, bounds.Width, Math.Max(0, bounds.Height - 1)),
            DragBarEdge.Bottom => new Rect(bounds.X, bounds.Y, bounds.Width, Math.Max(0, bounds.Height - 1)),
            _ => bounds
        };
        
        ContentChild.Arrange(contentBounds);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // The drag bar itself is focusable
        yield return this;
        
        if (ContentChild != null)
        {
            foreach (var focusable in ContentChild.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (ContentChild != null) yield return ContentChild;
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        // Render content with clipping
        if (ContentChild != null)
        {
            var previousLayout = context.CurrentLayoutProvider;
            var contentProvider = new RectLayoutProvider(ContentChild.Bounds);
            contentProvider.ParentLayoutProvider = previousLayout;
            context.CurrentLayoutProvider = contentProvider;
            
            context.RenderChild(ContentChild);
            
            context.CurrentLayoutProvider = previousLayout;
        }
        
        // Render the handle
        RenderHandle(context, theme);
    }

    private void RenderHandle(Hex1bRenderContext context, Hex1bTheme theme)
    {
        // Determine handle color — no inversion, just color changes like window borders
        Hex1bColor handleFg;
        
        if (IsFocused)
        {
            handleFg = theme.Get(DragBarPanelTheme.HandleFocusedColor);
        }
        else if (IsHovered)
        {
            handleFg = theme.Get(DragBarPanelTheme.HandleHoverColor);
        }
        else
        {
            handleFg = theme.Get(DragBarPanelTheme.HandleColor);
        }
        
        var showThumbs = IsHovered || IsFocused;
        
        if (IsHorizontalEdge)
        {
            RenderHorizontalHandle(context, theme, handleFg, showThumbs);
        }
        else
        {
            RenderVerticalHandle(context, theme, handleFg, showThumbs);
        }
    }

    private void RenderVerticalHandle(Hex1bRenderContext context, Hex1bTheme theme, Hex1bColor fg, bool showThumbs)
    {
        var handleChar = theme.Get(DragBarPanelTheme.VerticalHandleChar);
        var thumbChar = theme.Get(DragBarPanelTheme.VerticalThumbChar);
        var thumbColor = theme.Get(DragBarPanelTheme.ThumbColor);
        
        var handleX = ResolvedEdge == DragBarEdge.Left ? Bounds.X : Bounds.Right - 1;
        
        // Calculate thumb region (centered, ~1/3 of height)
        var thumbSize = Math.Max(3, Math.Min(Bounds.Height / 3, 7));
        var thumbStart = (Bounds.Height - thumbSize) / 2;
        var thumbEnd = thumbStart + thumbSize;
        
        for (int row = 0; row < Bounds.Height; row++)
        {
            context.SetCursorPosition(handleX, Bounds.Y + row);
            
            var isThumbRow = showThumbs && row >= thumbStart && row < thumbEnd;
            var ch = isThumbRow ? thumbChar : handleChar;
            var charFg = isThumbRow ? thumbColor : fg;
            
            context.Write($"{charFg.ToForegroundAnsi()}{ch}\x1b[0m");
        }
    }

    private void RenderHorizontalHandle(Hex1bRenderContext context, Hex1bTheme theme, Hex1bColor fg, bool showThumbs)
    {
        var handleChar = theme.Get(DragBarPanelTheme.HorizontalHandleChar);
        var thumbChar = theme.Get(DragBarPanelTheme.HorizontalThumbChar);
        var thumbColor = theme.Get(DragBarPanelTheme.ThumbColor);
        
        var handleY = ResolvedEdge == DragBarEdge.Top ? Bounds.Y : Bounds.Bottom - 1;
        
        // Calculate thumb region (centered, ~1/3 of width)
        var thumbSize = Math.Max(3, Math.Min(Bounds.Width / 3, 7));
        var thumbStart = (Bounds.Width - thumbSize) / 2;
        var thumbEnd = thumbStart + thumbSize;
        
        context.SetCursorPosition(Bounds.X, handleY);
        
        for (int col = 0; col < Bounds.Width; col++)
        {
            var isThumbCol = showThumbs && col >= thumbStart && col < thumbEnd;
            var ch = isThumbCol ? thumbChar : handleChar;
            var charFg = isThumbCol ? thumbColor : fg;
            
            context.Write($"{charFg.ToForegroundAnsi()}{ch}\x1b[0m");
        }
    }

    /// <inheritdoc />
    public ILayoutProvider? GetChildLayoutProvider(Hex1bNode child)
    {
        if (ReferenceEquals(child, ContentChild) && ContentChild != null)
        {
            return new RectLayoutProvider(ContentChild.Bounds);
        }
        return null;
    }
}
