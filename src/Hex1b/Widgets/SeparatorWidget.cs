using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A separator widget that draws a horizontal or vertical line.
/// When placed in a VStack, it draws a horizontal line.
/// When placed in an HStack, it draws a vertical line.
/// The axis can also be set explicitly.
/// Customize appearance using <see cref="Theming.SeparatorTheme"/> via ThemePanel.
/// </summary>
public sealed record SeparatorWidget : Hex1bWidget
{
    /// <summary>
    /// Optional explicit axis. If null, the axis is inferred from the parent container.
    /// </summary>
    public LayoutAxis? ExplicitAxis { get; init; }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SeparatorNode ?? new SeparatorNode();
        node.ExplicitAxis = ExplicitAxis;
        node.InferredAxis = context.LayoutAxis;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(SeparatorNode);
}
