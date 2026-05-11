namespace Hex1b.Theming;

/// <summary>
/// Theme elements specific to <see cref="Widgets.SplitButtonWidget"/>'s
/// secondary-affordance region — the divider plus dropdown arrow that
/// appears when secondary actions exist.
/// </summary>
/// <remarks>
/// SplitButton inherits its primary-region (label + chip padding) styling
/// from <see cref="ButtonTheme"/>; this class adds a dedicated colour
/// pair for the divider/arrow cells so the dropdown affordance reads as
/// distinct from the primary action.
///
/// All defaults are a half-shade darker (resting/hovered) or slightly
/// dimmed (focused) compared with their <see cref="ButtonTheme"/>
/// counterparts. Setting any colour to <see cref="Hex1bColor.Default"/>
/// makes the arrow region inherit the matching primary colour, producing
/// a uniform chip without a visible secondary tint.
/// </remarks>
public static class SplitButtonTheme
{
    /// <summary>
    /// Foreground colour for the divider + arrow text in the resting state.
    /// Defaults to <see cref="Hex1bColor.Default"/> so it inherits the
    /// global text colour, matching the resting primary label treatment.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ArrowForegroundColor =
        new($"{nameof(SplitButtonTheme)}.{nameof(ArrowForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Resting background tint for the divider + arrow region. Defaults to a
    /// half-shade darker than <see cref="ButtonTheme.BackgroundColor"/>
    /// so the dropdown affordance reads as a distinct sub-region of the chip.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ArrowBackgroundColor =
        new($"{nameof(SplitButtonTheme)}.{nameof(ArrowBackgroundColor)}", () => Hex1bColor.FromRgb(50, 50, 50));

    /// <summary>
    /// Foreground colour for the divider + arrow text when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedArrowForegroundColor =
        new($"{nameof(SplitButtonTheme)}.{nameof(FocusedArrowForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// Background tint for the divider + arrow region when focused. Defaults
    /// to a slightly dimmed white so the secondary affordance is still
    /// distinguishable from the brighter primary chip while staying inside
    /// the high-contrast focused palette.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedArrowBackgroundColor =
        new($"{nameof(SplitButtonTheme)}.{nameof(FocusedArrowBackgroundColor)}", () => Hex1bColor.FromRgb(225, 225, 225));

    /// <summary>
    /// Foreground colour for the divider + arrow text when hovered.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HoveredArrowForegroundColor =
        new($"{nameof(SplitButtonTheme)}.{nameof(HoveredArrowForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// Background tint for the divider + arrow region when hovered. Half-shade
    /// darker than <see cref="ButtonTheme.HoveredBackgroundColor"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HoveredArrowBackgroundColor =
        new($"{nameof(SplitButtonTheme)}.{nameof(HoveredArrowBackgroundColor)}", () => Hex1bColor.FromRgb(160, 160, 160));
}
