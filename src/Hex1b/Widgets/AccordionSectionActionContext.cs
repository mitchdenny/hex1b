using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Provides methods to programmatically control the expand/collapse state of an accordion section.
/// Accessible from icon click handlers via the accordion node.
/// </summary>
public sealed class AccordionSectionActionContext
{
    private readonly AccordionNode _node;
    private readonly int _sectionIndex;

    internal AccordionSectionActionContext(AccordionNode node, int sectionIndex)
    {
        _node = node;
        _sectionIndex = sectionIndex;
    }

    /// <summary>
    /// Gets whether this section is currently expanded.
    /// </summary>
    public bool IsExpanded => _node.IsSectionExpanded(_sectionIndex);

    /// <summary>
    /// Collapses this section.
    /// </summary>
    public void Collapse() => _node.SetSectionExpanded(_sectionIndex, false);

    /// <summary>
    /// Expands this section.
    /// </summary>
    public void Expand() => _node.SetSectionExpanded(_sectionIndex, true);

    /// <summary>
    /// Toggles the expand/collapse state of this section.
    /// </summary>
    public void Toggle() => _node.ToggleSection(_sectionIndex);
}
