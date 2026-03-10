namespace Hex1b.Theming;

/// <summary>
/// Theme elements for code folding.
/// </summary>
public static class FoldingTheme
{
    /// <summary>Character used to indicate a collapsed region in the gutter (e.g., '▶').</summary>
    public static readonly Hex1bThemeElement<char> CollapsedIndicator =
        new($"{nameof(FoldingTheme)}.{nameof(CollapsedIndicator)}", () => '▶');

    /// <summary>Character used to indicate an expanded region in the gutter (e.g., '▼').</summary>
    public static readonly Hex1bThemeElement<char> ExpandedIndicator =
        new($"{nameof(FoldingTheme)}.{nameof(ExpandedIndicator)}", () => '▼');

    /// <summary>Placeholder text shown for collapsed content.</summary>
    public static readonly Hex1bThemeElement<string> CollapsedPlaceholder =
        new($"{nameof(FoldingTheme)}.{nameof(CollapsedPlaceholder)}", () => "...");

    /// <summary>Foreground color of the collapsed placeholder text.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> PlaceholderForegroundColor =
        new($"{nameof(FoldingTheme)}.{nameof(PlaceholderForegroundColor)}", () => Hex1bColor.Gray);
}
