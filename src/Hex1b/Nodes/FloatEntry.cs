using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Represents a reconciled float child with its positioning information.
/// </summary>
public sealed class FloatEntry
{
    /// <summary>The reconciled child node.</summary>
    public required Hex1bNode Node { get; set; }

    /// <summary>Absolute X. Null if using anchor alignment.</summary>
    public int? AbsoluteX { get; set; }

    /// <summary>Absolute Y. Null if using anchor alignment.</summary>
    public int? AbsoluteY { get; set; }

    /// <summary>The anchor node for horizontal alignment.</summary>
    public Hex1bNode? HorizontalAnchor { get; set; }

    /// <summary>How to align horizontally.</summary>
    public FloatHorizontalAlignment HorizontalAlignment { get; set; }

    /// <summary>Horizontal offset.</summary>
    public int HorizontalOffset { get; set; }

    /// <summary>The anchor node for vertical alignment.</summary>
    public Hex1bNode? VerticalAnchor { get; set; }

    /// <summary>How to align vertically.</summary>
    public FloatVerticalAlignment VerticalAlignment { get; set; }

    /// <summary>Vertical offset.</summary>
    public int VerticalOffset { get; set; }

    /// <summary>Unresolved anchor widget reference for horizontal alignment (resolved after flow reconciliation).</summary>
    internal Hex1bWidget? HorizontalAnchorWidget { get; set; }

    /// <summary>Unresolved anchor widget reference for vertical alignment (resolved after flow reconciliation).</summary>
    internal Hex1bWidget? VerticalAnchorWidget { get; set; }
}
