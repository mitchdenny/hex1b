namespace Hex1b.Theming;

/// <summary>
/// Theme elements for grid borders (used when <see cref="GridLinesMode"/> is not <c>None</c>).
/// </summary>
public static class GridTheme
{
    /// <summary>Top-left corner (┌).</summary>
    public static readonly Hex1bThemeElement<char> TopLeft =
        new($"{nameof(GridTheme)}.{nameof(TopLeft)}", () => '┌');

    /// <summary>Top-right corner (┐).</summary>
    public static readonly Hex1bThemeElement<char> TopRight =
        new($"{nameof(GridTheme)}.{nameof(TopRight)}", () => '┐');

    /// <summary>Bottom-left corner (└).</summary>
    public static readonly Hex1bThemeElement<char> BottomLeft =
        new($"{nameof(GridTheme)}.{nameof(BottomLeft)}", () => '└');

    /// <summary>Bottom-right corner (┘).</summary>
    public static readonly Hex1bThemeElement<char> BottomRight =
        new($"{nameof(GridTheme)}.{nameof(BottomRight)}", () => '┘');

    /// <summary>Horizontal line character (─).</summary>
    public static readonly Hex1bThemeElement<char> Horizontal =
        new($"{nameof(GridTheme)}.{nameof(Horizontal)}", () => '─');

    /// <summary>Vertical line character (│).</summary>
    public static readonly Hex1bThemeElement<char> Vertical =
        new($"{nameof(GridTheme)}.{nameof(Vertical)}", () => '│');

    /// <summary>Top T-junction (┬).</summary>
    public static readonly Hex1bThemeElement<char> TeeDown =
        new($"{nameof(GridTheme)}.{nameof(TeeDown)}", () => '┬');

    /// <summary>Bottom T-junction (┴).</summary>
    public static readonly Hex1bThemeElement<char> TeeUp =
        new($"{nameof(GridTheme)}.{nameof(TeeUp)}", () => '┴');

    /// <summary>Left T-junction (├).</summary>
    public static readonly Hex1bThemeElement<char> TeeRight =
        new($"{nameof(GridTheme)}.{nameof(TeeRight)}", () => '├');

    /// <summary>Right T-junction (┤).</summary>
    public static readonly Hex1bThemeElement<char> TeeLeft =
        new($"{nameof(GridTheme)}.{nameof(TeeLeft)}", () => '┤');

    /// <summary>Cross intersection (┼).</summary>
    public static readonly Hex1bThemeElement<char> Cross =
        new($"{nameof(GridTheme)}.{nameof(Cross)}", () => '┼');

    /// <summary>Border color.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BorderColor =
        new($"{nameof(GridTheme)}.{nameof(BorderColor)}", () => Hex1bColor.Default);
}
