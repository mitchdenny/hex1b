namespace Hex1b.Theming;

/// <summary>
/// Theme elements for the signature help panel.
/// </summary>
public static class SignaturePanelTheme
{
    /// <summary>Background color of the signature panel.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(SignaturePanelTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.FromRgb(37, 37, 38));

    /// <summary>Foreground color of signature text.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(SignaturePanelTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.FromRgb(204, 204, 204));

    /// <summary>Foreground color of the active parameter.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ActiveParameterForegroundColor =
        new($"{nameof(SignaturePanelTheme)}.{nameof(ActiveParameterForegroundColor)}", () => Hex1bColor.FromRgb(86, 156, 214));

    /// <summary>Whether the active parameter is rendered in bold.</summary>
    public static readonly Hex1bThemeElement<bool> ActiveParameterBold =
        new($"{nameof(SignaturePanelTheme)}.{nameof(ActiveParameterBold)}", () => true);
}
