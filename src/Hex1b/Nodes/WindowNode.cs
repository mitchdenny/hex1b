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
    /// The clip rectangle for child content (inner area excluding border and title bar).
    /// </summary>
    public Rect ClipRect => new(
        Bounds.X + 1,
        Bounds.Y + 2, // +1 for border, +1 for title bar
        Math.Max(0, Bounds.Width - 2),
        Math.Max(0, Bounds.Height - 3) // -2 for borders, -1 for title bar
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
        height = Math.Max(height, 5);

        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        // Content gets the inner area (minus border and title bar)
        if (Content != null)
        {
            var innerBounds = new Rect(
                bounds.X + 1,
                bounds.Y + 2, // +1 for top border, +1 for title bar
                Math.Max(0, bounds.Width - 2),
                Math.Max(0, bounds.Height - 3) // -2 for borders, -1 for title bar
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
        // Escape closes the window (unless modal with different behavior)
        bindings.Key(Hex1bKey.Escape).Action(_ =>
        {
            Entry?.Close();
            return Task.CompletedTask;
        }, "Close window");

        // Any mouse click on the window brings it to front
        bindings.Mouse(Input.MouseButton.Left).Action(BringToFront, "Activate window");
    }

    private Task BringToFront(Input.InputBindingActionContext ctx)
    {
        Entry?.BringToFront();
        return Task.CompletedTask;
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
        var closeGlyph = theme.Get(WindowTheme.CloseButtonGlyph);
        var closeFg = theme.Get(WindowTheme.CloseButtonForeground);

        var resetToGlobal = theme.GetResetToGlobalCodes();
        var innerWidth = Math.Max(0, width - 2);

        // Draw top border
        var borderFg = borderColor.ToForegroundAnsi();
        context.SetCursorPosition(x, y);
        context.Write($"{borderFg}{topLeft}{new string(horizontal[0], innerWidth)}{topRight}{resetToGlobal}");

        // Draw title bar (row below top border)
        if (height > 1)
        {
            var titleBarY = y + 1;
            var closeButtonWidth = 3; // " × "
            var availableTitleWidth = Math.Max(0, innerWidth - closeButtonWidth);

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

            // Render title bar: border + title + close button + border
            context.SetCursorPosition(x, titleBarY);
            context.Write($"{borderFg}{vertical}{resetToGlobal}");
            context.Write($"{titleFg.ToForegroundAnsi()}{titleBg.ToBackgroundAnsi()}{paddedTitle}");
            context.Write($" {closeFg.ToForegroundAnsi()}{closeGlyph} ");
            context.Write($"{resetToGlobal}{borderFg}{vertical}{resetToGlobal}");
        }

        // Draw content area rows
        var contentBgCode = contentBg.ToBackgroundAnsi();
        for (int row = 2; row < height - 1; row++)
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

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Content != null) yield return Content;
    }
}
