namespace Hex1b.Theming;

/// <summary>
/// Theme elements for gutter decorations (icons/markers in the editor margin).
/// </summary>
public static class GutterDecorationTheme
{
    /// <summary>Default foreground color for gutter decoration icons.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DefaultIconColor =
        new($"{nameof(GutterDecorationTheme)}.{nameof(DefaultIconColor)}", () => Hex1bColor.Gray);

    /// <summary>Foreground color for error gutter icons.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ErrorIconColor =
        new($"{nameof(GutterDecorationTheme)}.{nameof(ErrorIconColor)}", () => Hex1bColor.Red);

    /// <summary>Foreground color for warning gutter icons.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> WarningIconColor =
        new($"{nameof(GutterDecorationTheme)}.{nameof(WarningIconColor)}", () => Hex1bColor.Yellow);

    /// <summary>Foreground color for info gutter icons.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> InfoIconColor =
        new($"{nameof(GutterDecorationTheme)}.{nameof(InfoIconColor)}", () => Hex1bColor.Cyan);
}
