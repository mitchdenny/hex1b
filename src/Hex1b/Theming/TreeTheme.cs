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
        new($"{nameof(TreeTheme)}.{nameof(SelectedForegroundColor)}", () => Hex1bColor.Cyan);

    public static readonly Hex1bThemeElement<Hex1bColor> SelectedBackgroundColor =
        new($"{nameof(TreeTheme)}.{nameof(SelectedBackgroundColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> HoveredForegroundColor =
        new($"{nameof(TreeTheme)}.{nameof(HoveredForegroundColor)}", () => Hex1bColor.Black);

    public static readonly Hex1bThemeElement<Hex1bColor> HoveredBackgroundColor =
        new($"{nameof(TreeTheme)}.{nameof(HoveredBackgroundColor)}", () => Hex1bColor.FromRgb(180, 180, 180));

    public static readonly Hex1bThemeElement<Hex1bColor> GuideColor =
        new($"{nameof(TreeTheme)}.{nameof(GuideColor)}", () => Hex1bColor.DarkGray);

    #endregion

    #region Guide Characters - Unicode (default)

    public static readonly Hex1bThemeElement<string> UnicodeBranch =
        new($"{nameof(TreeTheme)}.{nameof(UnicodeBranch)}", () => "├─ ");

    public static readonly Hex1bThemeElement<string> UnicodeLastBranch =
        new($"{nameof(TreeTheme)}.{nameof(UnicodeLastBranch)}", () => "└─ ");

    public static readonly Hex1bThemeElement<string> UnicodeVertical =
        new($"{nameof(TreeTheme)}.{nameof(UnicodeVertical)}", () => "│  ");

    public static readonly Hex1bThemeElement<string> UnicodeSpace =
        new($"{nameof(TreeTheme)}.{nameof(UnicodeSpace)}", () => "   ");

    #endregion

    #region Guide Characters - ASCII

    public static readonly Hex1bThemeElement<string> AsciiBranch =
        new($"{nameof(TreeTheme)}.{nameof(AsciiBranch)}", () => "+- ");

    public static readonly Hex1bThemeElement<string> AsciiLastBranch =
        new($"{nameof(TreeTheme)}.{nameof(AsciiLastBranch)}", () => "\\- ");

    public static readonly Hex1bThemeElement<string> AsciiVertical =
        new($"{nameof(TreeTheme)}.{nameof(AsciiVertical)}", () => "|  ");

    public static readonly Hex1bThemeElement<string> AsciiSpace =
        new($"{nameof(TreeTheme)}.{nameof(AsciiSpace)}", () => "   ");

    #endregion

    #region Guide Characters - Bold

    public static readonly Hex1bThemeElement<string> BoldBranch =
        new($"{nameof(TreeTheme)}.{nameof(BoldBranch)}", () => "┣━ ");

    public static readonly Hex1bThemeElement<string> BoldLastBranch =
        new($"{nameof(TreeTheme)}.{nameof(BoldLastBranch)}", () => "┗━ ");

    public static readonly Hex1bThemeElement<string> BoldVertical =
        new($"{nameof(TreeTheme)}.{nameof(BoldVertical)}", () => "┃  ");

    public static readonly Hex1bThemeElement<string> BoldSpace =
        new($"{nameof(TreeTheme)}.{nameof(BoldSpace)}", () => "   ");

    #endregion

    #region Guide Characters - Double

    public static readonly Hex1bThemeElement<string> DoubleBranch =
        new($"{nameof(TreeTheme)}.{nameof(DoubleBranch)}", () => "╠═ ");

    public static readonly Hex1bThemeElement<string> DoubleLastBranch =
        new($"{nameof(TreeTheme)}.{nameof(DoubleLastBranch)}", () => "╚═ ");

    public static readonly Hex1bThemeElement<string> DoubleVertical =
        new($"{nameof(TreeTheme)}.{nameof(DoubleVertical)}", () => "║  ");

    public static readonly Hex1bThemeElement<string> DoubleSpace =
        new($"{nameof(TreeTheme)}.{nameof(DoubleSpace)}", () => "   ");

    #endregion

    #region Indicators

    public static readonly Hex1bThemeElement<string> ExpandedIndicator =
        new($"{nameof(TreeTheme)}.{nameof(ExpandedIndicator)}", () => "▼ ");

    public static readonly Hex1bThemeElement<string> CollapsedIndicator =
        new($"{nameof(TreeTheme)}.{nameof(CollapsedIndicator)}", () => "▶ ");

    public static readonly Hex1bThemeElement<string> LeafIndicator =
        new($"{nameof(TreeTheme)}.{nameof(LeafIndicator)}", () => "  ");

    public static readonly Hex1bThemeElement<string> LoadingIndicator =
        new($"{nameof(TreeTheme)}.{nameof(LoadingIndicator)}", () => "◌ ");

    public static readonly Hex1bThemeElement<string> CheckboxChecked =
        new($"{nameof(TreeTheme)}.{nameof(CheckboxChecked)}", () => "[x] ");

    public static readonly Hex1bThemeElement<string> CheckboxUnchecked =
        new($"{nameof(TreeTheme)}.{nameof(CheckboxUnchecked)}", () => "[ ] ");

    #endregion
}
