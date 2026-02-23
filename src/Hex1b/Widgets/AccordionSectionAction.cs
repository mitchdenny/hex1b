namespace Hex1b.Widgets;

/// <summary>
/// Represents an action icon displayed in an accordion section header.
/// Can be a simple icon or a toggle that changes appearance based on expand state.
/// </summary>
public sealed record AccordionSectionAction
{
    /// <summary>
    /// The icon displayed when the section is collapsed (or the only icon for non-toggle actions).
    /// </summary>
    public string Icon { get; }

    /// <summary>
    /// Optional icon displayed when the section is expanded. Only used for toggle actions.
    /// </summary>
    public string? ExpandedIcon { get; init; }

    /// <summary>
    /// Whether this action is a toggle that changes icon based on section expand state.
    /// </summary>
    public bool IsToggle { get; init; }

    /// <summary>
    /// Click handler for this action.
    /// </summary>
    internal Action<AccordionSectionActionContext>? ClickHandler { get; init; }

    /// <summary>
    /// Creates a new section action with the specified icon.
    /// </summary>
    public AccordionSectionAction(string icon)
    {
        Icon = icon;
    }

    /// <summary>
    /// Attaches a click handler to this action.
    /// </summary>
    public AccordionSectionAction OnClick(Action<AccordionSectionActionContext> handler)
        => this with { ClickHandler = handler };
}
