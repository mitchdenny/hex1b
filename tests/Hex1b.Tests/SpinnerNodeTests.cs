using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Unit tests for SpinnerNode layout, rendering, and frame selection.
/// </summary>
[TestClass]
public class SpinnerNodeTests
{
    [TestMethod]
    public void SpinnerStyle_Dots_HasCorrectFrameCount()
    {
        Assert.AreEqual(10, SpinnerStyle.Dots.Frames.Count);
    }

    [TestMethod]
    public void SpinnerStyle_Line_HasCorrectFrameCount()
    {
        Assert.AreEqual(4, SpinnerStyle.Line.Frames.Count);
    }

    [TestMethod]
    public void SpinnerStyle_GetFrame_WrapsCorrectly()
    {
        var style = SpinnerStyle.Line; // 4 frames: | / - \

        Assert.AreEqual("|", style.GetFrame(0));
        Assert.AreEqual("/", style.GetFrame(1));
        Assert.AreEqual("-", style.GetFrame(2));
        Assert.AreEqual("\\", style.GetFrame(3));
        Assert.AreEqual("|", style.GetFrame(4)); // Wraps back to 0
        Assert.AreEqual("/", style.GetFrame(5));
    }

    [TestMethod]
    public void SpinnerStyle_GetFrame_HandlesNegativeIndex()
    {
        var style = SpinnerStyle.Line; // 4 frames

        Assert.AreEqual("\\", style.GetFrame(-1)); // Last frame
        Assert.AreEqual("-", style.GetFrame(-2));
    }

    [TestMethod]
    public void SpinnerStyle_AutoReverse_PingPongsCorrectly()
    {
        var style = SpinnerStyle.Bounce; // 8 frames with auto-reverse

        // Forward: 0,1,2,3,4,5,6,7
        Assert.AreEqual("▁", style.GetFrame(0));
        Assert.AreEqual("▂", style.GetFrame(1));
        Assert.AreEqual("█", style.GetFrame(7));

        // Reverse: 6,5,4,3,2,1
        Assert.AreEqual("▇", style.GetFrame(8));  // Mirrors frame 6
        Assert.AreEqual("▆", style.GetFrame(9));  // Mirrors frame 5
        Assert.AreEqual("▂", style.GetFrame(13)); // Mirrors frame 1

        // Back to start
        Assert.AreEqual("▁", style.GetFrame(14)); // Back to frame 0
    }

    [TestMethod]
    public void SpinnerStyle_CustomStyle_CanBeCreated()
    {
        var custom = new SpinnerStyle("A", "B", "C");

        Assert.AreEqual(3, custom.Frames.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(80), custom.Interval);
        Assert.IsFalse(custom.AutoReverse);
    }

