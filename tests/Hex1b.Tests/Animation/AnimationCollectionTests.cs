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
            a.Start();
        });

        Assert.Equal(TimeSpan.FromMilliseconds(500), animator.Duration);
        Assert.True(animator.IsRunning);
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
            a.Start();
        });
        var a2 = collection.Get<OpacityAnimator>("a2", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(200);
            a.Start();
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
            a.Start();
        });
        var a2 = collection.Get<OpacityAnimator>("stopped");

        collection.AdvanceAll(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0.5, a1.RawProgress, 6);
        Assert.Equal(0.0, a2.RawProgress, 6);
    }

    [Fact]
    public void HasActiveAnimations_TrueWhenRunning()
    {
        var collection = new AnimationCollection();
        collection.Get<OpacityAnimator>("fade", a => a.Start());

        Assert.True(collection.HasActiveAnimations);
    }

    [Fact]
    public void HasActiveAnimations_FalseWhenNoneRunning()
    {
        var collection = new AnimationCollection();
        collection.Get<OpacityAnimator>("fade");

        Assert.False(collection.HasActiveAnimations);
    }

    [Fact]
    public void DisposeAll_ClearsCollection()
    {
        var collection = new AnimationCollection();
        collection.Get<OpacityAnimator>("fade", a => a.Start());

        collection.DisposeAll();

        Assert.False(collection.HasActiveAnimations);
        // Get after dispose creates a new instance
        var animator = collection.Get<OpacityAnimator>("fade");
        Assert.False(animator.IsRunning);
    }

    [Fact]
    public void AnimationCollection_WithTimer_SchedulesOnAdvance()
    {
        var timer = new AnimationTimer();
        var invalidateCount = 0;
        var collection = new AnimationCollection(timer, () => invalidateCount++);
        collection.Get<OpacityAnimator>("fade", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(1000);
            a.Start();
        });

        collection.AdvanceAll(TimeSpan.FromMilliseconds(16));

        Assert.True(timer.HasScheduledTimers);
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
            sp.Animations.Get<OpacityAnimator>("fade", a => a.Start());
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
                csp.Animations.Get<OpacityAnimator>("fade", a => a.Start());
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
