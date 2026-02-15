using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="WindowWidget"/>.
/// A floating window with title bar, close button, and content area.
/// </summary>
public sealed class WindowNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// The window entry from the WindowManager.
    /// </summary>
    public WindowEntry? Entry { get; set; }

    /// <summary>
    /// The window title displayed in the title bar.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// The child content node.
    /// </summary>
    public Hex1bNode? Content { get; set; }

    /// <summary>
    /// Whether this window can be resized.
    /// </summary>
    public bool IsResizable { get; set; }

    /// <summary>
    /// Whether the title bar is displayed.
    /// </summary>
    public bool ShowTitleBar { get; set; } = true;

    /// <summary>
    /// Actions displayed on the left side of the title bar.
    /// </summary>
    public IReadOnlyList<WindowAction> LeftTitleBarActions { get; set; } = [];

    /// <summary>
    /// Actions displayed on the right side of the title bar.
    /// </summary>
    public IReadOnlyList<WindowAction> RightTitleBarActions { get; set; } = [WindowAction.Close()];

    /// <summary>
    /// How Escape key is handled for this window.
    /// </summary>
    public WindowEscapeBehavior EscapeBehavior { get; set; } = WindowEscapeBehavior.Close;

    /// <summary>
    /// Whether this is a modal window.
    /// </summary>
    public bool IsModal { get; set; }

    // Composable title bar nodes
    private HStackNode? _titleBarNode;
    private readonly List<TitleBarIconNode> _leftActionNodes = new();
    private TextBlockNode? _titleTextNode;
    private readonly List<TitleBarIconNode> _rightActionNodes = new();

    /// <summary>
    /// Whether this window is the active (topmost) window.
    /// Set by WindowPanelNode during reconciliation.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// The clip mode for the window's content. Defaults to Clip.
    /// </summary>
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    #region ILayoutProvider Implementation

    /// <summary>
    /// Gets the vertical offset for content (border + optional title bar).
    /// </summary>
    private int ContentYOffset => ShowTitleBar ? 2 : 1;

    /// <summary>
    /// Gets the height taken by chrome (borders + optional title bar).
    /// </summary>
    private int ChromeHeight => ShowTitleBar ? 3 : 2;

    /// <summary>
    /// The clip rectangle for child content (inner area excluding border and title bar).
    /// </summary>
    public Rect ClipRect => new(
        Bounds.X + 1,
        Bounds.Y + ContentYOffset,
        Math.Max(0, Bounds.Width - 2),
        Math.Max(0, Bounds.Height - ChromeHeight)
    );

    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);

    #endregion

    /// <summary>
    /// Rebuilds the composable title bar node structure when actions or title change.
    /// </summary>
    private void RebuildTitleBarNodes()
    {
        if (!ShowTitleBar)
        {
            _titleBarNode = null;
            return;
        }

        // Build left action nodes
        _leftActionNodes.Clear();
        foreach (var action in LeftTitleBarActions)
        {
            var iconNode = new TitleBarIconNode { Action = action, Entry = Entry };
            _leftActionNodes.Add(iconNode);
        }

        // Build title text node (fills remaining space)
        _titleTextNode = new TextBlockNode 
        { 
            Text = Title,
            Overflow = TextOverflow.Ellipsis,
            WidthHint = SizeHint.Fill
        };

        // Build right action nodes
        _rightActionNodes.Clear();
        foreach (var action in RightTitleBarActions)
        {
            var iconNode = new TitleBarIconNode { Action = action, Entry = Entry };
            _rightActionNodes.Add(iconNode);
        }

        // Create HStack with all children: left icons + title + right icons
        var children = new List<Hex1bNode>();
        children.AddRange(_leftActionNodes);
        children.Add(_titleTextNode);
        children.AddRange(_rightActionNodes);
        
        // Add trailing spacer for right actions (1 space padding)
        if (RightTitleBarActions.Count > 0)
        {
            children.Add(new TitleBarSpacerNode(1));
        }

        _titleBarNode = new HStackNode { Children = children };
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        // Windows have fixed size from their entry
        var width = Entry?.Width ?? 40;
        var height = Entry?.Height ?? 15;

        // Ensure minimum size for border + title bar
        width = Math.Max(width, 10);
        height = Math.Max(height, ShowTitleBar ? 5 : 3);

        // Rebuild and measure title bar nodes
        if (ShowTitleBar)
        {
            RebuildTitleBarNodes();
            if (_titleBarNode != null)
            {
                var innerWidth = Math.Max(0, width - 2);
                _titleBarNode.Measure(new Constraints(0, innerWidth, 0, 1));
            }
        }

        // Measure content with inner constraints so it knows its available space
        if (Content != null)
        {
            var innerWidth = Math.Max(0, width - 2);
            var innerHeight = Math.Max(0, height - ChromeHeight);
            Content.Measure(new Constraints(0, innerWidth, 0, innerHeight));
        }

        return constraints.Constrain(new Size(width, height));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.Arrange(bounds);

        // Arrange title bar in the area between borders
        if (ShowTitleBar && _titleBarNode != null)
        {
            var titleBarBounds = new Rect(
                bounds.X + 1,  // After left border
                bounds.Y + 1,  // Title bar is row 1 (row 0 is top border)
                Math.Max(0, bounds.Width - 2),  // Minus left and right borders
                1
            );
            _titleBarNode.Arrange(titleBarBounds);
        }

        // Content gets the inner area (minus border and optional title bar)
        if (Content != null)
        {
            var innerBounds = new Rect(
                bounds.X + 1,
                bounds.Y + ContentYOffset,
                Math.Max(0, bounds.Width - 2),
                Math.Max(0, bounds.Height - ChromeHeight)
            );
            Content.Arrange(innerBounds);
        }
    }

    /// <summary>
    /// WindowNode is focusable to receive clicks on non-content areas (title bar, borders, empty space).
    /// </summary>
    public override bool IsFocusable => true;

    private bool _isFocused;
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                if (value)
                {
                    // When window itself gets focus (click on non-focusable area), bring to front
                    Entry?.BringToFront();
                }
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    /// <summary>
    /// Whether the mouse is currently over this window.
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

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // Return WindowNode first, then children
        // Hit testing returns the LAST match, so children take precedence when clicked directly
        // When clicking on non-focusable areas within window bounds, WindowNode is the match
        yield return this;
        
        // Title bar action nodes are focusable
        if (_titleBarNode != null)
        {
            foreach (var focusable in _titleBarNode.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
        
        if (Content != null)
        {
            foreach (var focusable in Content.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    /// <summary>
    /// When any child of this window receives focus, bring the window to front.
    /// </summary>
    public override void SyncFocusIndex()
    {
        // Check if any child is now focused (not WindowNode itself)
        if (Content != null)
        {
            foreach (var focusable in Content.GetFocusableNodes())
            {
                if (focusable.IsFocused)
                {
                    Entry?.BringToFront();
                    return;
                }
            }
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Escape behavior based on configuration
        bindings.Key(Hex1bKey.Escape).Action(_ =>
        {
            var shouldClose = EscapeBehavior switch
            {
                WindowEscapeBehavior.Close => true,
                WindowEscapeBehavior.CloseNonModal => !IsModal,
                WindowEscapeBehavior.Ignore => false,
                _ => true
            };

            if (shouldClose)
            {
                Entry?.Close();
            }
            return Task.CompletedTask;
        }, "Close window");

        // Mouse click handles button clicks and brings window to front
        bindings.Mouse(Input.MouseButton.Left).Action(ctx =>
        {
            // Check for button clicks first
            var localX = ctx.MouseX - Bounds.X;
            var localY = ctx.MouseY - Bounds.Y;

            var clickedAction = GetClickedAction(localX, localY);
            if (clickedAction.HasValue && Entry != null)
            {
                var (isLeft, actionIndex) = clickedAction.Value;
                var actions = isLeft ? LeftTitleBarActions : RightTitleBarActions;
                if (actionIndex >= 0 && actionIndex < actions.Count)
                {
                    var action = actions[actionIndex];
                    var actionContext = new WindowActionContext(Entry, ctx);
                    action.Handler(actionContext);
                    return Task.CompletedTask;
                }
            }

            // Not a button click - just bring to front
            Entry?.BringToFront();
            return Task.CompletedTask;
        }, "Window interaction");

        // Drag to move/resize window based on where drag starts
        bindings.Drag(Input.MouseButton.Left).Action((startX, startY) =>
        {
            // startX, startY are local coordinates (relative to this node's bounds)
            
            // Check if this is a resize drag (on an edge/corner)
            var resizeEdge = GetResizeEdge(startX, startY);
            if (resizeEdge != ResizeEdge.None)
            {
                return CreateResizeHandler(resizeEdge);
            }

            // Check if this is a title bar drag (for moving)
            if (!IsInTitleBar(startX, startY))
            {
                return new Input.DragHandler(); // Empty handler = reject drag
            }

            // Title bar drag - move the window
            Entry?.BringToFront();

            var startWindowX = Entry?.X ?? Bounds.X;
            var startWindowY = Entry?.Y ?? Bounds.Y;

            return Input.DragHandler.Simple(
                onMove: (deltaX, deltaY) =>
                {
                    if (Entry != null)
                    {
                        var newX = startWindowX + deltaX;
                        var newY = startWindowY + deltaY;
                        Entry.Manager.UpdatePosition(Entry, newX, newY);
                    }
                }
            );
        }, "Drag to move/resize window");
    }

    /// <summary>
    /// Creates a drag handler for resizing the window from a specific edge.
    /// </summary>
    private Input.DragHandler CreateResizeHandler(ResizeEdge edge)
    {
        if (Entry == null)
            return new Input.DragHandler();

        Entry.BringToFront();

        var startWidth = Entry.Width;
        var startHeight = Entry.Height;
        var startX = Entry.X ?? Bounds.X;
        var startY = Entry.Y ?? Bounds.Y;

        return Input.DragHandler.Simple(
            onMove: (deltaX, deltaY) =>
            {
                if (Entry == null)
                    return;

                var newWidth = startWidth;
                var newHeight = startHeight;
                var newX = startX;
                var newY = startY;

                switch (edge)
                {
                    case ResizeEdge.Top:
                        // Dragging top edge: height changes opposite to deltaY, position moves with it
                        newHeight = startHeight - deltaY;
                        newY = startY + deltaY;
                        break;

                    case ResizeEdge.Left:
                        // Dragging left edge: width changes opposite to deltaX, position moves with it
                        newWidth = startWidth - deltaX;
                        newX = startX + deltaX;
                        break;

                    case ResizeEdge.Right:
                        // Dragging right edge: width increases with deltaX
                        newWidth = startWidth + deltaX;
                        break;

                    case ResizeEdge.Bottom:
                        // Dragging bottom edge: height increases with deltaY
                        newHeight = startHeight + deltaY;
                        break;

                    case ResizeEdge.TopLeft:
                        // Combined top and left
                        newWidth = startWidth - deltaX;
                        newX = startX + deltaX;
                        newHeight = startHeight - deltaY;
                        newY = startY + deltaY;
                        break;

                    case ResizeEdge.TopRight:
                        // Combined top and right
                        newWidth = startWidth + deltaX;
                        newHeight = startHeight - deltaY;
                        newY = startY + deltaY;
                        break;

                    case ResizeEdge.BottomLeft:
                        // Combined left and bottom
                        newWidth = startWidth - deltaX;
                        newX = startX + deltaX;
                        newHeight = startHeight + deltaY;
                        break;

                    case ResizeEdge.BottomRight:
                        // Combined right and bottom
                        newWidth = startWidth + deltaX;
                        newHeight = startHeight + deltaY;
                        break;
                }

                // Apply constraints first to determine actual size change
                var constrainedWidth = Math.Max(Entry.MinWidth, newWidth);
                var constrainedHeight = Math.Max(Entry.MinHeight, newHeight);
                
                if (Entry.MaxWidth.HasValue)
                    constrainedWidth = Math.Min(Entry.MaxWidth.Value, constrainedWidth);
                if (Entry.MaxHeight.HasValue)
                    constrainedHeight = Math.Min(Entry.MaxHeight.Value, constrainedHeight);

                // For left edge resize, adjust position to account for size constraints
                if (edge == ResizeEdge.Left || edge == ResizeEdge.BottomLeft || edge == ResizeEdge.TopLeft)
                {
                    var actualWidthDelta = startWidth - constrainedWidth;
                    newX = startX + actualWidthDelta;
                }

                // For top edge resize, adjust position to account for size constraints
                if (edge == ResizeEdge.Top || edge == ResizeEdge.TopLeft || edge == ResizeEdge.TopRight)
                {
                    var actualHeightDelta = startHeight - constrainedHeight;
                    newY = startY + actualHeightDelta;
                }

                Entry.Manager.UpdatePosition(Entry, newX, newY);
                Entry.Manager.UpdateSize(Entry, constrainedWidth, constrainedHeight);
            }
        );
    }

    /// <summary>
    /// Represents a resize edge or corner of the window.
    /// </summary>
    private enum ResizeEdge
    {
        None,
        Top,
        Left,
        Right,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// Result of checking which title bar action was clicked.
    /// </summary>
    private readonly record struct ClickedAction(bool IsLeft, int Index);

    /// <summary>
    /// Determines which title bar action (if any) was clicked at the given local coordinates.
    /// Returns the action location (left or right) and index, or null if no action was clicked.
    /// </summary>
    private ClickedAction? GetClickedAction(int localX, int localY)
    {
        // Buttons are only on the title bar (row 1)
        if (localY != 1 || !ShowTitleBar)
            return null;

        // Check left actions (after left border)
        if (LeftTitleBarActions.Count > 0)
        {
            var leftActionsWidth = GetActionsDisplayWidth(LeftTitleBarActions);
            var leftActionStart = 1; // After left border
            var leftActionEnd = leftActionStart + leftActionsWidth;

            if (localX >= leftActionStart && localX < leftActionEnd)
            {
                // Find which action was clicked by walking through them
                var currentX = leftActionStart;
                for (int i = 0; i < LeftTitleBarActions.Count; i++)
                {
                    var actionWidth = 1 + DisplayWidth.GetStringWidth(LeftTitleBarActions[i].Icon);
                    if (localX >= currentX && localX < currentX + actionWidth)
                        return new ClickedAction(true, i);
                    currentX += actionWidth;
                }
            }
        }

        // Check right actions (before right border)
        if (RightTitleBarActions.Count > 0)
        {
            var rightActionsWidth = GetActionsDisplayWidth(RightTitleBarActions) + 1; // +1 for trailing space
            var rightActionStart = Bounds.Width - 1 - rightActionsWidth;

            if (localX > rightActionStart && localX < Bounds.Width - 1)
            {
                // Find which action was clicked by walking through them
                var currentX = rightActionStart + 1; // +1 to skip first space
                for (int i = 0; i < RightTitleBarActions.Count; i++)
                {
                    var actionWidth = 1 + DisplayWidth.GetStringWidth(RightTitleBarActions[i].Icon);
                    if (localX >= currentX && localX < currentX + actionWidth)
                        return new ClickedAction(false, i);
                    currentX += actionWidth;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if the given local coordinates are in the title bar area (draggable region).
    /// </summary>
    private bool IsInTitleBar(int localX, int localY)
    {
        // No title bar if ShowTitleBar is false
        if (!ShowTitleBar)
            return false;

        // localX/localY are already relative to window bounds (0,0 is top-left of window)
        // Title bar is row 1 (row 0 is top border)
        if (localY != 1)
            return false;

        // Must be within window bounds (excluding border columns)
        if (localX < 1 || localX >= Bounds.Width - 1)
            return false;

        // Exclude left action area
        var leftActionsWidth = GetActionsDisplayWidth(LeftTitleBarActions);
        if (localX < 1 + leftActionsWidth)
            return false;

        // Exclude right action area
        var rightActionsWidth = RightTitleBarActions.Count > 0 ? GetActionsDisplayWidth(RightTitleBarActions) + 1 : 0;
        var buttonStartX = Bounds.Width - 1 - rightActionsWidth;
        if (localX >= buttonStartX)
            return false;

        return true;
    }

    /// <summary>
    /// Calculates the total display width of a collection of window actions.
    /// Each action renders as: space + icon (where icon may be emoji with width > 1).
    /// </summary>
    private static int GetActionsDisplayWidth(IReadOnlyList<WindowAction> actions)
    {
        var width = 0;
        foreach (var action in actions)
        {
            width += 1 + DisplayWidth.GetStringWidth(action.Icon); // space + icon width
        }
        return width;
    }

    /// <summary>
    /// Determines which resize edge (if any) is at the given local coordinates.
    /// Only applies when IsResizable is true.
    /// </summary>
    private ResizeEdge GetResizeEdge(int localX, int localY)
    {
        if (!IsResizable)
            return ResizeEdge.None;

        var width = Bounds.Width;
        var height = Bounds.Height;

        // Top-left corner
        if (localX == 0 && localY == 0)
            return ResizeEdge.TopLeft;

        // Top-right corner
        if (localX == width - 1 && localY == 0)
            return ResizeEdge.TopRight;

        // Bottom-left corner
        if (localX == 0 && localY == height - 1)
            return ResizeEdge.BottomLeft;

        // Bottom-right corner
        if (localX == width - 1 && localY == height - 1)
            return ResizeEdge.BottomRight;

        // Top edge (excluding corners)
        if (localY == 0 && localX > 0 && localX < width - 1)
            return ResizeEdge.Top;

        // Left edge (excluding corners)
        if (localX == 0 && localY > 0 && localY < height - 1)
            return ResizeEdge.Left;

        // Right edge (excluding corners)
        if (localX == width - 1 && localY > 0 && localY < height - 1)
            return ResizeEdge.Right;

        // Bottom edge (excluding corners)
        if (localY == height - 1 && localX > 0 && localX < width - 1)
            return ResizeEdge.Bottom;

        return ResizeEdge.None;
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var x = Bounds.X;
        var y = Bounds.Y;
        var width = Bounds.Width;
        var height = Bounds.Height;

        // Get the parent's clip rect for clipping window chrome
        var clipRect = context.CurrentLayoutProvider?.ClipRect;

        // Helper to check if a row is visible
        bool IsRowVisible(int row) => clipRect == null || 
            (row >= clipRect.Value.Y && row < clipRect.Value.Y + clipRect.Value.Height);

        // Get theme colors based on active state
        var borderColor = IsActive
            ? theme.Get(WindowTheme.BorderActiveColor)
            : theme.Get(WindowTheme.BorderColor);
        var titleBg = IsActive
            ? theme.Get(WindowTheme.TitleBarActiveBackground)
            : theme.Get(WindowTheme.TitleBarBackground);
        var titleFg = IsActive
            ? theme.Get(WindowTheme.TitleBarActiveForeground)
            : theme.Get(WindowTheme.TitleBarForeground);
        var contentBg = theme.Get(WindowTheme.ContentBackground);

        // Border characters
        var topLeft = theme.Get(WindowTheme.TopLeftCorner);
        var topRight = theme.Get(WindowTheme.TopRightCorner);
        var bottomLeft = theme.Get(WindowTheme.BottomLeftCorner);
        var bottomRight = theme.Get(WindowTheme.BottomRightCorner);
        var horizontal = theme.Get(WindowTheme.HorizontalLine);
        var vertical = theme.Get(WindowTheme.VerticalLine);

        // Resize thumb characters
        var thumbHorizontal = theme.Get(WindowTheme.ResizeThumbHorizontal);
        var thumbVertical = theme.Get(WindowTheme.ResizeThumbVertical);
        var thumbTopLeft = theme.Get(WindowTheme.ResizeThumbTopLeft);
        var thumbTopRight = theme.Get(WindowTheme.ResizeThumbTopRight);
        var thumbBottomLeft = theme.Get(WindowTheme.ResizeThumbBottomLeft);
        var thumbBottomRight = theme.Get(WindowTheme.ResizeThumbBottomRight);
        var thumbColor = theme.Get(WindowTheme.ResizeThumbColor);

        var resetToGlobal = theme.GetResetToGlobalCodes();
        var innerWidth = Math.Max(0, width - 2);
        var innerHeight = Math.Max(0, height - 2);

        // Calculate proportional thumb sizes (roughly 1/3 of edge length, minimum 3)
        var hThumbSize = Math.Max(3, Math.Min(innerWidth / 3, 7));
        var vThumbSize = Math.Max(3, Math.Min(innerHeight / 3, 5));

        // Detect hovered resize edge if resizable and hovered
        var hoveredEdge = ResizeEdge.None;
        if (IsResizable && IsHovered && context.MouseX >= 0 && context.MouseY >= 0)
        {
            var localMouseX = context.MouseX - x;
            var localMouseY = context.MouseY - y;
            hoveredEdge = GetResizeEdge(localMouseX, localMouseY);
        }

        // Show all thumbs when any resize edge is hovered
        var showAllThumbs = hoveredEdge != ResizeEdge.None;

        var borderFg = borderColor.ToForegroundAnsi();
        var thumbFg = thumbColor.ToForegroundAnsi();

        // Draw top border with potential thumbs
        if (IsRowVisible(y))
        {
            RenderHorizontalEdge(context, x, y, innerWidth, showAllThumbs, hoveredEdge,
                ResizeEdge.Top, ResizeEdge.TopLeft, ResizeEdge.TopRight,
                topLeft, topRight, horizontal,
                thumbTopLeft, thumbTopRight, thumbHorizontal,
                borderFg, thumbFg, resetToGlobal, hThumbSize);
        }

        // Draw title bar (row below top border) if enabled
        if (height > 1 && ShowTitleBar && IsRowVisible(y + 1))
        {
            RenderComposableTitleBar(context, x, y + 1, innerWidth, borderFg, titleFg, titleBg, vertical, resetToGlobal);
        }

        // Draw content area rows with resize thumbs on hover
        var contentBgCode = contentBg.ToBackgroundAnsi();
        var contentStartRow = ShowTitleBar ? 2 : 1;

        // Calculate vertical thumb range (centered on edge)
        var vThumbStart = contentStartRow + (innerHeight - vThumbSize) / 2;
        var vThumbEnd = vThumbStart + vThumbSize;

        for (int row = contentStartRow; row < height - 1; row++)
        {
            if (!IsRowVisible(y + row)) continue;
            
            // Build the entire row content
            var rowContent = new System.Text.StringBuilder();
            
            // Left border - show thumb when any edge hovered and in thumb range
            var inLeftThumb = showAllThumbs && row >= vThumbStart && row < vThumbEnd;
            if (inLeftThumb)
            {
                rowContent.Append($"{thumbFg}{thumbVertical}{resetToGlobal}");
            }
            else
            {
                rowContent.Append($"{borderFg}{vertical}{resetToGlobal}");
            }
            
            rowContent.Append($"{contentBgCode}{new string(' ', innerWidth)}{resetToGlobal}");
            
            // Right border - show thumb when any edge hovered and in thumb range
            var inRightThumb = showAllThumbs && row >= vThumbStart && row < vThumbEnd;
            if (inRightThumb)
            {
                rowContent.Append($"{thumbFg}{thumbVertical}{resetToGlobal}");
            }
            else
            {
                rowContent.Append($"{borderFg}{vertical}{resetToGlobal}");
            }
            
            context.WriteClipped(x, y + row, rowContent.ToString());
        }

        // Draw bottom border with potential thumbs
        if (height > 1 && IsRowVisible(y + height - 1))
        {
            RenderHorizontalEdge(context, x, y + height - 1, innerWidth, showAllThumbs, hoveredEdge,
                ResizeEdge.Bottom, ResizeEdge.BottomLeft, ResizeEdge.BottomRight,
                bottomLeft, bottomRight, horizontal,
                thumbBottomLeft, thumbBottomRight, thumbHorizontal,
                borderFg, thumbFg, resetToGlobal, hThumbSize);
        }

        // Render child content with this window as the layout provider for clipping
        if (Content != null)
        {
            var previousLayout = context.CurrentLayoutProvider;
            ParentLayoutProvider = previousLayout;
            context.CurrentLayoutProvider = this;

            // Propagate content background so children inherit the window's background
            var previousAmbient = context.AmbientBackground;
            if (!contentBg.IsDefault)
            {
                context.AmbientBackground = contentBg;
            }

            context.RenderChild(Content);

            context.AmbientBackground = previousAmbient;
            context.CurrentLayoutProvider = previousLayout;
            ParentLayoutProvider = null;
        }
    }

    /// <summary>
    /// Renders a horizontal edge (top or bottom) with potential resize thumbs.
    /// </summary>
    private void RenderHorizontalEdge(
        Hex1bRenderContext context,
        int x,
        int y,
        int innerWidth,
        bool showAllThumbs,
        ResizeEdge hoveredEdge,
        ResizeEdge edgeType,
        ResizeEdge leftCornerType,
        ResizeEdge rightCornerType,
        string leftCorner,
        string rightCorner,
        string horizontal,
        string thumbLeftCorner,
        string thumbRightCorner,
        string thumbHorizontal,
        string borderFg,
        string thumbFg,
        string resetToGlobal,
        int thumbSize)
    {
        var sb = new System.Text.StringBuilder();

        // Left corner - show thumb when any edge is hovered
        if (showAllThumbs)
        {
            sb.Append($"{thumbFg}{thumbLeftCorner}{resetToGlobal}");
        }
        else
        {
            sb.Append($"{borderFg}{leftCorner}{resetToGlobal}");
        }

        // Edge content - show thumb when any edge is hovered
        if (showAllThumbs && innerWidth > 0)
        {
            // Show thumb in center of edge
            var leftPad = (innerWidth - thumbSize) / 2;
            var rightPad = innerWidth - thumbSize - leftPad;
            sb.Append($"{borderFg}{new string(horizontal[0], Math.Max(0, leftPad))}");
            sb.Append($"{thumbFg}{new string(thumbHorizontal[0], thumbSize)}{resetToGlobal}");
            sb.Append($"{borderFg}{new string(horizontal[0], Math.Max(0, rightPad))}{resetToGlobal}");
        }
        else
        {
            sb.Append($"{borderFg}{new string(horizontal[0], innerWidth)}{resetToGlobal}");
        }

        // Right corner - show thumb when any edge is hovered
        if (showAllThumbs)
        {
            sb.Append($"{thumbFg}{thumbRightCorner}{resetToGlobal}");
        }
        else
        {
            sb.Append($"{borderFg}{rightCorner}{resetToGlobal}");
        }

        context.WriteClipped(x, y, sb.ToString());
    }

    /// <summary>
    /// Renders the title bar using composable child nodes for proper layout.
    /// </summary>
    private void RenderComposableTitleBar(
        Hex1bRenderContext context,
        int x,
        int titleBarY,
        int innerWidth,
        string borderFg,
        Hex1bColor titleFg,
        Hex1bColor titleBg,
        string vertical,
        string resetToGlobal)
    {
        // Render left border
        context.WriteClipped(x, titleBarY, $"{borderFg}{vertical}{resetToGlobal}");

        // Render the composable title bar content with title bar background
        if (_titleBarNode != null)
        {
            var previousAmbient = context.AmbientBackground;
            context.AmbientBackground = titleBg;
            
            // Fill title bar background first
            var bgCode = titleBg.ToBackgroundAnsi();
            context.WriteClipped(x + 1, titleBarY, $"{bgCode}{new string(' ', innerWidth)}{resetToGlobal}");
            
            // Then render the composable children (they'll inherit the ambient background)
            context.RenderChild(_titleBarNode);
            
            context.AmbientBackground = previousAmbient;
        }

        // Render right border
        context.WriteClipped(x + innerWidth + 1, titleBarY, $"{borderFg}{vertical}{resetToGlobal}");
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (_titleBarNode != null) yield return _titleBarNode;
        if (Content != null) yield return Content;
    }
}

/// <summary>
/// A specialized icon node for title bar actions with spacing.
/// Renders as: space + icon (for proper visual separation).
/// </summary>
internal sealed class TitleBarIconNode : Hex1bNode
{
    /// <summary>
    /// The window action this node represents.
    /// </summary>
    public WindowAction? Action { get; set; }
    
    /// <summary>
    /// The window entry for invoking the action.
    /// </summary>
    public WindowEntry? Entry { get; set; }

    /// <summary>
    /// Measures the size: 1 (space) + icon display width.
    /// </summary>
    protected override Size MeasureCore(Constraints constraints)
    {
        var iconWidth = Action != null ? DisplayWidth.GetStringWidth(Action.Icon) : 0;
        // space + icon
        return constraints.Constrain(new Size(1 + iconWidth, 1));
    }

    /// <summary>
    /// Renders the icon with leading space.
    /// </summary>
    public override void Render(Hex1bRenderContext context)
    {
        if (Action == null) return;
        
        var theme = context.Theme;
        var actionFg = theme.Get(WindowTheme.CloseButtonForeground);
        var resetCodes = theme.GetResetToGlobalCodes();
        
        // Include ambient background so icon has the title bar background
        var bgCode = "";
        if (!context.AmbientBackground.IsDefault)
        {
            bgCode = context.AmbientBackground.ToBackgroundAnsi();
        }
        
        var output = $"{bgCode} {actionFg.ToForegroundAnsi()}{Action.Icon}{resetCodes}";
        context.WriteClipped(Bounds.X, Bounds.Y, output);
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        bindings.Mouse(Input.MouseButton.Left).Action(ctx =>
        {
            if (Action != null && Entry != null)
            {
                var actionContext = new WindowActionContext(Entry, ctx);
                Action.Handler(actionContext);
            }
            return Task.CompletedTask;
        }, "Click action");
    }

    public override bool IsFocusable => true;
}

/// <summary>
/// A simple spacer node for title bar padding.
/// </summary>
internal sealed class TitleBarSpacerNode : Hex1bNode
{
    private readonly int _width;

    public TitleBarSpacerNode(int width)
    {
        _width = width;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        return constraints.Constrain(new Size(_width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Render as spaces with the ambient background
        var bgCode = "";
        if (!context.AmbientBackground.IsDefault)
        {
            bgCode = context.AmbientBackground.ToBackgroundAnsi();
        }
        var resetCodes = context.Theme.GetResetToGlobalCodes();
        context.WriteClipped(Bounds.X, Bounds.Y, $"{bgCode}{new string(' ', _width)}{resetCodes}");
    }
}
