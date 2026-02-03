namespace Hex1b.Theming;

/// <summary>
/// Theme elements for CheckboxWidget.
/// </summary>
public static class CheckboxTheme
{
    #region Colors

    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Black);

    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.White);

    public static readonly Hex1bThemeElement<Hex1bColor> CheckMarkColor =
        new($"{nameof(CheckboxTheme)}.{nameof(CheckMarkColor)}", () => Hex1bColor.Green);

    public static readonly Hex1bThemeElement<Hex1bColor> IndeterminateColor =
        new($"{nameof(CheckboxTheme)}.{nameof(IndeterminateColor)}", () => Hex1bColor.Yellow);

    #endregion

    #region Characters

    /// <summary>
    /// The checkbox string when checked. Default is "[x]".
    /// </summary>
    public static readonly Hex1bThemeElement<string> CheckedBox =
        new($"{nameof(CheckboxTheme)}.{nameof(CheckedBox)}", () => "[x]");

    /// <summary>
    /// The checkbox string when unchecked. Default is "[ ]".
    /// </summary>
    public static readonly Hex1bThemeElement<string> UncheckedBox =
        new($"{nameof(CheckboxTheme)}.{nameof(UncheckedBox)}", () => "[ ]");

    /// <summary>
    /// The checkbox string when indeterminate. Default is "[-]".
    /// </summary>
    public static readonly Hex1bThemeElement<string> IndeterminateBox =
        new($"{nameof(CheckboxTheme)}.{nameof(IndeterminateBox)}", () => "[-]");

    #endregion
}
