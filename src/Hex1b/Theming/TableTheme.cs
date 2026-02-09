namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Table widgets.
/// </summary>
public static class TableTheme
{
    #region Border Characters
    
    /// <summary>
    /// Top-left corner character (┌).
    /// </summary>
    public static readonly Hex1bThemeElement<char> TopLeft = 
        new($"{nameof(TableTheme)}.{nameof(TopLeft)}", () => '┌');
    
    /// <summary>
    /// Top-right corner character (┐).
    /// </summary>
    public static readonly Hex1bThemeElement<char> TopRight = 
        new($"{nameof(TableTheme)}.{nameof(TopRight)}", () => '┐');
    
    /// <summary>
    /// Bottom-left corner character (└).
    /// </summary>
    public static readonly Hex1bThemeElement<char> BottomLeft = 
        new($"{nameof(TableTheme)}.{nameof(BottomLeft)}", () => '└');
    
    /// <summary>
    /// Bottom-right corner character (┘).
    /// </summary>
    public static readonly Hex1bThemeElement<char> BottomRight = 
        new($"{nameof(TableTheme)}.{nameof(BottomRight)}", () => '┘');
    
    /// <summary>
    /// Horizontal line character (─).
    /// </summary>
    public static readonly Hex1bThemeElement<char> Horizontal = 
        new($"{nameof(TableTheme)}.{nameof(Horizontal)}", () => '─');
    
    /// <summary>
    /// Vertical line character (│).
    /// </summary>
    public static readonly Hex1bThemeElement<char> Vertical = 
        new($"{nameof(TableTheme)}.{nameof(Vertical)}", () => '│');
    
    /// <summary>
    /// T pointing down for top border column separators (┬).
    /// </summary>
    public static readonly Hex1bThemeElement<char> TeeDown = 
        new($"{nameof(TableTheme)}.{nameof(TeeDown)}", () => '┬');
    
    /// <summary>
    /// T pointing up for bottom border column separators (┴).
    /// </summary>
    public static readonly Hex1bThemeElement<char> TeeUp = 
        new($"{nameof(TableTheme)}.{nameof(TeeUp)}", () => '┴');
    
    /// <summary>
    /// T pointing right for row separator left edge (├).
    /// </summary>
    public static readonly Hex1bThemeElement<char> TeeRight = 
        new($"{nameof(TableTheme)}.{nameof(TeeRight)}", () => '├');
    
    /// <summary>
    /// T pointing left for row separator right edge (┤).
    /// </summary>
    public static readonly Hex1bThemeElement<char> TeeLeft = 
        new($"{nameof(TableTheme)}.{nameof(TeeLeft)}", () => '┤');
    
    /// <summary>
    /// Cross intersection character (┼).
    /// </summary>
    public static readonly Hex1bThemeElement<char> Cross = 
        new($"{nameof(TableTheme)}.{nameof(Cross)}", () => '┼');
    
    #endregion
    
    #region Scrollbar Characters
    
    /// <summary>
    /// Scrollbar track character (thin vertical │).
    /// </summary>
    public static readonly Hex1bThemeElement<char> ScrollbarTrack = 
        new($"{nameof(TableTheme)}.{nameof(ScrollbarTrack)}", () => '│');
    
    /// <summary>
    /// Scrollbar thumb character (7/8 block ▉).
    /// </summary>
    public static readonly Hex1bThemeElement<char> ScrollbarThumb = 
        new($"{nameof(TableTheme)}.{nameof(ScrollbarThumb)}", () => '▉');
    
    #endregion
    
    #region Border Colors
    
    /// <summary>
    /// Background color for the entire table. When set, the table fills its entire area
    /// with this color before rendering, preventing background bleed-through from layers below.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(TableTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Color for table borders.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BorderColor = 
        new($"{nameof(TableTheme)}.{nameof(BorderColor)}", () => Hex1bColor.DarkGray);
    
