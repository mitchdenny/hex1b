namespace Hex1b.Theming;

/// <summary>
/// Theme elements for the Slider widget.
/// </summary>
/// <seealso cref="Widgets.SliderWidget"/>
/// <seealso cref="SliderNode"/>
public static class SliderTheme
{
    /// <summary>
    /// The character used to render the slider track.
    /// </summary>
    public static readonly Hex1bThemeElement<char> TrackCharacter =
        new($"{nameof(SliderTheme)}.{nameof(TrackCharacter)}", () => '─');

    /// <summary>
    /// The character used to render the slider handle (knob).
    /// </summary>
    public static readonly Hex1bThemeElement<char> HandleCharacter =
        new($"{nameof(SliderTheme)}.{nameof(HandleCharacter)}", () => '█');

    /// <summary>
    /// Foreground color for the track when unfocused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> TrackForegroundColor =
        new($"{nameof(SliderTheme)}.{nameof(TrackForegroundColor)}", () => Hex1bColor.DarkGray);

    /// <summary>
    /// Background color for the track.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> TrackBackgroundColor =
        new($"{nameof(SliderTheme)}.{nameof(TrackBackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Foreground color for the track when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedTrackForegroundColor =
        new($"{nameof(SliderTheme)}.{nameof(FocusedTrackForegroundColor)}", () => Hex1bColor.Gray);

    /// <summary>
    /// Foreground color for the handle when unfocused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HandleForegroundColor =
        new($"{nameof(SliderTheme)}.{nameof(HandleForegroundColor)}", () => Hex1bColor.DarkGray);

    /// <summary>
    /// Background color for the handle when unfocused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HandleBackgroundColor =
        new($"{nameof(SliderTheme)}.{nameof(HandleBackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Foreground color for the handle when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedHandleForegroundColor =
        new($"{nameof(SliderTheme)}.{nameof(FocusedHandleForegroundColor)}", () => Hex1bColor.LightGray);

    /// <summary>
    /// Background color for the handle when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedHandleBackgroundColor =
        new($"{nameof(SliderTheme)}.{nameof(FocusedHandleBackgroundColor)}", () => Hex1bColor.Default);
}
