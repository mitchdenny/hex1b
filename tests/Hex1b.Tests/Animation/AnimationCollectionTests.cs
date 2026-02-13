using Hex1b.Animation;

namespace Hex1b.Tests.Animation;

public class AnimationCollectionTests
{
    [Fact]
    public void Get_CreatesNewAnimator()
    {
        var collection = new AnimationCollection();

        var animator = collection.Get<OpacityAnimator>("fade");

        Assert.NotNull(animator);
        Assert.IsType<OpacityAnimator>(animator);
    }

    [Fact]
    public void Get_ReturnsSameInstance()
    {
        var collection = new AnimationCollection();

        var a1 = collection.Get<OpacityAnimator>("fade");
        var a2 = collection.Get<OpacityAnimator>("fade");

        Assert.Same(a1, a2);
    }

    [Fact]
    public void Get_ConfigureCalledOnCreation()
    {
        var collection = new AnimationCollection();

        var animator = collection.Get<OpacityAnimator>("fade", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(500);
        });

        Assert.Equal(TimeSpan.FromMilliseconds(500), animator.Duration);
        Assert.True(animator.IsRunning); // auto-started
    }

    [Fact]
    public void Get_ConfigureNotCalledOnRetrieval()
    {
        var collection = new AnimationCollection();
        var configCount = 0;

        collection.Get<OpacityAnimator>("fade", _ => configCount++);
        collection.Get<OpacityAnimator>("fade", _ => configCount++);

        // Configure called only once (on creation)
        Assert.Equal(1, configCount);
    }

    [Fact]
    public void Get_AutoStartTrue_StartsAnimator()
    {
        var collection = new AnimationCollection();
        var animator = collection.Get<OpacityAnimator>("fade");

        Assert.True(animator.IsRunning);
    }

    [Fact]
    public void Get_AutoStartFalse_DoesNotStartAnimator()
    {
        var collection = new AnimationCollection();
        var animator = collection.Get<OpacityAnimator>("fade", autoStart: false);

        Assert.False(animator.IsRunning);
    }

    [Fact]
    public void Get_DifferentNames_DifferentInstances()
    {
        var collection = new AnimationCollection();

        var a = collection.Get<OpacityAnimator>("fade-in");
        var b = collection.Get<OpacityAnimator>("fade-out");

        Assert.NotSame(a, b);
    }

    [Fact]
    public void AdvanceAll_TicksAllRunning()
    {
        var collection = new AnimationCollection();
        var a1 = collection.Get<OpacityAnimator>("a1", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(100);
        });
        var a2 = collection.Get<OpacityAnimator>("a2", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(200);
        });

        collection.AdvanceAll(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0.5, a1.RawProgress, 6);
        Assert.Equal(0.25, a2.RawProgress, 6);
    }

    [Fact]
    public void AdvanceAll_SkipsNonRunning()
    {
        var collection = new AnimationCollection();
        var a1 = collection.Get<OpacityAnimator>("running", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(100);
        });
        var a2 = collection.Get<OpacityAnimator>("stopped", autoStart: false);

        collection.AdvanceAll(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0.5, a1.RawProgress, 6);
        Assert.Equal(0.0, a2.RawProgress, 6);
    }

    [Fact]
    public void HasActiveAnimations_TrueWhenRunning()
    {
        var collection = new AnimationCollection();
        collection.Get<OpacityAnimator>("fade");

        Assert.True(collection.HasActiveAnimations);
    }

    [Fact]
    public void HasActiveAnimations_FalseWhenNoneRunning()
    {
        var collection = new AnimationCollection();
        collection.Get<OpacityAnimator>("fade", autoStart: false);

        Assert.False(collection.HasActiveAnimations);
    }

    [Fact]
    public void DisposeAll_ClearsCollection()
    {
        var collection = new AnimationCollection();
        collection.Get<OpacityAnimator>("fade");

        collection.DisposeAll();

        Assert.False(collection.HasActiveAnimations);
        // Get after dispose creates a new instance
        var animator = collection.Get<OpacityAnimator>("fade", autoStart: false);
        Assert.False(animator.IsRunning);
    }

    [Fact]
    public async Task Reconcile_AdvancesAnimations_AcrossFrames()
    {
        // This is the critical test: prove that animations actually progress
        // when reconciliation happens across multiple frames.
        var stateKey = new object();
        double progressOnFrame1 = -1;
        double progressOnFrame2 = -1;

        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();

        // Frame 1: create animation (auto-started)
        var widget1 = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            var anim = sp.Animations.Get<OpacityAnimator>("fade", a =>
            {
                a.Duration = TimeSpan.FromMilliseconds(500);
            });
            progressOnFrame1 = anim.RawProgress;
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        var node = await widget1.ReconcileAsync(null, context);
        Assert.Equal(0.0, progressOnFrame1, 6); // Just started

        // Wait real time so Stopwatch.GetTimestamp() moves forward
        await Task.Delay(100);

        // Frame 2: re-reconcile — animations should have advanced
        var widget2 = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            var anim = sp.Animations.Get<OpacityAnimator>("fade");
            progressOnFrame2 = anim.RawProgress;
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        await widget2.ReconcileAsync(node, context);

        // Animation MUST have advanced — this is the bug the original code missed
        Assert.True(progressOnFrame2 > 0.0,
            $"Animation did not advance! Progress was {progressOnFrame2} on frame 2");
    }

    [Fact]
    public async Task Reconcile_SchedulesTimerCallback_WhenAnimationsActive()
    {
        var stateKey = new object();
        var scheduledCallbacks = new List<(TimeSpan delay, Action callback)>();

        var context = Hex1b.Widgets.ReconcileContext.CreateRoot(
            scheduleTimerCallback: (delay, cb) => scheduledCallbacks.Add((delay, cb)),
            invalidateCallback: () => { });

        // Frame 1: create animation (auto-started)
        var widget = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            sp.Animations.Get<OpacityAnimator>("fade", a =>
            {
                a.Duration = TimeSpan.FromMilliseconds(500);
            });
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        await widget.ReconcileAsync(null, context);

        // Timer callback MUST be scheduled for re-render
        Assert.NotEmpty(scheduledCallbacks);
        Assert.Equal(TimeSpan.FromMilliseconds(16), scheduledCallbacks[0].delay);
    }

    [Fact]
    public async Task Reconcile_DoesNotScheduleTimer_WhenNoActiveAnimations()
    {
        var stateKey = new object();
        var scheduledCallbacks = new List<(TimeSpan delay, Action callback)>();

        var context = Hex1b.Widgets.ReconcileContext.CreateRoot(
            scheduleTimerCallback: (delay, cb) => scheduledCallbacks.Add((delay, cb)),
            invalidateCallback: () => { });

        // No animations started
        var widget = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
            new Hex1b.Widgets.TextBlockWidget("test"));

        await widget.ReconcileAsync(null, context);

        Assert.Empty(scheduledCallbacks);
    }

    [Fact]
    public async Task Reconcile_AnimationValue_ChangesOverTime()
    {
        // End-to-end: animation value is different between frames
        var stateKey = new object();
        double valueFrame1 = -1;
        double valueFrame2 = -1;

        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();

        var widget = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            var anim = sp.Animations.Get<NumericAnimator<double>>("slide", a =>
            {
                a.From = 0.0;
                a.To = 100.0;
                a.Duration = TimeSpan.FromMilliseconds(200);
            });
            return new Hex1b.Widgets.TextBlockWidget($"Value: {anim.Value}");
        });

        // Frame 1
        var node = await widget.ReconcileAsync(null, context);
        valueFrame1 = ((Hex1b.Nodes.StatePanelNode)node).Animations
            .Get<NumericAnimator<double>>("slide").Value;

        await Task.Delay(100);

        // Frame 2
        var widget2 = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            var anim = sp.Animations.Get<NumericAnimator<double>>("slide");
            valueFrame2 = anim.Value;
            return new Hex1b.Widgets.TextBlockWidget($"Value: {anim.Value}");
        });
        await widget2.ReconcileAsync(node, context);

        Assert.Equal(0.0, valueFrame1, 1);
        Assert.True(valueFrame2 > 0.0,
            $"NumericAnimator value did not change! Was {valueFrame2}");
    }

    // --- Integration with StatePanel ---

    [Fact]
    public async Task StatePanelContext_ExposesAnimations()
    {
        AnimationCollection? capturedAnimations = null;
        var stateKey = new object();

        var widget = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            capturedAnimations = sp.Animations;
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();
        await widget.ReconcileAsync(null, context);

        Assert.NotNull(capturedAnimations);
    }

    [Fact]
    public async Task Animations_PersistAcrossReconciliation()
    {
        var stateKey = new object();
        AnimationCollection? animations1 = null;
        AnimationCollection? animations2 = null;

        var widget1 = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            animations1 = sp.Animations;
            sp.Animations.Get<OpacityAnimator>("fade");
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();
        var node = await widget1.ReconcileAsync(null, context);

        var widget2 = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            animations2 = sp.Animations;
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        await widget2.ReconcileAsync(node, context);

        // Same AnimationCollection instance persists
        Assert.Same(animations1, animations2);
        Assert.True(animations2!.HasActiveAnimations);
    }

    [Fact]
    public async Task Animations_DisposedOnSweep()
    {
        var keyRoot = new object();
        var keyChild = new object();
        AnimationCollection? childAnimations = null;

        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();

        // Frame 1: root with child that has animations
        var widget1 = new Hex1b.Widgets.StatePanelWidget(keyRoot, sp =>
            new Hex1b.Widgets.StatePanelWidget(keyChild, csp =>
            {
                childAnimations = csp.Animations;
                csp.Animations.Get<OpacityAnimator>("fade");
                return new Hex1b.Widgets.TextBlockWidget("child");
            }));

        var root = await widget1.ReconcileAsync(null, context);
        Assert.True(childAnimations!.HasActiveAnimations);

        // Frame 2: root without child — child gets swept
        var widget2 = new Hex1b.Widgets.StatePanelWidget(keyRoot, sp =>
            new Hex1b.Widgets.TextBlockWidget("no child"));

        await widget2.ReconcileAsync(root, context);

        // Animations should be disposed
        Assert.False(childAnimations.HasActiveAnimations);
    }
}
