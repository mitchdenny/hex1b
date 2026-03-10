namespace Hex1b.Theming;

/// <summary>
/// Theme elements for the editor gutter (line numbers and decoration columns).
/// </summary>
public static class GutterTheme
{
    /// <summary>Foreground color for line numbers.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> LineNumberForegroundColor =
        new($"{nameof(GutterTheme)}.{nameof(LineNumberForegroundColor)}", () => Hex1bColor.DarkGray);

    /// <summary>Background color for the gutter area.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(GutterTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>Separator character between the gutter and content area.</summary>
    public static readonly Hex1bThemeElement<char> SeparatorCharacter =
        new($"{nameof(GutterTheme)}.{nameof(SeparatorCharacter)}", () => ' ');
}
