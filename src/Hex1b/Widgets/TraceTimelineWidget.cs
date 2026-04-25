using Hex1b.Charts;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A composite widget that displays an OTEL-style trace timeline with a tree view
/// on the left (showing span hierarchy) and timeline bars on the right (showing duration).
/// </summary>
/// <typeparam name="T">The type of data item bound to the trace timeline.</typeparam>
/// <remarks>
/// <para>
/// The widget accepts a flat list of spans and builds the tree structure internally
/// using <see cref="SpanIdSelector"/> and <see cref="ParentIdSelector"/>. Root spans
/// are those with a null or missing parent ID.
/// </para>
/// <para>
/// Internally composes a <see cref="TreeWidget"/> for hierarchy navigation and a
/// <see cref="VStackWidget"/> of <see cref="TraceTimelineSpanWidget"/> instances for
/// the timeline bars, laid out side-by-side in a <see cref="SplitterWidget"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ctx.TraceTimeline([
///     new TraceSpanItem("1", null, "GET /api/orders", "api-gateway",
///         DateTimeOffset.Now, TimeSpan.FromMilliseconds(450)),
///     new TraceSpanItem("2", "1", "authenticate", "auth-svc",
///         DateTimeOffset.Now.AddMilliseconds(20), TimeSpan.FromMilliseconds(100)),
/// ])
/// </code>
/// </example>
public sealed record TraceTimelineWidget<T> : Hex1bWidget
{
    /// <summary>
    /// Gets the data source for the trace timeline.
    /// </summary>
    public IReadOnlyList<T>? Data { get; init; }

    /// <summary>
    /// Function to extract the display label from a data item.
    /// </summary>
    internal Func<T, string>? LabelSelector { get; init; }

    /// <summary>
    /// Function to extract the start time from a data item.
    /// </summary>
    internal Func<T, DateTimeOffset>? StartTimeSelector { get; init; }

    /// <summary>
    /// Function to extract the duration from a data item.
    /// </summary>
    internal Func<T, TimeSpan>? DurationSelector { get; init; }

    /// <summary>
    /// Function to extract the span ID from a data item.
    /// </summary>
    internal Func<T, string>? SpanIdSelector { get; init; }

    /// <summary>
    /// Function to extract the parent span ID from a data item.
    /// </summary>
    internal Func<T, string?>? ParentIdSelector { get; init; }

    /// <summary>
    /// Optional function to extract the inner (self-time) duration from a data item.
    /// </summary>
    internal Func<T, TimeSpan?>? InnerDurationSelector { get; init; }

    /// <summary>
    /// Optional function to extract the status from a data item.
    /// </summary>
    internal Func<T, TraceSpanStatus>? StatusSelector { get; init; }

    /// <summary>
    /// Optional function to extract the service name from a data item.
    /// </summary>
    internal Func<T, string?>? ServiceNameSelector { get; init; }

    /// <summary>
    /// Optional custom formatter for duration labels. When null, a default formatter is used
    /// that produces human-readable strings like "120ms", "1.2s", "2.5min".
    /// </summary>
    internal Func<TimeSpan, string>? DurationFormatter { get; init; }

    #region Fluent API

    /// <summary>Sets the function that extracts the display label.</summary>
    public TraceTimelineWidget<T> Label(Func<T, string> selector)
        => this with { LabelSelector = selector };

    /// <summary>Sets the function that extracts the start time.</summary>
    public TraceTimelineWidget<T> StartTime(Func<T, DateTimeOffset> selector)
        => this with { StartTimeSelector = selector };

    /// <summary>Sets the function that extracts the duration.</summary>
    public TraceTimelineWidget<T> Duration(Func<T, TimeSpan> selector)
        => this with { DurationSelector = selector };

    /// <summary>Sets the function that extracts the span ID.</summary>
    public TraceTimelineWidget<T> SpanId(Func<T, string> selector)
        => this with { SpanIdSelector = selector };

    /// <summary>Sets the function that extracts the parent span ID.</summary>
    public TraceTimelineWidget<T> ParentId(Func<T, string?> selector)
        => this with { ParentIdSelector = selector };

    /// <summary>Sets the optional function that extracts the inner (self-time) duration.</summary>
    public TraceTimelineWidget<T> InnerDuration(Func<T, TimeSpan?> selector)
        => this with { InnerDurationSelector = selector };

