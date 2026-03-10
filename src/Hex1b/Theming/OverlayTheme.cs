namespace Hex1b.Theming;

/// <summary>
/// Theme elements for editor overlays (hover popups, completion menus, etc.).
/// </summary>
public static class OverlayTheme
{
    /// <summary>Default background color for overlay popups.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(OverlayTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.FromRgb(30, 30, 30));

    /// <summary>Default foreground color for overlay text.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(OverlayTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.FromRgb(200, 200, 200));

    /// <summary>Border color for overlay popups.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BorderColor =
        new($"{nameof(OverlayTheme)}.{nameof(BorderColor)}", () => Hex1bColor.FromRgb(100, 100, 100));

    /// <summary>Whether to show a border around overlays.</summary>
    public static readonly Hex1bThemeElement<bool> ShowBorder =
        new($"{nameof(OverlayTheme)}.{nameof(ShowBorder)}", () => true);

    /// <summary>Title foreground color when overlay has a title.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> TitleForegroundColor =
        new($"{nameof(OverlayTheme)}.{nameof(TitleForegroundColor)}", () => Hex1bColor.White);
}
