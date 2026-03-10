namespace Hex1b.Theming;

/// <summary>
/// Theme elements for inline hints (virtual text rendered inline in the editor).
/// </summary>
public static class InlineHintTheme
{
    /// <summary>Foreground color for inline hint text.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(InlineHintTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.FromRgb(115, 115, 115));

    /// <summary>Background color for inline hint text.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(InlineHintTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>Whether inline hint text is rendered in italic.</summary>
    public static readonly Hex1bThemeElement<bool> IsItalic =
        new($"{nameof(InlineHintTheme)}.{nameof(IsItalic)}", () => true);

    /// <summary>Whether inline hint text is rendered in bold.</summary>
    public static readonly Hex1bThemeElement<bool> IsBold =
        new($"{nameof(InlineHintTheme)}.{nameof(IsBold)}", () => false);
}
