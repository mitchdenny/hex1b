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

    #endregion

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TraceTimelineNode<T> ?? new TraceTimelineNode<T>();
        node.MarkDirty();

        if (Data == null || Data.Count == 0 || LabelSelector == null ||
            StartTimeSelector == null || DurationSelector == null ||
            SpanIdSelector == null || ParentIdSelector == null)
        {
            node.ComposedChild = null;
            return node;
        }

        // Build the tree structure from flat spans
        var tree = BuildSpanTree();

        // Compute trace-wide time range
        var (traceStart, traceDuration) = ComputeTimeRange();

        // Build TreeWidget items and matching span widgets
        var treeItems = new List<TreeItemWidget>();
        var spanWidgets = new List<Hex1bWidget>();
        BuildWidgets(tree, traceStart, traceDuration, treeItems, spanWidgets);

        // Left panel: standard TreeWidget with labels
        var treeWidget = new TreeWidget(treeItems);

        // Right panel: VStack of span bar widgets (matches flattened tree order)
        var timelinePanel = new VStackWidget(spanWidgets.ToArray());

        // Compose: Splitter(Tree, Timeline)
        var splitter = new SplitterWidget(treeWidget, timelinePanel, firstSize: 30);

        // Reconcile the composed widget
        node.ComposedChild = await context.ReconcileChildAsync(node.ComposedChild, splitter, node);

        return node;
    }

    private void BuildWidgets(
        List<SpanTreeEntry> treeEntries,
        DateTimeOffset traceStart,
        TimeSpan traceDuration,
        List<TreeItemWidget> treeItems,
        List<Hex1bWidget> spanWidgets)
    {
        var data = Data!;

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

            var spanWidget = new TraceTimelineSpanWidget
            {
                StartFraction = startFrac,
                DurationFraction = durationFrac,
                InnerDurationFraction = innerFrac,
                Status = status,
                DurationLabel = FormatDuration(duration),
            };

            // Build tree item
            var treeItem = new TreeItemWidget(label);

            if (entry.Children.Count > 0)
            {
                var childTreeItems = new List<TreeItemWidget>();
                var childSpanWidgets = new List<Hex1bWidget>();
                BuildWidgets(entry.Children, traceStart, traceDuration, childTreeItems, childSpanWidgets);
                treeItem = treeItem.Children(childTreeItems.ToArray()).Expanded();

                // This span first, then children (matches flattened tree order)
                spanWidgets.Add(spanWidget);
                spanWidgets.AddRange(childSpanWidgets);
            }
            else
            {
                spanWidgets.Add(spanWidget);
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

    private static string FormatDuration(TimeSpan duration)
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
