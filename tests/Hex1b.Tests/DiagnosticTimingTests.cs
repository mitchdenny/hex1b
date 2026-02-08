using System.Diagnostics;
using Hex1b.Diagnostics;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Tests;

/// <summary>
/// Tests for diagnostic timing and tree output improvements.
/// </summary>
public class DiagnosticTimingTests
{
    [Fact]
    public void DiagnosticTiming_FromNode_ConvertsTicksToMilliseconds()
    {
        var node = new TextBlockNode { Text = "test" };
        // Simulate 1ms of reconcile time (using Stopwatch frequency)
        node.DiagReconcileTicks = Stopwatch.Frequency / 1000; // 1ms
        node.DiagRenderTicks = Stopwatch.Frequency / 2000; // 0.5ms
        node.DiagLastRenderedTimestamp = Stopwatch.GetTimestamp() - Stopwatch.Frequency / 100; // 10ms ago

        var now = Stopwatch.GetTimestamp();
        var timing = DiagnosticTiming.FromNode(node, now);

        Assert.InRange(timing.ReconcileMs, 0.9, 1.1);
        Assert.InRange(timing.RenderMs, 0.4, 0.6);
        Assert.InRange(timing.LastRenderedMsAgo, 5, 15); // ~10ms ago with tolerance
    }

    [Fact]
    public void DiagnosticTiming_FromNode_ZeroTicks_ReturnsZeros()
    {
        var node = new TextBlockNode { Text = "test" };
        var now = Stopwatch.GetTimestamp();

        var timing = DiagnosticTiming.FromNode(node, now);

        Assert.Equal(0, timing.ReconcileMs);
        Assert.Equal(0, timing.RenderMs);
        Assert.Equal(-1, timing.LastRenderedMsAgo);
    }

    [Fact]
    public void DiagnosticTiming_ToString_FormatsCorrectly()
    {
        var timing = new DiagnosticTiming
        {
            ReconcileMs = 0.15,
            RenderMs = 0.30,
            LastRenderedMsAgo = 12
        };

        var result = timing.ToString();

        Assert.Contains("reconcile=0.15ms", result);
        Assert.Contains("render=0.30ms", result);
        Assert.Contains("last=12ms ago", result);
    }

    [Fact]
    public void DiagnosticTiming_ToString_OmitsZeroValues()
    {
        var timing = new DiagnosticTiming
        {
            ReconcileMs = 0,
            RenderMs = 0.5,
            LastRenderedMsAgo = -1
        };

        var result = timing.ToString();

        Assert.DoesNotContain("reconcile", result);
        Assert.Contains("render=0.50ms", result);
        Assert.DoesNotContain("last=", result);
    }

    [Fact]
    public void DiagnosticNode_FromNode_IncludesTimingWhenSet()
    {
        var node = new ButtonNode { Label = "Click" };
        node.DiagReconcileTicks = Stopwatch.Frequency / 1000; // 1ms
        node.DiagRenderTicks = Stopwatch.Frequency / 2000; // 0.5ms
        node.DiagLastRenderedTimestamp = Stopwatch.GetTimestamp();

        var diagNode = DiagnosticNode.FromNode(node);

        Assert.NotNull(diagNode.Timing);
        Assert.True(diagNode.Timing.ReconcileMs > 0);
        Assert.True(diagNode.Timing.RenderMs > 0);
    }

    [Fact]
    public void DiagnosticNode_FromNode_NoTimingWhenZero()
    {
        var node = new ButtonNode { Label = "Click" };
        // No timing fields set — all are default 0

        var diagNode = DiagnosticNode.FromNode(node);

        Assert.Null(diagNode.Timing);
    }

    [Fact]
    public void DiagnosticRect_ToString_IncludesCornerCoordinates()
    {
        var rect = DiagnosticRect.FromRect(new Rect(5, 10, 20, 8));

        var str = rect.ToString();

        Assert.Contains("x=5", str);
        Assert.Contains("y=10", str);
        Assert.Contains("w=20", str);
        Assert.Contains("h=8", str);
        Assert.Contains("(5,10 → 25,18)", str);
    }

    [Fact]
    public void DiagnosticFrameInfo_ReportsTimingEnabled()
    {
        var frameInfo = new DiagnosticFrameInfo
        {
            BuildMs = 1.5,
            ReconcileMs = 0.3,
            RenderMs = 2.0,
            TimingEnabled = true
        };

        Assert.True(frameInfo.TimingEnabled);
        Assert.Equal(1.5, frameInfo.BuildMs);
        Assert.Equal(0.3, frameInfo.ReconcileMs);
        Assert.Equal(2.0, frameInfo.RenderMs);
    }

    [Fact]
    public void Hex1bNode_TimingFields_DefaultToZero()
    {
        var node = new TextBlockNode { Text = "test" };

        Assert.Equal(0, node.DiagReconcileTicks);
        Assert.Equal(0, node.DiagRenderTicks);
        Assert.Equal(0, node.DiagLastRenderedTimestamp);
    }
}
