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

    public static readonly Hex1bThemeElement<Hex1bColor> SelectionForegroundColor =
        new($"{nameof(EditorTheme)}.{nameof(SelectionForegroundColor)}", () => Hex1bColor.White);

    public static readonly Hex1bThemeElement<Hex1bColor> SelectionBackgroundColor =
        new($"{nameof(EditorTheme)}.{nameof(SelectionBackgroundColor)}", () => Hex1bColor.FromRgb(0, 80, 140));

    public static readonly Hex1bThemeElement<Hex1bColor> LineNumberForegroundColor =
        new($"{nameof(EditorTheme)}.{nameof(LineNumberForegroundColor)}", () => Hex1bColor.DarkGray);

    /// <summary>
    /// Background color for bytes that are part of a multi-byte UTF-8 character
    /// when <see cref="HexEditorViewRenderer.HighlightMultiByteChars"/> is enabled.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> MultiByteBackgroundColor =
        new($"{nameof(EditorTheme)}.{nameof(MultiByteBackgroundColor)}", () => Hex1bColor.FromRgb(200, 200, 200));

    /// <summary>
    /// Foreground color for bytes that are part of a multi-byte UTF-8 character
    /// when <see cref="HexEditorViewRenderer.HighlightMultiByteChars"/> is enabled.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> MultiByteForegroundColor =
        new($"{nameof(EditorTheme)}.{nameof(MultiByteForegroundColor)}", () => Hex1bColor.Black);
}
