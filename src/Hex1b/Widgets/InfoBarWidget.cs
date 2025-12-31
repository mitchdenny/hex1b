using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A one-line status bar widget, typically placed at the bottom of the screen.
/// By default, renders with inverted colors from the theme.
/// </summary>
/// <param name="Sections">The sections to display in the info bar.</param>
/// <param name="InvertColors">Whether to invert foreground/background colors (default: true).</param>
public sealed record InfoBarWidget(
    IReadOnlyList<InfoBarSection> Sections,
    bool InvertColors = true) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as InfoBarNode ?? new InfoBarNode();
        node.Sections = Sections;
        node.InvertColors = InvertColors;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(InfoBarNode);
}
