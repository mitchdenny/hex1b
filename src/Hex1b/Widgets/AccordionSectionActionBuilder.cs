namespace Hex1b.Widgets;

/// <summary>
/// Builder context for creating accordion section header actions.
/// Provides factory methods for common action types.
/// </summary>
public readonly struct AccordionSectionActionBuilder
{
    /// <summary>
    /// Creates a toggle action that changes icon based on expand state and toggles the section on click.
    /// Uses the theme's chevron characters by default.
    /// </summary>
    /// <param name="collapsedIcon">Icon shown when collapsed. Null uses theme default (▸).</param>
    /// <param name="expandedIcon">Icon shown when expanded. Null uses theme default (▾).</param>
    public AccordionSectionAction Toggle(string? collapsedIcon = null, string? expandedIcon = null)
        => new(collapsedIcon ?? "")
        {
            ExpandedIcon = expandedIcon ?? "",
            IsToggle = true,
            ClickHandler = ctx => ctx.Toggle()
        };

    /// <summary>
    /// Creates a simple icon action.
    /// </summary>
    /// <param name="icon">The icon to display.</param>
    public AccordionSectionAction Icon(string icon)
        => new(icon);

    /// <summary>
    /// Creates a collapse action that collapses the section on click.
    /// </summary>
    /// <param name="icon">The icon to display. Defaults to "−".</param>
    public AccordionSectionAction Collapse(string icon = "−")
        => new(icon) { ClickHandler = ctx => ctx.Collapse() };

    /// <summary>
    /// Creates an expand action that expands the section on click.
    /// </summary>
    /// <param name="icon">The icon to display. Defaults to "+".</param>
    public AccordionSectionAction Expand(string icon = "+")
        => new(icon) { ClickHandler = ctx => ctx.Expand() };

    /// <summary>
    /// Creates the default toggle action used when no toggle is provided by the user.
    /// </summary>
    internal static AccordionSectionAction DefaultToggle()
        => new("")
        {
            ExpandedIcon = "",
            IsToggle = true,
            ClickHandler = ctx => ctx.Toggle()
        };
}
