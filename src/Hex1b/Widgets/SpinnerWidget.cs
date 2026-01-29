namespace Hex1b.Widgets;

/// <summary>
/// Displays an animated spinner to indicate ongoing activity.
/// </summary>
/// <remarks>
/// <para>
/// SpinnerWidget shows a single animated character (or multi-character sequence) that cycles
/// through frames to indicate ongoing work.
/// </para>
/// <para>
/// By default, spinners are time-based: the animation runs at the style's suggested interval
/// regardless of how often the screen is redrawn. Use <see cref="AnimationExtensions.RedrawAfter{TWidget}(TWidget, int)"/>
/// to schedule periodic redraws.
/// </para>
/// <para>
/// For manual control, set <see cref="FrameIndex"/> to explicitly control which frame is displayed.
/// </para>
/// <para>
/// The spinner style can be set explicitly or inherited from the theme via
/// <see cref="Theming.SpinnerTheme.Style"/>.
/// </para>
/// </remarks>
/// <example>
/// <para>Time-based spinner (recommended):</para>
/// <code>
/// ctx.Spinner()
///    .RedrawAfter(100); // Redraw periodically; animation is time-based
/// </code>
/// <para>Spinner with explicit style:</para>
/// <code>
/// ctx.Spinner(SpinnerStyle.Arrow)
///    .RedrawAfter(100);
/// </code>
/// <para>Manual frame control:</para>
/// <code>
/// ctx.Spinner(SpinnerStyle.Dots, frameIndex: myFrameCounter)
/// </code>
/// </example>
/// <seealso cref="SpinnerStyle"/>
/// <seealso cref="SpinnerExtensions"/>
/// <seealso cref="Theming.SpinnerTheme"/>
public sealed record SpinnerWidget : Hex1bWidget
{
    /// <summary>
    /// Gets the explicit spinner style, or null to use the theme's default.
    /// </summary>
    public SpinnerStyle? Style { get; init; }

    /// <summary>
    /// Gets the explicit frame index for manual control, or null for time-based animation.
    /// </summary>
    /// <remarks>
    /// When null (default), the spinner calculates the current frame based on elapsed time
    /// and the style's interval. This ensures consistent animation speed regardless of redraw rate.
    /// When set, the spinner displays exactly the specified frame and no automatic redraw is scheduled.
    /// </remarks>
    public int? FrameIndex { get; init; }

    /// <summary>
    /// Gets the redraw delay for animation, auto-configured from style interval.
    /// </summary>
    /// <remarks>
    /// For time-based spinners (FrameIndex is null), this defaults to the style's interval
    /// so animation happens automatically. For manual frame control, no auto-redraw is scheduled.
    /// </remarks>
    public new TimeSpan? RedrawDelay
    {
        get => base.RedrawDelay ?? GetDefaultRedrawDelay();
        init => base.RedrawDelay = value;
    }

    private TimeSpan? GetDefaultRedrawDelay()
    {
        // Only auto-schedule for time-based mode (no explicit frame)
        if (FrameIndex.HasValue)
        {
            return null;
        }

        // Use style interval or default
        return Style?.Interval ?? SpinnerStyle.Dots.Interval;
    }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SpinnerNode ?? new SpinnerNode();

        // Mark dirty if properties changed
        if (node.Style != Style || node.ExplicitFrameIndex != FrameIndex)
        {
            node.MarkDirty();
        }

        node.Style = Style;
        node.ExplicitFrameIndex = FrameIndex;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(SpinnerNode);
}
