using Hex1b.Charts;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="TraceTimelineWidget{T}"/> instances.
/// </summary>
public static class TraceTimelineExtensions
{
    /// <summary>
    /// Creates a trace timeline bound to the specified data.
    /// </summary>
    /// <typeparam name="T">The data item type.</typeparam>
    /// <param name="ctx">The root context.</param>
    /// <param name="data">The data source for the trace timeline.</param>
    public static TraceTimelineWidget<T> TraceTimeline<T>(this RootContext ctx, IReadOnlyList<T> data)
        => new() { Data = data };

    /// <summary>
    /// Creates a trace timeline with <see cref="TraceSpanItem"/> data (selectors pre-wired).
    /// </summary>
    public static TraceTimelineWidget<TraceSpanItem> TraceTimeline(this RootContext ctx, IReadOnlyList<TraceSpanItem> data)
        => new()
        {
            Data = data,
            LabelSelector = s => s.Label,
            StartTimeSelector = s => s.StartTime,
            DurationSelector = s => s.Duration,
            SpanIdSelector = s => s.SpanId,
            ParentIdSelector = s => s.ParentSpanId,
            InnerDurationSelector = s => s.InnerDuration,
            StatusSelector = s => s.Status,
            ServiceNameSelector = s => s.ServiceName,
        };

    /// <summary>
    /// Creates a trace timeline with <see cref="TraceSpanItem"/> data (params overload).
    /// </summary>
    public static TraceTimelineWidget<TraceSpanItem> TraceTimeline(this RootContext ctx, params TraceSpanItem[] data)
        => ctx.TraceTimeline((IReadOnlyList<TraceSpanItem>)data);

    /// <summary>
    /// Creates a trace timeline bound to the specified data.
    /// </summary>
    /// <typeparam name="T">The data item type.</typeparam>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="data">The data source for the trace timeline.</param>
    public static TraceTimelineWidget<T> TraceTimeline<T, TParent>(
        this WidgetContext<TParent> ctx, IReadOnlyList<T> data)
        where TParent : Hex1bWidget
        => new() { Data = data };

    /// <summary>
    /// Creates a trace timeline with <see cref="TraceSpanItem"/> data (selectors pre-wired).
    /// </summary>
    public static TraceTimelineWidget<TraceSpanItem> TraceTimeline<TParent>(
        this WidgetContext<TParent> ctx, IReadOnlyList<TraceSpanItem> data)
        where TParent : Hex1bWidget
        => new()
        {
            Data = data,
            LabelSelector = s => s.Label,
            StartTimeSelector = s => s.StartTime,
            DurationSelector = s => s.Duration,
            SpanIdSelector = s => s.SpanId,
            ParentIdSelector = s => s.ParentSpanId,
            InnerDurationSelector = s => s.InnerDuration,
            StatusSelector = s => s.Status,
            ServiceNameSelector = s => s.ServiceName,
        };

    /// <summary>
    /// Creates a trace timeline with <see cref="TraceSpanItem"/> data (params overload).
    /// </summary>
    public static TraceTimelineWidget<TraceSpanItem> TraceTimeline<TParent>(
        this WidgetContext<TParent> ctx, params TraceSpanItem[] data)
        where TParent : Hex1bWidget
        => ctx.TraceTimeline((IReadOnlyList<TraceSpanItem>)data);
}
