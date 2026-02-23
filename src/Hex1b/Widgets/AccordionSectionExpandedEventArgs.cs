namespace Hex1b.Widgets;

/// <summary>
/// Event arguments for accordion section expand/collapse changes.
/// </summary>
public sealed class AccordionSectionExpandedEventArgs : EventArgs
{
    /// <summary>
    /// The index of the section that changed.
    /// </summary>
    public int SectionIndex { get; init; }

    /// <summary>
    /// Whether the section is now expanded.
    /// </summary>
    public bool IsExpanded { get; init; }

    /// <summary>
    /// The title of the section that changed.
    /// </summary>
    public string SectionTitle { get; init; } = "";
}
