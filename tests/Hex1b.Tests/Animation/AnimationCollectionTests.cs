using Hex1b;
using Hex1b.Animation;

namespace Hex1b.Tests.Animation;

[TestClass]
public class AnimationCollectionTests
{
    [TestMethod]
    public void Get_CreatesNewAnimator()
    {
        var collection = new AnimationCollection();

        var animator = collection.Get<NumericAnimator<double>>("fade");

        Assert.IsNotNull(animator);
        TestSeq.IsType<NumericAnimator<double>>(animator);
    }

    [TestMethod]
    public void Get_ReturnsSameInstance()
    {
        var collection = new AnimationCollection();

        var a1 = collection.Get<NumericAnimator<double>>("fade");
        var a2 = collection.Get<NumericAnimator<double>>("fade");

        Assert.AreSame(a1, a2);
    }

    [TestMethod]
    public void Get_ConfigureCalledOnCreation()
    {
        var collection = new AnimationCollection();

        var animator = collection.Get<NumericAnimator<double>>("fade", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(500);
        });

        Assert.AreEqual(TimeSpan.FromMilliseconds(500), animator.Duration);
        Assert.IsTrue(animator.IsRunning); // auto-started
    }

    [TestMethod]
    public void Get_ConfigureNotCalledOnRetrieval()
    {
        var collection = new AnimationCollection();
        var configCount = 0;

        collection.Get<NumericAnimator<double>>("fade", _ => configCount++);
        collection.Get<NumericAnimator<double>>("fade", _ => configCount++);

        // Configure called only once (on creation)
        Assert.AreEqual(1, configCount);
    }

    [TestMethod]
    public void Get_AutoStartTrue_StartsAnimator()
    {
        var collection = new AnimationCollection();
        var animator = collection.Get<NumericAnimator<double>>("fade");

        Assert.IsTrue(animator.IsRunning);
    }

    [TestMethod]
    public void Get_AutoStartFalse_DoesNotStartAnimator()
    {
        var collection = new AnimationCollection();
        var animator = collection.Get<NumericAnimator<double>>("fade", autoStart: false);

        Assert.IsFalse(animator.IsRunning);
    }

    [TestMethod]
    public void Get_DifferentNames_DifferentInstances()
    {
        var collection = new AnimationCollection();

        var a = collection.Get<NumericAnimator<double>>("fade-in");
        var b = collection.Get<NumericAnimator<double>>("fade-out");

        Assert.AreNotSame(a, b);
    }

    [TestMethod]
    public void AdvanceAll_TicksAllRunning()
    {
        var collection = new AnimationCollection();
        var a1 = collection.Get<NumericAnimator<double>>("a1", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(100);
        });
        var a2 = collection.Get<NumericAnimator<double>>("a2", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(200);
        });

        collection.AdvanceAll(TimeSpan.FromMilliseconds(50));

        Assert.AreEqual(0.5, a1.RawProgress, 6);
        Assert.AreEqual(0.25, a2.RawProgress, 6);
    }

    [TestMethod]
    public void AdvanceAll_SkipsNonRunning()
    {
        var collection = new AnimationCollection();
        var a1 = collection.Get<NumericAnimator<double>>("running", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(100);
        });
        var a2 = collection.Get<NumericAnimator<double>>("stopped", autoStart: false);

        collection.AdvanceAll(TimeSpan.FromMilliseconds(50));

        Assert.AreEqual(0.5, a1.RawProgress, 6);
        Assert.AreEqual(0.0, a2.RawProgress, 6);
    }

    [TestMethod]
    public void HasActiveAnimations_TrueWhenRunning()
    {
        var collection = new AnimationCollection();
        collection.Get<NumericAnimator<double>>("fade");

        Assert.IsTrue(collection.HasActiveAnimations);
    }

    [TestMethod]
    public void HasActiveAnimations_FalseWhenNoneRunning()
    {
        var collection = new AnimationCollection();
        collection.Get<NumericAnimator<double>>("fade", autoStart: false);

        Assert.IsFalse(collection.HasActiveAnimations);
    }

    [TestMethod]
    public void DisposeAll_ClearsCollection()
    {
        var collection = new AnimationCollection();
        collection.Get<NumericAnimator<double>>("fade");

        collection.Dispose();

        Assert.IsFalse(collection.HasActiveAnimations);
        // Get after dispose creates a new instance
        var animator = collection.Get<NumericAnimator<double>>("fade", autoStart: false);
        Assert.IsFalse(animator.IsRunning);
    }

    [TestMethod]
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
            var anim = sp.GetAnimations().Get<NumericAnimator<double>>("fade", a =>
            {
                a.Duration = TimeSpan.FromMilliseconds(500);
            });
            progressOnFrame1 = anim.RawProgress;
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        var node = await widget1.ReconcileAsync(null, context);
        Assert.AreEqual(0.0, progressOnFrame1, 6); // Just started

        // Wait real time so Stopwatch.GetTimestamp() moves forward
        await Task.Delay(100);

        // Frame 2: re-reconcile — animations should have advanced
        var widget2 = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            var anim = sp.GetAnimations().Get<NumericAnimator<double>>("fade");
            progressOnFrame2 = anim.RawProgress;
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        await widget2.ReconcileAsync(node, context);

        // Animation MUST have advanced — this is the bug the original code missed
        Assert.IsTrue(progressOnFrame2 > 0.0, $"Animation did not advance! Progress was {progressOnFrame2} on frame 2");
    }

    [TestMethod]
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
            sp.GetAnimations().Get<NumericAnimator<double>>("fade", a =>
            {
                a.Duration = TimeSpan.FromMilliseconds(500);
            });
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        await widget.ReconcileAsync(null, context);

        // Timer callback MUST be scheduled for re-render
        Assert.IsNotEmpty(scheduledCallbacks);
        Assert.AreEqual(TimeSpan.FromMilliseconds(16), scheduledCallbacks[0].delay);
    }

    [TestMethod]
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

        Assert.IsEmpty(scheduledCallbacks);
    }

    [TestMethod]
    public async Task Reconcile_AnimationValue_ChangesOverTime()
    {
        // End-to-end: animation value is different between frames
        var stateKey = new object();
        double valueFrame1 = -1;
        double valueFrame2 = -1;

        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();

        var widget = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            var anim = sp.GetAnimations().Get<NumericAnimator<double>>("slide", a =>
            {
                a.From = 0.0;
                a.To = 100.0;
                a.Duration = TimeSpan.FromMilliseconds(200);
            });
            return new Hex1b.Widgets.TextBlockWidget($"Value: {anim.Value}");
        });

        // Frame 1
        var node = await widget.ReconcileAsync(null, context);
        valueFrame1 = ((Hex1b.Nodes.StatePanelNode)node).GetState(() => new AnimationCollection())
            .Get<NumericAnimator<double>>("slide").Value;

        await Task.Delay(100);

        // Frame 2
        var widget2 = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            var anim = sp.GetAnimations().Get<NumericAnimator<double>>("slide");
            valueFrame2 = anim.Value;
            return new Hex1b.Widgets.TextBlockWidget($"Value: {anim.Value}");
        });
        await widget2.ReconcileAsync(node, context);

        Assert.AreEqual(0.0, valueFrame1, 1);
        Assert.IsTrue(valueFrame2 > 0.0, $"NumericAnimator value did not change! Was {valueFrame2}");
    }

    // --- Integration with StatePanel ---

    [TestMethod]
    public async Task StatePanelContext_ExposesAnimations()
    {
        AnimationCollection? capturedAnimations = null;
        var stateKey = new object();

        var widget = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            capturedAnimations = sp.GetAnimations();
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();
        await widget.ReconcileAsync(null, context);

        Assert.IsNotNull(capturedAnimations);
    }

    [TestMethod]
    public async Task Animations_PersistAcrossReconciliation()
    {
        var stateKey = new object();
        AnimationCollection? animations1 = null;
        AnimationCollection? animations2 = null;

        var widget1 = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            animations1 = sp.GetAnimations();
            sp.GetAnimations().Get<NumericAnimator<double>>("fade");
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();
        var node = await widget1.ReconcileAsync(null, context);

        var widget2 = new Hex1b.Widgets.StatePanelWidget(stateKey, sp =>
        {
            animations2 = sp.GetAnimations();
            return new Hex1b.Widgets.TextBlockWidget("test");
        });

        await widget2.ReconcileAsync(node, context);

        // Same AnimationCollection instance persists
        Assert.AreSame(animations1, animations2);
        Assert.IsTrue(animations2!.HasActiveAnimations);
    }

    [TestMethod]
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
                childAnimations = csp.GetAnimations();
                csp.GetAnimations().Get<NumericAnimator<double>>("fade");
                return new Hex1b.Widgets.TextBlockWidget("child");
            }));

        var root = await widget1.ReconcileAsync(null, context);
        Assert.IsTrue(childAnimations!.HasActiveAnimations);

        // Frame 2: root without child — child gets swept
        var widget2 = new Hex1b.Widgets.StatePanelWidget(keyRoot, sp =>
            new Hex1b.Widgets.TextBlockWidget("no child"));

        await widget2.ReconcileAsync(root, context);

        // Animations should be disposed
        Assert.IsFalse(childAnimations.HasActiveAnimations);
    }
}
