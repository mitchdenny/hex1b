using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Specifies the primary layout direction for a <see cref="WrapPanelWidget"/>.
/// </summary>
public enum WrapOrientation
{
    /// <summary>
    /// Children flow left-to-right and wrap to the next row when the width is exceeded.
    /// </summary>
    Horizontal,

    /// <summary>
    /// Children flow top-to-bottom and wrap to the next column when the height is exceeded.
    /// </summary>
    Vertical,
}
