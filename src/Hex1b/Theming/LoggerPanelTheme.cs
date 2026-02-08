using Hex1b.Theming;

namespace Hex1b.Theming;

/// <summary>
/// Theme elements for LoggerPanel widgets.
/// </summary>
public static class LoggerPanelTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> TraceColor =
        new($"{nameof(LoggerPanelTheme)}.{nameof(TraceColor)}", () => Hex1bColor.FromRgb(128, 128, 128));

    public static readonly Hex1bThemeElement<Hex1bColor> DebugColor =
        new($"{nameof(LoggerPanelTheme)}.{nameof(DebugColor)}", () => Hex1bColor.FromRgb(128, 128, 128));

    public static readonly Hex1bThemeElement<Hex1bColor> InformationColor =
        new($"{nameof(LoggerPanelTheme)}.{nameof(InformationColor)}", () => Hex1bColor.White);

    public static readonly Hex1bThemeElement<Hex1bColor> WarningColor =
        new($"{nameof(LoggerPanelTheme)}.{nameof(WarningColor)}", () => Hex1bColor.Yellow);

    public static readonly Hex1bThemeElement<Hex1bColor> ErrorColor =
        new($"{nameof(LoggerPanelTheme)}.{nameof(ErrorColor)}", () => Hex1bColor.Red);

    public static readonly Hex1bThemeElement<Hex1bColor> CriticalColor =
        new($"{nameof(LoggerPanelTheme)}.{nameof(CriticalColor)}", () => Hex1bColor.Red);
}
