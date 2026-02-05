namespace Hex1b;

/// <summary>
/// Specifies how a window should be initially positioned within its container.
/// </summary>
public enum WindowPosition
{
    /// <summary>
    /// Center the window in the container. This is the default.
    /// </summary>
    Center,

    /// <summary>
    /// Position at the top-left corner.
    /// </summary>
    TopLeft,

    /// <summary>
    /// Position at the top-right corner.
    /// </summary>
    TopRight,

    /// <summary>
    /// Position at the bottom-left corner.
    /// </summary>
    BottomLeft,

    /// <summary>
    /// Position at the bottom-right corner.
    /// </summary>
    BottomRight,

    /// <summary>
    /// Center horizontally, align to top.
    /// </summary>
    CenterTop,

    /// <summary>
    /// Center horizontally, align to bottom.
    /// </summary>
    CenterBottom,

    /// <summary>
    /// Center vertically, align to left.
    /// </summary>
    CenterLeft,

    /// <summary>
    /// Center vertically, align to right.
    /// </summary>
    CenterRight,

    /// <summary>
    /// Use explicit X, Y coordinates (specified separately).
    /// </summary>
    Absolute
}

/// <summary>
/// Specifies window position with optional offset.
/// </summary>
/// <param name="Position">The base positioning strategy.</param>
/// <param name="OffsetX">Horizontal offset from the calculated position (positive = right).</param>
/// <param name="OffsetY">Vertical offset from the calculated position (positive = down).</param>
public readonly record struct WindowPositionSpec(
    WindowPosition Position = WindowPosition.Center,
    int OffsetX = 0,
    int OffsetY = 0)
{
    /// <summary>
    /// Creates a centered position.
    /// </summary>
    public static WindowPositionSpec Center => new(WindowPosition.Center);

    /// <summary>
    /// Creates a top-left position.
    /// </summary>
    public static WindowPositionSpec TopLeft => new(WindowPosition.TopLeft);

    /// <summary>
    /// Creates a top-right position.
    /// </summary>
    public static WindowPositionSpec TopRight => new(WindowPosition.TopRight);

    /// <summary>
    /// Creates a bottom-left position.
    /// </summary>
    public static WindowPositionSpec BottomLeft => new(WindowPosition.BottomLeft);

    /// <summary>
    /// Creates a bottom-right position.
    /// </summary>
    public static WindowPositionSpec BottomRight => new(WindowPosition.BottomRight);

    /// <summary>
    /// Creates a center-top position.
    /// </summary>
    public static WindowPositionSpec CenterTop => new(WindowPosition.CenterTop);

    /// <summary>
    /// Creates a center-bottom position.
    /// </summary>
    public static WindowPositionSpec CenterBottom => new(WindowPosition.CenterBottom);

    /// <summary>
    /// Creates a position with an offset from center.
    /// </summary>
    public static WindowPositionSpec CenterWithOffset(int offsetX, int offsetY) 
        => new(WindowPosition.Center, offsetX, offsetY);

    /// <summary>
    /// Calculates the actual X, Y position within the given bounds.
    /// </summary>
    /// <param name="panelBounds">The bounds of the containing panel.</param>
    /// <param name="windowWidth">The width of the window.</param>
    /// <param name="windowHeight">The height of the window.</param>
    /// <param name="absoluteX">Explicit X position when Position is Absolute.</param>
    /// <param name="absoluteY">Explicit Y position when Position is Absolute.</param>
    /// <returns>The calculated (x, y) position, clamped to panel bounds.</returns>
    public (int x, int y) Calculate(
        Layout.Rect panelBounds, 
        int windowWidth, 
        int windowHeight,
        int? absoluteX = null,
        int? absoluteY = null)
    {
        int x, y;

        switch (Position)
        {
            case WindowPosition.TopLeft:
                x = panelBounds.X;
                y = panelBounds.Y;
                break;

            case WindowPosition.TopRight:
                x = panelBounds.X + panelBounds.Width - windowWidth;
                y = panelBounds.Y;
                break;

            case WindowPosition.BottomLeft:
                x = panelBounds.X;
                y = panelBounds.Y + panelBounds.Height - windowHeight;
                break;

            case WindowPosition.BottomRight:
                x = panelBounds.X + panelBounds.Width - windowWidth;
                y = panelBounds.Y + panelBounds.Height - windowHeight;
                break;

            case WindowPosition.CenterTop:
                x = panelBounds.X + (panelBounds.Width - windowWidth) / 2;
                y = panelBounds.Y;
                break;

            case WindowPosition.CenterBottom:
                x = panelBounds.X + (panelBounds.Width - windowWidth) / 2;
                y = panelBounds.Y + panelBounds.Height - windowHeight;
                break;

            case WindowPosition.CenterLeft:
                x = panelBounds.X;
                y = panelBounds.Y + (panelBounds.Height - windowHeight) / 2;
                break;

            case WindowPosition.CenterRight:
                x = panelBounds.X + panelBounds.Width - windowWidth;
                y = panelBounds.Y + (panelBounds.Height - windowHeight) / 2;
                break;

            case WindowPosition.Absolute:
                x = absoluteX ?? panelBounds.X;
                y = absoluteY ?? panelBounds.Y;
                break;

            case WindowPosition.Center:
            default:
                x = panelBounds.X + (panelBounds.Width - windowWidth) / 2;
                y = panelBounds.Y + (panelBounds.Height - windowHeight) / 2;
                break;
        }

        // Apply offsets
        x += OffsetX;
        y += OffsetY;

        // Clamp to panel bounds
        x = Math.Max(panelBounds.X, Math.Min(x, panelBounds.X + panelBounds.Width - windowWidth));
        y = Math.Max(panelBounds.Y, Math.Min(y, panelBounds.Y + panelBounds.Height - windowHeight));

        return (x, y);
    }
}