    /// <summary>Sets the optional function that extracts the span status.</summary>
    public TraceTimelineWidget<T> Status(Func<T, TraceSpanStatus> selector)
        => this with { StatusSelector = selector };

    /// <summary>Sets the optional function that extracts the service name.</summary>
    public TraceTimelineWidget<T> ServiceName(Func<T, string?> selector)
        => this with { ServiceNameSelector = selector };

    /// <summary>
    /// Sets a custom formatter for duration labels displayed after each span bar.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.TraceTimeline(data).FormatDuration(d => $"{d.TotalSeconds:F2}s")
    /// </code>
    /// </example>
    public TraceTimelineWidget<T> FormatDuration(Func<TimeSpan, string> formatter)
        => this with { DurationFormatter = formatter };

    #endregion

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TraceTimelineNode<T> ?? new TraceTimelineNode<T>();
        node.MarkDirty();

        if (Data == null || Data.Count == 0 || LabelSelector == null ||
            StartTimeSelector == null || DurationSelector == null ||
            SpanIdSelector == null || ParentIdSelector == null)
        {
            node.TreeChild = null;
            node.TimelineChild = null;
            return node;
        }

        // Build the tree structure from flat spans
        var tree = BuildSpanTree();

        // Compute trace-wide time range
        var (traceStart, traceDuration) = ComputeTimeRange();

        // Build TreeWidget items with span data attached for correlation
        var treeItems = new List<TreeItemWidget>();
        BuildTreeItems(tree, treeItems);

        // Phase 1: Reconcile the tree first (preserves expand/collapse state)
        var treeWidget = new TreeWidget(treeItems);
        node.TreeChild = await context.ReconcileChildAsync(node.TreeChild, treeWidget, node);

        // Phase 2: Query visible items from the reconciled tree
        var treeNode = node.TreeChild as TreeNode;
        var visibleItems = treeNode?.GetVisibleItems() ?? [];

        // Compute max duration label width across visible items for alignment
        var maxDurationLabelWidth = 0;
        foreach (var visibleItem in visibleItems)
        {
            if (visibleItem.TryGetData<SpanTimingData>(out var t))
            {
                maxDurationLabelWidth = Math.Max(maxDurationLabelWidth, t.DurationLabel.Length);
            }
        }

        // Phase 3: Build span widgets only for visible items
        var spanWidgets = new List<Hex1bWidget>();
        foreach (var visibleItem in visibleItems)
        {
            if (visibleItem.TryGetData<SpanTimingData>(out var timing))
            {
                spanWidgets.Add(new TraceTimelineSpanWidget
                {
                    StartFraction = timing.StartFraction,
                    DurationFraction = timing.DurationFraction,
                    InnerDurationFraction = timing.InnerDurationFraction,
                    Status = timing.Status,
                    DurationLabel = timing.DurationLabel,
                    DurationLabelWidth = maxDurationLabelWidth,
                });
            }
        }

        // Reconcile the timeline VStack
        var timelinePanel = new VStackWidget(spanWidgets.ToArray());
        node.TimelineChild = await context.ReconcileChildAsync(node.TimelineChild, timelinePanel, node);

