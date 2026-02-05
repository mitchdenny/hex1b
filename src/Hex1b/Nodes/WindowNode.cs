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
    /// The chrome style for this window.
    /// </summary>
    public WindowChromeStyle ChromeStyle { get; set; } = WindowChromeStyle.TitleAndClose;

    /// <summary>
    /// How Escape key is handled for this window.
    /// </summary>
    public WindowEscapeBehavior EscapeBehavior { get; set; } = WindowEscapeBehavior.Close;

    /// <summary>
    /// Whether this is a modal window.
    /// </summary>
    public bool IsModal { get; set; }

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
    private int ContentYOffset => ChromeStyle == WindowChromeStyle.None ? 1 : 2;

    /// <summary>
    /// Gets the height taken by chrome (borders + optional title bar).
    /// </summary>
    private int ChromeHeight => ChromeStyle == WindowChromeStyle.None ? 2 : 3;

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

    public override Size Measure(Constraints constraints)
    {
        // Windows have fixed size from their entry
        var width = Entry?.Width ?? 40;
        var height = Entry?.Height ?? 15;

        // Ensure minimum size for border + title bar
        width = Math.Max(width, 10);
        height = Math.Max(height, ChromeStyle == WindowChromeStyle.None ? 3 : 5);

        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

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

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // Return WindowNode first, then children
        // Hit testing returns the LAST match, so children take precedence when clicked directly
        // When clicking on non-focusable areas within window bounds, WindowNode is the match
        yield return this;
        
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

            var clickedButton = GetClickedButton(localX, localY);
            switch (clickedButton)
            {
                case TitleBarButton.Close:
                    Entry?.Close();
                    return Task.CompletedTask;
                case TitleBarButton.Minimize:
                    Entry?.Minimize();
                    return Task.CompletedTask;
                case TitleBarButton.Maximize:
                    Entry?.ToggleMaximize();
                    return Task.CompletedTask;
            }

            // Not a button click - just bring to front
            Entry?.BringToFront();
            return Task.CompletedTask;
        }, "Window interaction");

        // Drag to move window (only from title bar)
        bindings.Drag(Input.MouseButton.Left).Action((startX, startY) =>
        {
            // Only allow drag from title bar area (row 1, excluding close button area)
            // Title bar is at Y = Bounds.Y + 1 (row below top border)
            if (!IsInTitleBar(startX, startY))
            {
                return new Input.DragHandler(); // Empty handler = reject drag
            }

            // Bring window to front when starting drag
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
        }, "Drag to move window");
    }

    private enum TitleBarButton { None, Close, Minimize, Maximize }

    /// <summary>
    /// Determines which title bar button (if any) was clicked at the given local coordinates.
    /// </summary>
    private TitleBarButton GetClickedButton(int localX, int localY)
    {
        // Buttons are only on the title bar (row 1)
        if (localY != 1)
            return TitleBarButton.None;

        // No buttons if chrome style doesn't include them
        if (ChromeStyle == WindowChromeStyle.None || ChromeStyle == WindowChromeStyle.TitleOnly)
            return TitleBarButton.None;

        // Button layout from right to left: " × " for close, " □ " for max, " − " for min
        // Each button is 2 chars wide (space + glyph), with trailing space
        var innerWidth = Bounds.Width - 2; // Exclude border columns

        if (ChromeStyle == WindowChromeStyle.TitleAndClose)
        {
            // Close button: positions innerWidth-3 to innerWidth-1 (relative to inner area)
            // In local coords (including left border): Bounds.Width - 4 to Bounds.Width - 2
            if (localX >= Bounds.Width - 4 && localX < Bounds.Width - 1)
                return TitleBarButton.Close;
        }
        else if (ChromeStyle == WindowChromeStyle.Full)
        {
            // Buttons from right: " − □ × " = 7 chars total
            // Close: last 2 chars before border
            // Maximize: 2 chars before close
            // Minimize: 2 chars before maximize
            var buttonAreaStart = Bounds.Width - 1 - 7; // Start of button area

            if (localX >= Bounds.Width - 4 && localX < Bounds.Width - 1)
                return TitleBarButton.Close;
            if (localX >= Bounds.Width - 6 && localX < Bounds.Width - 4)
                return TitleBarButton.Maximize;
            if (localX >= Bounds.Width - 8 && localX < Bounds.Width - 6)
                return TitleBarButton.Minimize;
        }

        return TitleBarButton.None;
    }

    /// <summary>
    /// Checks if the given local coordinates are in the title bar area.
    /// </summary>
    private bool IsInTitleBar(int localX, int localY)
    {
        // No title bar if chrome style is None
        if (ChromeStyle == WindowChromeStyle.None)
            return false;

        // localX/localY are already relative to window bounds (0,0 is top-left of window)
        // Title bar is row 1 (row 0 is top border)
        if (localY != 1)
            return false;

        // Must be within window bounds (excluding border columns)
        if (localX < 1 || localX >= Bounds.Width - 1)
            return false;

        // Exclude button area on the right side
        var buttonsWidth = ChromeStyle switch
        {
            WindowChromeStyle.Full => 7, // " − □ × "
            WindowChromeStyle.TitleAndClose => 3, // " × "
            _ => 0
        };

        var buttonStartX = Bounds.Width - 1 - buttonsWidth;
        if (localX >= buttonStartX)
            return false;

        return true;
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var x = Bounds.X;
        var y = Bounds.Y;
        var width = Bounds.Width;
        var height = Bounds.Height;

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

        var resetToGlobal = theme.GetResetToGlobalCodes();
        var innerWidth = Math.Max(0, width - 2);

        // Draw top border
        var borderFg = borderColor.ToForegroundAnsi();
        context.SetCursorPosition(x, y);
        context.Write($"{borderFg}{topLeft}{new string(horizontal[0], innerWidth)}{topRight}{resetToGlobal}");

        // Draw title bar (row below top border) based on chrome style
        if (height > 1 && ChromeStyle != WindowChromeStyle.None)
        {
            RenderTitleBar(context, theme, x, y + 1, innerWidth, borderFg, titleFg, titleBg, vertical, resetToGlobal);
        }

        // Draw content area rows
        var contentBgCode = contentBg.ToBackgroundAnsi();
        var contentStartRow = ChromeStyle == WindowChromeStyle.None ? 1 : 2;
        for (int row = contentStartRow; row < height - 1; row++)
        {
            context.SetCursorPosition(x, y + row);
            context.Write($"{borderFg}{vertical}{resetToGlobal}");
            context.Write($"{contentBgCode}{new string(' ', innerWidth)}{resetToGlobal}");
            context.SetCursorPosition(x + width - 1, y + row);
            context.Write($"{borderFg}{vertical}{resetToGlobal}");
        }

        // Draw bottom border
        if (height > 1)
        {
            context.SetCursorPosition(x, y + height - 1);
            context.Write($"{borderFg}{bottomLeft}{new string(horizontal[0], innerWidth)}{bottomRight}{resetToGlobal}");
        }

        // Render child content with this window as the layout provider for clipping
        if (Content != null)
        {
            var previousLayout = context.CurrentLayoutProvider;
            ParentLayoutProvider = previousLayout;
            context.CurrentLayoutProvider = this;

            context.RenderChild(Content);

            context.CurrentLayoutProvider = previousLayout;
            ParentLayoutProvider = null;
        }
    }

    private void RenderTitleBar(
        Hex1bRenderContext context,
        Hex1bTheme theme,
        int x,
        int titleBarY,
        int innerWidth,
        string borderFg,
        Hex1bColor titleFg,
        Hex1bColor titleBg,
        string vertical,
        string resetToGlobal)
    {
        // Calculate buttons width based on chrome style
        var buttonsBuilder = new System.Text.StringBuilder();
        var buttonsWidth = 0;

        if (ChromeStyle == WindowChromeStyle.Full)
        {
            // Minimize button
            var minGlyph = theme.Get(WindowTheme.MinimizeButtonGlyph);
            var minFg = theme.Get(WindowTheme.MinimizeButtonForeground);
            buttonsBuilder.Append($" {minFg.ToForegroundAnsi()}{minGlyph}");
            buttonsWidth += 2;

            // Maximize/Restore button
            var maxGlyph = Entry?.State == WindowState.Maximized
                ? theme.Get(WindowTheme.RestoreButtonGlyph)
                : theme.Get(WindowTheme.MaximizeButtonGlyph);
            var maxFg = theme.Get(WindowTheme.MaximizeButtonForeground);
            buttonsBuilder.Append($" {maxFg.ToForegroundAnsi()}{maxGlyph}");
            buttonsWidth += 2;
        }

        if (ChromeStyle is WindowChromeStyle.TitleAndClose or WindowChromeStyle.Full)
        {
            // Close button
            var closeGlyph = theme.Get(WindowTheme.CloseButtonGlyph);
            var closeFg = theme.Get(WindowTheme.CloseButtonForeground);
            buttonsBuilder.Append($" {closeFg.ToForegroundAnsi()}{closeGlyph}");
            buttonsWidth += 2;
        }

        // Add trailing space if we have buttons
        if (buttonsWidth > 0)
        {
            buttonsBuilder.Append(' ');
            buttonsWidth += 1;
        }

        var availableTitleWidth = Math.Max(0, innerWidth - buttonsWidth);

        // Truncate title if needed
        var displayTitle = Title;
        if (DisplayWidth.GetStringWidth(displayTitle) > availableTitleWidth)
        {
            var (sliced, _, _, _) = DisplayWidth.SliceByDisplayWidth(displayTitle, 0, availableTitleWidth - 1);
            displayTitle = sliced + "…";
        }

        // Pad title to fill available space
        var titleDisplayWidth = DisplayWidth.GetStringWidth(displayTitle);
        var padding = availableTitleWidth - titleDisplayWidth;
        var paddedTitle = displayTitle + new string(' ', Math.Max(0, padding));

        // Render title bar: border + title + buttons + border
        context.SetCursorPosition(x, titleBarY);
        context.Write($"{borderFg}{vertical}{resetToGlobal}");
        context.Write($"{titleFg.ToForegroundAnsi()}{titleBg.ToBackgroundAnsi()}{paddedTitle}");
        context.Write(buttonsBuilder.ToString());
        context.Write($"{resetToGlobal}{borderFg}{vertical}{resetToGlobal}");
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Content != null) yield return Content;
    }
}
