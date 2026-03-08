namespace Hex1b.Theming;

/// <summary>
/// Theme elements for the TilePanel widget.
/// </summary>
public static class TilePanelTheme
{
    /// <summary>
    /// Foreground color for empty tiles (tiles with no content from the data source).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> EmptyTileForegroundColor =
        new($"{nameof(TilePanelTheme)}.{nameof(EmptyTileForegroundColor)}", () => Hex1bColor.DarkGray);

    /// <summary>
    /// Background color for empty tiles.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> EmptyTileBackgroundColor =
        new($"{nameof(TilePanelTheme)}.{nameof(EmptyTileBackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Character used to fill empty tiles.
    /// </summary>
    public static readonly Hex1bThemeElement<char> EmptyTileCharacter =
        new($"{nameof(TilePanelTheme)}.{nameof(EmptyTileCharacter)}", () => '·');

    /// <summary>
    /// Foreground color for point-of-interest labels.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> PoiLabelForegroundColor =
        new($"{nameof(TilePanelTheme)}.{nameof(PoiLabelForegroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// Background color for point-of-interest labels.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> PoiLabelBackgroundColor =
        new($"{nameof(TilePanelTheme)}.{nameof(PoiLabelBackgroundColor)}", () => Hex1bColor.Default);
}
