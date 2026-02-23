using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Internal spacer widget injected into accordion section content to fill remaining vertical space.
/// This ensures expanded sections fill their allocated height within the accordion.
/// </summary>
internal sealed record AccordionSectionSpacerWidget() : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as AccordionSectionSpacerNode ?? new AccordionSectionSpacerNode();
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(AccordionSectionSpacerNode);
}

/// <summary>
/// Internal spacer node that fills remaining vertical space in an accordion section.
/// </summary>
internal sealed class AccordionSectionSpacerNode : Hex1bNode
{
    protected override Size MeasureCore(Constraints constraints)
    {
        // Fill available space
        return new Size(constraints.MaxWidth, constraints.MaxHeight < int.MaxValue ? constraints.MaxHeight : 0);
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Nothing to render — just occupies space
    }
}
