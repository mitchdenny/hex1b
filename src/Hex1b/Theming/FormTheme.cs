namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Form containers and form text fields.
/// </summary>
public static class FormTheme
{
    /// <summary>
    /// Foreground color for form field labels.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> LabelForegroundColor =
        new($"{nameof(FormTheme)}.{nameof(LabelForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Color used for validation error indicators and messages.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ValidationErrorColor =
        new($"{nameof(FormTheme)}.{nameof(ValidationErrorColor)}", () => Hex1bColor.Red);

    /// <summary>
    /// Color used for fields that have passed validation.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ValidationSuccessColor =
        new($"{nameof(FormTheme)}.{nameof(ValidationSuccessColor)}", () => Hex1bColor.Green);

    /// <summary>
    /// Error indicator character shown next to invalid fields.
    /// </summary>
    public static readonly Hex1bThemeElement<string> ErrorIndicator =
        new($"{nameof(FormTheme)}.{nameof(ErrorIndicator)}", () => " ✗");

    /// <summary>
    /// Success indicator character shown next to valid fields (when validation has run).
    /// </summary>
    public static readonly Hex1bThemeElement<string> SuccessIndicator =
        new($"{nameof(FormTheme)}.{nameof(SuccessIndicator)}", () => " ✓");
}
