namespace Hex1b.Theming;

/// <summary>
/// Theme elements for menu separators.
/// </summary>
public static class MenuSeparatorTheme
{
    /// <summary>
    /// The color of the separator line.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> Color =
        new($"{nameof(MenuSeparatorTheme)}.{nameof(Color)}", () => Hex1bColor.Gray);

    /// <summary>
    /// The character used for the separator line.
    /// </summary>
    public static readonly Hex1bThemeElement<char> Character =
        new($"{nameof(MenuSeparatorTheme)}.{nameof(Character)}", () => '─');
}
