using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Wraps a child widget and removes it from the container's normal layout flow.
/// The float is positioned either at absolute coordinates or relative to an anchor widget.
/// </summary>
/// <remarks>
/// FloatWidget is not reconciled directly — the parent container (VStack, HStack, etc.)
/// detects float children and handles their reconciliation, measurement, and arrangement
/// separately from flow children. Floats render after all flow children to maintain
/// correct z-ordering.
/// </remarks>
/// <example>
/// <code>
/// // Absolute positioning
/// ctx.VStack(v => [
///     v.Text("Normal flow"),
///     v.Float(v.Icon("📍")).Absolute(10, 5),
/// ])
///
/// // Anchor-relative positioning
/// var header = v.Text("Header");
/// ctx.VStack(v => [
///     header,
///     v.Float(v.Text("Tooltip")).AlignRight(header).ExtendBottom(header),
/// ])
/// </code>
/// </example>
/// <param name="Child">The child widget to float out of the layout flow.</param>
public sealed record FloatWidget(Hex1bWidget Child) : Hex1bWidget
{
    /// <summary>Absolute X coordinate within the container. Null if using anchor alignment.</summary>
    internal int? AbsoluteX { get; init; }

    /// <summary>Absolute Y coordinate within the container. Null if using anchor alignment.</summary>
    internal int? AbsoluteY { get; init; }

    /// <summary>The widget to align horizontally relative to.</summary>
    internal Hex1bWidget? HorizontalAnchor { get; init; }

    /// <summary>How to align horizontally relative to the anchor.</summary>
    internal FloatHorizontalAlignment HorizontalAlignment { get; init; }

    /// <summary>Horizontal offset from the computed anchor position.</summary>
    internal int HorizontalOffset { get; init; }

    /// <summary>The widget to align vertically relative to.</summary>
    internal Hex1bWidget? VerticalAnchor { get; init; }

    /// <summary>How to align vertically relative to the anchor.</summary>
    internal FloatVerticalAlignment VerticalAlignment { get; init; }

    /// <summary>Vertical offset from the computed anchor position.</summary>
    internal int VerticalOffset { get; init; }

    /// <summary>
    /// Positions the float at absolute (x, y) character coordinates within the container.
    /// </summary>
    public FloatWidget Absolute(int x, int y) => this with { AbsoluteX = x, AbsoluteY = y };

    /// <summary>Float's left edge aligns with anchor's left edge.</summary>
    public FloatWidget AlignLeft(Hex1bWidget anchor, int offset = 0)
        => this with { HorizontalAnchor = anchor, HorizontalAlignment = FloatHorizontalAlignment.AlignLeft, HorizontalOffset = offset };

    /// <summary>Float's right edge aligns with anchor's right edge.</summary>
    public FloatWidget AlignRight(Hex1bWidget anchor, int offset = 0)
        => this with { HorizontalAnchor = anchor, HorizontalAlignment = FloatHorizontalAlignment.AlignRight, HorizontalOffset = offset };

    /// <summary>Float's left edge aligns with anchor's right edge (place beside, to the right).</summary>
    public FloatWidget ExtendRight(Hex1bWidget anchor, int offset = 0)
        => this with { HorizontalAnchor = anchor, HorizontalAlignment = FloatHorizontalAlignment.ExtendRight, HorizontalOffset = offset };

    /// <summary>Float's right edge aligns with anchor's left edge (place beside, to the left).</summary>
    public FloatWidget ExtendLeft(Hex1bWidget anchor, int offset = 0)
        => this with { HorizontalAnchor = anchor, HorizontalAlignment = FloatHorizontalAlignment.ExtendLeft, HorizontalOffset = offset };

    /// <summary>Float's top edge aligns with anchor's top edge.</summary>
    public FloatWidget AlignTop(Hex1bWidget anchor, int offset = 0)
        => this with { VerticalAnchor = anchor, VerticalAlignment = FloatVerticalAlignment.AlignTop, VerticalOffset = offset };

    /// <summary>Float's bottom edge aligns with anchor's bottom edge.</summary>
    public FloatWidget AlignBottom(Hex1bWidget anchor, int offset = 0)
        => this with { VerticalAnchor = anchor, VerticalAlignment = FloatVerticalAlignment.AlignBottom, VerticalOffset = offset };

    /// <summary>Float's top edge aligns with anchor's bottom edge (place below).</summary>
    public FloatWidget ExtendBottom(Hex1bWidget anchor, int offset = 0)
        => this with { VerticalAnchor = anchor, VerticalAlignment = FloatVerticalAlignment.ExtendBottom, VerticalOffset = offset };

    /// <summary>Float's bottom edge aligns with anchor's top edge (place above).</summary>
    public FloatWidget ExtendTop(Hex1bWidget anchor, int offset = 0)
        => this with { VerticalAnchor = anchor, VerticalAlignment = FloatVerticalAlignment.ExtendTop, VerticalOffset = offset };

    /// <summary>
    /// FloatWidget is not reconciled directly — the parent container handles it.
    /// </summary>
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        // This should not be called directly — parent containers detect FloatWidget
        // children and reconcile them via FloatLayoutHelper.
        throw new InvalidOperationException(
            "FloatWidget must be a direct child of a container that implements IFloatWidgetContainer (VStack, HStack, ZStack, Grid).");
    }

    internal override Type GetExpectedNodeType() => typeof(Hex1bNode);
}
