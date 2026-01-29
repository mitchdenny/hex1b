using Hex1b.Widgets;

namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Spinner widgets.
/// </summary>
/// <remarks>
/// <para>
/// SpinnerTheme provides customization for spinner appearance:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="Style"/>: The default spinner animation style</description></item>
/// <item><description><see cref="ForegroundColor"/>: The spinner character color</description></item>
/// <item><description><see cref="BackgroundColor"/>: The spinner background color</description></item>
/// </list>
/// </remarks>
/// <example>
/// <para>Customize spinner appearance via theme:</para>
/// <code>
/// var theme = new Hex1bThemeBuilder()
///     .Set(SpinnerTheme.Style, SpinnerStyle.Arrow)
///     .Set(SpinnerTheme.ForegroundColor, Hex1bColor.Green)
///     .Build();
/// </code>
/// </example>
/// <seealso cref="SpinnerWidget"/>
/// <seealso cref="SpinnerStyle"/>
public static class SpinnerTheme
{
    /// <summary>
    /// The default spinner style used when no explicit style is specified.
    /// </summary>
    /// <remarks>
    /// Themes can override this to provide a consistent spinner appearance across the app.
    /// Individual spinners can still override with an explicit style.
    /// Default is <see cref="SpinnerStyle.Dots"/>.
    /// </remarks>
    public static readonly Hex1bThemeElement<SpinnerStyle> Style =
        new($"{nameof(SpinnerTheme)}.{nameof(Style)}", () => SpinnerStyle.Dots);

    /// <summary>
    /// The foreground color for the spinner character.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="Hex1bColor.Default"/> (terminal's default foreground).
    /// Themed applications can override this for branded colors.
    /// </remarks>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(SpinnerTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// The background color for the spinner character.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="Hex1bColor.Default"/> (terminal's default background).
    /// </remarks>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(SpinnerTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
}
