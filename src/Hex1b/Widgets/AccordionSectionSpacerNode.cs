using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

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
