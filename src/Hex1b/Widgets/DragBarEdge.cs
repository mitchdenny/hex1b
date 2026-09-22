using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Specifies which edge of a DragBarPanel the resize handle appears on.
/// </summary>
public enum DragBarEdge
{
    /// <summary>Handle on the left edge (drag left to grow, right to shrink).</summary>
    Left,
    /// <summary>Handle on the right edge (drag right to grow, left to shrink).</summary>
    Right,
    /// <summary>Handle on the top edge (drag up to grow, down to shrink).</summary>
    Top,
    /// <summary>Handle on the bottom edge (drag down to grow, up to shrink).</summary>
    Bottom
}
