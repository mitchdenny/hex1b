namespace Hex1b.Theming;

/// <summary>
/// Theme elements for <see cref="Hex1b.Widgets.DatePickerWidget"/>.
/// </summary>
public static class DatePickerTheme
{
    /// <summary>Foreground color for the trigger field text.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FieldForegroundColor =
        new($"{nameof(DatePickerTheme)}.{nameof(FieldForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>Background color for the trigger field.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FieldBackgroundColor =
        new($"{nameof(DatePickerTheme)}.{nameof(FieldBackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>Foreground color for year/month grid cells.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CellForegroundColor =
        new($"{nameof(DatePickerTheme)}.{nameof(CellForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>Background color for year/month grid cells.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CellBackgroundColor =
        new($"{nameof(DatePickerTheme)}.{nameof(CellBackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>Foreground color for the currently selected year/month cell.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedCellForegroundColor =
        new($"{nameof(DatePickerTheme)}.{nameof(SelectedCellForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>Background color for the currently selected year/month cell.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedCellBackgroundColor =
        new($"{nameof(DatePickerTheme)}.{nameof(SelectedCellBackgroundColor)}", () => Hex1bColor.White);

    /// <summary>Foreground color for page navigation arrows (◀ ▶).</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> PageIndicatorColor =
        new($"{nameof(DatePickerTheme)}.{nameof(PageIndicatorColor)}", () => Hex1bColor.Default);

    /// <summary>Background color for hovered year/month cells.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HoverBackgroundColor =
        new($"{nameof(DatePickerTheme)}.{nameof(HoverBackgroundColor)}", () => Hex1bColor.Gray);

    /// <summary>Foreground color for the current year/month cell (subtle hint).</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CurrentCellForegroundColor =
        new($"{nameof(DatePickerTheme)}.{nameof(CurrentCellForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>Background color for the current year/month cell (subtle hint).</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CurrentCellBackgroundColor =
        new($"{nameof(DatePickerTheme)}.{nameof(CurrentCellBackgroundColor)}", () => Hex1bColor.Gray);
}
