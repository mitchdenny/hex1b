using Hex1b.Charts;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Internal widget that renders a single span's timeline bar with sub-cell precision.
/// </summary>
/// <remarks>
/// This widget is not intended for direct use — it is composed internally by
/// <see cref="TraceTimelineWidget{T}"/> to render one row of the timeline panel.
/// </remarks>
internal sealed record TraceTimelineSpanWidget : Hex1bWidget
{
    /// <summary>
    /// The fractional start position of the bar within the timeline (0.0 to 1.0).
    /// </summary>
    public double StartFraction { get; init; }

    /// <summary>
    /// The fractional width of the outer (total) duration bar (0.0 to 1.0).
    /// </summary>
    public double DurationFraction { get; init; }

    /// <summary>
    /// The optional fractional width of the inner (self-time) duration bar (0.0 to 1.0).
    /// When null, only the outer bar is rendered.
    /// </summary>
    public double? InnerDurationFraction { get; init; }

    /// <summary>
    /// The span status, used to select the bar color from the theme.
    /// </summary>
    public TraceSpanStatus Status { get; init; }

    /// <summary>
    /// The formatted duration label displayed after the bar (e.g. "120ms", "1.2s").
    /// </summary>
    public string? DurationLabel { get; init; }

    /// <summary>
    /// Fixed width for the duration label column. All span bars use the same width
    /// so bars and labels are vertically aligned. When 0, the label's natural width is used.
    /// </summary>
    public int DurationLabelWidth { get; init; }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TraceTimelineSpanNode ?? new TraceTimelineSpanNode();

        node.MarkDirty();

        node.StartFraction = StartFraction;
        node.DurationFraction = DurationFraction;
        node.InnerDurationFraction = InnerDurationFraction;
        node.Status = Status;
        node.DurationLabel = DurationLabel;
        node.DurationLabelWidth = DurationLabelWidth;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(TraceTimelineSpanNode);
}
