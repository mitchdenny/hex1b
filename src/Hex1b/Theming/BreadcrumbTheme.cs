namespace Hex1b.Theming;

/// <summary>
/// Theme elements for the breadcrumb/outline navigation bar.
/// </summary>
public static class BreadcrumbTheme
{
    /// <summary>Separator character between breadcrumb segments.</summary>
    public static readonly Hex1bThemeElement<char> SeparatorCharacter =
        new($"{nameof(BreadcrumbTheme)}.{nameof(SeparatorCharacter)}", () => '>');

    /// <summary>Foreground color of breadcrumb text.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(BreadcrumbTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Gray);

    /// <summary>Foreground color of the active (current) breadcrumb segment.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ActiveForegroundColor =
        new($"{nameof(BreadcrumbTheme)}.{nameof(ActiveForegroundColor)}", () => Hex1bColor.White);
}
