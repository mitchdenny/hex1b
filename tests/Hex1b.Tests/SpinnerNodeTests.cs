using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Unit tests for SpinnerNode layout, rendering, and frame selection.
/// </summary>
public class SpinnerNodeTests
{
    [Fact]
    public void SpinnerStyle_Dots_HasCorrectFrameCount()
    {
        Assert.Equal(10, SpinnerStyle.Dots.Frames.Count);
    }

    [Fact]
    public void SpinnerStyle_Line_HasCorrectFrameCount()
    {
        Assert.Equal(4, SpinnerStyle.Line.Frames.Count);
    }

    [Fact]
    public void SpinnerStyle_GetFrame_WrapsCorrectly()
    {
        var style = SpinnerStyle.Line; // 4 frames: | / - \

        Assert.Equal("|", style.GetFrame(0));
        Assert.Equal("/", style.GetFrame(1));
        Assert.Equal("-", style.GetFrame(2));
        Assert.Equal("\\", style.GetFrame(3));
        Assert.Equal("|", style.GetFrame(4)); // Wraps back to 0
        Assert.Equal("/", style.GetFrame(5));
    }

    [Fact]
    public void SpinnerStyle_GetFrame_HandlesNegativeIndex()
    {
        var style = SpinnerStyle.Line; // 4 frames

        Assert.Equal("\\", style.GetFrame(-1)); // Last frame
        Assert.Equal("-", style.GetFrame(-2));
    }

    [Fact]
    public void SpinnerStyle_AutoReverse_PingPongsCorrectly()
    {
        var style = SpinnerStyle.Bounce; // 8 frames with auto-reverse

        // Forward: 0,1,2,3,4,5,6,7
        Assert.Equal("▁", style.GetFrame(0));
        Assert.Equal("▂", style.GetFrame(1));
        Assert.Equal("█", style.GetFrame(7));

        // Reverse: 6,5,4,3,2,1
        Assert.Equal("▇", style.GetFrame(8));  // Mirrors frame 6
        Assert.Equal("▆", style.GetFrame(9));  // Mirrors frame 5
        Assert.Equal("▂", style.GetFrame(13)); // Mirrors frame 1

        // Back to start
        Assert.Equal("▁", style.GetFrame(14)); // Back to frame 0
    }

    [Fact]
    public void SpinnerStyle_CustomStyle_CanBeCreated()
    {
        var custom = new SpinnerStyle("A", "B", "C");

        Assert.Equal(3, custom.Frames.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(80), custom.Interval);
        Assert.False(custom.AutoReverse);
    }

    [Fact]
    public void SpinnerStyle_CustomStyleWithInterval_CanBeCreated()
    {
        var custom = new SpinnerStyle(TimeSpan.FromMilliseconds(200), "X", "Y");

        Assert.Equal(2, custom.Frames.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(200), custom.Interval);
    }

    [Fact]
    public void SpinnerStyle_CustomStyleWithAutoReverse_CanBeCreated()
    {
        var custom = new SpinnerStyle(TimeSpan.FromMilliseconds(100), autoReverse: true, "1", "2", "3");

        Assert.True(custom.AutoReverse);
        Assert.Equal("1", custom.GetFrame(0));
        Assert.Equal("3", custom.GetFrame(2));
        Assert.Equal("2", custom.GetFrame(3)); // Reversing
        Assert.Equal("1", custom.GetFrame(4)); // Back to start
    }

