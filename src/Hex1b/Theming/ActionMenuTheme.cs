namespace Hex1b.Theming;

/// <summary>
/// Theme elements for the action menu popup.
/// </summary>
public static class ActionMenuTheme
{
    /// <summary>Background color of the menu popup.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(ActionMenuTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.FromRgb(37, 37, 38));

    /// <summary>Foreground color of menu items.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(ActionMenuTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.FromRgb(204, 204, 204));

    /// <summary>Background color of the selected item.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedBackgroundColor =
        new($"{nameof(ActionMenuTheme)}.{nameof(SelectedBackgroundColor)}", () => Hex1bColor.FromRgb(4, 57, 94));

    /// <summary>Foreground color of the selected item.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedForegroundColor =
        new($"{nameof(ActionMenuTheme)}.{nameof(SelectedForegroundColor)}", () => Hex1bColor.White);
}
