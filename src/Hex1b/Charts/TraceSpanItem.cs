using Hex1b.Theming;

namespace Hex1b.Charts;

/// <summary>
/// Status of a trace span, matching OTEL span status codes.
/// </summary>
public enum TraceSpanStatus
{
    /// <summary>The default status, indicating no status has been set.</summary>
    Unset,

    /// <summary>The span completed successfully.</summary>
    Ok,

    /// <summary>The span completed with an error.</summary>
    Error,
}

/// <summary>
/// A convenience data type for ad-hoc trace timeline data.
/// </summary>
/// <remarks>
/// <para>
/// When used with <see cref="TraceTimelineExtensions"/> overloads that accept <see cref="TraceSpanItem"/>,
/// all selectors (label, start time, duration, span ID, parent ID) are pre-wired automatically.
/// </para>
/// </remarks>
/// <param name="SpanId">Unique identifier for this span.</param>
/// <param name="ParentSpanId">The parent span ID, or null for root spans.</param>
/// <param name="Label">The operation name or display label for the span.</param>
/// <param name="ServiceName">Optional service name for grouping or display.</param>
/// <param name="StartTime">When the span started.</param>
/// <param name="Duration">Total (outer) duration of the span.</param>
/// <param name="InnerDuration">Optional self-time excluding child spans. When null, inner duration is not shown.</param>
/// <param name="Status">The span status for color-coding. Defaults to <see cref="TraceSpanStatus.Ok"/>.</param>
public record TraceSpanItem(
    string SpanId,
    string? ParentSpanId,
    string Label,
    string? ServiceName,
    DateTimeOffset StartTime,
    TimeSpan Duration,
    TimeSpan? InnerDuration = null,
    TraceSpanStatus Status = TraceSpanStatus.Ok);
