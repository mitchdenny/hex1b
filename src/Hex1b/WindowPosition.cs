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
