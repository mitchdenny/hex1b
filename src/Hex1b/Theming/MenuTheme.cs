namespace Hex1b.Theming;

/// <summary>
/// Theme elements for menu popups (the dropdown container).
/// </summary>
public static class MenuTheme
{
    /// <summary>
    /// The background color of the menu popup.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(MenuTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.FromRgb(50, 50, 50));

    /// <summary>
    /// The foreground color of the menu popup.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(MenuTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// The border color of the menu popup.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BorderColor =
        new($"{nameof(MenuTheme)}.{nameof(BorderColor)}", () => Hex1bColor.Gray);

    /// <summary>
    /// Top-left corner character.
    /// </summary>
    public static readonly Hex1bThemeElement<char> BorderTopLeft =
        new($"{nameof(MenuTheme)}.{nameof(BorderTopLeft)}", () => '┌');

    /// <summary>
    /// Top-right corner character.
    /// </summary>
    public static readonly Hex1bThemeElement<char> BorderTopRight =
        new($"{nameof(MenuTheme)}.{nameof(BorderTopRight)}", () => '┐');

    /// <summary>
    /// Bottom-left corner character.
    /// </summary>
    public static readonly Hex1bThemeElement<char> BorderBottomLeft =
        new($"{nameof(MenuTheme)}.{nameof(BorderBottomLeft)}", () => '└');

    /// <summary>
    /// Bottom-right corner character.
    /// </summary>
    public static readonly Hex1bThemeElement<char> BorderBottomRight =
        new($"{nameof(MenuTheme)}.{nameof(BorderBottomRight)}", () => '┘');

    /// <summary>
    /// Horizontal border character.
    /// </summary>
    public static readonly Hex1bThemeElement<char> BorderHorizontal =
        new($"{nameof(MenuTheme)}.{nameof(BorderHorizontal)}", () => '─');

    /// <summary>
    /// Vertical border character.
    /// </summary>
    public static readonly Hex1bThemeElement<char> BorderVertical =
        new($"{nameof(MenuTheme)}.{nameof(BorderVertical)}", () => '│');
}
