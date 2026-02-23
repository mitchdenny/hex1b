namespace Hex1b.Theming;

/// <summary>
/// Theme elements for AccordionWidget.
/// </summary>
public static class AccordionTheme
{
    /// <summary>
    /// Foreground color for section headers.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HeaderForegroundColor =
        new($"{nameof(AccordionTheme)}.{nameof(HeaderForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Background color for section headers.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HeaderBackgroundColor =
        new($"{nameof(AccordionTheme)}.{nameof(HeaderBackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Foreground color for focused section headers.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedHeaderForegroundColor =
        new($"{nameof(AccordionTheme)}.{nameof(FocusedHeaderForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// Background color for focused section headers.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedHeaderBackgroundColor =
        new($"{nameof(AccordionTheme)}.{nameof(FocusedHeaderBackgroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// Character used to indicate an expanded section.
    /// </summary>
    public static readonly Hex1bThemeElement<char> ExpandedChevron =
        new($"{nameof(AccordionTheme)}.{nameof(ExpandedChevron)}", () => '▾');

    /// <summary>
    /// Character used to indicate a collapsed section.
    /// </summary>
    public static readonly Hex1bThemeElement<char> CollapsedChevron =
        new($"{nameof(AccordionTheme)}.{nameof(CollapsedChevron)}", () => '▸');
}
