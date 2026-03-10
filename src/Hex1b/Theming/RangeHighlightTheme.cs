namespace Hex1b.Theming;

/// <summary>
/// Theme elements for range highlights (background-colored document ranges).
/// </summary>
public static class RangeHighlightTheme
{
    /// <summary>Background color for default highlights (e.g., search results).</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DefaultBackground =
        new($"{nameof(RangeHighlightTheme)}.{nameof(DefaultBackground)}", () => Hex1bColor.FromRgb(60, 60, 40));

    /// <summary>Background color for read-access highlights.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ReadAccessBackground =
        new($"{nameof(RangeHighlightTheme)}.{nameof(ReadAccessBackground)}", () => Hex1bColor.FromRgb(40, 55, 70));

    /// <summary>Background color for write-access highlights.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> WriteAccessBackground =
        new($"{nameof(RangeHighlightTheme)}.{nameof(WriteAccessBackground)}", () => Hex1bColor.FromRgb(70, 50, 40));
}
