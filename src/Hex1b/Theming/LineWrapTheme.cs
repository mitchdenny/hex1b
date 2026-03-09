namespace Hex1b.Theming;

/// <summary>
/// Theme elements for line wrapping indicators.
/// </summary>
public static class LineWrapTheme
{
    /// <summary>Character shown at the end of a wrapped line to indicate continuation.</summary>
    public static readonly Hex1bThemeElement<char> WrapIndicator =
        new($"{nameof(LineWrapTheme)}.{nameof(WrapIndicator)}", () => '↩');

    /// <summary>Foreground color of the wrap indicator.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> IndicatorForegroundColor =
        new($"{nameof(LineWrapTheme)}.{nameof(IndicatorForegroundColor)}", () => Hex1bColor.Gray);

    /// <summary>Number of columns to indent continuation lines (0 for no indent).</summary>
    public static readonly Hex1bThemeElement<int> ContinuationIndent =
        new($"{nameof(LineWrapTheme)}.{nameof(ContinuationIndent)}", () => 0);
}