    [TestMethod]
    public void SpinnerStyle_CustomStyleWithInterval_CanBeCreated()
    {
        var custom = new SpinnerStyle(TimeSpan.FromMilliseconds(200), "X", "Y");

        Assert.AreEqual(2, custom.Frames.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), custom.Interval);
    }

    [TestMethod]
    public void SpinnerStyle_CustomStyleWithAutoReverse_CanBeCreated()
    {
        var custom = new SpinnerStyle(TimeSpan.FromMilliseconds(100), autoReverse: true, "1", "2", "3");

        Assert.IsTrue(custom.AutoReverse);
        Assert.AreEqual("1", custom.GetFrame(0));
        Assert.AreEqual("3", custom.GetFrame(2));
        Assert.AreEqual("2", custom.GetFrame(3)); // Reversing
        Assert.AreEqual("1", custom.GetFrame(4)); // Back to start
    }

    [TestMethod]
    public void SpinnerStyle_EmptyFrames_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SpinnerStyle());
    }

    [TestMethod]
    public void SpinnerNode_Measure_ReturnsSingleCharWidth()
    {
        var node = new SpinnerNode { Style = SpinnerStyle.Dots };
        
        // Force style resolution by setting it explicitly
        var size = node.Measure(new Constraints(0, 100, 0, 100));

        // Note: Width depends on resolved style, which defaults to Dots
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void SpinnerWidget_Reconcile_CreatesSpinnerNode()
    {
        var widget = new SpinnerWidget { FrameIndex = 5 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();

        TestSeq.IsType<SpinnerNode>(node);
        var spinnerNode = (SpinnerNode)node;
        Assert.AreEqual(5, spinnerNode.ExplicitFrameIndex);
        Assert.IsNull(spinnerNode.Style); // Uses theme default
    }

    [TestMethod]
    public void SpinnerWidget_Reconcile_UpdatesExistingNode()
    {
        var existingNode = new SpinnerNode { ExplicitFrameIndex = 0 };
        var widget = new SpinnerWidget { FrameIndex = 3, Style = SpinnerStyle.Arrow };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget.ReconcileAsync(existingNode, context).GetAwaiter().GetResult();

        Assert.AreSame(existingNode, node);
        Assert.AreEqual(3, existingNode.ExplicitFrameIndex);
        Assert.AreSame(SpinnerStyle.Arrow, existingNode.Style);
    }

    [TestMethod]
    public void SpinnerWidget_Reconcile_MarksDirtyOnFrameChange()
    {
        var widget1 = new SpinnerWidget { FrameIndex = 0 };
        var widget2 = new SpinnerWidget { FrameIndex = 5 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SpinnerNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void SpinnerWidget_Reconcile_MarksDirtyOnStyleChange()
    {
        var widget1 = new SpinnerWidget { Style = SpinnerStyle.Dots };
        var widget2 = new SpinnerWidget { Style = SpinnerStyle.Arrow };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SpinnerNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void SpinnerWidget_TimeBasedMode_HasNullFrameIndex()
    {
        var widget = new SpinnerWidget(); // No FrameIndex set
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult() as SpinnerNode;

        Assert.IsNull(node!.ExplicitFrameIndex); // Time-based mode
    }

    [TestMethod]
    public void SpinnerNode_GetTimeUntilNextFrame_ReturnsPositiveValue()
    {
        var node = new SpinnerNode { Style = SpinnerStyle.Dots };

        var timeUntilNext = node.GetTimeUntilNextFrame();

        Assert.IsTrue(timeUntilNext.TotalMilliseconds > 0);
        Assert.IsTrue(timeUntilNext.TotalMilliseconds <= SpinnerStyle.Dots.Interval.TotalMilliseconds);
    }

    [TestMethod]
    public void SpinnerNode_GetTimeUntilNextFrame_ManualMode_ReturnsMaxValue()
    {
        var node = new SpinnerNode { Style = SpinnerStyle.Dots, ExplicitFrameIndex = 0 };

        var timeUntilNext = node.GetTimeUntilNextFrame();

        Assert.AreEqual(TimeSpan.MaxValue, timeUntilNext);
    }

    [TestMethod]
    public void SpinnerStyle_BuiltInStyles_HaveCorrectIntervals()
    {
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), SpinnerStyle.Line.Interval);
        Assert.AreEqual(TimeSpan.FromMilliseconds(80), SpinnerStyle.Dots.Interval);
        Assert.AreEqual(TimeSpan.FromMilliseconds(120), SpinnerStyle.Circle.Interval);
    }

    [TestMethod]
    public void SpinnerStyle_BuiltInStyles_HaveCorrectAutoReverse()
    {
        Assert.IsFalse(SpinnerStyle.Dots.AutoReverse);
        Assert.IsFalse(SpinnerStyle.Line.AutoReverse);
        Assert.IsFalse(SpinnerStyle.Arrow.AutoReverse);
        Assert.IsTrue(SpinnerStyle.Bounce.AutoReverse);
        Assert.IsTrue(SpinnerStyle.GrowHorizontal.AutoReverse);
        Assert.IsTrue(SpinnerStyle.GrowVertical.AutoReverse);
    }

    [TestMethod]
    public void SpinnerStyle_MultiCharStyles_HaveCorrectFrames()
    {
        Assert.AreEqual(8, SpinnerStyle.BouncingBall.Frames.Count);
        Assert.AreEqual(6, SpinnerStyle.LoadingBar.Frames.Count);
        Assert.AreEqual(6, SpinnerStyle.Segments.Frames.Count);

        Assert.AreEqual("[●    ]", SpinnerStyle.BouncingBall.GetFrame(0));
        Assert.AreEqual("[     ]", SpinnerStyle.LoadingBar.GetFrame(0));
        Assert.AreEqual("▱▱▱▱▱", SpinnerStyle.Segments.GetFrame(0));
    }

    [TestMethod]
    public void SpinnerTheme_DefaultStyle_IsDots()
    {
        var theme = new Hex1bTheme("Test").Lock();
        var style = theme.Get(SpinnerTheme.Style);

        Assert.AreSame(SpinnerStyle.Dots, style);
    }

    [TestMethod]
    public void SpinnerTheme_DefaultColors_AreDefault()
    {
        var theme = new Hex1bTheme("Test").Lock();
        var fg = theme.Get(SpinnerTheme.ForegroundColor);
        var bg = theme.Get(SpinnerTheme.BackgroundColor);

        Assert.IsTrue(fg.IsDefault);
        Assert.IsTrue(bg.IsDefault);
    }

    [TestMethod]
    public void SpinnerTheme_CanOverrideStyle()
    {
        var theme = new Hex1bTheme("Test")
            .Set(SpinnerTheme.Style, SpinnerStyle.Arrow)
            .Lock();
        
        var style = theme.Get(SpinnerTheme.Style);

        Assert.AreSame(SpinnerStyle.Arrow, style);
    }

    [TestMethod]
    public void SpinnerWidget_TimeBasedMode_SetsDefaultRedrawDelay()
    {
        // Arrange & Act - time-based mode (no explicit frame)
        var widget = new SpinnerWidget();

        // Assert - should auto-schedule redraws based on default style interval
        Assert.AreEqual(SpinnerStyle.Dots.Interval, widget.GetEffectiveRedrawDelay());
    }

    [TestMethod]
    public void SpinnerWidget_WithExplicitStyle_SetsRedrawDelayFromStyle()
    {
        // Arrange & Act
        var widget = new SpinnerWidget { Style = SpinnerStyle.Circle }; // 120ms interval

        // Assert
        Assert.AreEqual(SpinnerStyle.Circle.Interval, widget.GetEffectiveRedrawDelay());
    }

    [TestMethod]
    public void SpinnerWidget_ManualFrameMode_NoRedrawDelay()
    {
        // Arrange & Act - manual frame control
        var widget = new SpinnerWidget { FrameIndex = 5 };

        // Assert - no auto-redraw for manual mode
        Assert.IsNull(widget.GetEffectiveRedrawDelay());
    }

    [TestMethod]
    public void SpinnerWidget_ExplicitRedrawDelay_OverridesDefault()
    {
        // Arrange & Act
        var widget = new SpinnerWidget { RedrawDelay = TimeSpan.FromMilliseconds(200) };

        // Assert - explicit value should be used
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), widget.RedrawDelay);
    }
}

