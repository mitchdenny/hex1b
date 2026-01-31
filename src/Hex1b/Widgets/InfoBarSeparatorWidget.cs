using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A visual separator between info bar sections.
/// Displays a character or string to divide sections.
/// </summary>
/// <param name="Character">The separator character(s) to display. Defaults to "|".</param>
/// <param name="Foreground">Optional foreground color override.</param>
/// <param name="Background">Optional background color override.</param>
public sealed record InfoBarSeparatorWidget(
    string Character = "|",
    Hex1bColor? Foreground = null,
    Hex1bColor? Background = null) : IInfoBarChild
{
    /// <summary>
    /// Optional theme customization for this separator.
    /// </summary>
    public Func<Hex1bTheme, Hex1bTheme>? ThemeMutator { get; init; }

    /// <summary>
    /// Applies custom theme settings to this separator.
    /// </summary>
    /// <param name="mutator">A function that customizes the theme for this separator.</param>
    /// <returns>A new separator with the theme customization.</returns>
    public InfoBarSeparatorWidget Theme(Func<Hex1bTheme, Hex1bTheme> mutator)
        => this with { ThemeMutator = mutator };

    /// <summary>
    /// Builds the widget tree for this separator.
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
