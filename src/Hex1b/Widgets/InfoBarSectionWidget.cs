using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Alignment options for content within an info bar section.
/// </summary>
public enum InfoBarSectionAlignment
{
    /// <summary>
    /// Align content to the left (default).
    /// </summary>
    Left,
    
    /// <summary>
    /// Center content within the section.
    /// </summary>
    Center,
    
    /// <summary>
    /// Align content to the right.
    /// </summary>
    Right
}

/// <summary>
/// A section within an info bar that contains content (text or widgets).
/// Sections can have custom colors, width hints, and alignment.
/// </summary>
/// <param name="Content">The widget content to display in this section.</param>
/// <param name="Foreground">Optional foreground color override.</param>
/// <param name="Background">Optional background color override.</param>
public sealed record InfoBarSectionWidget(
    Hex1bWidget Content,
    Hex1bColor? Foreground = null,
    Hex1bColor? Background = null) : IInfoBarChild
{
    /// <summary>
    /// The alignment of content within this section.
    /// </summary>
    public InfoBarSectionAlignment Alignment { get; init; } = InfoBarSectionAlignment.Left;

    /// <summary>
    /// Optional width hint for layout.
    /// </summary>
    public SizeHint? WidthHint { get; init; }

    /// <summary>
    /// Optional theme customization for this section.
    /// </summary>
    public Func<Hex1bTheme, Hex1bTheme>? ThemeMutator { get; init; }

    /// <summary>
    /// Sets this section to a fixed width.
    /// </summary>
    /// <param name="width">The fixed width in characters.</param>
    /// <returns>A new section with fixed width.</returns>
    public InfoBarSectionWidget FixedWidth(int width)
        => this with { WidthHint = SizeHint.Fixed(width) };

    /// <summary>
    /// Sets this section to fill available width with an optional weight.
    /// </summary>
    /// <param name="weight">The fill weight (higher values take more space).</param>
    /// <returns>A new section with fill width.</returns>
    public InfoBarSectionWidget FillWidth(int weight = 1)
        => this with { WidthHint = SizeHint.Weighted(weight) };

    /// <summary>
    /// Sets this section to size to its content (default behavior).
    /// </summary>
    /// <returns>A new section with content width.</returns>
    public InfoBarSectionWidget ContentWidth()
        => this with { WidthHint = SizeHint.Content };

    /// <summary>
    /// Aligns content to the left within this section.
    /// </summary>
    /// <returns>A new section with left alignment.</returns>
    public InfoBarSectionWidget AlignLeft()
        => this with { Alignment = InfoBarSectionAlignment.Left };

    /// <summary>
    /// Centers content within this section.
    /// </summary>
    /// <returns>A new section with center alignment.</returns>
    public InfoBarSectionWidget AlignCenter()
        => this with { Alignment = InfoBarSectionAlignment.Center };

    /// <summary>
    /// Aligns content to the right within this section.
    /// </summary>
    /// <returns>A new section with right alignment.</returns>
    public InfoBarSectionWidget AlignRight()
        => this with { Alignment = InfoBarSectionAlignment.Right };

    /// <summary>
    /// Applies custom theme settings to this section.
    /// </summary>
    /// <param name="mutator">A function that customizes the theme for this section.</param>
    /// <returns>A new section with the theme customization.</returns>
    public InfoBarSectionWidget Theme(Func<Hex1bTheme, Hex1bTheme> mutator)
        => this with { ThemeMutator = mutator };

    /// <summary>
    /// Builds the widget tree for this section.
    /// </summary>
    internal Hex1bWidget Build()
    {
        // Start with content, apply width hint
        var widget = Content;
        if (WidthHint.HasValue)
        {
            widget = widget with { WidthHint = WidthHint };
        }

        // TODO: Handle Foreground/Background via theme if set
        // For now, Foreground/Background are handled via ThemeMutator or InfoBar's default theme

        // Wrap in ThemePanel if theme customization is present
        if (ThemeMutator != null)
        {
            widget = new ThemePanelWidget(ThemeMutator, widget);
        }

        return widget;
    }
}
