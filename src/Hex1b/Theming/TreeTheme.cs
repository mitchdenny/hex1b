namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Tree widgets.
/// </summary>
public static class TreeTheme
{
    #region Colors

    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(TreeTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(TreeTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor =
        new($"{nameof(TreeTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Black);

    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor =
        new($"{nameof(TreeTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.White);

    public static readonly Hex1bThemeElement<Hex1bColor> SelectedForegroundColor =
        new($"{nameof(TreeTheme)}.{nameof(SelectedForegroundColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> SelectedBackgroundColor =
        new($"{nameof(TreeTheme)}.{nameof(SelectedBackgroundColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> HoveredForegroundColor =
        new($"{nameof(TreeTheme)}.{nameof(HoveredForegroundColor)}", () => Hex1bColor.Black);

    public static readonly Hex1bThemeElement<Hex1bColor> HoveredBackgroundColor =
        new($"{nameof(TreeTheme)}.{nameof(HoveredBackgroundColor)}", () => Hex1bColor.FromRgb(180, 180, 180));

    public static readonly Hex1bThemeElement<Hex1bColor> GuideColor =
        new($"{nameof(TreeTheme)}.{nameof(GuideColor)}", () => Hex1bColor.DarkGray);

    #endregion

    #region Guide Characters

    /// <summary>
    /// Branch connector for non-last items (default: "├─ ").
    /// </summary>
    public static readonly Hex1bThemeElement<string> Branch =
        new($"{nameof(TreeTheme)}.{nameof(Branch)}", () => "├─ ");

    /// <summary>
    /// Branch connector for last items (default: "└─ ").
    /// </summary>
    public static readonly Hex1bThemeElement<string> LastBranch =
        new($"{nameof(TreeTheme)}.{nameof(LastBranch)}", () => "└─ ");

    /// <summary>
    /// Vertical continuation line (default: "│  ").
    /// </summary>
    public static readonly Hex1bThemeElement<string> Vertical =
        new($"{nameof(TreeTheme)}.{nameof(Vertical)}", () => "│  ");

    /// <summary>
    /// Empty space for indentation (default: "   ").
    /// </summary>
    public static readonly Hex1bThemeElement<string> Space =
        new($"{nameof(TreeTheme)}.{nameof(Space)}", () => "   ");

    #endregion

    #region Indicators

    public static readonly Hex1bThemeElement<string> ExpandedIndicator =
        new($"{nameof(TreeTheme)}.{nameof(ExpandedIndicator)}", () => "▼ ");

    public static readonly Hex1bThemeElement<string> CollapsedIndicator =
        new($"{nameof(TreeTheme)}.{nameof(CollapsedIndicator)}", () => "▶ ");

    public static readonly Hex1bThemeElement<string> LeafIndicator =
        new($"{nameof(TreeTheme)}.{nameof(LeafIndicator)}", () => " ");

    public static readonly Hex1bThemeElement<string> CheckboxChecked =
        new($"{nameof(TreeTheme)}.{nameof(CheckboxChecked)}", () => "[x] ");

    public static readonly Hex1bThemeElement<string> CheckboxUnchecked =
        new($"{nameof(TreeTheme)}.{nameof(CheckboxUnchecked)}", () => "[ ] ");

    public static readonly Hex1bThemeElement<string> CheckboxIndeterminate =
        new($"{nameof(TreeTheme)}.{nameof(CheckboxIndeterminate)}", () => "[-] ");

    #endregion
}