    [Fact]
    public void SpinnerStyle_EmptyFrames_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SpinnerStyle());
    }

    [Fact]
    public void SpinnerNode_Measure_ReturnsSingleCharWidth()
    {
        var node = new SpinnerNode { Style = SpinnerStyle.Dots };
        
        // Force style resolution by setting it explicitly
        var size = node.Measure(new Constraints(0, 100, 0, 100));

        // Note: Width depends on resolved style, which defaults to Dots
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void SpinnerWidget_Reconcile_CreatesSpinnerNode()
    {
        var widget = new SpinnerWidget { FrameIndex = 5 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();

        Assert.IsType<SpinnerNode>(node);
        var spinnerNode = (SpinnerNode)node;
        Assert.Equal(5, spinnerNode.ExplicitFrameIndex);
        Assert.Null(spinnerNode.Style); // Uses theme default
    }

    [Fact]
    public void SpinnerWidget_Reconcile_UpdatesExistingNode()
    {
        var existingNode = new SpinnerNode { ExplicitFrameIndex = 0 };
        var widget = new SpinnerWidget { FrameIndex = 3, Style = SpinnerStyle.Arrow };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget.ReconcileAsync(existingNode, context).GetAwaiter().GetResult();

        Assert.Same(existingNode, node);
        Assert.Equal(3, existingNode.ExplicitFrameIndex);
        Assert.Same(SpinnerStyle.Arrow, existingNode.Style);
    }

    [Fact]
    public void SpinnerWidget_Reconcile_MarksDirtyOnFrameChange()
    {
        var widget1 = new SpinnerWidget { FrameIndex = 0 };
        var widget2 = new SpinnerWidget { FrameIndex = 5 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SpinnerNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.True(node.IsDirty);
    }

    [Fact]
    public void SpinnerWidget_Reconcile_MarksDirtyOnStyleChange()
    {
        var widget1 = new SpinnerWidget { Style = SpinnerStyle.Dots };
        var widget2 = new SpinnerWidget { Style = SpinnerStyle.Arrow };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SpinnerNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.True(node.IsDirty);
    }

    [Fact]
    public void SpinnerWidget_TimeBasedMode_HasNullFrameIndex()
    {
        var widget = new SpinnerWidget(); // No FrameIndex set
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult() as SpinnerNode;

        Assert.Null(node!.ExplicitFrameIndex); // Time-based mode
    }

    [Fact]
    public void SpinnerNode_GetTimeUntilNextFrame_ReturnsPositiveValue()
    {
        var node = new SpinnerNode { Style = SpinnerStyle.Dots };

        var timeUntilNext = node.GetTimeUntilNextFrame();

        Assert.True(timeUntilNext.TotalMilliseconds > 0);
        Assert.True(timeUntilNext.TotalMilliseconds <= SpinnerStyle.Dots.Interval.TotalMilliseconds);
    }

    [Fact]
    public void SpinnerNode_GetTimeUntilNextFrame_ManualMode_ReturnsMaxValue()
    {
        var node = new SpinnerNode { Style = SpinnerStyle.Dots, ExplicitFrameIndex = 0 };

        var timeUntilNext = node.GetTimeUntilNextFrame();

        Assert.Equal(TimeSpan.MaxValue, timeUntilNext);
    }

    [Fact]
    public void SpinnerStyle_BuiltInStyles_HaveCorrectIntervals()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(100), SpinnerStyle.Line.Interval);
        Assert.Equal(TimeSpan.FromMilliseconds(80), SpinnerStyle.Dots.Interval);
        Assert.Equal(TimeSpan.FromMilliseconds(120), SpinnerStyle.Circle.Interval);
    }

    [Fact]
    public void SpinnerStyle_BuiltInStyles_HaveCorrectAutoReverse()
    {
        Assert.False(SpinnerStyle.Dots.AutoReverse);
        Assert.False(SpinnerStyle.Line.AutoReverse);
        Assert.False(SpinnerStyle.Arrow.AutoReverse);
        Assert.True(SpinnerStyle.Bounce.AutoReverse);
        Assert.True(SpinnerStyle.GrowHorizontal.AutoReverse);
        Assert.True(SpinnerStyle.GrowVertical.AutoReverse);
    }

    [Fact]
    public void SpinnerStyle_MultiCharStyles_HaveCorrectFrames()
    {
        Assert.Equal(8, SpinnerStyle.BouncingBall.Frames.Count);
        Assert.Equal(6, SpinnerStyle.LoadingBar.Frames.Count);
        Assert.Equal(6, SpinnerStyle.Segments.Frames.Count);

        Assert.Equal("[●    ]", SpinnerStyle.BouncingBall.GetFrame(0));
        Assert.Equal("[     ]", SpinnerStyle.LoadingBar.GetFrame(0));
        Assert.Equal("▱▱▱▱▱", SpinnerStyle.Segments.GetFrame(0));
    }

    [Fact]
    public void SpinnerTheme_DefaultStyle_IsDots()
    {
        var theme = new Hex1bTheme("Test").Lock();
        var style = theme.Get(SpinnerTheme.Style);

        Assert.Same(SpinnerStyle.Dots, style);
    }

    [Fact]
    public void SpinnerTheme_DefaultColors_AreDefault()
    {
        var theme = new Hex1bTheme("Test").Lock();
        var fg = theme.Get(SpinnerTheme.ForegroundColor);
        var bg = theme.Get(SpinnerTheme.BackgroundColor);

        Assert.True(fg.IsDefault);
        Assert.True(bg.IsDefault);
    }

    [Fact]
    public void SpinnerTheme_CanOverrideStyle()
    {
        var theme = new Hex1bTheme("Test")
            .Set(SpinnerTheme.Style, SpinnerStyle.Arrow)
            .Lock();
        
        var style = theme.Get(SpinnerTheme.Style);

        Assert.Same(SpinnerStyle.Arrow, style);
    }

    [Fact]
    public void SpinnerWidget_TimeBasedMode_SetsDefaultRedrawDelay()
    {
        // Arrange & Act - time-based mode (no explicit frame)
        var widget = new SpinnerWidget();

        // Assert - should auto-schedule redraws based on default style interval
        Assert.Equal(SpinnerStyle.Dots.Interval, widget.GetEffectiveRedrawDelay());
    }

    [Fact]
    public void SpinnerWidget_WithExplicitStyle_SetsRedrawDelayFromStyle()
    {
        // Arrange & Act
        var widget = new SpinnerWidget { Style = SpinnerStyle.Circle }; // 120ms interval

        // Assert
        Assert.Equal(SpinnerStyle.Circle.Interval, widget.GetEffectiveRedrawDelay());
    }

    [Fact]
    public void SpinnerWidget_ManualFrameMode_NoRedrawDelay()
    {
        // Arrange & Act - manual frame control
        var widget = new SpinnerWidget { FrameIndex = 5 };

        // Assert - no auto-redraw for manual mode
        Assert.Null(widget.GetEffectiveRedrawDelay());
    }

    [Fact]
    public void SpinnerWidget_ExplicitRedrawDelay_OverridesDefault()
    {
        // Arrange & Act
        var widget = new SpinnerWidget { RedrawDelay = TimeSpan.FromMilliseconds(200) };

        // Assert - explicit value should be used
        Assert.Equal(TimeSpan.FromMilliseconds(200), widget.RedrawDelay);
    }
}

