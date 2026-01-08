using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for DrawerWidget.
/// An expandable/collapsible panel with a header toggle and content area.
/// </summary>
public sealed class DrawerNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// Whether the drawer is currently expanded.
    /// </summary>
    public bool IsExpanded { get; set; }
    
    /// <summary>
    /// The position of the drawer (which edge it anchors to).
    /// </summary>
    public DrawerPosition Position { get; set; } = DrawerPosition.Left;
    
    /// <summary>
    /// The display mode of the drawer.
    /// </summary>
    public DrawerMode Mode { get; set; } = DrawerMode.Docked;
    
    /// <summary>
    /// The fixed size when expanded (width for left/right, height for top/bottom).
    /// </summary>
    public int? ExpandedSize { get; set; }
    
    /// <summary>
    /// The header widget node.
    /// </summary>
    public Hex1bNode? Header { get; set; }
    
    /// <summary>
    /// The content widget node (shown when expanded).
    /// </summary>
    public Hex1bNode? Content { get; set; }
    
    /// <summary>
    /// The source widget for typed event args.
    /// </summary>
    public DrawerWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// The async action to execute when the drawer toggle is activated.
    /// </summary>
    public Func<InputBindingActionContext, Task>? ToggleAction { get; set; }
    
    /// <summary>
    /// The clip mode for the drawer's content. Defaults to Clip.
    /// </summary>
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    private bool _isFocused;
    
    /// <summary>
    /// Whether the drawer toggle is focused.
    /// </summary>
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
    
    /// <summary>
    /// Whether the mouse is hovering over the drawer toggle.
    /// </summary>
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
    /// The drawer toggle is focusable.
    /// </summary>
    public override bool IsFocusable => true;
    
    /// <summary>
    /// Drawer manages focus for its content children.
    /// </summary>
    public override bool ManagesChildFocus => true;

    // Track header and content bounds for hit testing and layout
    private Rect _toggleBounds;
    private Rect _contentBounds;

    #region ILayoutProvider Implementation
    
    /// <summary>
    /// The clip rectangle for child content.
    /// </summary>
    public Rect ClipRect => _contentBounds;
    
    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);
    
    #endregion

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Enter and Space toggle the drawer
        if (ToggleAction != null)
        {
            bindings.Key(Hex1bKey.Enter).Action(ToggleAction, "Toggle drawer");
            bindings.Key(Hex1bKey.Spacebar).Action(ToggleAction, "Toggle drawer");
            
            // Left click on the toggle activates it
            bindings.Mouse(MouseButton.Left).Action(ToggleAction, "Toggle drawer");
        }
        
        // Tab navigation
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
        
        // Escape collapses the drawer if expanded
        if (ToggleAction != null)
        {
            bindings.Key(Hex1bKey.Escape).Action(async ctx =>
            {
                if (IsExpanded)
                {
                    await ToggleAction(ctx);
                }
            }, "Collapse drawer");
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // The toggle row: indicator + header
        // Indicator is 2 chars (e.g., "▶ ")
        const int indicatorWidth = 2;
        
        // Measure header
        var headerSize = Header?.Measure(new Constraints(0, Math.Max(0, constraints.MaxWidth - indicatorWidth), 0, 1)) ?? Size.Zero;
        var toggleHeight = Math.Max(1, headerSize.Height);
        var toggleWidth = indicatorWidth + headerSize.Width;
        
        if (!IsExpanded)
        {
            // Collapsed: just the toggle row
            if (IsHorizontal)
            {
                // For left/right position, height can fill available
                return constraints.Constrain(new Size(toggleWidth, constraints.MaxHeight));
            }
            else
            {
                // For top/bottom position, width fills available
                return constraints.Constrain(new Size(constraints.MaxWidth, toggleHeight));
            }
        }
        
        // Expanded: toggle + content
        if (IsHorizontal)
        {
            // Left/Right: toggle column + content area side by side
            var contentWidth = ExpandedSize ?? 20; // Default expanded width
            var contentHeight = constraints.MaxHeight;
            
            // Measure content with available space
            var contentSize = Content?.Measure(new Constraints(0, contentWidth, 0, contentHeight)) ?? Size.Zero;
            contentWidth = ExpandedSize ?? contentSize.Width;
            
            return constraints.Constrain(new Size(toggleWidth + contentWidth, constraints.MaxHeight));
        }
        else
        {
            // Top/Bottom: toggle row + content area stacked
            var contentHeight = ExpandedSize ?? 5; // Default expanded height
            var contentWidth = constraints.MaxWidth;
            
            // Measure content with available space
            var contentSize = Content?.Measure(new Constraints(0, contentWidth, 0, contentHeight)) ?? Size.Zero;
            contentHeight = ExpandedSize ?? contentSize.Height;
            
            return constraints.Constrain(new Size(constraints.MaxWidth, toggleHeight + contentHeight));
        }
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        const int indicatorWidth = 2;
        
        // Measure header to get toggle dimensions
        var headerSize = Header?.Measure(new Constraints(0, Math.Max(0, bounds.Width - indicatorWidth), 0, 1)) ?? Size.Zero;
        var toggleHeight = Math.Max(1, headerSize.Height);
        var toggleWidth = indicatorWidth + headerSize.Width;
        
        if (!IsExpanded)
        {
            // Collapsed: just the toggle row fills the bounds
            if (IsHorizontal)
            {
                _toggleBounds = new Rect(bounds.X, bounds.Y, toggleWidth, bounds.Height);
                // Arrange header within toggle bounds (after indicator)
                Header?.Arrange(new Rect(bounds.X + indicatorWidth, bounds.Y, headerSize.Width, toggleHeight));
            }
            else
            {
                _toggleBounds = new Rect(bounds.X, bounds.Y, bounds.Width, toggleHeight);
                Header?.Arrange(new Rect(bounds.X + indicatorWidth, bounds.Y, headerSize.Width, toggleHeight));
            }
            _contentBounds = Rect.Zero;
            return;
        }
        
        // Expanded: arrange toggle and content
        if (IsHorizontal)
        {
            // Left/Right position
            if (Position == DrawerPosition.Left)
            {
                // Toggle on left, content to the right
                _toggleBounds = new Rect(bounds.X, bounds.Y, toggleWidth, bounds.Height);
                Header?.Arrange(new Rect(bounds.X + indicatorWidth, bounds.Y, headerSize.Width, toggleHeight));
                
                var contentWidth = bounds.Width - toggleWidth;
                _contentBounds = new Rect(bounds.X + toggleWidth, bounds.Y, contentWidth, bounds.Height);
            }
            else
            {
                // Toggle on right, content to the left
                var contentWidth = bounds.Width - toggleWidth;
                _contentBounds = new Rect(bounds.X, bounds.Y, contentWidth, bounds.Height);
                
                _toggleBounds = new Rect(bounds.X + contentWidth, bounds.Y, toggleWidth, bounds.Height);
                Header?.Arrange(new Rect(bounds.X + contentWidth + indicatorWidth, bounds.Y, headerSize.Width, toggleHeight));
            }
        }
        else
        {
            // Top/Bottom position
            if (Position == DrawerPosition.Top)
            {
                // Toggle on top, content below
                _toggleBounds = new Rect(bounds.X, bounds.Y, bounds.Width, toggleHeight);
                Header?.Arrange(new Rect(bounds.X + indicatorWidth, bounds.Y, headerSize.Width, toggleHeight));
                
                var contentHeight = bounds.Height - toggleHeight;
                _contentBounds = new Rect(bounds.X, bounds.Y + toggleHeight, bounds.Width, contentHeight);
            }
            else
            {
                // Toggle on bottom, content above
                var contentHeight = bounds.Height - toggleHeight;
                _contentBounds = new Rect(bounds.X, bounds.Y, bounds.Width, contentHeight);
                
                _toggleBounds = new Rect(bounds.X, bounds.Y + contentHeight, bounds.Width, toggleHeight);
                Header?.Arrange(new Rect(bounds.X + indicatorWidth, bounds.Y + contentHeight, headerSize.Width, toggleHeight));
            }
        }
        
        // Arrange content
        Content?.Arrange(_contentBounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        // Get theme colors
        Hex1bColor headerFg, headerBg;
        if (IsFocused)
        {
            headerFg = theme.Get(DrawerTheme.FocusedForeground);
            headerBg = theme.Get(DrawerTheme.FocusedBackground);
        }
        else
        {
            headerFg = theme.Get(DrawerTheme.HeaderForeground);
            headerBg = theme.Get(DrawerTheme.HeaderBackground);
        }
        
        // Get indicator based on state and position
        string indicator = GetIndicator(theme);
        
        // Render toggle row background
        var headerFgAnsi = headerFg.IsDefault ? theme.GetGlobalForeground().ToForegroundAnsi() : headerFg.ToForegroundAnsi();
        var headerBgAnsi = headerBg.IsDefault ? theme.GetGlobalBackground().ToBackgroundAnsi() : headerBg.ToBackgroundAnsi();
        
        // Render indicator
        var indicatorColor = IsFocused ? theme.Get(DrawerTheme.FocusedIndicatorColor) : headerFg;
        var indicatorFgAnsi = indicatorColor.IsDefault ? headerFgAnsi : indicatorColor.ToForegroundAnsi();
        
        var indicatorOutput = $"{indicatorFgAnsi}{headerBgAnsi}{indicator} {resetToGlobal}";
        WriteClipped(context, _toggleBounds.X, _toggleBounds.Y, indicatorOutput);
        
        // Render header
        if (Header != null)
        {
            var previousLayout = context.CurrentLayoutProvider;
            context.SetCursorPosition(Header.Bounds.X, Header.Bounds.Y);
            Header.Render(context);
            context.CurrentLayoutProvider = previousLayout;
        }
        
        // Render content if expanded
        if (IsExpanded && Content != null && _contentBounds.Width > 0 && _contentBounds.Height > 0)
        {
            var previousLayout = context.CurrentLayoutProvider;
            ParentLayoutProvider = previousLayout;
            context.CurrentLayoutProvider = this;
            
            // Fill content area with background
            var contentBg = theme.Get(DrawerTheme.ContentBackground);
            var contentBgAnsi = contentBg.IsDefault ? theme.GetGlobalBackground().ToBackgroundAnsi() : contentBg.ToBackgroundAnsi();
            var fillLine = $"{contentBgAnsi}{new string(' ', _contentBounds.Width)}{resetToGlobal}";
            for (int row = 0; row < _contentBounds.Height; row++)
            {
                WriteClipped(context, _contentBounds.X, _contentBounds.Y + row, fillLine);
            }
            
            // Render content
            context.SetCursorPosition(Content.Bounds.X, Content.Bounds.Y);
            Content.Render(context);
            
            context.CurrentLayoutProvider = previousLayout;
            ParentLayoutProvider = null;
        }
    }

    private string GetIndicator(Hex1bTheme theme)
    {
        if (IsExpanded)
        {
            return theme.Get(DrawerTheme.ExpandedIndicator);
        }
        
        return Position switch
        {
            DrawerPosition.Left => theme.Get(DrawerTheme.CollapsedIndicatorLeft),
            DrawerPosition.Right => theme.Get(DrawerTheme.CollapsedIndicatorRight),
            DrawerPosition.Top => theme.Get(DrawerTheme.CollapsedIndicatorTop),
            DrawerPosition.Bottom => theme.Get(DrawerTheme.CollapsedIndicatorBottom),
            _ => theme.Get(DrawerTheme.CollapsedIndicatorLeft)
        };
    }

    private static void WriteClipped(Hex1bRenderContext context, int x, int y, string text)
    {
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(x, y, text);
        }
        else
        {
            context.SetCursorPosition(x, y);
            context.Write(text);
        }
    }

    private bool IsHorizontal => Position == DrawerPosition.Left || Position == DrawerPosition.Right;

    /// <summary>
    /// Gets all focusable nodes in this subtree.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // The drawer toggle is focusable
        yield return this;
        
        // If expanded, include content focusables
        if (IsExpanded && Content != null)
        {
            foreach (var focusable in Content.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    /// <summary>
    /// Gets the direct children of this node.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Header != null) yield return Header;
        if (Content != null && IsExpanded) yield return Content;
    }

    /// <summary>
    /// Gets the bounds used for mouse hit testing.
    /// Only the toggle area responds to clicks when collapsed.
    /// </summary>
    public override Rect HitTestBounds => _toggleBounds;
}
