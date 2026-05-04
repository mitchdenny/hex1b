using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A visual divider between info bar sections.
/// Displays a character or string to divide sections (distinct from a <see cref="InfoBarSpacerWidget"/>,
/// which is a flexible gap that pushes sections apart).
/// </summary>
/// <param name="Character">The divider character(s) to display. Defaults to "|".</param>
/// <param name="Foreground">Optional foreground color override.</param>
/// <param name="Background">Optional background color override.</param>
public sealed record InfoBarDividerWidget(
    string Character = "|",
    Hex1bColor? Foreground = null,
    Hex1bColor? Background = null) : IInfoBarChild
{
    /// <summary>
    /// Optional theme customization for this divider.
    /// </summary>
    public Func<Hex1bTheme, Hex1bTheme>? ThemeMutator { get; init; }

    /// <summary>
    /// Applies custom theme settings to this divider.
    /// </summary>
    /// <param name="mutator">A function that customizes the theme for this divider.</param>
    /// <returns>A new divider with the theme customization.</returns>
    public InfoBarDividerWidget Theme(Func<Hex1bTheme, Hex1bTheme> mutator)
        => this with { ThemeMutator = mutator };

    /// <summary>
    /// Builds the widget tree for this divider.
    /// </summary>
    internal Hex1bWidget Build()
    {
        Hex1bWidget widget = new TextBlockWidget(Character);

        // Wrap in ThemePanel if theme customization is present
        if (ThemeMutator != null)
        {
            widget = new ThemePanelWidget(ThemeMutator, widget);
        }

        return widget;
    }
}
