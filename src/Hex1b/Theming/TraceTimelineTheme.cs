using Hex1b.Charts;

namespace Hex1b.Theming;

/// <summary>
/// Theme elements for TraceTimeline widgets.
/// </summary>
public static class TraceTimelineTheme
{
    /// <summary>
    /// Default fractional block characters for sub-cell precision (0/8 through 8/8, left-to-right).
    /// </summary>
    private static readonly string[] DefaultBarBlocks =
    [
        " ", // 0/8 — empty
        "▏", // 1/8
        "▎", // 2/8
        "▍", // 3/8
        "▌", // 4/8
        "▋", // 5/8
        "▊", // 6/8
        "▉", // 7/8
        "█", // 8/8 — full
    ];
    #region Bar Colors

    /// <summary>
    /// Bar color for spans with <see cref="TraceSpanStatus.Ok"/> status.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> OkBarColor =
        new($"{nameof(TraceTimelineTheme)}.{nameof(OkBarColor)}", () => Hex1bColor.Green);

    /// <summary>
    /// Bar color for spans with <see cref="TraceSpanStatus.Error"/> status.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ErrorBarColor =
        new($"{nameof(TraceTimelineTheme)}.{nameof(ErrorBarColor)}", () => Hex1bColor.Red);

    /// <summary>
    /// Bar color for spans with <see cref="TraceSpanStatus.Unset"/> status.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnsetBarColor =
        new($"{nameof(TraceTimelineTheme)}.{nameof(UnsetBarColor)}", () => Hex1bColor.DarkGray);

    /// <summary>
    /// Bar color for the inner (self-time) duration portion within a span bar.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> InnerDurationBarColor =
        new($"{nameof(TraceTimelineTheme)}.{nameof(InnerDurationBarColor)}", () => Hex1bColor.Yellow);

    /// <summary>
    /// The lane/track color shown in the leading gap before a span bar starts.
    /// This represents the "runway" — time elapsed before the span began.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> LaneColor =
        new($"{nameof(TraceTimelineTheme)}.{nameof(LaneColor)}", () => Hex1bColor.FromRgb(40, 40, 40));

    #endregion

    #region Duration Label

    /// <summary>
    /// Foreground color for the duration label displayed after the bar.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DurationLabelColor =
        new($"{nameof(TraceTimelineTheme)}.{nameof(DurationLabelColor)}", () => Hex1bColor.DarkGray);

    #endregion

    #region Bar Block Style

    /// <summary>
    /// The set of block characters used for sub-cell precision rendering of span bars.
    /// An ordered list of 9 strings representing 0/8 through 8/8 fill (left-to-right).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default uses Unicode fractional block elements (▏▎▍▌▋▊▉█) for smooth sub-cell bars.
    /// Set all entries to "█" for a blockier/chunky look.
    /// </para>
    /// <para>
    /// Both bar edges are handled by the same left-to-right character set via foreground/background
    /// color inversion — no right-to-left block characters needed.
    /// </para>
    /// </remarks>
    public static readonly Hex1bThemeElement<IReadOnlyList<string>> BarBlockStyle =
        new($"{nameof(TraceTimelineTheme)}.{nameof(BarBlockStyle)}", () => DefaultBarBlocks);

    #endregion
}
