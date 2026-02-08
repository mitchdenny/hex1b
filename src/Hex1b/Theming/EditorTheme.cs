namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Editor widgets.
/// </summary>
public static class EditorTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(EditorTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(EditorTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> CursorForegroundColor =
        new($"{nameof(EditorTheme)}.{nameof(CursorForegroundColor)}", () => Hex1bColor.Black);

    public static readonly Hex1bThemeElement<Hex1bColor> CursorBackgroundColor =
        new($"{nameof(EditorTheme)}.{nameof(CursorBackgroundColor)}", () => Hex1bColor.White);

    public static readonly Hex1bThemeElement<Hex1bColor> LineNumberForegroundColor =
        new($"{nameof(EditorTheme)}.{nameof(LineNumberForegroundColor)}", () => Hex1bColor.DarkGray);
}
