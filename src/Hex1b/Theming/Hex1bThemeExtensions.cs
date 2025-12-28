namespace Hex1b.Theming;

/// <summary>
/// Extension methods for <see cref="Hex1bTheme"/> to support global color operations.
/// </summary>
public static class Hex1bThemeExtensions
{
    /// <summary>
    /// Gets the global foreground color from the theme.
    /// </summary>
    public static Hex1bColor GetGlobalForeground(this Hex1bTheme theme)
        => theme.Get(GlobalTheme.ForegroundColor);

    /// <summary>
    /// Gets the global background color from the theme.
    /// </summary>
    public static Hex1bColor GetGlobalBackground(this Hex1bTheme theme)
        => theme.Get(GlobalTheme.BackgroundColor);

    /// <summary>
    /// Gets the ANSI codes to apply global colors from the theme, or empty string if default.
    /// </summary>
    public static string GetGlobalColorCodes(this Hex1bTheme theme)
    {
        var result = "";
        var fg = theme.GetGlobalForeground();
        var bg = theme.GetGlobalBackground();
        if (!fg.IsDefault)
            result += fg.ToForegroundAnsi();
        if (!bg.IsDefault)
            result += bg.ToBackgroundAnsi();
        return result;
    }

    /// <summary>
    /// Gets the ANSI codes to reset colors back to global theme values (or default if none).
    /// Use this after applying temporary color changes.
    /// </summary>
    public static string GetResetToGlobalCodes(this Hex1bTheme theme)
    {
        var fg = theme.GetGlobalForeground();
        var bg = theme.GetGlobalBackground();

        if (fg.IsDefault && bg.IsDefault)
            return "\x1b[0m";

        var result = "\x1b[0m"; // Reset all first
        if (!fg.IsDefault)
            result += fg.ToForegroundAnsi();
        if (!bg.IsDefault)
            result += bg.ToBackgroundAnsi();
        return result;
    }
}
