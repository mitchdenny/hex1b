namespace Hex1b.Theming;

/// <summary>
/// Theme elements for diagnostic decorations (error/warning/info/hint underlines).
/// These map to common diagnostic severity levels used by language servers.
/// </summary>
public static class DiagnosticTheme
{
    /// <summary>Underline color for error diagnostics.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ErrorUnderlineColor =
        new($"{nameof(DiagnosticTheme)}.{nameof(ErrorUnderlineColor)}", () => Hex1bColor.FromRgb(255, 0, 0));

    /// <summary>Underline color for warning diagnostics.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> WarningUnderlineColor =
        new($"{nameof(DiagnosticTheme)}.{nameof(WarningUnderlineColor)}", () => Hex1bColor.FromRgb(255, 200, 0));

    /// <summary>Underline color for information diagnostics.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> InfoUnderlineColor =
        new($"{nameof(DiagnosticTheme)}.{nameof(InfoUnderlineColor)}", () => Hex1bColor.FromRgb(75, 156, 211));

    /// <summary>Underline color for hint diagnostics.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HintUnderlineColor =
        new($"{nameof(DiagnosticTheme)}.{nameof(HintUnderlineColor)}", () => Hex1bColor.FromRgb(128, 128, 128));
}