        return node;
    }

    private void BuildTreeItems(
        List<SpanTreeEntry> treeEntries,
        List<TreeItemWidget> treeItems)
    {
        var data = Data!;
        var (traceStart, traceDuration) = ComputeTimeRange();

        foreach (var entry in treeEntries)
        {
            var item = data[entry.DataIndex];
            var label = LabelSelector!(item);
            var startTime = StartTimeSelector!(item);
            var duration = DurationSelector!(item);
            var status = StatusSelector?.Invoke(item) ?? TraceSpanStatus.Ok;
            var innerDuration = InnerDurationSelector?.Invoke(item);

            // Compute fractional positions
            var startFrac = (startTime - traceStart).TotalMilliseconds / traceDuration.TotalMilliseconds;
            var durationFrac = duration.TotalMilliseconds / traceDuration.TotalMilliseconds;
            double? innerFrac = innerDuration.HasValue
                ? innerDuration.Value.TotalMilliseconds / traceDuration.TotalMilliseconds
                : null;

            var timing = new SpanTimingData(startFrac, durationFrac, innerFrac, status, FormatSpanDuration(duration));

            // Build tree item with timing data for correlation
            var treeItem = new TreeItemWidget(label).Data(timing);

            if (entry.Children.Count > 0)
            {
                var childTreeItems = new List<TreeItemWidget>();
                BuildTreeItems(entry.Children, childTreeItems);
                treeItem = treeItem.Children(childTreeItems.ToArray()).Expanded();
            }

            treeItems.Add(treeItem);
        }
    }

    private List<SpanTreeEntry> BuildSpanTree()
    {
        var data = Data!;
        var spanIdSelector = SpanIdSelector!;
        var parentIdSelector = ParentIdSelector!;

        // Index children by parent ID
        var childrenByParent = new Dictionary<string, List<int>>();
        var rootIndices = new List<int>();

        for (int i = 0; i < data.Count; i++)
        {
            var parentId = parentIdSelector(data[i]);
            if (string.IsNullOrEmpty(parentId))
            {
                rootIndices.Add(i);
            }
            else
            {
                if (!childrenByParent.TryGetValue(parentId, out var children))
                {
                    children = new List<int>();
                    childrenByParent[parentId] = children;
                }
                children.Add(i);
            }
        }

        // Sort roots and children by start time
        var startTimeSelector = StartTimeSelector!;
        rootIndices.Sort((a, b) => startTimeSelector(data[a]).CompareTo(startTimeSelector(data[b])));

        var result = new List<SpanTreeEntry>();
        foreach (var rootIdx in rootIndices)
        {
            BuildTreeRecursive(rootIdx, childrenByParent, result);
        }

        return result;
    }

    private void BuildTreeRecursive(int dataIndex, Dictionary<string, List<int>> childrenByParent, List<SpanTreeEntry> result)
    {
        var data = Data!;
        var spanId = SpanIdSelector!(data[dataIndex]);
        var entry = new SpanTreeEntry(dataIndex);

        if (childrenByParent.TryGetValue(spanId, out var childIndices))
        {
            var startTimeSelector = StartTimeSelector!;
            childIndices.Sort((a, b) => startTimeSelector(data[a]).CompareTo(startTimeSelector(data[b])));

            foreach (var childIdx in childIndices)
            {
                BuildTreeRecursive(childIdx, childrenByParent, entry.Children);
            }
        }

        result.Add(entry);
    }

    private (DateTimeOffset traceStart, TimeSpan traceDuration) ComputeTimeRange()
    {
        var data = Data!;
        var startTimeSelector = StartTimeSelector!;
        var durationSelector = DurationSelector!;

        var minStart = DateTimeOffset.MaxValue;
        var maxEnd = DateTimeOffset.MinValue;

        foreach (var item in data)
        {
            var start = startTimeSelector(item);
            var end = start + durationSelector(item);
            if (start < minStart) minStart = start;
            if (end > maxEnd) maxEnd = end;
        }

        var traceDuration = maxEnd - minStart;
        if (traceDuration <= TimeSpan.Zero)
            traceDuration = TimeSpan.FromMilliseconds(1);

        return (minStart, traceDuration);
    }

    private string FormatSpanDuration(TimeSpan duration)
    {
        if (DurationFormatter != null)
            return DurationFormatter(duration);
        return DefaultFormatDuration(duration);
    }

    private static string DefaultFormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F1}min";
        if (duration.TotalSeconds >= 1)
            return $"{duration.TotalSeconds:F1}s";
        return $"{duration.TotalMilliseconds:F0}ms";
    }

    internal override Type GetExpectedNodeType() => typeof(TraceTimelineNode<T>);

    /// <summary>
    /// Internal helper for building the span tree from flat data.
    /// </summary>
    private sealed class SpanTreeEntry
    {
        public int DataIndex { get; }
        public List<SpanTreeEntry> Children { get; } = new();

        public SpanTreeEntry(int dataIndex)
        {
            DataIndex = dataIndex;
        }
    }
}

/// <summary>
/// Pre-computed timing data for a span, stored on tree items via Data&lt;T&gt;().
/// </summary>
internal sealed record SpanTimingData(
    double StartFraction,
    double DurationFraction,
    double? InnerDurationFraction,
    TraceSpanStatus Status,
    string DurationLabel);
