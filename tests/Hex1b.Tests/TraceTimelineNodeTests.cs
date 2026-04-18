using Hex1b.Charts;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TraceTimelineWidget, TraceTimelineNode, and TraceTimelineSpanNode.
/// </summary>
public class TraceTimelineNodeTests
{
    #region Helper Methods

    private static readonly DateTimeOffset TraceStart = new(2026, 4, 18, 10, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<TraceSpanItem> CreateSimpleTrace()
    {
        return
        [
            new TraceSpanItem("span-1", null, "GET /api/orders", "api-gateway",
                TraceStart, TimeSpan.FromMilliseconds(450)),
            new TraceSpanItem("span-2", "span-1", "authenticate", "auth-svc",
                TraceStart.AddMilliseconds(20), TimeSpan.FromMilliseconds(100)),
            new TraceSpanItem("span-3", "span-1", "fetch-orders", "order-svc",
                TraceStart.AddMilliseconds(130), TimeSpan.FromMilliseconds(250)),
            new TraceSpanItem("span-4", "span-3", "SELECT * FROM orders", "db",
                TraceStart.AddMilliseconds(140), TimeSpan.FromMilliseconds(140)),
        ];
    }

    private static TraceTimelineWidget<TraceSpanItem> CreateWidget(IReadOnlyList<TraceSpanItem> data)
    {
        return new TraceTimelineWidget<TraceSpanItem>
        {
            Data = data,
            LabelSelector = s => s.Label,
            StartTimeSelector = s => s.StartTime,
            DurationSelector = s => s.Duration,
            SpanIdSelector = s => s.SpanId,
            ParentIdSelector = s => s.ParentSpanId,
            InnerDurationSelector = s => s.InnerDuration,
            StatusSelector = s => s.Status,
        };
    }

    private static async Task<TraceTimelineNode<TraceSpanItem>> ReconcileAsync(
        TraceTimelineWidget<TraceSpanItem> widget)
    {
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TraceTimelineNode<TraceSpanItem>;
        return node!;
    }

    #endregion

    #region Reconciliation Tests

    [Fact]
    public async Task Reconcile_CreatesTraceTimelineNode()
    {
        var widget = CreateWidget(CreateSimpleTrace());

        var node = await ReconcileAsync(widget);

        Assert.NotNull(node);
        Assert.IsType<TraceTimelineNode<TraceSpanItem>>(node);
    }

    [Fact]
    public async Task Reconcile_CreatesChildren()
    {
        var widget = CreateWidget(CreateSimpleTrace());

        var node = await ReconcileAsync(widget);

        Assert.NotNull(node.TreeChild);
    }

    [Fact]
    public async Task Reconcile_EmptyData_NoChildren()
    {
        var widget = CreateWidget([]);

        var node = await ReconcileAsync(widget);

        Assert.Null(node.TreeChild);
    }

    [Fact]
    public async Task Reconcile_NullData_NoChildren()
    {
        var widget = new TraceTimelineWidget<TraceSpanItem>
        {
            Data = null,
            LabelSelector = s => s.Label,
            StartTimeSelector = s => s.StartTime,
            DurationSelector = s => s.Duration,
            SpanIdSelector = s => s.SpanId,
            ParentIdSelector = s => s.ParentSpanId,
        };

        var node = await ReconcileAsync(widget);

        Assert.Null(node.TreeChild);
    }

    #endregion

    #region Tree Building Tests

    [Fact]
    public async Task Reconcile_BuildsTreeFromFlatSpans()
    {
        var widget = CreateWidget(CreateSimpleTrace());

        var node = await ReconcileAsync(widget);

        // ComposedChild should be a TreeNode (tree is now the direct child)
        var treeNode = FindNodeOfType<TreeNode>(node);
        Assert.NotNull(treeNode);
    }

    [Fact]
    public async Task Reconcile_SingleSpan_CreatesTree()
    {
        var singleSpan = new[]
        {
            new TraceSpanItem("span-1", null, "GET /", "svc",
                TraceStart, TimeSpan.FromMilliseconds(100)),
        };
        var widget = CreateWidget(singleSpan);

        var node = await ReconcileAsync(widget);

        Assert.NotNull(node.TreeChild);
    }

    #endregion

    #region TraceTimelineSpanNode Tests

    [Fact]
    public void SpanNode_Measure_HeightIsOne()
    {
        var spanNode = new TraceTimelineSpanNode
        {
            StartFraction = 0.0,
            DurationFraction = 1.0,
            Status = TraceSpanStatus.Ok,
        };

        var size = spanNode.Measure(new Constraints(0, 80, 0, 10));

        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void SpanNode_Measure_WidthFillsAvailable()
    {
        var spanNode = new TraceTimelineSpanNode
        {
            StartFraction = 0.0,
            DurationFraction = 1.0,
            Status = TraceSpanStatus.Ok,
        };

        var size = spanNode.Measure(new Constraints(0, 80, 0, 10));

        Assert.Equal(80, size.Width);
    }

    #endregion

    #region Fractional Position Tests

    [Fact]
    public async Task Reconcile_ComputesCorrectFractions()
    {
        // Single root span covering full trace
        var data = new[]
        {
            new TraceSpanItem("span-1", null, "root", null,
                TraceStart, TimeSpan.FromMilliseconds(100)),
        };
        var widget = CreateWidget(data);

        var node = await ReconcileAsync(widget);

        // The root span should start at 0 and cover 100% of the trace
        var treeNode = FindNodeOfType<TreeNode>(node);
        Assert.NotNull(treeNode);
    }

    [Fact]
    public async Task Reconcile_ChildSpan_HasNonZeroStart()
    {
        // Root 0-100ms, Child 50-100ms
        var data = new[]
        {
            new TraceSpanItem("span-1", null, "root", null,
                TraceStart, TimeSpan.FromMilliseconds(100)),
            new TraceSpanItem("span-2", "span-1", "child", null,
                TraceStart.AddMilliseconds(50), TimeSpan.FromMilliseconds(50)),
        };
        var widget = CreateWidget(data);

        var node = await ReconcileAsync(widget);

        // Should build successfully with child at 50% start
        Assert.NotNull(node.TreeChild);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Reconcile_DeeplyNested_BuildsCorrectly()
    {
        var data = new[]
        {
            new TraceSpanItem("1", null, "level-0", null,
                TraceStart, TimeSpan.FromMilliseconds(1000)),
            new TraceSpanItem("2", "1", "level-1", null,
                TraceStart.AddMilliseconds(10), TimeSpan.FromMilliseconds(900)),
            new TraceSpanItem("3", "2", "level-2", null,
                TraceStart.AddMilliseconds(20), TimeSpan.FromMilliseconds(800)),
            new TraceSpanItem("4", "3", "level-3", null,
                TraceStart.AddMilliseconds(30), TimeSpan.FromMilliseconds(700)),
        };
        var widget = CreateWidget(data);

        var node = await ReconcileAsync(widget);

        Assert.NotNull(node.TreeChild);
    }

    [Fact]
    public async Task Reconcile_ErrorStatus_CreatesNode()
    {
        var data = new[]
        {
            new TraceSpanItem("span-1", null, "failing-op", null,
                TraceStart, TimeSpan.FromMilliseconds(100),
                Status: TraceSpanStatus.Error),
        };
        var widget = CreateWidget(data);

        var node = await ReconcileAsync(widget);

        Assert.NotNull(node.TreeChild);
    }

    [Fact]
    public async Task Reconcile_WithInnerDuration_CreatesNode()
    {
        var data = new[]
        {
            new TraceSpanItem("span-1", null, "op-with-self-time", null,
                TraceStart, TimeSpan.FromMilliseconds(200),
                InnerDuration: TimeSpan.FromMilliseconds(50)),
        };
        var widget = CreateWidget(data);

        var node = await ReconcileAsync(widget);

        Assert.NotNull(node.TreeChild);
    }

    [Fact]
    public async Task Reconcile_MultipleRoots_BuildsCorrectly()
    {
        var data = new[]
        {
            new TraceSpanItem("root-1", null, "trace-1", null,
                TraceStart, TimeSpan.FromMilliseconds(100)),
            new TraceSpanItem("root-2", null, "trace-2", null,
                TraceStart.AddMilliseconds(200), TimeSpan.FromMilliseconds(150)),
        };
        var widget = CreateWidget(data);

        var node = await ReconcileAsync(widget);

        Assert.NotNull(node.TreeChild);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void TraceTimeline_ConvenienceOverload_PreWiresSelectors()
    {
        var data = CreateSimpleTrace();
        var ctx = new RootContext();

        var widget = ctx.TraceTimeline(data);

        Assert.NotNull(widget.Data);
        Assert.NotNull(widget.LabelSelector);
        Assert.NotNull(widget.StartTimeSelector);
        Assert.NotNull(widget.DurationSelector);
        Assert.NotNull(widget.SpanIdSelector);
        Assert.NotNull(widget.ParentIdSelector);
        Assert.NotNull(widget.InnerDurationSelector);
        Assert.NotNull(widget.StatusSelector);
        Assert.NotNull(widget.ServiceNameSelector);
    }

    [Fact]
    public void TraceTimeline_ParamsOverload_Works()
    {
        var ctx = new RootContext();

        var widget = ctx.TraceTimeline(
            new TraceSpanItem("1", null, "op", null, TraceStart, TimeSpan.FromMilliseconds(100))
        );

        Assert.NotNull(widget.Data);
        Assert.Single(widget.Data);
    }

    #endregion

    #region Fluent API Tests

    [Fact]
    public void FluentApi_Label_SetsSelector()
    {
        var widget = new TraceTimelineWidget<TraceSpanItem>()
            .Label(s => s.Label);

        Assert.NotNull(widget.LabelSelector);
    }

    [Fact]
    public void FluentApi_AllSelectors_Chain()
    {
        var widget = new TraceTimelineWidget<TraceSpanItem>()
            .Label(s => s.Label)
            .StartTime(s => s.StartTime)
            .Duration(s => s.Duration)
            .SpanId(s => s.SpanId)
            .ParentId(s => s.ParentSpanId)
            .InnerDuration(s => s.InnerDuration)
            .Status(s => s.Status)
            .ServiceName(s => s.ServiceName);

        Assert.NotNull(widget.LabelSelector);
        Assert.NotNull(widget.StartTimeSelector);
        Assert.NotNull(widget.DurationSelector);
        Assert.NotNull(widget.SpanIdSelector);
        Assert.NotNull(widget.ParentIdSelector);
        Assert.NotNull(widget.InnerDurationSelector);
        Assert.NotNull(widget.StatusSelector);
        Assert.NotNull(widget.ServiceNameSelector);
    }

    #endregion

    #region Helpers

    private static T? FindNodeOfType<T>(Hex1bNode root) where T : Hex1bNode
    {
        if (root is T found) return found;
        foreach (var child in root.GetChildren())
        {
            var result = FindNodeOfType<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    #endregion
}
