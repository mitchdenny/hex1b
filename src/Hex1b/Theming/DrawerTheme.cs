namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Drawer widgets.
/// </summary>
public static class DrawerTheme
{
    /// <summary>
    /// Background color for the drawer's content area.
    /// When set, fills the content bounds with this color before rendering children,
    /// preventing background bleed-through from layers below (e.g., in overlay mode).
    /// Defaults to <see cref="Hex1bColor.Default"/> (no fill).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(DrawerTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
}