    /// <summary>
    /// Color for focused row borders (heavy lines).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBorderColor = 
        new($"{nameof(TableTheme)}.{nameof(FocusedBorderColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Color for scrollbar track.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ScrollbarTrackColor = 
        new($"{nameof(TableTheme)}.{nameof(ScrollbarTrackColor)}", () => Hex1bColor.DarkGray);
    
    /// <summary>
    /// Color for scrollbar thumb.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ScrollbarThumbColor = 
        new($"{nameof(TableTheme)}.{nameof(ScrollbarThumbColor)}", () => Hex1bColor.Gray);
    
    #endregion
    
    #region Selection Column
    
    /// <summary>
    /// Character shown for unchecked (unselected) rows.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CheckboxUnchecked = 
        new($"{nameof(TableTheme)}.{nameof(CheckboxUnchecked)}", () => "[ ]");
    
    /// <summary>
    /// Character shown for checked (selected) rows.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CheckboxChecked = 
        new($"{nameof(TableTheme)}.{nameof(CheckboxChecked)}", () => "[x]");
    
    /// <summary>
    /// Character shown in header when some (but not all) rows are selected.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CheckboxIndeterminate = 
        new($"{nameof(TableTheme)}.{nameof(CheckboxIndeterminate)}", () => "[-]");
    
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
    
    /// <summary>
    /// Width of the selection column (including padding).
    /// </summary>
    public static readonly Hex1bThemeElement<int> SelectionColumnWidth = 
        new($"{nameof(TableTheme)}.{nameof(SelectionColumnWidth)}", () => 3);
    
    /// <summary>
    /// Vertical border character for the selection column separator.
    /// </summary>
    public static readonly Hex1bThemeElement<char> SelectionColumnVertical = 
        new($"{nameof(TableTheme)}.{nameof(SelectionColumnVertical)}", () => '│');
    
    /// <summary>
    /// Color for the selection column vertical border.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectionColumnBorderColor = 
        new($"{nameof(TableTheme)}.{nameof(SelectionColumnBorderColor)}", () => Hex1bColor.Default);
    
    #endregion
    
    #region Row Styling
    
    /// <summary>
    /// Background color for header row.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HeaderBackground = 
        new($"{nameof(TableTheme)}.{nameof(HeaderBackground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Foreground color for header row.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HeaderForeground = 
        new($"{nameof(TableTheme)}.{nameof(HeaderForeground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Background color for normal rows.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> RowBackground = 
        new($"{nameof(TableTheme)}.{nameof(RowBackground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Foreground color for normal rows.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> RowForeground = 
        new($"{nameof(TableTheme)}.{nameof(RowForeground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Background color for alternating rows (zebra striping). Default means disabled.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> AlternateRowBackground = 
        new($"{nameof(TableTheme)}.{nameof(AlternateRowBackground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Background color for focused row.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedRowBackground = 
        new($"{nameof(TableTheme)}.{nameof(FocusedRowBackground)}", () => Hex1bColor.FromRgb(50, 50, 50));
    
    /// <summary>
    /// Foreground color for focused row.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedRowForeground = 
        new($"{nameof(TableTheme)}.{nameof(FocusedRowForeground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Background color for selected rows.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedRowBackground = 
        new($"{nameof(TableTheme)}.{nameof(SelectedRowBackground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Foreground color for selected rows.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedRowForeground = 
        new($"{nameof(TableTheme)}.{nameof(SelectedRowForeground)}", () => Hex1bColor.Default);
    
    #endregion
    
    #region Empty State
    
    /// <summary>
    /// Text to display when table has no data.
    /// </summary>
    public static readonly Hex1bThemeElement<string> EmptyText = 
        new($"{nameof(TableTheme)}.{nameof(EmptyText)}", () => "No data");
    
    /// <summary>
    /// Foreground color for empty state text.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> EmptyTextForeground = 
        new($"{nameof(TableTheme)}.{nameof(EmptyTextForeground)}", () => Hex1bColor.Gray);
    
    /// <summary>
    /// Text to display when loading data.
    /// </summary>
    public static readonly Hex1bThemeElement<string> LoadingText = 
        new($"{nameof(TableTheme)}.{nameof(LoadingText)}", () => "Loading...");
    
    /// <summary>
    /// Foreground color for loading state text.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> LoadingTextForeground = 
        new($"{nameof(TableTheme)}.{nameof(LoadingTextForeground)}", () => Hex1bColor.Gray);
    
    #endregion
    
    #region Table Focus Indicator
    
    /// <summary>
    /// Whether to show a focus indicator when the table has focus.
    /// </summary>
    public static readonly Hex1bThemeElement<bool> ShowFocusIndicator = 
        new($"{nameof(TableTheme)}.{nameof(ShowFocusIndicator)}", () => true);
    
    /// <summary>
    /// Border color for outer table borders when table is focused (mid-tone).
    /// This creates a 3-tone system: dark grey (unfocused), grey (table focused), 
    /// and default foreground (row focused indicator).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> TableFocusedBorderColor = 
        new($"{nameof(TableTheme)}.{nameof(TableFocusedBorderColor)}", () => Hex1bColor.Gray);
    
    #endregion
}
