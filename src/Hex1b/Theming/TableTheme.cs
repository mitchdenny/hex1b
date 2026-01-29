namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Table widgets.
/// </summary>
public static class TableTheme
{
    // Selection column checkbox characters
    
    /// <summary>
    /// Character shown for unchecked (unselected) rows.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CheckboxUnchecked = 
        new($"{nameof(TableTheme)}.{nameof(CheckboxUnchecked)}", () => "☐");
    
    /// <summary>
    /// Character shown for checked (selected) rows.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CheckboxChecked = 
        new($"{nameof(TableTheme)}.{nameof(CheckboxChecked)}", () => "☑");
    
    /// <summary>
    /// Character shown in header when some (but not all) rows are selected.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CheckboxIndeterminate = 
        new($"{nameof(TableTheme)}.{nameof(CheckboxIndeterminate)}", () => "☒");
    
    // Selection column colors
    
    /// <summary>
    /// Foreground color for unchecked checkbox.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CheckboxUncheckedForeground = 
        new($"{nameof(TableTheme)}.{nameof(CheckboxUncheckedForeground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Foreground color for checked checkbox.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CheckboxCheckedForeground = 
        new($"{nameof(TableTheme)}.{nameof(CheckboxCheckedForeground)}", () => Hex1bColor.Green);
    
    // Row highlighting
    
    /// <summary>
    /// Background color for focused row.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedRowBackground = 
        new($"{nameof(TableTheme)}.{nameof(FocusedRowBackground)}", () => Hex1bColor.FromRgb(40, 40, 60));
    
    /// <summary>
    /// Background color for selected rows.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedRowBackground = 
        new($"{nameof(TableTheme)}.{nameof(SelectedRowBackground)}", () => Hex1bColor.FromRgb(30, 50, 80));
    
    /// <summary>
    /// Foreground color for selected rows.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedRowForeground = 
        new($"{nameof(TableTheme)}.{nameof(SelectedRowForeground)}", () => Hex1bColor.Default);
    
    // Selection column width
    
    /// <summary>
    /// Width of the selection column (including padding).
    /// </summary>
    public static readonly Hex1bThemeElement<int> SelectionColumnWidth = 
        new($"{nameof(TableTheme)}.{nameof(SelectionColumnWidth)}", () => 3);
}
